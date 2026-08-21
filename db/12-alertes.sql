-- =====================================================================
-- Journal des alertes de validation.
--
--   psql -h <IP> -U <USER> -d <BASE> -f db/12-alertes.sql
--
-- Script idempotent.
--
-- POURQUOI UNE TABLE ET PAS UN E-MAIL : aucun serveur SMTP n'est joignable
-- sur cette VM (ports 25 et 587 fermés), et passer par un fournisseur externe
-- exigerait un mot de passe d'application stocké quelque part. Une alerte
-- ecrite en base est consultable par l'interface, versionnée avec le reste, et
-- ne depend d'aucun secret.
-- =====================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS public.alertes_validation (
    id            serial PRIMARY KEY,
    genere_le     timestamptz NOT NULL DEFAULT now(),
    nb_en_revue   integer NOT NULL,
    nb_brouillon  integer NOT NULL,
    -- Ancienneté de la plus vieille valeur en attente : c'est elle qui
    -- indique si le circuit de validation est bloqué.
    plus_ancienne timestamptz,
    detail        jsonb NOT NULL DEFAULT '[]'::jsonb
);

CREATE INDEX IF NOT EXISTS alertes_validation_date_idx
    ON public.alertes_validation (genere_le DESC);

-- Vue des valeurs en attente, avec leur ancienneté en jours.
CREATE OR REPLACE VIEW public.valeurs_en_attente AS
    SELECT v.id,
           i.code,
           i.nom,
           v.valeur,
           i.unite,
           v.statut,
           v.saisie_par,
           v.saisie_le,
           extract(day FROM now() - v.saisie_le)::integer AS jours_attente
    FROM public.valeurs_indicateurs v
    JOIN public.indicateurs i ON i.id = v.indicateur_id
    WHERE v.is_valid IS FALSE
      AND v.statut IN ('EnRevue', 'Brouillon')
    ORDER BY v.saisie_le;

-- Génère une alerte à partir de l'état courant. Renvoie le nombre de valeurs
-- en attente, pour que l'appelant sache s'il doit alerter.
CREATE OR REPLACE FUNCTION public.generer_alerte_validation()
RETURNS integer
LANGUAGE plpgsql AS $$
DECLARE
    v_revue      integer;
    v_brouillon  integer;
    v_ancienne   timestamptz;
    v_detail     jsonb;
BEGIN
    SELECT count(*) FILTER (WHERE statut = 'EnRevue'),
           count(*) FILTER (WHERE statut = 'Brouillon'),
           min(saisie_le)
      INTO v_revue, v_brouillon, v_ancienne
      FROM public.valeurs_en_attente;

    SELECT coalesce(jsonb_agg(to_jsonb(x)), '[]'::jsonb)
      INTO v_detail
      FROM (SELECT code, nom, valeur, unite, statut, saisie_par, jours_attente
            FROM public.valeurs_en_attente LIMIT 20) x;

    -- On n'enregistre une alerte que s'il y a réellement quelque chose en
    -- attente : un journal rempli de lignes « rien à signaler » devient vite
    -- illisible et personne ne le consulte plus.
    IF coalesce(v_revue, 0) + coalesce(v_brouillon, 0) > 0 THEN
        INSERT INTO public.alertes_validation
            (nb_en_revue, nb_brouillon, plus_ancienne, detail)
        VALUES (v_revue, v_brouillon, v_ancienne, v_detail);
    END IF;

    RETURN coalesce(v_revue, 0) + coalesce(v_brouillon, 0);
END;
$$;

COMMIT;

-- Contrôles.
SELECT count(*) || ' valeur(s) en attente de validation' AS etat FROM public.valeurs_en_attente;
SELECT code, statut, jours_attente FROM public.valeurs_en_attente LIMIT 5;
