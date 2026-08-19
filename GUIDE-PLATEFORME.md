# Guide de la plateforme

Inventaire de ce que contient le projet : services, données, API, fichiers, et
les décisions de conception derrière.

---

## 1. Vue d'ensemble

```
Navigateur ─▶ frontend ─▶ gateway ─┬─▶ metier-service ─▶ PostgreSQL
  :8080       (nginx)     :5169    │        ▲             (externe en K8s)
                                   │        │ HTTP interne
                                   └─▶ ia-service ─▶ ollama (modèle local)
```

Une seule porte d'entrée : le **Gateway**. Le navigateur ne joint jamais le
service métier ni le service IA directement.

## 2. Les cinq services

| Service | Techno | Rôle | Exposition |
|---|---|---|---|
| `frontend` | Angular 22 + nginx 1.27 | Interface, sert le SPA et relaie `/api` | port 8080 |
| `gateway` | .NET 8 | Point d'entrée unique des API | port 5169 |
| `metier-service` | .NET 8 + EF Core | CRUD, workflow de validation, réclamations | interne |
| `ia-service` | .NET 8 | Construction du prompt, appel du modèle | interne |
| `ollama` | Ollama + qwen2.5:0.5b | Modèle IA local (397 Mo) | interne |

PostgreSQL 17 complète l'ensemble. En Kubernetes c'est un **serveur externe**,
jamais conteneurisé — contrainte du sujet. Le `docker-compose.yml` le
conteneurise uniquement pour le confort de développement, et le dit en
commentaire.

## 3. Le modèle de données — 8 tables

| Table | Colonnes | Contenu |
|---|---|---|
| `indicateurs` | 12 | Définition : code, nom, unité, cible, fréquence, source |
| `valeurs_indicateurs` | 15 | **Cœur du système** — une mesure, son statut, son territoire |
| `categories` | 2 | Regroupement thématique |
| `organisations` | 4 | Producteurs de données |
| `periodes` | 6 | Périodes de référence |
| `reclamations` | 10 | Signalements des utilisateurs |
| `utilisateurs` | 4 | **Référentiel des agents** — pas des comptes, aucune authentification |
| `meta_data` | 10 | **Documents justificatifs** attachés à un indicateur |

**La table qui compte est `valeurs_indicateurs`.** Un indicateur est une
définition ; une valeur est une mesure, rattachée à une organisation, une
période et un territoire. C'est la valeur qui se valide, pas l'indicateur.

### La contrainte centrale

```sql
CHECK ((is_valid IS TRUE  AND statut =  'Valide')
    OR (is_valid IS FALSE AND statut <> 'Valide'))
```

Le drapeau lu par l'IA et le statut du workflow ne peuvent pas diverger. Un
`UPDATE` manuel incohérent est rejeté par PostgreSQL.

### Scripts SQL — appliqués dans l'ordre

| Fichier | Rôle |
|---|---|
| `db/00-schema-initial.sql` | Dump de départ |
| `db/01-migration-alignement.sql` | Aligne la base sur le modèle EF Core |
| `db/02-donnees-demo.sql` | 5 indicateurs, 5 valeurs dont 2 validées |
| `db/06-indicateurs-complementaires.sql` | 7 indicateurs de plus, aucun validé |
| `db/03-reclamations.sql` | Table des réclamations |
| `db/04-localisation.sql` | Colonnes pays et gouvernorat |
| `db/05-documents-et-utilisateurs.sql` | Documents, référentiel d'agents, extension `unaccent` |

Tous sont **idempotents** : les rejouer ne casse rien.

## 4. L'API — 20 endpoints, tous via le Gateway

### Santé

| Méthode | Route | Rôle |
|---|---|---|
| `GET` | `/health` | Le Gateway seul |
| `GET` | `/health/plateforme` | Gateway + métier + IA en un appel |

### Indicateurs

| Méthode | Route |
|---|---|
| `GET` | `/api/indicators` |
| `GET` | `/api/indicators/{id}` |
| `POST` | `/api/indicators` |
| `PUT` | `/api/indicators/{id}` |
| `DELETE` | `/api/indicators/{id}` |
| `GET` | `/api/indicators/validated` — **périmètre exact de l'IA** |

### Valeurs et validation

| Méthode | Route |
|---|---|
| `GET` | `/api/indicators/{id}/valeurs` |
| `POST` | `/api/indicators/{id}/valeurs` |
| `PUT` | `/api/indicators/values/{id}` |
| `DELETE` | `/api/indicators/values/{id}` |
| `PATCH` | `/api/indicators/values/{id}/validate` |
| `PATCH` | `/api/indicators/values/{id}/devalidate` |
| `PATCH` | `/api/indicators/values/{id}/statut` |

### Réclamations

| Méthode | Route |
|---|---|
| `GET` | `/api/reclamations` |
| `GET` | `/api/reclamations/{id}` |
| `GET` | `/api/reclamations/statistiques` |
| `POST` | `/api/reclamations` |
| `PATCH` | `/api/reclamations/{id}/statut` |
| `DELETE` | `/api/reclamations/{id}` |

