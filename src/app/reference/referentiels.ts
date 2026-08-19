/**
 * Référentiels de saisie proposés dans les formulaires.
 *
 * Ce sont des **suggestions d'interface**, pas des contraintes métier : les
 * champs correspondants restent libres côté base et côté API, afin de ne pas
 * rejeter les valeurs déjà enregistrées. Les listes sont volontairement ici,
 * en un seul endroit, pour rester alignées entre les écrans.
 */

/** Unités de mesure les plus fréquentes pour un indicateur statistique. */
export const UNITES_COURANTES: { valeur: string; libelle: string }[] = [
  { valeur: '%', libelle: '% — pourcentage' },
  { valeur: 'nombre', libelle: 'nombre — effectif brut' },
  { valeur: 'pour 1 000 hab.', libelle: 'pour 1 000 habitants' },
  { valeur: 'pour 10 000 hab.', libelle: 'pour 10 000 habitants' },
  { valeur: 'pour 100 000 hab.', libelle: 'pour 100 000 habitants' },
  { valeur: 'TND', libelle: 'TND — dinar tunisien' },
  { valeur: 'milliers TND', libelle: 'milliers de TND' },
  { valeur: 'millions TND', libelle: 'millions de TND' },
  { valeur: 'années', libelle: 'années' },
  { valeur: 'jours', libelle: 'jours' },
  { valeur: 'heures', libelle: 'heures' },
  { valeur: 'km²', libelle: 'km² — kilomètres carrés' },
  { valeur: 'km', libelle: 'km — kilomètres' },
  { valeur: 'tonnes', libelle: 'tonnes' },
  { valeur: 'kWh', libelle: 'kWh — kilowattheures' },
  { valeur: 'm³', libelle: 'm³ — mètres cubes' },
  { valeur: 'ratio', libelle: 'ratio' },
  { valeur: 'indice', libelle: 'indice (base 100)' },
  { valeur: 'points', libelle: 'points' },
];

/** Mode de collecte de la donnée. */
export const TYPES_COLLECTE: string[] = [
  'Enquête',
  'Registre',
  'Recensement',
  'Estimation',
  'Déclaratif',
  'Données administratives',
  'Télédétection',
];

/** Périodicité de production de l'indicateur. */
export const FREQUENCES: string[] = [
  'Mensuelle',
  'Trimestrielle',
  'Semestrielle',
  'Annuelle',
  'Biennale',
  'Quinquennale',
  'Ponctuelle',
];

/** Degré de fiabilité déclaré à la saisie d'une valeur. */
export const DEGRES_FIABILITE: { valeur: string; libelle: string }[] = [
  { valeur: 'haute', libelle: 'Haute' },
  { valeur: 'moyenne', libelle: 'Moyenne' },
  { valeur: 'faible', libelle: 'Faible' },
];
