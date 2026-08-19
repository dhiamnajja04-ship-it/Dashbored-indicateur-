# Plateforme Indicateurs

Plateforme conteneurisée d'indicateurs avec workflow de validation et analyse par
un modèle IA local. Stage DevOps.

## Principe

L'utilisateur consulte des indicateurs stockés en PostgreSQL, valide certaines
valeurs, puis déclenche une analyse IA — **qui ne porte que sur les valeurs
validées**.

```
Navigateur ─▶ frontend ─▶ gateway ─┬─▶ metier-service ─▶ PostgreSQL (hors cluster)
  :30080      (nginx)     :30169   │       ▲
                                   │       │ HTTP interne (valeurs validées)
                                   └─▶ ia-service ─▶ ollama (modèle local)
```

## Services

| Dossier | Service | Techno | Exposition |
|---|---|---|---|
| [`src/`](src/) | Frontend | Angular 22 + nginx | NodePort 30080 |
| [`GatewayService/`](GatewayService/) | Gateway | .NET 8 | NodePort 30169 |
| [`MetierService/`](MetierService/) | Métier | .NET 8 + EF Core | ClusterIP |
| [`IaService/`](IaService/) | IA | .NET 8 | ClusterIP |
| — | Modèle | Ollama (mistral) | ClusterIP + PVC |

PostgreSQL est un **serveur externe**, jamais conteneurisé.

## Règle centrale

Le service IA n'analyse que les valeurs dont `is_valid = true`. Cette règle est
appliquée à quatre niveaux, du plus profond au plus visible :

1. **Base** — contrainte `CHECK` liant `is_valid` et `statut`.
2. **Métier** — `GET /api/indicators/validated` filtre `Where(v => v.IsValid)` ;
   `POST` et `PUT` refusent toute auto-validation par le client.
3. **Infrastructure** — le pod IA n'a aucune chaîne de connexion PostgreSQL : il
   lui est matériellement impossible de contourner le métier.
4. **Interface** — la réponse affiche les codes des indicateurs réellement
   transmis au modèle, donc le périmètre est vérifiable à l'œil.

## Démarrage rapide (VM Linux + Minikube)

```bash
# 1. Base de données
psql -h <IP> -U <USER> -d <BASE> -f db/01-migration-alignement.sql
psql -h <IP> -U <USER> -d <BASE> -f db/02-donnees-demo.sql

# 2. Secret (l'espace initial évite l'historique du shell)
 kubectl create namespace indicateurs
 kubectl -n indicateurs create secret generic postgres-secret \
   --from-literal=connection-string="Host=<IP>;Port=5432;Database=<BASE>;Username=<USER>;Password=<MDP>"

# 3. Build + déploiement
./k8s/deploy.sh

# 4. Modèle (une seule fois, persiste dans le PVC)
kubectl -n indicateurs exec deploy/ollama -- ollama pull mistral
```

Interface : `http://$(minikube ip):30080`

Détails et dépannage : [`k8s/README.md`](k8s/README.md).

## Lancement manuel sur une seule machine (Docker Compose)

Pour développer ou faire une démonstration sans cluster :

```bash
docker compose up -d --build          # construit et démarre les 6 conteneurs
docker compose exec ollama ollama pull mistral   # une seule fois (~4 Go)
docker compose ps
```

| Quoi | URL |
|---|---|
| Interface | http://localhost:8080 |
| Gateway | http://localhost:5169/health |
| Santé globale | http://localhost:5169/health/plateforme |
| PostgreSQL | localhost:15432 (`postgres` / `testpwd`) |

La base est initialisée automatiquement au premier démarrage : dump, migration,
puis données de démonstration (5 indicateurs dont 2 validés).

```bash
docker compose logs -f metier-service     # suivre un service
docker compose down                       # arrêter
docker compose down -v                    # arrêter et effacer la base
```

⚠️ [`docker-compose.yml`](docker-compose.yml) conteneurise PostgreSQL **pour le
confort de développement uniquement**. Le déploiement de référence reste
[`k8s/`](k8s/), où PostgreSQL est un serveur externe, comme l'impose le sujet.

## Développement local (sans Docker ni Kubernetes)

Prérequis : SDK .NET 8, Node.js 22, Ollama. Quatre terminaux :

```bash
cd MetierService  && dotnet run     # http://localhost:5039
cd GatewayService && dotnet run     # http://localhost:5169
cd IaService      && dotnet run     # http://localhost:5210
npm start                           # http://localhost:4200
```

`ng serve` relaie `/api` vers le Gateway via [`proxy.conf.json`](proxy.conf.json).
Ollama doit tourner sur la machine (`ollama serve` + `ollama pull mistral`).

La chaîne de connexion locale se met dans `MetierService/appsettings.Development.json`,
qui n'est pas versionné.

## API (via le Gateway)

### Santé

| Méthode | Route | Rôle |
|---|---|---|
| `GET` | `/health` | Gateway seul |
| `GET` | `/health/plateforme` | Gateway + métier + IA |

### Indicateurs

| Méthode | Route |
|---|---|
| `GET` | `/api/indicators` |
| `GET` | `/api/indicators/{id}` |
| `POST` | `/api/indicators` |
| `PUT` | `/api/indicators/{id}` |
| `DELETE` | `/api/indicators/{id}` |
| `GET` | `/api/indicators/validated` — périmètre exact de l'IA |

### Valeurs et validation

| Méthode | Route |
|---|---|
| `GET` | `/api/indicators/{id}/valeurs[?validesUniquement=true]` |
| `POST` | `/api/indicators/{id}/valeurs` |
| `PUT` | `/api/indicators/values/{id}` |
| `DELETE` | `/api/indicators/values/{id}` |
| `PATCH` | `/api/indicators/values/{id}/validate` |
| `PATCH` | `/api/indicators/values/{id}/devalidate` |
| `PATCH` | `/api/indicators/values/{id}/statut` |

### IA

| Méthode | Route |
|---|---|
| `POST` | `/api/ia/analyse` — corps : `{"question": "...", "indicateurId": 1}` (tout facultatif) |
| `GET` | `/api/ia/contexte` — le prompt exact, sans appeler le modèle |

## Workflow de validation

```
Brouillon ──soumettre──▶ EnRevue ──valider──▶ Valide
    ▲                       │                   │
    └────────rejeter────────┘                   │
    └──────────────dévalider────────────────────┘
```

Seul `Valide` met `is_valid = true`. Modifier le chiffre d'une valeur validée la
ramène en `Brouillon` : la validation porte sur un chiffre précis, pas sur une
ligne de table.

## Structure

```
rw9980/
├── src/                    # Frontend Angular
├── GatewayService/         # .NET 8 — routage
├── MetierService/          # .NET 8 — CRUD + validation
├── IaService/              # .NET 8 — prompt + modèle
├── db/                     # Migration SQL + données de démo
├── k8s/                    # Manifests + deploy.sh
├── livraisons/             # Comptes-rendus semaines 1 à 8
├── Dockerfile              # Image du frontend
└── nginx.conf              # Sert le SPA + relaie /api
```

## Commandes Angular

```bash
npm start          # serveur de développement
npm run build      # build de production -> dist/
npm test           # tests unitaires (Vitest)
```
