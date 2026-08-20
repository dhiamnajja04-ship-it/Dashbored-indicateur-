#!/usr/bin/env bash
# =====================================================================
# Déploiement de la plateforme sur un cluster Kubernetes local (kind).
#
#   ./k8s/deploy-kind.sh
#
# kind est retenu plutôt que minikube ou k3s parce qu'il ne demande AUCUN
# droit root : le cluster est lui-même constitué de conteneurs Docker. Sur une
# VM où l'on n'est pas administrateur, c'est la seule option praticable.
#
# PostgreSQL reste un serveur EXTERNE au cluster, comme l'impose le sujet :
# seule la chaîne de connexion entre dans Kubernetes, via un Secret.
# =====================================================================
set -euo pipefail

export PATH="$HOME/bin:$PATH"
NAMESPACE=indicateurs
CLUSTER=indicateurs
RACINE="$(cd "$(dirname "$0")/.." && pwd)"

# Adresse par laquelle les pods joignent les serveurs restés hors du cluster.
HOTE="${HOTE_EXTERNE:-$(hostname -I | awk '{print $1}')}"
PG_PORT="${PG_PORT:-15432}"
PG_BASE="${PG_BASE:-indicateurs_db}"
PG_USER="${PG_USER:-postgres}"
PG_MDP="${PG_MDP:-testpwd}"

echo "▸ Hôte des services externes : $HOTE"

if ! kind get clusters 2>/dev/null | grep -qx "$CLUSTER"; then
  echo "▸ Création du cluster"
  kind create cluster --config "$RACINE/k8s/kind-cluster.yaml"
else
  echo "▸ Cluster déjà présent"
fi

echo "▸ Construction et chargement des images"
for svc in metier ia gateway frontend; do
  case "$svc" in
    frontend) contexte="$RACINE" ; fichier="$RACINE/Dockerfile" ;;
    metier)   contexte="$RACINE/MetierService"  ; fichier="$contexte/Dockerfile" ;;
    ia)       contexte="$RACINE/IaService"      ; fichier="$contexte/Dockerfile" ;;
    gateway)  contexte="$RACINE/GatewayService" ; fichier="$contexte/Dockerfile" ;;
  esac
  docker build -q -t "indicateurs/$svc:1.0" -f "$fichier" "$contexte" > /dev/null
  kind load docker-image "indicateurs/$svc:1.0" --name "$CLUSTER" > /dev/null
  echo "   $svc"
done

echo "▸ Namespace, Secret et ConfigMap"
kubectl apply -f "$RACINE/k8s/00-namespace.yaml" > /dev/null

# Le mot de passe ne transite jamais par un fichier versionné.
kubectl -n "$NAMESPACE" create secret generic postgres-secret \
  --from-literal=connection-string="Host=$HOTE;Port=$PG_PORT;Database=$PG_BASE;Username=$PG_USER;Password=$PG_MDP" \
  --dry-run=client -o yaml | kubectl apply -f - > /dev/null

sed "s|http://ollama:11434|http://$HOTE:11434|" "$RACINE/k8s/02-configmap.yaml" \
  | kubectl apply -f - > /dev/null

echo "▸ Déploiements"
for f in 10-metier 30-ia 40-gateway 45-loadbalancer 50-frontend; do
  kubectl apply -f "$RACINE/k8s/$f.yaml" > /dev/null
done

echo "▸ Attente des pods"
kubectl -n "$NAMESPACE" wait --for=condition=ready pod --all --timeout=240s

echo
kubectl -n "$NAMESPACE" get pods
echo
echo "Interface : http://localhost:30080"
echo "API       : http://localhost:30169/health/plateforme"
