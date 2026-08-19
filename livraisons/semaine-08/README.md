# Semaine 8 — Validation & plateforme complète

## URL de démo

| Quoi | URL |
|---|---|
| **Interface** | `http://<IP_MINIKUBE>:30080` |
| Gateway (curl) | `http://<IP_MINIKUBE>:30169` |
| Santé globale | `http://<IP_MINIKUBE>:30169/health/plateforme` |

```bash
minikube ip
```

## 1. Architecture finale

```
                         ┌──────────────────────────────────────┐
  Navigateur ───────────▶│ frontend — nginx + Angular 22        │
   :30080                │ NodePort 30080 ; relaie /api ──┐     │
                         └────────────────────────────────┼─────┘
                                                          ▼
                         ┌──────────────────────────────────────┐
  curl ─────────────────▶│ gateway — .NET 8   NodePort 30169    │
                         │  /api/indicators/** ──┐              │
                         │  /api/ia/**  ─────────┼──┐           │
                         └───────────────────────┼──┼───────────┘
                                                 ▼  │
                    ┌───────────────────────────────┐│
                    │ metier-service — ClusterIP    ││
                    │ CRUD + WORKFLOW DE VALIDATION ││
                    │ GET /api/indicators/validated │◀┼─ HTTP interne
                    └───────────────┬───────────────┘│  (pas de SQL)
                                    │ Secret k8s     ▼
                                    ▼        ┌────────────────────┐
                    ┌───────────────────────┐│ ia-service         │
                    │ PostgreSQL HORS cluster││ ClusterIP          │
                    │ <IP>:5432             │└──────────┬─────────┘
                    └───────────────────────┘           ▼
                                                ┌────────────────────┐
                                                │ ollama — qwen2.5   │
                                                │ ClusterIP + PVC    │
                                                └────────────────────┘
```

## 2. Workflow de validation

### Les états

```
   ┌────────────┐  soumettre  ┌────────────┐   valider   ┌────────────┐
   │ Brouillon  │────────────▶│  EnRevue   │────────────▶│   Valide   │
   │ is_valid=F │◀────────────│ is_valid=F │             │ is_valid=T │
   └────────────┘             └─────┬──────┘             └─────┬──────┘
         ▲                          │ rejeter                  │
         │                          ▼                          │
         │                    ┌────────────┐                   │
         └────────────────────│  Rejete    │                   │
                   dévalider  │ is_valid=F │◀──────────────────┘
                              └────────────┘
```

Seul l'état **`Valide`** met `is_valid = true`, et **`is_valid` est le seul
critère lu par l'IA**.

### Qui fait quoi

| Acteur | Action | Effet |
|---|---|---|
| Agent de saisie | Crée une valeur | `Brouillon`, `is_valid = false` — imposé par le serveur |
| Agent de saisie | « Soumettre » | `Brouillon → EnRevue` |
| Validateur | « Valider » | `→ Valide`, `is_valid = true`, `valide_par` renseigné |
| Validateur | « Rejeter » | `EnRevue → Rejete` |
| Validateur | « Dévalider » | `Valide → Brouillon`, sort du périmètre IA sans perte de donnée |

### API

| Méthode | Route | Rôle |
|---|---|---|
| `PATCH` | `/api/indicators/values/{id}/validate` | Valider |
| `PATCH` | `/api/indicators/values/{id}/devalidate` | Dévalider |
| `PATCH` | `/api/indicators/values/{id}/statut` | Transition libre |
| `GET` | `/api/indicators/validated` | **Périmètre exact de l'IA** |
| `PUT` | `/api/indicators/values/{id}/valider` | Ancienne route, conservée |

Une transition interdite renvoie un `400` qui indique le statut actuel **et** les
transitions possibles, plutôt qu'un refus sec.

### Trois garde-fous

La règle « l'IA ne voit que le validé » n'est pas seulement une intention : elle
est appliquée à trois niveaux, chacun couvrant une faille des autres.

