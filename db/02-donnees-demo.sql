-- =====================================================================
-- Jeu de données de démonstration (semaine 8).
--
--   psql -h <IP> -U <USER> -d <BASE> -f db/02-donnees-demo.sql
--
-- Il crée volontairement 5 valeurs dont 2 seulement sont validées :
-- c'est exactement le scénario décrit dans le sujet
-- (« 5 indicateurs en base, 2 validés → la réponse IA ne parle que de ces 2-là »).
-- =====================================================================

BEGIN;

INSERT INTO public.categories (nom) VALUES ('Économie')
    ON CONFLICT (nom) DO NOTHING;
INSERT INTO public.categories (nom) VALUES ('Social')
    ON CONFLICT (nom) DO NOTHING;

INSERT INTO public.organisations (nom, niveau_administratif)
SELECT 'Ministère du Plan', 'ministere'
WHERE NOT EXISTS (SELECT 1 FROM public.organisations WHERE nom = 'Ministère du Plan');

INSERT INTO public.periodes (annee, libelle, type_periode, date_debut, date_fin)
SELECT 2025, 'Année 2025', 'annuelle', DATE '2025-01-01', DATE '2025-12-31'
WHERE NOT EXISTS (SELECT 1 FROM public.periodes WHERE libelle = 'Année 2025');

-- --- Indicateurs ---
INSERT INTO public.indicateurs
    (code, nom, description, statut, unite, type_collecte, frequence, valeur_cible, annee_reference, categorie_id)
VALUES
    ('IND-CHOM', 'Taux de chômage', 'Part de la population active sans emploi', 'Actif', '%', 'Enquête', 'Annuelle', 10.0, 2025,
        (SELECT id FROM public.categories WHERE nom = 'Économie')),
    ('IND-INFL', 'Taux d''inflation', 'Évolution annuelle des prix à la consommation', 'Actif', '%', 'Enquête', 'Mensuelle', 4.0, 2025,
        (SELECT id FROM public.categories WHERE nom = 'Économie')),
    ('IND-SCOL', 'Taux de scolarisation', 'Enfants scolarisés dans le primaire', 'Actif', '%', 'Registre', 'Annuelle', 98.0, 2025,
        (SELECT id FROM public.categories WHERE nom = 'Social')),
    ('IND-SANT', 'Densité médicale', 'Médecins pour 10 000 habitants', 'Actif', 'pour 10 000 hab.', 'Registre', 'Annuelle', 15.0, 2025,
        (SELECT id FROM public.categories WHERE nom = 'Social')),
    ('IND-NUM', 'Couverture Internet', 'Ménages disposant d''un accès Internet', 'Actif', '%', 'Enquête', 'Annuelle', 80.0, 2025,
        (SELECT id FROM public.categories WHERE nom = 'Économie'))
ON CONFLICT (code) DO NOTHING;

-- --- Valeurs : 2 validées, 3 non validées ---
-- Le contraste est le point de la démo : l'IA ne doit citer que IND-CHOM et IND-SCOL.
INSERT INTO public.valeurs_indicateurs
    (indicateur_id, organisation_id, periode_id, valeur, statut, is_valid, valide_par,
     degre_de_fiabilite, saisie_par, commentaire, saisie_le, update_at)
SELECT
    i.id,
    (SELECT id FROM public.organisations WHERE nom = 'Ministère du Plan'),
    (SELECT id FROM public.periodes WHERE libelle = 'Année 2025'),
    d.valeur, d.statut, d.is_valid, d.valide_par, d.fiabilite, 'stagiaire', d.commentaire, now(), now()
FROM (VALUES
    ('IND-CHOM', 12.4, 'Valide',    true,  'Administrateur', 'haute',   'Chiffre consolidé, relu par le service statistique'),
    ('IND-SCOL', 96.2, 'Valide',    true,  'Administrateur', 'haute',   'Source : registre national, validé'),
    ('IND-INFL',  6.8, 'EnRevue',   false, '',               'moyenne', 'En attente de confirmation par la banque centrale'),
    ('IND-SANT',  9.1, 'Brouillon', false, '',               'faible',  'Saisie provisoire, à recouper'),
    ('IND-NUM',  71.5, 'Rejete',    false, '',               'faible',  'Écart trop important avec l''enquête précédente')
) AS d(code, valeur, statut, is_valid, valide_par, fiabilite, commentaire)
JOIN public.indicateurs i ON i.code = d.code
WHERE NOT EXISTS (
    SELECT 1 FROM public.valeurs_indicateurs v WHERE v.indicateur_id = i.id
);

COMMIT;

-- Contrôle : doit afficher 2 lignes (IND-CHOM et IND-SCOL) — le périmètre exact de l'IA.
SELECT i.code, i.nom, v.valeur, i.unite, v.statut
FROM public.valeurs_indicateurs v
JOIN public.indicateurs i ON i.id = v.indicateur_id
WHERE v.is_valid IS TRUE
ORDER BY i.code;
