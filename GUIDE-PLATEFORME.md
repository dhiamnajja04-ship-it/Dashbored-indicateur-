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
| `loadbalancer` | nginx 1.27 | Répartit la charge sur les répliques du Gateway | port 5169 |
| `gateway` | .NET 8 | Point d'entrée unique des API | interne, **2 répliques** |
| `metier-service` | .NET 8 + EF Core | CRUD, workflow de validation, réclamations | interne |
| `ia-service` | .NET 8 | Construction du prompt, appel du modèle | interne |
| `ollama` | Ollama + qwen2.5:1.5b | Modèle IA local (986 Mo) | interne |

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
| `organisations` | 4 | **Producteurs de données**, hiérarchisés via `id_parent` — exposés par l'API |
| `periodes` | 6 | **Périodes de référence** bornées par de vraies dates — exposées par l'API |
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
| `db/07-organisations-et-periodes.sql` | Organisations hiérarchisées et périodes datées |

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

## 10. Déploiement Kubernetes

La plateforme tourne **réellement** sur un cluster Kubernetes, pas seulement
sur Docker Compose.

```bash
./k8s/deploy-kind.sh
```

| | |
|---|---|
| Interface | http://localhost:30080 |
| API | http://localhost:30169 |

### Pourquoi kind plutôt que minikube ou k3s

Ces deux-là exigent des droits root. Sur la VM de stage, `sudo` demande un mot
de passe interactif. **kind** constitue le cluster avec des conteneurs Docker :
il s'installe et tourne sans être administrateur.

### PostgreSQL reste hors du cluster

Comme l'impose le sujet. Seule la chaîne de connexion entre dans Kubernetes,
via un `Secret` créé à la volée — jamais dans un fichier versionné.

### Ce que le cluster apporte, mesuré

| Test | Résultat |
|---|---|
| 5 pods démarrés | `metier`, `ia`, `frontend`, **2× `gateway`** |
| Le métier joint le PostgreSQL externe | readinessProbe satisfaite |
| Le Service répartit sur les 2 pods | `10.244.0.7:8080, 10.244.0.8:8080` |
| **Suppression d'un pod gateway** | **10/10 requêtes réussies, zéro interruption** |
| Auto-réparation | Kubernetes recrée le pod en ~15 s |

### Une différence avec Docker Compose

Sous Compose, la répartition de charge exige un conteneur nginx dédié. Sous
Kubernetes, **un `Service` le fait nativement** : `45-loadbalancer.yaml` ne
contient qu'un Service, aucun conteneur.

Il porte volontairement le nom `loadbalancer`, celui vers lequel l'image du
frontend relaie `/api`. **La même image fonctionne donc dans les deux
environnements, sans reconstruction.**

## 11. Répartition de charge

Le Gateway était le point d'entrée unique : sa panne coupait toute la
plateforme. Il est désormais **répliqué**, avec un répartiteur nginx devant.

```
Navigateur / Postman ─▶ loadbalancer:5169 ─┬─▶ gateway (réplique 1)
                                            └─▶ gateway (réplique 2)
```

Changer le nombre de répliques :

```bash
docker compose up -d --scale gateway=3
```

Rien à reconfigurer : le répartiteur découvre les répliques par le DNS interne
de Docker.

### Le piège évité

Un bloc `upstream` nginx classique ne résout les noms **qu'au démarrage** : il
fige l'adresse d'une seule réplique et ignore les autres. Mesuré : avec deux
répliques dont une arrêtée, **4 requêtes sur 8 échouaient**.

La configuration passe donc par une **variable** dans `proxy_pass`, ce qui force
une résolution à chaque requête via le résolveur Docker `127.0.0.11`. Après
correction : **12 requêtes sur 12** aboutissent avec une réplique arrêtée.

### Vérifier

```bash
curl http://localhost:5169/lb-health          # le répartiteur lui-même
docker compose ps gateway                     # les répliques
docker stop rw9980-gateway-2                  # simuler une panne
curl http://localhost:5169/health/plateforme  # répond toujours
```

## 12. Inspecter la base avec pgAdmin

pgAdmin est inclus dans la plateforme, avec la **connexion déjà enregistrée** :

| | |
|---|---|
| Adresse | **http://localhost:5050** |
| Identifiant pgAdmin | `admin@pictorsolution.tn` |
| Mot de passe pgAdmin | `admin` |
| Mot de passe PostgreSQL | `testpwd` (demandé une fois au premier clic sur le serveur) |

Le serveur « Plateforme Indicateurs » apparaît déjà dans l'arbre à gauche.
Chemin vers les données : *Servers → Plateforme Indicateurs → Databases →
indicateurs_db → Schemas → public → Tables*.

Pour voir la règle centrale directement en SQL :

```sql
-- Ce que l'IA reçoit, et rien d'autre
SELECT i.code, i.nom, v.valeur, v.statut, v.is_valid, v.valide_par
FROM valeurs_indicateurs v
JOIN indicateurs i ON i.id = v.indicateur_id
WHERE v.is_valid IS TRUE;
```

⚠️ Ces identifiants conviennent à une démonstration locale. En production, ils
devraient venir d'un Secret, comme la chaîne de connexion PostgreSQL.

## 13. Tester l'API avec Postman

Une collection prête à importer : [`postman/Plateforme-Indicateurs.postman_collection.json`](postman/Plateforme-Indicateurs.postman_collection.json)
— **7 dossiers, 33 requêtes**, chacune documentée.

Import : Postman → *Import* → sélectionner le fichier. Régler ensuite la
variable `base_url` (`http://localhost:5169` depuis la VM).

Le parcours à suivre pour vérifier la règle centrale :

1. `GET /api/indicators/validated` — le périmètre exact de l'IA
2. `PATCH /api/indicators/values/{id}/validate` — valider une valeur de plus
3. `GET /api/ia/contexte` — le prompt a changé, sans avoir appelé le modèle
4. `PATCH .../devalidate` — et il revient en arrière

## 14. Les autres guides

| Guide | Pour quoi |
|---|---|
| [`GUIDE-UTILISATION.md`](GUIDE-UTILISATION.md) | Installer et se servir de la plateforme |
| [`GUIDE-LANCEMENT.md`](GUIDE-LANCEMENT.md) | Démarrer, vérifier, dépanner |
| [`GUIDE-PRESENTATION.md`](GUIDE-PRESENTATION.md) | Soutenance : déroulé et questions attendues |
| [`README.md`](README.md) | Vue d'ensemble et référence API |