1. **Le client ne peut pas s'auto-valider.** `POST .../valeurs` force
   `is_valid = false` et `PUT .../values/{id}` ignore le statut envoyé dans le
   corps. Sans cela, il suffirait de poster `{"isValid": true}` pour injecter
   dans l'IA une donnée que personne n'a relue.

2. **Modifier un chiffre validé le dévalide.** Si la valeur numérique d'une
   mesure déjà validée change, elle repasse en `Brouillon`. La validation porte
   sur un chiffre précis, pas sur une ligne de table.

3. **La base refuse l'incohérence.** La contrainte `valeurs_statut_coherent`
   interdit `is_valid = true` avec un statut autre que `Valide` — un `UPDATE`
   manuel en SQL ne peut pas contourner le workflow.

## 3. Filtrage côté IA

Le filtre est écrit **une seule fois**, dans le service métier :

```csharp
Valeurs = i.ValeursIndicateurs.Where(v => v.IsValid)   // MetierService
```

Le service IA ne peut pas le contourner : il n'a aucune connexion PostgreSQL (le
manifest `30-ia.yaml` ne lui en fournit pas), et son unique source de données est
`/api/indicators/validated`. Il applique en outre un second filtre défensif avant
de construire le prompt.

Si aucune valeur n'est validée, **le modèle n'est pas appelé du tout** : il
répondrait sinon un texte plausible et entièrement inventé.

## 4. Scénario de démonstration

Jeu de données : `db/02-donnees-demo.sql` — 5 indicateurs, **2 validés**.

| Code | Indicateur | Valeur | Statut | Vu par l'IA |
|---|---|---|---|---|
| `IND-CHOM` | Taux de chômage | 12,4 % | **Valide** | ✅ |
| `IND-SCOL` | Taux de scolarisation | 96,2 % | **Valide** | ✅ |
| `IND-INFL` | Taux d'inflation | 6,8 % | EnRevue | ❌ |
| `IND-SANT` | Densité médicale | 9,1 | Brouillon | ❌ |
| `IND-NUM` | Couverture Internet | 71,5 % | Rejete | ❌ |

### Étape 1 — Périmètre initial

```bash
IP=$(minikube ip)
curl -s http://$IP:30169/api/indicators/validated | jq '.[].code'
# "IND-CHOM"
# "IND-SCOL"
```

### Étape 2 — L'IA ne parle que de ces deux-là

```bash
curl -s -X POST http://$IP:30169/api/ia/analyse \
  -H 'Content-Type: application/json' -d '{}' | jq '{indicateursUtilises, nbValeursValidees, reponse}'
```

`indicateursUtilises` vaut `["IND-CHOM","IND-SCOL"]`. Le texte ne mentionne ni
l'inflation, ni la densité médicale, ni la couverture Internet.

### Étape 3 — Valider une troisième valeur

Dans l'interface : `/indicateurs/{id de IND-INFL}` → bouton **✔️ Valider**.
Ou en curl :

```bash
VID=$(curl -s http://$IP:30169/api/indicators | jq '[.[] | select(.code=="IND-INFL")][0].valeursIndicateurs[0].id')
curl -s -X PATCH http://$IP:30169/api/indicators/values/$VID/validate \
  -H 'Content-Type: application/json' -d '{"utilisateur":"Tuteur"}' | jq
```

### Étape 4 — Le périmètre s'élargit

```bash
curl -s -X POST http://$IP:30169/api/ia/analyse \
  -H 'Content-Type: application/json' -d '{}' | jq '.indicateursUtilises'
# ["IND-CHOM","IND-INFL","IND-SCOL"]
```

L'inflation apparaît maintenant dans la réponse.

### Étape 5 — Dévalider : le périmètre se referme

