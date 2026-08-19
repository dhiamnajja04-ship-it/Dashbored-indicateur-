# Plateforme Indicateurs

Plateforme conteneurisée d'indicateurs avec workflow de validation et analyse par
un modèle IA local. Stage DevOps.

## Guides

| Guide | Pour quoi |
|---|---|
| [`GUIDE-UTILISATION.md`](GUIDE-UTILISATION.md) | **Installer** et **se servir** de la plateforme, écran par écran |
| [`GUIDE-PLATEFORME.md`](GUIDE-PLATEFORME.md) | **Ce que contient le projet** : services, tables, API, fichiers, limites |
| [`GUIDE-LANCEMENT.md`](GUIDE-LANCEMENT.md) | Démarrer, vérifier, dépanner |
| [`GUIDE-PRESENTATION.md`](GUIDE-PRESENTATION.md) | Soutenance : déroulé et questions attendues |

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
| — | Modèle | Ollama (`qwen2.5:0.5b`) | ClusterIP + PVC |

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
psql -h <IP> -U <USER> -d <BASE> -f db/03-reclamations.sql

# 2. Secret (l'espace initial évite l'historique du shell)
 kubectl create namespace indicateurs
 kubectl -n indicateurs create secret generic postgres-secret \
   --from-literal=connection-string="Host=<IP>;Port=5432;Database=<BASE>;Username=<USER>;Password=<MDP>"

# 3. Build + déploiement
./k8s/deploy.sh

# 4. Modèle (une seule fois, persiste dans le PVC)
kubectl -n indicateurs exec deploy/ollama -- ollama pull qwen2.5:0.5b
```

Interface : `http://$(minikube ip):30080`

Détails et dépannage : [`k8s/README.md`](k8s/README.md).

## Lancement manuel sur une seule machine (Docker Compose)

Pour développer ou faire une démonstration sans cluster :

