-- =====================================================================
-- Localisation des valeurs d'indicateurs : pays et gouvernorat.
--
--   psql -h <IP> -U <USER> -d <BASE> -f db/04-localisation.sql
--
-- Script idempotent.
--
-- Choix de modélisation : la localisation porte sur la VALEUR, pas sur
-- l'indicateur. « Taux de chômage » est une définition nationale ; c'est la
-- mesure qui se rapporte à un territoire précis. Cela permet de stocker, pour
-- un même indicateur, une valeur nationale et une valeur par gouvernorat.
--
-- « gouvernorat » vide signifie une valeur au niveau national.
-- =====================================================================

BEGIN;

ALTER TABLE public.valeurs_indicateurs
    ADD COLUMN IF NOT EXISTS pays        character varying(80);
ALTER TABLE public.valeurs_indicateurs
    ADD COLUMN IF NOT EXISTS gouvernorat character varying(80);

-- Les données existantes sont des agrégats nationaux tunisiens : on les
-- qualifie explicitement plutôt que de les laisser sans localisation.
UPDATE public.valeurs_indicateurs
   SET pays = 'Tunisie'
 WHERE pays IS NULL OR pays = '';

ALTER TABLE public.valeurs_indicateurs ALTER COLUMN pays SET DEFAULT 'Tunisie';

CREATE INDEX IF NOT EXISTS valeurs_localisation_idx
    ON public.valeurs_indicateurs (pays, gouvernorat);

COMMIT;

-- Contrôle : répartition des valeurs par territoire.
SELECT COALESCE(NULLIF(gouvernorat, ''), '(national)') AS territoire,
       pays,
       count(*) AS nb_valeurs
FROM public.valeurs_indicateurs
GROUP BY 1, 2
ORDER BY 1;

-- Exemple régional : une même définition d'indicateur porte une valeur
-- nationale ET une valeur de gouvernorat. Inséré une seule fois.
INSERT INTO public.valeurs_indicateurs
    (indicateur_id, organisation_id, periode_id, valeur, pays, gouvernorat,
     statut, is_valid, valide_par, degre_de_fiabilite, saisie_par, commentaire,
     saisie_le, update_at)
SELECT i.id, 1, 1, 18.7, 'Tunisie', 'Kasserine',
       'Brouillon', false, '', 'moyenne', 'Direction régionale',
       'Estimation régionale, à consolider', now(), now()
FROM public.indicateurs i
WHERE i.code = 'IND-CHOM'
  AND NOT EXISTS (
      SELECT 1 FROM public.valeurs_indicateurs v
      WHERE v.indicateur_id = i.id AND v.gouvernorat = 'Kasserine'
  );