```bash
curl -s -X PATCH http://$IP:30169/api/indicators/values/$VID/devalidate \
  -H 'Content-Type: application/json' -d '{"utilisateur":"Tuteur"}' | jq
```

L'inflation disparaît de nouveau de l'analyse. La donnée, elle, est toujours en
base : c'est sa **visibilité par l'IA** qui a changé, pas son existence.

### Étape 6 — Cas limite

```bash
# Tout dévalider
psql -h <IP> -U <USER> -d <BASE> -c \
  "UPDATE valeurs_indicateurs SET is_valid=false, statut='Brouillon';"

curl -s -X POST http://$IP:30169/api/ia/analyse \
  -H 'Content-Type: application/json' -d '{}' | jq -r '.reponse'
# "Aucun indicateur validé n'est disponible pour le moment. ..."
```

Aucun appel au modèle n'est effectué : rien n'est inventé.

## 5. Démonstration bout en bout dans le navigateur

1. `http://<IP>:30080` — les indicateurs affichés viennent de PostgreSQL ;
   le tableau de bord affiche « 2 valeurs validées / 4 en attente ».
2. Ouvrir un indicateur → chaque valeur porte son badge de statut
   (Validé / En revue / Brouillon / Rejeté) et ses boutons d'action.
3. Cliquer **✔️ Valider** → le badge passe au vert, le compteur se met à jour.
4. Revenir au tableau de bord → **« Lancer l'analyse »**.
5. La réponse s'affiche, avec les codes des indicateurs réellement transmis.
6. Dévalider une valeur, relancer l'analyse : elle a disparu de la réponse.

## 6. État des conteneurs

```bash
kubectl -n indicateurs get pods,svc
curl -s http://$IP:30169/health/plateforme | jq
# {"status":"OK","gateway":"OK","metier":"OK","ia":"OK",...}
```

| Pod | Rôle | Exposition |
|---|---|---|
| `frontend` | Angular + nginx | NodePort 30080 |
| `gateway` | Point d'entrée API | NodePort 30169 |
| `metier-service` | CRUD + validation | ClusterIP |
| `ia-service` | Analyse | ClusterIP |
| `ollama` | Modèle local | ClusterIP + PVC 15 Gi |

PostgreSQL reste hors cluster, sur le serveur du tuteur.

## 7. Captures — scénario réellement exécuté

Captures prises sur la plateforme en fonctionnement (Docker Compose sur la VM),
dans [`captures/`](captures/). Le scénario a été joué de bout en bout dans le
navigateur, pas reconstitué.

| Capture | Ce qu'elle montre |
|---|---|
| [`01-tableau-de-bord.png`](captures/01-tableau-de-bord.png) | 5 indicateurs, 6 valeurs saisies, **2 validées, 4 en attente** |
| [`02-liste-indicateurs.png`](captures/02-liste-indicateurs.png) | Liste alimentée par PostgreSQL via le Gateway |
| [`03-detail-valeur-validee.png`](captures/03-detail-valeur-validee.png) | IND-CHOM : valeur `12,4 %`, statut **Validé**, « par Administrateur » |
| [`04-detail-valeur-non-validee.png`](captures/04-detail-valeur-non-validee.png) | IND-INFL : valeur `6,8 %`, statut **EnRevue** — hors périmètre IA |
| [`05-reponse-ia-validees-uniquement.png`](captures/05-reponse-ia-validees-uniquement.png) | Réponse IA + badges **IND-CHOM** et **IND-SCOL** uniquement |
| [`06-apres-devalidation-statut-brouillon.png`](captures/06-apres-devalidation-statut-brouillon.png) | Après « Dévalider » : statut **Brouillon**, « 0 valeur(s) validée(s) sur 1 » |
| [`07-reponse-ia-perimetre-reduit.png`](captures/07-reponse-ia-perimetre-reduit.png) | Nouvelle analyse : **IND-CHOM a disparu**, seul IND-SCOL subsiste |

### Interface (captures 08 à 19)