### IA

| Méthode | Route |
|---|---|
| `POST` | `/api/ia/analyse` |
| `GET` | `/api/ia/contexte` — le prompt exact, **sans appeler le modèle** |

`/api/ia/contexte` est l'endpoint de vérification : il montre précisément ce qui
part vers le modèle.

## 5. La règle centrale, appliquée à quatre niveaux

Le service IA n'analyse que les valeurs `is_valid = true`. Cette règle tient à
quatre endroits, du plus profond au plus visible :

1. **Base** — la contrainte `CHECK` ci-dessus.
2. **Métier** — `/api/indicators/validated` filtre `Where(v => v.IsValid)` ;
   `POST` force `is_valid = false`, un client ne peut pas s'auto-valider.
3. **Infrastructure** — le pod IA n'a **aucune** chaîne de connexion
   PostgreSQL. Contourner le métier est matériellement impossible, pas
   seulement déconseillé.
4. **Interface** — la réponse affiche les codes réellement transmis.

Le niveau 3 est le plus solide : ce n'est pas une politique, c'est une
impossibilité.

## 6. Le frontend — 8 composants, 6 services

| Écran | Route | Contenu |
|---|---|---|
| Tableau de bord | `/` | Compteurs, barre d'avancement, panneau IA, cartes |
| Indicateurs | `/indicateurs` | Tableau, recherche, tri, formulaire |
| Détail | `/indicateurs/:id` | Valeurs, workflow, filtre, analyse ciblée |
| Statistiques | `/statistiques` | Écart à la cible, fiabilité, territoire |
| Réclamations | `/reclamations` | Dépôt, filtres, traitement |

Services transverses : appels API, rôles, notifications, référentiels de saisie.

### Choix d'interface documentés

- **Aucune dépendance à un CDN** — Bootstrap et ses icônes sont dans le bundle.
  L'interface reste complète sans Internet.
- **La couleur ne porte jamais l'information seule** — badges à pastille,
  légendes doublées d'un libellé, écarts fléchés. Lisible en daltonisme.
- **La palette des graphiques est validée** — écart CVD 24,6 sous protanopie
  pour la paire divergente.
- **Ambre/bleu et non rouge/bleu pour les écarts** — le rouge jugerait, or
  dépasser une cible est souhaitable pour certains indicateurs.
- **Recherche et tri en mémoire** — adapté à cette échelle ; au-delà de
  quelques centaines d'indicateurs, à déporter côté API avec pagination.

## 7. Kubernetes — 8 manifestes

| Fichier | Contenu |
|---|---|
| `00-namespace.yaml` | Namespace `indicateurs` |
| `01-secret-postgres.example.yaml` | **Gabarit** — le vrai secret n'est jamais versionné |
| `02-configmap.yaml` | URLs internes, modèle et délais |
| `10-metier.yaml` | Déploiement + Service ClusterIP |
| `20-ollama.yaml` | Déploiement + PVC 15 Gi |
| `30-ia.yaml` | Déploiement + Service ClusterIP |
| `40-gateway.yaml` | Déploiement + NodePort 30169 |
| `50-frontend.yaml` | Déploiement + NodePort 30080 |

Seuls `frontend` et `gateway` sont exposés. Le métier, l'IA et Ollama sont en
`ClusterIP`, injoignables de l'extérieur.

## 8. Ce que le projet ne fait pas

Annoncé plutôt que découvert :

| Limite | Pourquoi |
|---|---|
| **Pas d'authentification** | Non demandée par le sujet. Les rôles filtrent l'interface, pas l'API. |
| **Pas de tests automatisés** | Non demandés. Vérifications par scénario reproductible. |

| **Modèle IA léger** | 4 cœurs sans GPU. Le périmètre est garanti, pas la qualité rédactionnelle. |



## 9. Volumétrie

| | |
|---|---|
| Fichiers versionnés | 139 |
| Composants Angular | 8 |
| Services Angular | 6 |
| Contrôleurs .NET | 3 |
| Scripts SQL | 5 |
| Manifestes Kubernetes | 8 |
| Endpoints exposés | 20 |
| Tables en base | 8 |
| Livraisons hebdomadaires | 8 semaines |
| Captures documentées | 25 |

## 10. Les autres guides

| Guide | Pour quoi |
|---|---|
| [`GUIDE-UTILISATION.md`](GUIDE-UTILISATION.md) | Installer et se servir de la plateforme |
| [`GUIDE-LANCEMENT.md`](GUIDE-LANCEMENT.md) | Démarrer, vérifier, dépanner |
| [`GUIDE-PRESENTATION.md`](GUIDE-PRESENTATION.md) | Soutenance : déroulé et questions attendues |
| [`README.md`](README.md) | Vue d'ensemble et référence API |
