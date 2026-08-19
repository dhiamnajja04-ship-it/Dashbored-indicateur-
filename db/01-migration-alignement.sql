-- =====================================================================
-- Migration : aligner la base sur le modèle EF Core du service métier.
--
-- À exécuter sur le PostgreSQL du tuteur :
--   psql -h <IP> -U <USER> -d <BASE> -f db/01-migration-alignement.sql
--
-- Le script est idempotent : on peut le rejouer sans risque.
--
-- Deux écarts sont corrigés ici, tous deux responsables d'erreurs 500 :
--
--   1. Noms de colonnes. Le dump initial (schema_db.sql) expose
--      « saisie_at » alors que AppDbContext attend « saisie_le », et il
--      ne contient ni update_at, ni valide_par, ni commentaire, etc.
--
--   2. Type des colonnes de date. Elles étaient en
--      « timestamp without time zone » alors que le code écrit des
--      DateTime.UtcNow (Kind = Utc). Depuis Npgsql 6, cette combinaison
--      lève « Cannot write DateTime with Kind=Utc to PostgreSQL type
--      timestamp without time zone ». On passe donc en timestamptz.
-- =====================================================================

BEGIN;

-- ---------------------------------------------------------------------
-- Table « indicateurs »
-- ---------------------------------------------------------------------

-- Le dump d'origine nommait la colonne « nom_indicateur ».
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_name = 'indicateurs' AND column_name = 'nom_indicateur')
       AND NOT EXISTS (SELECT 1 FROM information_schema.columns
                       WHERE table_name = 'indicateurs' AND column_name = 'nom')
    THEN
        ALTER TABLE public.indicateurs RENAME COLUMN nom_indicateur TO nom;
    END IF;
END $$;

ALTER TABLE public.indicateurs ADD COLUMN IF NOT EXISTS nom              character varying(150);
ALTER TABLE public.indicateurs ADD COLUMN IF NOT EXISTS code             character varying(50);
ALTER TABLE public.indicateurs ADD COLUMN IF NOT EXISTS description      text;
ALTER TABLE public.indicateurs ADD COLUMN IF NOT EXISTS statut           character varying(50);
ALTER TABLE public.indicateurs ADD COLUMN IF NOT EXISTS unite            character varying(50);
ALTER TABLE public.indicateurs ADD COLUMN IF NOT EXISTS source_de_donner character varying(150);
ALTER TABLE public.indicateurs ADD COLUMN IF NOT EXISTS type_collecte    character varying(50);
ALTER TABLE public.indicateurs ADD COLUMN IF NOT EXISTS frequence        character varying(50);
ALTER TABLE public.indicateurs ADD COLUMN IF NOT EXISTS valeur_cible     numeric(15,2);
ALTER TABLE public.indicateurs ADD COLUMN IF NOT EXISTS annee_reference  integer;
ALTER TABLE public.indicateurs ADD COLUMN IF NOT EXISTS categorie_id     integer;

-- Le modèle C# déclare ces champs non-nullables : une valeur NULL en base
-- ferait planter la désérialisation côté service métier.
UPDATE public.indicateurs SET code          = 'IND-' || id WHERE code IS NULL OR code = '';
UPDATE public.indicateurs SET nom           = 'Indicateur ' || id WHERE nom IS NULL OR nom = '';
UPDATE public.indicateurs SET statut        = 'Actif' WHERE statut IS NULL;
UPDATE public.indicateurs SET unite         = ''      WHERE unite IS NULL;
UPDATE public.indicateurs SET type_collecte = ''      WHERE type_collecte IS NULL;

ALTER TABLE public.indicateurs ALTER COLUMN code          SET NOT NULL;
ALTER TABLE public.indicateurs ALTER COLUMN nom           SET NOT NULL;
ALTER TABLE public.indicateurs ALTER COLUMN statut        SET DEFAULT 'Actif';
ALTER TABLE public.indicateurs ALTER COLUMN unite         SET DEFAULT '';
ALTER TABLE public.indicateurs ALTER COLUMN type_collecte SET DEFAULT '';

-- Le service métier traduit la violation 23505 en message « code déjà utilisé ».
CREATE UNIQUE INDEX IF NOT EXISTS indicateurs_code_key ON public.indicateurs (code);

-- Colonnes de l'ancien dump devenues inutiles : la valeur d'un indicateur vit
-- désormais dans valeurs_indicateurs (une valeur par organisation et période).
ALTER TABLE public.indicateurs DROP COLUMN IF EXISTS valeur;
ALTER TABLE public.indicateurs DROP COLUMN IF EXISTS date_enregistrement;

-- ---------------------------------------------------------------------
-- Table « valeurs_indicateurs »
-- ---------------------------------------------------------------------

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_name = 'valeurs_indicateurs' AND column_name = 'saisie_at')
       AND NOT EXISTS (SELECT 1 FROM information_schema.columns
                       WHERE table_name = 'valeurs_indicateurs' AND column_name = 'saisie_le')
    THEN
        ALTER TABLE public.valeurs_indicateurs RENAME COLUMN saisie_at TO saisie_le;
    END IF;
