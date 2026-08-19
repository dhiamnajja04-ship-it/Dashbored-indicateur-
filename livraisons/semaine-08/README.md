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
                                                │ ollama — mistral   │
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
   le tableau de bord affiche « 2 valeurs validées / 3 en attente ».
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

## Captures à joindre

- `capture-validees-vs-non-validees.png`
- `capture-reponse-ia.png`
- `capture-apres-devalidation.png`
