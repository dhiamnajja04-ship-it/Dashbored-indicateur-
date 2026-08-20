-- =====================================================================
-- Organisations et périodes : deux tables référencées par
-- valeurs_indicateurs mais jamais exposées par l'API. Le formulaire
-- demandait donc de saisir « 1 » à la main, ce qui n'a aucun sens pour un
-- utilisateur.
--
--   psql -h <IP> -U <USER> -d <BASE> -f db/07-organisations-et-periodes.sql
--
-- Script idempotent.
-- =====================================================================

BEGIN;

-- --- Organisations : une hiérarchie, pas une liste plate ---------------
INSERT INTO public.organisations (nom, niveau_administratif)
VALUES
    ('Institut National de la Statistique', 'ministere'),
    ('Ministère de la Santé',               'ministere'),
    ('Ministère de l''Éducation',           'ministere')
ON CONFLICT DO NOTHING;

-- Directions régionales rattachées à l'INS via id_parent.
INSERT INTO public.organisations (nom, niveau_administratif, id_parent)
SELECT d.nom, 'gouvernorat'::niveau_administratif_enum, p.id
FROM (VALUES
    ('Direction régionale de Tunis'),
    ('Direction régionale de Sfax'),
    ('Direction régionale de Kasserine')
) AS d(nom)
CROSS JOIN (SELECT id FROM public.organisations WHERE nom = 'Institut National de la Statistique' LIMIT 1) p
WHERE NOT EXISTS (SELECT 1 FROM public.organisations o WHERE o.nom = d.nom);

-- --- Périodes : plusieurs années et trimestres -------------------------
INSERT INTO public.periodes (annee, libelle, type_periode, date_debut, date_fin)
SELECT d.annee, d.libelle, d.type_periode, d.debut::date, d.fin::date
FROM (VALUES
    (2023, 'Année 2023',        'annuelle',      '2023-01-01', '2023-12-31'),
    (2024, 'Année 2024',        'annuelle',      '2024-01-01', '2024-12-31'),
    (2025, 'T1 2025',           'trimestrielle', '2025-01-01', '2025-03-31'),
    (2025, 'T2 2025',           'trimestrielle', '2025-04-01', '2025-06-30'),
    (2025, 'T3 2025',           'trimestrielle', '2025-07-01', '2025-09-30'),
    (2025, 'T4 2025',           'trimestrielle', '2025-10-01', '2025-12-31'),
    (2026, 'Année 2026',        'annuelle',      '2026-01-01', '2026-12-31')
) AS d(annee, libelle, type_periode, debut, fin)
WHERE NOT EXISTS (SELECT 1 FROM public.periodes p WHERE p.libelle = d.libelle);

COMMIT;

SELECT count(*) AS nb_organisations FROM public.organisations;
SELECT count(*) AS nb_periodes FROM public.periodes;
