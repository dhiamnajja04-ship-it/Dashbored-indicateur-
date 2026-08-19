# Semaine 6 — Frontend branché aux APIs

## Ce qui change

Les données statiques de la semaine 2 sont remplacées par des appels HTTP au
service métier **via le Gateway**. Les écrans sont inchangés : seule la source
des données a bougé, comme demandé.

## Suppression de l'IP codée en dur

Le service Angular pointait sur une adresse figée :

```ts
private readonly baseUrl = 'http://192.168.153.131:5169/api/indicators';
```

C'était un obstacle concret : l'IP est celle d'une VM précise, elle est compilée
dans le bundle JavaScript, et l'image Docker aurait donc dû être reconstruite à
chaque changement d'environnement.

Le code utilise maintenant un chemin **relatif** :

```ts
private readonly baseUrl = `${environment.apiBaseUrl}/api/indicators`;  // -> /api/indicators
```

Le relais vers le Gateway est assuré par l'infrastructure, pas par le code :

| Contexte | Mécanisme |
|---|---|
| `ng serve` (développement) | [`proxy.conf.json`](../../proxy.conf.json) → `http://localhost:5169` |
| Kubernetes (production) | [`nginx.conf`](../../nginx.conf) → `http://gateway:8080` |

Bénéfices : une seule image Docker pour toutes les VM, et plus aucun problème de
CORS puisque le navigateur ne voit qu'une seule origine.

## Appels utilisés

| Écran | Appel |
|---|---|
| Tableau de bord | `GET /api/indicators` puis calcul des compteurs |
| Liste | `GET /api/indicators`, `POST`, `PUT /{id}`, `DELETE /{id}` |
| Détail | `GET /api/indicators/{id}` (valeurs incluses) |
| Détail — valeurs | `POST /{id}/valeurs`, `PUT /values/{id}`, `DELETE /values/{id}` |

## Gestion des erreurs et du chargement

- Indicateur de chargement pendant les requêtes.
- Message d'erreur lisible : `messageErreur()` privilégie le champ `message`
  renvoyé par le service métier, et traite le cas `status === 0` (serveur
  injoignable) séparément — sinon l'utilisateur voit « erreur 0 », qui ne veut
  rien dire.
- Boutons désactivés pendant l'enregistrement (`enregistrementEnCours`), pour
  éviter le double-envoi.

## Vérification

```bash
IP=$(minikube ip)

# Créer un indicateur en curl
curl -s -X POST http://$IP:30169/api/indicators \
  -H 'Content-Type: application/json' \
  -d '{"code":"IND-DEMO","nom":"Créé en curl","unite":"%","typeCollecte":"Enquête","statut":"Actif","categorieId":1}'
```

Rafraîchir `http://<IP>:30080` : l'indicateur apparaît dans la liste et sur le
tableau de bord. Les données affichées viennent bien de PostgreSQL.

## Captures

- `capture-liste-live.png` — *(à ajouter)*
- `capture-detail-live.png` — *(à ajouter)*