Ces captures documentent l'interface livrée. Elles complètent le scénario
ci-dessus, qui porte sur la règle métier.

| Capture | Ce qu'elle montre |
|---|---|
| [`08-interface-tableau-de-bord.png`](captures/08-interface-tableau-de-bord.png) | Compteurs, panneau IA, cartes d'indicateurs |
| [`09-interface-liste-indicateurs.png`](captures/09-interface-liste-indicateurs.png) | Tableau, colonne Source, actions par ligne |
| [`10-interface-detail-indicateur.png`](captures/10-interface-detail-indicateur.png) | Fil d'Ariane, en-tête, valeurs et workflow |
| [`11-interface-formulaire-indicateur.png`](captures/11-interface-formulaire-indicateur.png) | Formulaire en sections, champs obligatoires signalés |
| [`12-interface-colonnes-valeurs.png`](captures/12-interface-colonnes-valeurs.png) | Fiabilité, saisi par, commentaire, dates de saisie et de mise à jour |
| [`13-interface-roles.png`](captures/13-interface-roles.png) | Sélecteur de rôle et actions filtrées |
| [`14-interface-reclamations.png`](captures/14-interface-reclamations.png) | Dépôt, filtres et traitement d'une réclamation |
| [`15-interface-notification.png`](captures/15-interface-notification.png) | Notification après un changement de statut |
| [`16-interface-recherche-et-tri.png`](captures/16-interface-recherche-et-tri.png) | Recherche filtrante et tri par colonne |
| [`17-interface-selecteur-unite.png`](captures/17-interface-selecteur-unite.png) | 19 unités en 5 groupes, échappatoire « Autre unité » |
| [`18-interface-ecart-a-la-cible.png`](captures/18-interface-ecart-a-la-cible.png) | Écart entre la valeur et la cible de l'indicateur |
| [`19-interface-territoire.png`](captures/19-interface-territoire.png) | Valeur nationale et valeur de gouvernorat côte à côte |
| [`20-interface-sans-cdn.png`](captures/20-interface-sans-cdn.png) | Interface complète avec **zéro requête externe** : Bootstrap et ses icônes sont empaquetés |

### Impression (captures 21 et 22)

| Fichier | Contenu |
|---|---|
| [`21-impression-liste-indicateurs.pdf`](captures/21-impression-liste-indicateurs.pdf) | Liste des indicateurs, mise en page papier |
| [`22-impression-valeurs-indicateur.pdf`](captures/22-impression-valeurs-indicateur.pdf) | Valeurs d'un indicateur, mise en page papier |

En-tête Pictor Solution ajouté, navigation, boutons, panneau IA et colonne
Actions masqués : on imprime la donnée, pas l'application.


### Preuve chiffrée du filtrage

Le endpoint `GET /api/ia/contexte` renvoie le périmètre exact soumis au modèle,
sans l'appeler. Mesures relevées pendant le scénario :

