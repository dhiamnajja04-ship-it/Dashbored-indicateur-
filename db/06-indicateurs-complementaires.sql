-- =====================================================================
-- Sept indicateurs complémentaires.
--
--   psql -h <IP> -U <USER> -d <BASE> -f db/06-indicateurs-complementaires.sql
--
-- Script idempotent.
--
-- Objectif : disposer d'un volume réaliste (12 indicateurs) pour que la
-- pagination, la recherche et le tri soient démontrables. Avec cinq lignes,
-- une pagination ne se déclenche jamais et la fonctionnalité reste théorique.
--
-- IMPORTANT : aucune de ces valeurs n'est validée. Le périmètre transmis à
-- l'IA reste EXACTEMENT celui du sujet — IND-CHOM et IND-SCOL. La
-- démonstration en est même renforcée : 12 indicateurs en base, 2 validés,
-- et la réponse de l'IA ne parle que de ces deux-là.
-- =====================================================================

BEGIN;

INSERT INTO public.indicateurs
    (code, nom, description, statut, unite, source_de_donner, type_collecte,
     frequence, valeur_cible, annee_reference, categorie_id)
VALUES
    ('IND-PIB',  'Croissance du PIB', 'Variation annuelle du produit intérieur brut réel',
     'Actif', '%', 'Institut National de la Statistique', 'Estimation', 'Trimestrielle', 3.5, 2025,
     (SELECT id FROM public.categories WHERE nom = 'Économie')),

    ('IND-EXP',  'Exportations de biens', 'Valeur des exportations de marchandises',
     'Actif', 'millions TND', 'Banque Centrale de Tunisie', 'Données administratives', 'Mensuelle', 45000, 2025,
     (SELECT id FROM public.categories WHERE nom = 'Économie')),

    ('IND-TOUR', 'Arrivées touristiques', 'Nombre de touristes non résidents entrés sur le territoire',
     'Actif', 'nombre', 'Ministère du Tourisme', 'Registre', 'Mensuelle', 10000000, 2025,
     (SELECT id FROM public.categories WHERE nom = 'Économie')),

    ('IND-ELEC', 'Taux d''électrification', 'Ménages raccordés au réseau électrique',
     'Actif', '%', 'STEG', 'Registre', 'Annuelle', 100.0, 2025,
     (SELECT id FROM public.categories WHERE nom = 'Social')),

    ('IND-EAU',  'Accès à l''eau potable', 'Population desservie par le réseau public',
     'Actif', '%', 'SONEDE', 'Registre', 'Annuelle', 98.0, 2025,
     (SELECT id FROM public.categories WHERE nom = 'Social')),

    ('IND-ALPH', 'Taux d''alphabétisation', 'Population de 15 ans et plus sachant lire et écrire',
     'Actif', '%', 'Institut National de la Statistique', 'Recensement', 'Quinquennale', 90.0, 2025,
     (SELECT id FROM public.categories WHERE nom = 'Social')),

    ('IND-MORT', 'Mortalité infantile', 'Décès d''enfants de moins d''un an',
     'Actif', 'pour 1 000 hab.', 'Ministère de la Santé', 'Registre', 'Annuelle', 12.0, 2025,
     (SELECT id FROM public.categories WHERE nom = 'Social'))
ON CONFLICT (code) DO NOTHING;

-- Valeurs associées — TOUTES non validées, volontairement.
INSERT INTO public.valeurs_indicateurs
    (indicateur_id, organisation_id, periode_id, valeur, pays, gouvernorat,
     statut, is_valid, valide_par, degre_de_fiabilite, saisie_par, commentaire,
     saisie_le, update_at)
SELECT i.id, 1, 1, d.valeur, 'Tunisie', d.gouvernorat,
       d.statut, false, '', d.fiabilite, d.saisie_par, d.commentaire, now(), now()
FROM (VALUES
    ('IND-PIB',   2.4,  NULL,        'EnRevue',   'moyenne', 'Sonia Trabelsi', 'Estimation provisoire, en attente de confirmation'),
    ('IND-EXP',  41200.0, NULL,      'EnRevue',   'haute',   'Amine Bouzid',   'Cumul sur onze mois'),
    ('IND-TOUR', 9100000.0, NULL,    'Brouillon', 'moyenne', 'Amine Bouzid',   'Saisie en cours de consolidation'),
    ('IND-ELEC',  99.8, NULL,        'Brouillon', 'haute',   'Sonia Trabelsi', 'Chiffre national STEG'),
    ('IND-EAU',   96.3, NULL,        'EnRevue',   'haute',   'Sonia Trabelsi', 'Hors zones rurales isolées'),
    ('IND-ALPH',  82.7, NULL,        'Brouillon', 'moyenne', 'Amine Bouzid',   'Extrapolation depuis le dernier recensement'),
    ('IND-MORT',  14.8, 'Kairouan',  'Rejete',    'faible',  'Amine Bouzid',   'Écart trop important avec la série historique')
) AS d(code, valeur, gouvernorat, statut, fiabilite, saisie_par, commentaire)
JOIN public.indicateurs i ON i.code = d.code
WHERE NOT EXISTS (
    SELECT 1 FROM public.valeurs_indicateurs v WHERE v.indicateur_id = i.id
);

COMMIT;

-- Contrôle : le périmètre de l'IA doit rester à 2 valeurs.
SELECT count(*) AS total_indicateurs FROM public.indicateurs;
SELECT count(*) AS valeurs_validees FROM public.valeurs_indicateurs WHERE is_valid IS TRUE;
