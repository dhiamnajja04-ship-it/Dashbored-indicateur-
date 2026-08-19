-- =====================================================================
-- Exploitation des deux tables restées inutilisées depuis le schéma initial.
--
--   psql -h <IP> -U <USER> -d <BASE> -f db/05-documents-et-utilisateurs.sql
--
-- Script idempotent.
--
-- 1. « utilisateurs » devient le référentiel des agents. Les champs
--    saisie_par et valide_par étaient du texte libre : deux orthographes du
--    même nom donnaient deux agents différents. On garde le texte en base
--    (pas de clé étrangère) pour ne pas invalider l'existant, mais
--    l'interface propose désormais une liste fermée.
--
--    Ce n'est PAS de l'authentification : personne ne se connecte, on choisit
--    simplement qui effectue la saisie dans un référentiel commun.
--
-- 2. « meta_data » porte déjà une clé étrangère vers indicateurs. Elle devient
--    la table des documents justificatifs attachés à un indicateur.
-- =====================================================================

BEGIN;

-- ---------------------------------------------------------------------
-- Référentiel des agents
-- ---------------------------------------------------------------------

ALTER TABLE public.utilisateurs
    ADD COLUMN IF NOT EXISTS actif boolean NOT NULL DEFAULT true;

CREATE UNIQUE INDEX IF NOT EXISTS utilisateurs_nom_key
    ON public.utilisateurs (nom_utilisateur);

-- Les rôles reprennent exactement ceux de l'interface.
INSERT INTO public.utilisateurs (nom_utilisateur, role)
VALUES
    ('Amine Bouzid',    'Agent de saisie'),
    ('Sonia Trabelsi',  'Agent de saisie'),
    ('Karim Hamdi',     'Validateur'),
    ('Leila Ben Salah', 'Validateur'),
    ('Administrateur',  'Administrateur')
ON CONFLICT (nom_utilisateur) DO NOTHING;

-- ---------------------------------------------------------------------
-- Documents justificatifs
-- ---------------------------------------------------------------------

ALTER TABLE public.meta_data ADD COLUMN IF NOT EXISTS nom_fichier    character varying(255);
ALTER TABLE public.meta_data ADD COLUMN IF NOT EXISTS nom_stocke     character varying(255);
ALTER TABLE public.meta_data ADD COLUMN IF NOT EXISTS type_mime      character varying(120);
ALTER TABLE public.meta_data ADD COLUMN IF NOT EXISTS taille_octets  bigint;
ALTER TABLE public.meta_data ADD COLUMN IF NOT EXISTS depose_par     character varying(100);
ALTER TABLE public.meta_data ADD COLUMN IF NOT EXISTS depose_le      timestamptz DEFAULT now();

-- Un même fichier stocké ne doit pas être référencé deux fois.
CREATE UNIQUE INDEX IF NOT EXISTS meta_data_nom_stocke_key
    ON public.meta_data (nom_stocke) WHERE nom_stocke IS NOT NULL;

CREATE INDEX IF NOT EXISTS meta_data_indicateur_idx
    ON public.meta_data (indicateur_id);

-- Supprimer un indicateur doit emporter ses documents, pas laisser des
-- lignes orphelines pointant vers un identifiant disparu.
ALTER TABLE public.meta_data DROP CONSTRAINT IF EXISTS meta_data_indicateur_id_fkey;
ALTER TABLE public.meta_data
    ADD CONSTRAINT meta_data_indicateur_id_fkey
    FOREIGN KEY (indicateur_id) REFERENCES public.indicateurs(id) ON DELETE CASCADE;

COMMIT;

-- Contrôles.
SELECT role, count(*) AS nb_agents FROM public.utilisateurs GROUP BY role ORDER BY role;
SELECT count(*) AS nb_documents FROM public.meta_data WHERE nom_stocke IS NOT NULL;

-- ---------------------------------------------------------------------
-- Recherche insensible aux accents
-- ---------------------------------------------------------------------
-- Sans cela, chercher « densite » ne trouve pas « Densité médicale » : sur un
-- corpus francophone c'est la règle plutôt que l'exception. L'extension
-- unaccent est fournie avec PostgreSQL (contrib).
CREATE EXTENSION IF NOT EXISTS unaccent;