| Étape | `nbIndicateurs` | `nbValeursValidees` |
|---|---|---|
| État initial (2 valeurs validées) | **2** | **2** |
| Après « Dévalider » sur IND-CHOM (dans l'interface) | **1** | **1** |
| Après re-validation | **2** | **2** |

C'est la démonstration demandée : *« 5 indicateurs en base, 2 validés → la
réponse IA ne parle que de ces 2-là »*. Dévalider en retire un du périmètre
**sans supprimer la donnée**.

## 8. Rôles et libellés du workflow

### Sélecteur de rôle

Un sélecteur « Agir en tant que » est disponible dans la barre de navigation :

| Rôle | Peut saisir / modifier | Peut valider, rejeter, dévalider | Peut supprimer |
|---|---|---|---|
| Agent de saisie | ✅ | ❌ | ❌ |
| Validateur | ❌ | ✅ | ❌ |
| Administrateur | ✅ | ✅ | ✅ |

Le rôle choisi est enregistré dans le navigateur et sert d'auteur dans le champ
`valide_par` lors d'un changement de statut.

> ⚠️ **Ce n'est pas un mécanisme de sécurité.** Il n'y a ni compte, ni mot de
> passe, ni contrôle côté serveur : n'importe qui peut changer de rôle depuis le
> menu, et l'API `MetierService` accepte toujours toutes les requêtes. C'est un
> confort de démonstration qui rend le workflow lisible. Une vraie séparation
> des droits demanderait une authentification (table utilisateurs, jeton, et
> attribut `[Authorize]` sur les contrôleurs métier).

### Libellés affichés

Les libellés ont été alignés sur le vocabulaire métier attendu, **sans toucher
aux valeurs techniques ni à la règle de filtrage** :

| Valeur en base / API | Libellé affiché | `is_valid` |
|---|---|---|
| `Brouillon` | Brouillon | `false` |
| `EnRevue` | **En validation** | `false` |
| `Valide` | **Validation nationale** | **`true`** — périmètre IA |
| `Rejete` | Rejeté | `false` |

**L'état « Certifié » n'est pas implémenté.** Il demanderait un cinquième état :
constante dans `MetierService/Models/StatutValeur.cs`, nouvelles transitions,
mise à jour de la contrainte `CHECK valeurs_statut_coherent` en base, et une
décision explicite — est-ce que « Certifié » met aussi `is_valid = true`, ou
seulement « Validation nationale » ? Comme cette décision change le périmètre
analysé par l'IA, c'est-à-dire la règle évaluée du sujet, elle n'a pas été prise
unilatéralement.

## 9. Limite connue — qualité du modèle

Le modèle retenu est `qwen2.5:0.5b`, imposé par la VM (4 cœurs, voir
[semaine-01](../semaine-01/README.md#révision-du-choix-après-mesure-sur-la-vm)).
Il faut distinguer deux choses :

- **Le filtrage est fiable** — c'est la règle évaluée par le sujet. Le modèle ne
  reçoit *que* les valeurs validées : le périmètre est construit côté métier
  (`GET /api/indicators/validated`), vérifiable via `GET /api/ia/contexte`, et
  affiché à l'écran. Sur toutes les exécutions, seuls `IND-CHOM` et `IND-SCOL`
  ont été transmis et cités.
- **La rédaction est faible** — à 0,5 milliard de paramètres, le modèle répète
  des sections et lui arrive d'inventer des lignes. Exemple réellement obtenu :

  ```
  1. Taux de chômage (IND-CHOM) : 12,4%
     - 12,4% (… période #1 …)
     - 12,4% (… période #2 …)   <-- n'existe pas en base
     - 12,4% (… période #3 …)   <-- n'existe pas en base
  ```

  Une seule valeur existe en base. Le modèle n'a pas ajouté d'indicateur non
  validé (la règle tient), mais il a dupliqué une ligne.

**Conséquence honnête** : la contrainte « pas de texte inventé » (semaine 5) est
respectée sur le *périmètre*, pas encore sur le *détail rédactionnel*.

C'est une limite du dimensionnement de la VM, pas de l'architecture : le modèle
se change avec une seule variable (`Ollama__Model`), sans reconstruire d'image.

**Montée en gamme recommandée** dès qu'une connexion stable est disponible —
`gemma2:2b` (~1,6 Go) rédige nettement mieux en français :

```bash
docker compose exec ollama ollama pull gemma2:2b
# puis dans docker-compose.yml : Ollama__Model: "gemma2:2b"
docker compose up -d --force-recreate ia-service
```

Deux tentatives de téléchargement ont échoué ici (`TLS handshake timeout`, puis
`unexpected EOF`) : le débit de la VM s'est révélé très irrégulier, entre
200 Ko/s et 3,6 Mo/s. `ollama pull` reprend un téléchargement interrompu, il
suffit de relancer la commande.
