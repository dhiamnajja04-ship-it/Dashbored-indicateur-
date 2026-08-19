# Semaine 3 — Gateway

## Rôle

Point d'entrée unique des appels API. Le frontend ne parle jamais directement à
`metier-service` ni à `ia-service` : ces deux services sont en `ClusterIP` et
n'ont aucune exposition externe.

## Routes

| Route entrante | Destination | Configuration |
|---|---|---|
| `GET /health` | Gateway lui-même | — |
| `GET /health/plateforme` | Sonde agrégée gateway + métier + IA | — |
| `* /api/indicators/**` | `metier-service:8080/api/indicators` | `MetierServiceUrl` |
| `* /api/ia/**` | `ia-service:8080/api/ia` | `IaServiceUrl` |

Le relais est générique (`{**catchAll}`) et conserve la méthode HTTP, la query
string et le corps. Une seule règle couvre donc `GET /api/indicators`,
`PATCH /api/indicators/values/3/validate`, `POST /api/ia/analyse`, etc. — il n'y
a pas à ajouter de route au Gateway chaque fois qu'un endpoint apparaît en aval.

Les URLs cibles viennent du ConfigMap `plateforme-config`, jamais du code.

## Gestion des pannes

Le sujet demande « un mock ou une réponse 502 documentée » tant que le métier
n'existe pas. Le choix retenu est le **502 explicite**, dans
[`GatewayService/Program.cs`](../../GatewayService/Program.cs) :

| Situation | Réponse |
|---|---|
| Service cible injoignable | `502` + `{"message":"Service interne injoignable..."}` |
| Service cible trop lent | `504` + message explicite |
| Service cible répond | Code et corps recopiés tels quels |

Un mock aurait renvoyé de fausses données ressemblant à des vraies — impossible
alors de distinguer « le métier est tombé » de « le métier a répondu ».

## Commandes de vérification

```bash
IP=$(minikube ip)

# 1. Le Gateway tourne
curl -s http://$IP:30169/health
# {"status":"OK","timestamp":"...","service":"GatewayService"}

# 2. Chaîne complète
curl -s http://$IP:30169/health/plateforme
# {"status":"OK","gateway":"OK","metier":"OK","ia":"OK",...}

# 3. Route métier
curl -s http://$IP:30169/api/indicators

# 4. Métier arrêté volontairement -> 502 documenté
kubectl -n indicateurs scale deploy/metier-service --replicas=0
curl -i -s http://$IP:30169/api/indicators | head -1
# HTTP/1.1 502 Bad Gateway
kubectl -n indicateurs scale deploy/metier-service --replicas=1

# 5. Le frontend de la S2 reste accessible
curl -s -o /dev/null -w "%{http_code}\n" http://$IP:30080
# 200
```

## Déploiement

```bash
docker build -t indicateurs/gateway:1.0 ./GatewayService
kubectl apply -f k8s/02-configmap.yaml
kubectl apply -f k8s/40-gateway.yaml
kubectl -n indicateurs get pods
```

Frontend et Gateway tournent bien simultanément.