```bash
docker compose up -d --build          # construit et démarre les 6 conteneurs
docker compose exec ollama ollama pull qwen2.5:0.5b   # une seule fois (~400 Mo)
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
Ollama doit tourner sur la machine (`ollama serve` + `ollama pull qwen2.5:0.5b`).

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

### Réclamations

| Méthode | Route |
|---|---|
| `GET` | `/api/reclamations[?statut=Nouvelle&indicateurId=1]` |
| `GET` | `/api/reclamations/{id}` |
| `GET` | `/api/reclamations/statistiques` |
| `POST` | `/api/reclamations` |
| `PATCH` | `/api/reclamations/{id}/statut` |
| `DELETE` | `/api/reclamations/{id}` |

Une réclamation ne modifie jamais une valeur : elle n'a donc aucun effet sur
`is_valid` et n'entre jamais dans le périmètre transmis à l'IA.

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

**Codes techniques et libellés affichés ne sont pas identiques.** La base et
l'API manipulent les codes ; l'interface affiche des libellés métier :

| Code en base / API | Libellé affiché | `is_valid` |
|---|---|---|
| `Brouillon` | Brouillon | `false` |
| `EnRevue` | En validation | `false` |
| `Valide` | **Validation nationale** | **`true`** |
| `Rejete` | Rejeté | `false` |

Ce découplage évite une migration de toutes les données à chaque changement de
vocabulaire métier : renommer un libellé est une ligne dans
[`indicateur.service.ts`](src/app/services/indicateur.service.ts), pas un
`UPDATE` sur la base.

Seul `Valide` met `is_valid = true`. Modifier le chiffre d'une valeur validée la
ramène en `Brouillon` : la validation porte sur un chiffre précis, pas sur une
ligne de table.

## Rôles

Trois rôles filtrent les actions disponibles dans l'interface. Le rôle est
choisi dans la barre de navigation et mémorisé dans le navigateur.

| Rôle | Saisir | Valider / Dévalider | Gérer les indicateurs | Supprimer |
|---|---|---|---|---|
| Agent de saisie | ✅ | ❌ | ❌ | ❌ |
| Validateur | ❌ | ✅ | ❌ | ❌ |
| Administrateur | ✅ | ✅ | ✅ | ✅ |

> ⚠️ C'est une séparation **d'interface**, pas une sécurité. Il n'y a pas
> d'authentification : l'API reste ouverte, et un appel `curl` direct ignore le
> rôle. Le sujet du stage ne demandait pas d'authentification ; ajouter un
> véritable contrôle d'accès supposerait une table utilisateurs, des jetons et
> une vérification côté serveur sur chaque endpoint.

## Localisation des valeurs

La localisation porte sur la **valeur**, pas sur l'indicateur : « taux de
chômage » est une définition nationale, c'est *la mesure* qui se rapporte à un
territoire. Un même indicateur porte donc une valeur nationale **et** des
valeurs par gouvernorat.

- `pays` — 6 pays proposés, `Tunisie` par défaut
- `gouvernorat` — les 24 gouvernorats de Tunisie ; **vide = niveau national**

Le territoire est transmis au modèle IA, avec une consigne explicite de ne
jamais comparer un gouvernorat à un total national comme s'ils étaient de même
nature.

## Réclamations

Module de signalement indépendant du workflow de validation
([`ReclamationsController`](MetierService/Controllers/ReclamationsController.cs)).

| Méthode | Route |
|---|---|
| `GET` | `/api/reclamations` |
| `GET` | `/api/reclamations/{id}` |
| `GET` | `/api/reclamations/statistiques` |
| `POST` | `/api/reclamations` |
| `PATCH` | `/api/reclamations/{id}/statut` |
| `DELETE` | `/api/reclamations/{id}` |

## Interface

- **Marque Pictor Solution**, barre de navigation et pied de page complet
- **Notifications** en surimpression après chaque action (succès, erreur,
  changement de statut), avec `aria-live` pour les lecteurs d'écran
- **Recherche et tri** sur la liste des indicateurs — filtrage sur 7 champs,
  tri sur 5 colonnes. Fait **en mémoire** : correct à cette échelle, à déporter
  côté API au-delà de quelques centaines d'indicateurs
- **Impression** des tableaux (`@media print`) : en-tête Pictor Solution, colonne
  Actions masquée, badges lisibles en noir et blanc
- **Écart à la cible** affiché par valeur, volontairement **neutre en couleur** :
  dépasser une cible de scolarisation est bon, dépasser une cible de chômage est
  mauvais — l'interface donne le sens de l'écart, elle ne le juge pas
- **Unités** : liste fermée de 19 unités en 5 groupes, avec échappatoire
  « Autre unité » pour ne jamais bloquer une donnée existante
- **Pagination côté serveur** : la page, la recherche et le tri sont calculés
  par PostgreSQL. Seule la page demandée transite. La recherche est insensible
  à la casse **et aux accents** (`unaccent`) : « densite » trouve « Densité
  médicale ».
- **Documents justificatifs** : dépôt de fichier réel (10 Mo max, extensions
  documentaires uniquement), stocké sur un volume, métadonnées en base.
- **Référentiel d'agents** : « saisi par » est choisi dans une liste fermée au
  lieu d'être du texte libre.
- **Page Statistiques** : écart de chaque indicateur à sa cible, répartition
  par degré de fiabilité, couverture territoriale, et une **vue tabulaire** qui
  double le graphique pour rester lisible sans les couleurs.
- **Avancement de la validation** sur le tableau de bord : une barre segmentée
  montre la répartition **réelle** par statut, pas seulement « validé / reste ».
  Une valeur non validée peut être en revue, en brouillon ou rejetée.
- **Filtre par statut** sur le tableau des valeurs. Les statuts absents ne sont
  pas proposés : afficher « Rejeté (0) » n'aide personne.
- **Accessibilité clavier** : lien d'évitement vers le contenu, repère `<main>`,
  `aria-sort` sur les colonnes triables, `aria-pressed` sur les filtres, et un
  contour de focus visible uniquement au clavier (`:focus-visible`).
- **Aucune dépendance à Internet** : Bootstrap et ses icônes sont empaquetés
  dans le bundle, pas chargés depuis un CDN. L'interface reste complète sur une
  machine isolée — indispensable pour une démonstration hors ligne.

## Structure

```
rw9980/
├── src/                    # Frontend Angular
├── GatewayService/         # .NET 8 — routage
├── MetierService/          # .NET 8 — CRUD + validation
├── IaService/              # .NET 8 — prompt + modèle
├── db/                     # Migration SQL + données de démo
├── k8s/                    # Manifests + deploy.sh
├── livraisons/             # Comptes-rendus semaines 1 à 8 + captures
├── Dockerfile              # Image du frontend
├── nginx.conf              # Sert le SPA + relaie /api
├── docs/besoins/           # Sujet du stage
├── GUIDE-UTILISATION.md    # Installation et utilisation
├── GUIDE-PLATEFORME.md     # Inventaire complet du projet
├── GUIDE-LANCEMENT.md      # Démarrer la plateforme pas à pas
└── GUIDE-PRESENTATION.md   # Soutenance : déroulé et questions attendues
```

## Commandes Angular

```bash
npm start          # serveur de développement
npm run build      # build de production -> dist/
```

> Le projet ne contient **pas** de tests unitaires : le sujet du stage n'en
> demande pas, et aucun fichier `*.spec.ts` n'est présent. Vitest et jsdom
> figurent encore dans les `devDependencies` du gabarit Angular, mais le script
> `npm test` (`ng test`) n'aurait rien à exécuter.
