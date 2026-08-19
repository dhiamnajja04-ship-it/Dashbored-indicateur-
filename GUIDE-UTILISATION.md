# Guide d'utilisation

Installer la plateforme, puis s'en servir écran par écran.

---

# Partie 1 — Installation

## Prérequis

| Besoin | Version utilisée | Vérifier |
|---|---|---|
| Docker + Compose | 29.x | `docker compose version` |
| RAM disponible | 4 Go minimum | `free -m` |
| Disque | 10 Go | `df -h /` |
| Accès Internet | à l'installation seulement | — |

L'accès Internet n'est requis **que pour l'installation** (images Docker et
modèle IA). Une fois en place, la plateforme fonctionne entièrement hors ligne :
Bootstrap et ses icônes sont empaquetés dans le bundle, et le modèle IA tourne
en local.

## Installation en trois commandes

```bash
git clone https://github.com/dhiamnajja04-ship-it/Dashbored-indicateur-.git
cd Dashbored-indicateur-
docker compose up -d --build
```

Six conteneurs démarrent. La base est initialisée automatiquement au premier
lancement : schéma initial, migrations, réclamations, localisation, puis données
de démonstration.

## Télécharger le modèle IA (une seule fois)

```bash
docker compose exec ollama ollama pull qwen2.5:0.5b
```

397 Mo. Le modèle persiste dans le volume `ollama-data` : il survit à un
redémarrage, et même à `docker compose down` (mais **pas** à `down -v`).

## Vérifier

```bash
curl -s http://localhost:5169/health/plateforme
```

Attendu :

```json
{"status":"OK","gateway":"OK","metier":"OK","ia":"OK"}
```

Puis ouvrir **http://localhost:8080**.

Depuis une autre machine du réseau, remplacer `localhost` par l'IP de la
machine hôte (`hostname -I`).

## Installation sur Kubernetes

PostgreSQL y est un **serveur externe**, non conteneurisé.

```bash
kubectl create namespace indicateurs
 kubectl -n indicateurs create secret generic postgres-secret \
   --from-literal=connection-string="Host=<IP>;Port=5432;Database=<BASE>;Username=<USER>;Password=<MDP>"
./k8s/deploy.sh
kubectl -n indicateurs exec deploy/ollama -- ollama pull qwen2.5:0.5b
```

L'espace avant `kubectl create secret` est volontaire : il empêche le mot de
passe d'entrer dans l'historique du shell.

Interface : `http://$(minikube ip):30080`

---

# Partie 2 — Utilisation

## Choisir son rôle

Le sélecteur en haut à droite détermine les actions disponibles.

| Rôle | Saisir | Valider / Dévalider | Gérer les indicateurs | Supprimer |
|---|---|---|---|---|
| Agent de saisie | ✅ | ❌ | ❌ | ❌ |
| Validateur | ❌ | ✅ | ❌ | ❌ |
| Administrateur | ✅ | ✅ | ✅ | ✅ |

Le rôle est mémorisé dans le navigateur. **Si un bouton attendu n'apparaît pas,
c'est presque toujours le rôle qu'il faut changer.**

> Ce filtrage est une aide à l'usage, **pas une sécurité** : l'API reste
> ouverte et un appel direct ignore le rôle.

## Tableau de bord

La page d'accueil donne l'état général :

- **quatre compteurs** : indicateurs, valeurs saisies, valeurs validées, en attente ;
- **une barre d'avancement** segmentée par statut réel ;
- **le panneau d'analyse IA** ;
- **les cartes d'indicateurs**, cliquables.

## Gérer les indicateurs

Menu **Indicateurs**.

### Créer

Bouton **+ Nouvel indicateur**. Champs obligatoires marqués d'un astérisque
rouge : code, nom, unité.

- **Code** — identifiant unique, en majuscules (`IND-CHOM`). Un code déjà pris
  produit un message explicite, pas une erreur technique.
- **Unité** — liste de 19 unités en 5 groupes. Si la vôtre n'y est pas :
  **« Autre unité… »** bascule en saisie libre.
- **Source de données** — organisme producteur ou publication de référence.

### Rechercher et trier

La barre de recherche filtre sur le code, le nom, la description, l'unité, la
source, la fréquence et le type de collecte. Le compteur indique « N sur M ».

