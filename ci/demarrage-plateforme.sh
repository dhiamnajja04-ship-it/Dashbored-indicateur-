#!/usr/bin/env bash
# =====================================================================
# Démarrage et réparation automatiques de la plateforme.
#
#   ./ci/demarrage-plateforme.sh          # démarre ce qui manque
#   ./ci/demarrage-plateforme.sh --verifier  # ne répare pas, rapporte seulement
#
# Appelé au démarrage de la machine (@reboot) et périodiquement par cron.
# Idempotent : relancer un service déjà en route ne fait rien.
#
# Il n'y a pas de service systemd ici : la VM n'accorde pas les droits root.
# Tout passe donc par la crontab de l'utilisateur, qui suffit.
# =====================================================================
set -uo pipefail
export PATH="$HOME/bin:/usr/local/bin:/usr/bin:/bin"

PROJET="/home/stage/rw9980"
JOURNAL="$HOME/plateforme-demarrage.log"
VERIFIER_SEULEMENT="${1:-}"

cd "$PROJET" || exit 1

tracer() { printf '%s  %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$1" | tee -a "$JOURNAL"; }

reparations=0

# --- 1. Le démon Docker ------------------------------------------------
if ! docker info >/dev/null 2>&1; then
  tracer "ERREUR : le démon Docker ne répond pas. Rien ne peut être réparé sans lui."
  exit 1
fi

# --- 2. La pile Docker Compose -----------------------------------------
attendus=9
actifs=$(docker compose ps --format '{{.Name}}' 2>/dev/null | wc -l)

if [ "$actifs" -lt "$attendus" ]; then
  tracer "Pile Compose incomplète : $actifs/$attendus conteneurs."
  if [ "$VERIFIER_SEULEMENT" != "--verifier" ]; then
    docker compose up -d --scale gateway=2 >>"$JOURNAL" 2>&1
    sleep 15
    actifs=$(docker compose ps --format '{{.Name}}' 2>/dev/null | wc -l)
    tracer "Après relance : $actifs/$attendus conteneurs."
    reparations=$((reparations + 1))
  fi
fi

# --- 3. La santé applicative -------------------------------------------
# Un conteneur « Up » ne garantit pas que le service répond : on interroge
# la chaîne complète, pas seulement l'état Docker.
sante=$(curl -s --max-time 10 http://localhost:5169/health/plateforme 2>/dev/null)
if ! echo "$sante" | grep -q '"status":"OK"'; then
  tracer "Santé dégradée : ${sante:-aucune réponse}"
  if [ "$VERIFIER_SEULEMENT" != "--verifier" ]; then
    docker compose restart metier-service ia-service gateway >>"$JOURNAL" 2>&1
    sleep 20
    sante=$(curl -s --max-time 10 http://localhost:5169/health/plateforme 2>/dev/null)
    tracer "Après redémarrage des services : ${sante:-aucune réponse}"
    reparations=$((reparations + 1))
  fi
fi

# --- 4. Le cluster Kubernetes ------------------------------------------
if command -v kubectl >/dev/null 2>&1; then
  if ! docker ps --format '{{.Names}}' | grep -q 'indicateurs-control-plane'; then
    tracer "Nœud kind arrêté."
    if [ "$VERIFIER_SEULEMENT" != "--verifier" ]; then
      docker start indicateurs-control-plane >>"$JOURNAL" 2>&1
      sleep 30
      reparations=$((reparations + 1))
    fi
  fi

  prets=$(kubectl -n indicateurs get pods --no-headers 2>/dev/null | grep -c '1/1' || echo 0)
  if [ "$prets" -lt 5 ] && [ "$VERIFIER_SEULEMENT" != "--verifier" ]; then
    tracer "Seulement $prets/5 pods prêts — attente de la convergence."
    # Kubernetes rétablit seul l'état désiré : on lui laisse le temps plutôt
    # que de forcer un redéploiement qui coûterait plus cher.
    sleep 30
    prets=$(kubectl -n indicateurs get pods --no-headers 2>/dev/null | grep -c '1/1' || echo 0)
    tracer "Après convergence : $prets/5 pods prêts."
  fi
fi

# --- 5. Rapport ---------------------------------------------------------
if [ "$reparations" -eq 0 ]; then
  tracer "Plateforme opérationnelle, aucune intervention nécessaire."
else
  tracer "$reparations réparation(s) effectuée(s)."
fi
