-- =====================================================================
-- Table « reclamations »
--
-- Permet à un utilisateur de signaler un problème sur un indicateur ou sur
-- l'une de ses valeurs : chiffre qui paraît erroné, source à corriger,
-- demande de précision.
--
--   psql -h <IP> -U <USER> -d <BASE> -f db/03-reclamations.sql
--
-- Script idempotent : il peut être rejoué sans risque.
--
-- Note d'architecture : une réclamation ne modifie JAMAIS une valeur. Elle
-- n'a donc aucun effet sur is_valid, et reste hors du périmètre transmis à
-- l'IA. Corriger un chiffre reste une action explicite passant par le
-- workflow de validation.
-- =====================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS public.reclamations (
    id            serial PRIMARY KEY,
    indicateur_id integer,
    objet         character varying(150) NOT NULL,
    message       text                   NOT NULL,
    soumis_par    character varying(100) NOT NULL,
    email         character varying(150),
    statut        character varying(30)  NOT NULL DEFAULT 'Nouvelle',
    reponse       text,
    cree_le       timestamptz            NOT NULL DEFAULT now(),
    traite_le     timestamptz
);

-- L'indicateur visé est facultatif : une réclamation peut être générale.
-- ON DELETE SET NULL : supprimer un indicateur ne doit pas effacer
-- l'historique des réclamations le concernant.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_name = 'reclamations_indicateur_fk'
    ) THEN
        ALTER TABLE public.reclamations
            ADD CONSTRAINT reclamations_indicateur_fk
            FOREIGN KEY (indicateur_id) REFERENCES public.indicateurs (id)
            ON DELETE SET NULL;
    END IF;
END $$;

-- Le statut est contraint en base : le service métier refuse déjà les valeurs
-- inconnues, mais un UPDATE manuel ne doit pas pouvoir introduire un état
-- que l'interface ne saurait pas afficher.
ALTER TABLE public.reclamations DROP CONSTRAINT IF EXISTS reclamations_statut_connu;
ALTER TABLE public.reclamations
    ADD CONSTRAINT reclamations_statut_connu
    CHECK (statut IN ('Nouvelle', 'EnCours', 'Traitee', 'Rejetee'));

CREATE INDEX IF NOT EXISTS reclamations_statut_idx      ON public.reclamations (statut);
CREATE INDEX IF NOT EXISTS reclamations_indicateur_idx  ON public.reclamations (indicateur_id);
CREATE INDEX IF NOT EXISTS reclamations_cree_le_idx     ON public.reclamations (cree_le DESC);

COMMIT;

-- Deux réclamations de démonstration, seulement si la table est vide.
INSERT INTO public.reclamations (indicateur_id, objet, message, soumis_par, email, statut)
SELECT i.id,
       'Écart avec la publication trimestrielle',
       'Le taux de chômage publié dans le bulletin trimestriel diffère de la valeur affichée ici. Pouvez-vous confirmer la période de référence retenue ?',
       'Service statistique régional',
       'stat.regional@example.org',
       'Nouvelle'
FROM public.indicateurs i
WHERE i.code = 'IND-CHOM'
  AND NOT EXISTS (SELECT 1 FROM public.reclamations);

INSERT INTO public.reclamations (indicateur_id, objet, message, soumis_par, statut, reponse, traite_le)
SELECT i.id,
       'Source de données à préciser',
       'La source du taux de scolarisation n''est pas renseignée. Merci d''indiquer le registre utilisé.',
       'Direction de la planification',
       'Traitee',
       'Source ajoutée : registre national de la scolarisation, campagne 2025.',
       now()
FROM public.indicateurs i
WHERE i.code = 'IND-SCOL'
  AND (SELECT count(*) FROM public.reclamations) = 1;