Un clic sur un en-tête de colonne trie ; un second clic inverse le sens.

## Saisir et valider une valeur

Ouvrir un indicateur via **Détails**.

### Ajouter une valeur

**+ Ajouter une valeur**. Le formulaire est en trois sections :

1. **Mesure** — la valeur, son degré de fiabilité, qui l'a saisie ;
2. **Territoire** — pays et gouvernorat. *Laisser « Niveau national » pour un
   agrégat sur tout le pays* ;
3. **Rattachement** — organisation, période, commentaire.

Toute valeur créée est en **Brouillon**. Le serveur l'impose : impossible de
créer une valeur déjà validée.

### Le parcours de validation

```
Brouillon ──Soumettre──▶ En validation ──Valider──▶ Validation nationale
    ▲                          │                            │
    └────────Rejeter───────────┘                            │
    └──────────────────Dévalider─────────────────────────────┘
```

| Bouton | Effet |
|---|---|
| **Soumettre** | La valeur part en revue |
| **Valider** | Elle passe en validation nationale — **elle entre alors dans le périmètre de l'IA** |
| **Rejeter** | Elle est écartée, sans être supprimée |
| **Dévalider** | Elle sort du périmètre de l'IA, sans perte de donnée |

Chaque action déclenche une notification en haut à droite.

> **Attention** — modifier le chiffre d'une valeur validée la **ramène en
> Brouillon**. La validation porte sur un chiffre précis, pas sur une ligne de
> tableau. Il faut donc revalider après correction.

### Filtrer et imprimer

Les puces au-dessus du tableau filtrent par statut ; seuls les statuts présents
sont proposés. **Imprimer** produit une page sans navigation ni boutons, avec
un en-tête Pictor Solution.

## Lancer une analyse IA

Depuis le tableau de bord (tous les indicateurs) ou depuis un indicateur
(analyse ciblée).

1. Laisser la question vide pour une synthèse générale, ou poser une question
   libre ;
2. Cliquer **Lancer l'analyse** ;
3. Patienter — le modèle tourne en local, sans carte graphique.

**Sous la réponse figurent les codes exactement transmis au modèle.** C'est la
garantie visible que seules les valeurs validées ont été analysées : si un
indicateur n'y est pas, il n'a pas pu influencer la réponse.

> Le modèle installé par défaut est **léger (0,5 milliard de paramètres)**. Il
> peut se tromper dans sa rédaction. Ce qui est garanti, c'est le **périmètre** —
> vérifiable via `GET /api/ia/contexte`, qui renvoie le prompt exact.

## Statistiques

Menu **Statistiques** :

- **écart à la cible** de chaque indicateur, en pourcentage de la cible pour
  rester comparable entre unités différentes ;
- **degré de fiabilité** des valeurs ;
- **couverture territoriale**.

La couleur donne le **sens** de l'écart, pas un jugement : dépasser une cible de
scolarisation est souhaitable, dépasser une cible de chômage ne l'est pas.

Le bouton **Voir en tableau** affiche les mêmes données sans dépendre des
couleurs.

## Réclamations

Menu **Réclamations** : déposer un signalement, filtrer par statut, répondre et
clore. Ce circuit est **indépendant** du workflow de validation.

---

# Partie 3 — En cas de problème

| Symptôme | Cause | Solution |
|---|---|---|
| L'écran reste sur « Chargement… » | Ancien bundle en cache | `Ctrl+Shift+R` |
| Un bouton attendu est absent | Rôle insuffisant | Changer de rôle en haut à droite |
| L'analyse IA renvoie 504 | Modèle trop lent | `docker compose exec ollama ollama ps` |
| « Firefox is already running » | Profil verrouillé | `firefox --no-remote --profile /tmp/ff` |
| Tous les conteneurs arrêtés | Session hôte terminée | `docker compose up -d` |
| Erreur 502 sur l'interface | Service interne arrêté | `docker compose ps` puis `logs` |

Diagnostic en une commande :

```bash
curl -s http://localhost:5169/health/plateforme
```

Le champ fautif (`metier` ou `ia`) désigne le maillon en panne.

## Arrêter

```bash
docker compose down      # arrête, conserve les données
docker compose down -v   # arrête ET EFFACE la base et le modèle
```

⚠️ `down -v` oblige à refaire le `ollama pull`.
