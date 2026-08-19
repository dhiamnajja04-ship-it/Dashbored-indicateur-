# Déploiement Kubernetes

## Vue d'ensemble

```
                    ┌─────────────────────────────────────────────┐
   Navigateur ─────▶│ frontend (nginx + Angular)  NodePort 30080  │
                    │   sert le SPA, relaie /api ─────────┐       │
                    └────────────────────────────────────┼────────┘
                                                         ▼
                    ┌────────────────────────────────────────────┐
   curl ───────────▶│ gateway (.NET 8)            NodePort 30169  │
                    │   /api/indicators/**  ──▶ metier-service    │
                    │   /api/ia/**          ──▶ ia-service        │
                    └───────┬────────────────────────┬───────────┘
                            ▼                        ▼
        ┌──────────────────────────┐   ┌──────────────────────────┐
        │ metier-service (.NET 8)  │◀──│ ia-service (.NET 8)      │
        │ ClusterIP — CRUD +       │   │ ClusterIP — prompt +     │
        │ workflow de validation   │   │ appel du modèle          │
        └───────────┬──────────────┘   └───────────┬──────────────┘
                    │ Secret                        │
                    ▼                               ▼
        ┌──────────────────────────┐   ┌──────────────────────────┐
        │ PostgreSQL — HORS cluster│   │ ollama (ClusterIP)       │
        │ serveur du tuteur :5432  │   │ modèle local + PVC 15 Gi │
        └──────────────────────────┘   └──────────────────────────┘
```

Deux règles d'architecture sont matérialisées ici :

- **`metier-service` et `ia-service` sont en `ClusterIP`**, donc inaccessibles
  depuis l'extérieur. Tout passe par le Gateway.
- **`ia-service` n'a aucune variable de connexion PostgreSQL.** Même en cas
  d'erreur de code, il lui est impossible de lire la base directement : il ne
  peut obtenir des indicateurs que via `metier-service`, qui filtre les valeurs
  non validées.

PostgreSQL n'apparaît dans aucun manifest : c'est un serveur externe, conformément
à la consigne.

## Prérequis

```bash
minikube start --cpus=4 --memory=10g   # Ollama a besoin de RAM
kubectl get nodes
```

Vérifier que la VM joint le serveur Postgres :

```bash
nc -zv <IP_POSTGRES> 5432
```

## 1. Créer le Secret (jamais dans Git)

Le fichier `01-secret-postgres.example.yaml` n'est qu'un modèle : il ne doit pas
être appliqué tel quel. Le vrai secret se crée en ligne de commande — l'espace
initial évite de laisser le mot de passe dans l'historique du shell :

```bash
 kubectl create namespace indicateurs
 kubectl -n indicateurs create secret generic postgres-secret \
   --from-literal=connection-string="Host=<IP>;Port=5432;Database=<BASE>;Username=<USER>;Password=<MDP>"
```

## 2. Préparer la base

```bash
psql -h <IP> -U <USER> -d <BASE> -f ../db/01-migration-alignement.sql
psql -h <IP> -U <USER> -d <BASE> -f ../db/02-donnees-demo.sql
```

## 3. Déployer

```bash
./deploy.sh
```

## 4. Télécharger le modèle (une seule fois)

Le modèle n'est pas dans l'image : il est téléchargé dans le volume persistant,
et survit donc aux redémarrages du pod.

```bash
kubectl -n indicateurs exec deploy/ollama -- ollama pull qwen2.5:0.5b
kubectl -n indicateurs exec deploy/ollama -- ollama list
```

## 5. Vérifier

```bash
IP=$(minikube ip)

curl -s http://$IP:30169/health                 # Gateway seul
curl -s http://$IP:30169/health/plateforme      # Gateway + métier + IA
curl -s http://$IP:30169/api/indicators | jq
curl -s http://$IP:30169/api/indicators/validated | jq   # périmètre exact de l'IA

curl -s -X POST http://$IP:30169/api/ia/analyse \
  -H 'Content-Type: application/json' -d '{}' | jq
```

Interface : `http://<IP_MINIKUBE>:30080`

## Dépannage

| Symptôme | Cause probable | Vérification |
|---|---|---|
| `ErrImageNeverPull` | Image construite hors du démon Minikube | `eval $(minikube docker-env)` puis rebuild |
| Pod métier en `CrashLoopBackOff` | Secret absent ou chaîne invalide | `kubectl -n indicateurs logs deploy/metier-service` |
| Métier `0/1 Running` | `/health/ready` KO : Postgres injoignable | `curl .../health/ready` depuis le pod gateway |
| IA répond 503 | Modèle pas encore téléchargé | `kubectl -n indicateurs exec deploy/ollama -- ollama list` |
| IA répond « aucun indicateur validé » | Comportement normal : rien n'est validé | `curl .../api/indicators/validated` |
| Analyse IA très lente | Génération CPU | Augmenter la RAM/CPU Minikube, ou modèle plus léger (`phi3:mini`) |
