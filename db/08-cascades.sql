-- =====================================================================
-- Suppression en cascade des valeurs d'un indicateur.
--
--   psql -h <IP> -U <USER> -d <BASE> -f db/08-cascades.sql
--
-- Script idempotent.
--
-- Sans cette contrainte, DELETE /api/indicators/{id} echouait en 500 des que
-- l'indicateur portait au moins une valeur : PostgreSQL refusait la
-- suppression, et l'interface promettait pourtant « supprimer l'indicateur et
-- toutes ses valeurs ».
--
-- Le choix de la cascade est volontaire : une valeur n'a aucun sens sans son
-- indicateur. La garde-fou reste cote interface, qui demande confirmation en
-- annoncant explicitement la portee de la suppression.
-- =====================================================================

BEGIN;

ALTER TABLE public.valeurs_indicateurs
    DROP CONSTRAINT IF EXISTS valeurs_indicateurs_indicateur_id_fkey;

ALTER TABLE public.valeurs_indicateurs
    ADD CONSTRAINT valeurs_indicateurs_indicateur_id_fkey
    FOREIGN KEY (indicateur_id) REFERENCES public.indicateurs(id) ON DELETE CASCADE;

COMMIT;

-- Controle : les cles etrangeres pointant vers indicateurs et leur regle.
SELECT tc.table_name, tc.constraint_name, rc.delete_rule
FROM information_schema.table_constraints tc
JOIN information_schema.referential_constraints rc
  ON rc.constraint_name = tc.constraint_name
JOIN information_schema.constraint_column_usage ccu
  ON ccu.constraint_name = tc.constraint_name
WHERE ccu.table_name = 'indicateurs' AND tc.constraint_type = 'FOREIGN KEY'
ORDER BY tc.table_name;