END $$;

ALTER TABLE public.valeurs_indicateurs ADD COLUMN IF NOT EXISTS saisie_le          timestamptz DEFAULT now();
ALTER TABLE public.valeurs_indicateurs ADD COLUMN IF NOT EXISTS update_at          timestamptz DEFAULT now();
ALTER TABLE public.valeurs_indicateurs ADD COLUMN IF NOT EXISTS statut             character varying(50);
ALTER TABLE public.valeurs_indicateurs ADD COLUMN IF NOT EXISTS degre_de_fiabilite character varying(50);
ALTER TABLE public.valeurs_indicateurs ADD COLUMN IF NOT EXISTS saisie_par         character varying(100);
ALTER TABLE public.valeurs_indicateurs ADD COLUMN IF NOT EXISTS commentaire        text;
ALTER TABLE public.valeurs_indicateurs ADD COLUMN IF NOT EXISTS valide_par         character varying(100);
ALTER TABLE public.valeurs_indicateurs ADD COLUMN IF NOT EXISTS is_valid           boolean DEFAULT false;

-- Passage en timestamptz : sans cela, Npgsql refuse les DateTime.UtcNow écrits
-- par le service métier (erreur 500 à chaque saisie ou validation).
-- La conversion n'est tentée que si la colonne est encore « sans fuseau »,
-- sinon rejouer le script reconvertirait la colonne dans le mauvais sens.
DO $$
DECLARE
    col text;
BEGIN
    FOREACH col IN ARRAY ARRAY['saisie_le', 'update_at'] LOOP
        IF EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_name = 'valeurs_indicateurs'
                     AND column_name = col
                     AND data_type = 'timestamp without time zone')
        THEN
            EXECUTE format(
                'ALTER TABLE public.valeurs_indicateurs ALTER COLUMN %I TYPE timestamptz USING %I AT TIME ZONE ''UTC''',
                col, col);
        END IF;
    END LOOP;
END $$;

-- Alignement sur le workflow de validation (MetierService/Models/StatutValeur.cs).
-- L'ancien libellé « Validé » (accentué) devient « Valide ».
UPDATE public.valeurs_indicateurs SET statut = 'Valide'
    WHERE statut IN ('Validé', 'valide', 'VALIDE');
UPDATE public.valeurs_indicateurs SET statut = 'Valide'    WHERE is_valid IS TRUE  AND statut IS DISTINCT FROM 'Valide';
UPDATE public.valeurs_indicateurs SET statut = 'Brouillon' WHERE statut IS NULL OR statut = '';
UPDATE public.valeurs_indicateurs SET is_valid = false     WHERE is_valid IS NULL;

-- Champs non-nullables côté C# : on remplace les NULL par des chaînes vides.
UPDATE public.valeurs_indicateurs SET degre_de_fiabilite = '' WHERE degre_de_fiabilite IS NULL;
UPDATE public.valeurs_indicateurs SET saisie_par         = '' WHERE saisie_par IS NULL;
UPDATE public.valeurs_indicateurs SET commentaire        = '' WHERE commentaire IS NULL;
UPDATE public.valeurs_indicateurs SET valide_par         = '' WHERE valide_par IS NULL;
UPDATE public.valeurs_indicateurs SET saisie_le = now() WHERE saisie_le IS NULL;
UPDATE public.valeurs_indicateurs SET update_at = COALESCE(saisie_le, now()) WHERE update_at IS NULL;

ALTER TABLE public.valeurs_indicateurs ALTER COLUMN is_valid  SET NOT NULL;
ALTER TABLE public.valeurs_indicateurs ALTER COLUMN saisie_le SET NOT NULL;
ALTER TABLE public.valeurs_indicateurs ALTER COLUMN update_at SET NOT NULL;
ALTER TABLE public.valeurs_indicateurs ALTER COLUMN statut    SET DEFAULT 'Brouillon';

-- Garde-fou : le drapeau lu par l'IA et le statut du workflow ne peuvent pas diverger.
-- Sans cette contrainte, un UPDATE manuel en base pourrait rendre une valeur
-- visible par l'IA sans qu'elle soit passée par la validation.
ALTER TABLE public.valeurs_indicateurs DROP CONSTRAINT IF EXISTS valeurs_statut_coherent;
ALTER TABLE public.valeurs_indicateurs
    ADD CONSTRAINT valeurs_statut_coherent
    CHECK ((is_valid IS TRUE AND statut = 'Valide') OR (is_valid IS FALSE AND statut <> 'Valide'));

-- L'IA ne lit que les lignes validées : un index partiel garde cette requête rapide.
CREATE INDEX IF NOT EXISTS idx_valeurs_validees
    ON public.valeurs_indicateurs (indicateur_id)
    WHERE is_valid IS TRUE;

-- Table de test du dump initial, sans usage dans l'application.
DROP TABLE IF EXISTS public.test_connexion;

COMMIT;
