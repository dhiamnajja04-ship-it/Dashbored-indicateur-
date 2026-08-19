#!/usr/bin/env bash
# Construit les 4 images et déploie la plateforme sur le Kubernetes local de la VM.
#
#   ./k8s/deploy.sh
#
# Prérequis : Minikube démarré, et le Secret postgres-secret déjà créé
# (voir k8s/01-secret-postgres.example.yaml).

set -euo pipefail

RACINE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NS=indicateurs
TAG="${TAG:-1.0}"

echo "==> Utilisation du démon Docker de Minikube"
# Sans cela, les images seraient construites sur l'hôte et le cluster ne les
# trouverait pas (les manifests utilisent imagePullPolicy: Never).
eval "$(minikube docker-env)"

echo "==> Construction des images"
docker build -t "indicateurs/metier:${TAG}"   "${RACINE}/MetierService"
docker build -t "indicateurs/ia:${TAG}"       "${RACINE}/IaService"
docker build -t "indicateurs/gateway:${TAG}"  "${RACINE}/GatewayService"
docker build -t "indicateurs/frontend:${TAG}" "${RACINE}"

echo "==> Namespace et configuration"
kubectl apply -f "${RACINE}/k8s/00-namespace.yaml"
kubectl apply -f "${RACINE}/k8s/02-configmap.yaml"

if ! kubectl -n "${NS}" get secret postgres-secret >/dev/null 2>&1; then
  echo "ERREUR : le Secret 'postgres-secret' n'existe pas." >&2
  echo "Crée-le d'abord (voir k8s/01-secret-postgres.example.yaml)." >&2
  exit 1
fi

echo "==> Déploiement des services"
kubectl apply -f "${RACINE}/k8s/10-metier.yaml"
kubectl apply -f "${RACINE}/k8s/20-ollama.yaml"
kubectl apply -f "${RACINE}/k8s/30-ia.yaml"
kubectl apply -f "${RACINE}/k8s/40-gateway.yaml"
kubectl apply -f "${RACINE}/k8s/50-frontend.yaml"

echo "==> Redémarrage pour prendre les nouvelles images"
kubectl -n "${NS}" rollout restart deploy/metier-service deploy/ia-service deploy/gateway deploy/frontend

echo "==> Attente de la disponibilité (Ollama peut être long au premier démarrage)"
kubectl -n "${NS}" rollout status deploy/metier-service --timeout=180s
kubectl -n "${NS}" rollout status deploy/gateway        --timeout=180s
kubectl -n "${NS}" rollout status deploy/frontend       --timeout=180s
kubectl -n "${NS}" rollout status deploy/ollama         --timeout=300s || true

echo
echo "Si le modèle n'a jamais été téléchargé, lance une fois :"
echo "  kubectl -n ${NS} exec deploy/ollama -- ollama pull mistral"
echo
IP="$(minikube ip)"
echo "Interface  : http://${IP}:30080"
echo "Gateway    : http://${IP}:30169/health"
echo "Plateforme : http://${IP}:30169/health/plateforme"
