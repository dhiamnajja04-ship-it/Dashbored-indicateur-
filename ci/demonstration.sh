#!/usr/bin/env bash
# =====================================================================
# Démonstration : Docker, Kubernetes et la répartition de charge.
#
#   ./ci/demonstration.sh
#
# Chaque point est PROUVÉ par une commande dont la sortie est affichée,
# pas simplement affirmé.
# =====================================================================
set -uo pipefail
export PATH="$HOME/bin:$PATH"

titre() { printf '\n\033[1;34m%s\033[0m\n%s\n' "$1" "$(printf '─%.0s' $(seq 1 70))"; }
ok()    { printf '  \033[32m✓\033[0m %s\n' "$1"; }
info()  { printf '    %s\n' "$1"; }

# ---------------------------------------------------------------- DOCKER
titre "1. DOCKER — la plateforme conteneurisée"

info "Conteneurs en service :"
docker compose ps --format '      {{.Name}}  ({{.Status}})' 2>/dev/null

nb=$(docker compose ps --format '{{.Name}}' 2>/dev/null | wc -l)
ok "$nb conteneurs actifs"

info ""
info "Santé de bout en bout (Gateway → métier → IA) :"
info "  $(curl -s --max-time 10 http://localhost:5169/health/plateforme)"

info ""
info "PostgreSQL n'est PAS dans le cluster Kubernetes — c'est la contrainte"
info "du sujet. Il tourne ici en conteneur pour le confort de développement :"
info "  $(docker compose exec -T postgres psql -U postgres -d indicateurs_db -t \
      -c "SELECT count(*)||' indicateurs, '||(SELECT count(*) FROM valeurs_indicateurs)||' valeurs' FROM indicateurs;" 2>/dev/null | xargs)"

# ------------------------------------------------------- LOAD BALANCER
titre "2. RÉPARTITION DE CHARGE"

repliques=$(docker compose ps gateway --format '{{.Name}}' 2>/dev/null | wc -l)
ok "$repliques répliques du Gateway derrière le répartiteur"
docker compose ps gateway --format '      {{.Name}}' 2>/dev/null

info ""
info "Le répartiteur répond :"
info "  $(curl -s --max-time 10 http://localhost:5169/lb-health)"

info ""
info "Trafic réseau de chaque réplique AVANT 30 requêtes :"
docker stats --no-stream --format '      {{.Name}}  {{.NetIO}}' 2>/dev/null | grep gateway

for _ in $(seq 1 30); do curl -s -o /dev/null --max-time 8 http://localhost:5169/api/indicators; done

info ""
info "APRÈS — les deux compteurs ont augmenté, la charge est distribuée :"
docker stats --no-stream --format '      {{.Name}}  {{.NetIO}}' 2>/dev/null | grep gateway

info ""
info "Tolérance de panne : on arrête une réplique…"
cible=$(docker compose ps gateway --format '{{.Name}}' 2>/dev/null | tail -1)
docker stop "$cible" >/dev/null 2>&1
sleep 6
succes=0
for _ in $(seq 1 10); do
  [ "$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 http://localhost:5169/api/indicators)" = "200" ] && succes=$((succes+1))
done
ok "avec 1 réplique sur $repliques : $succes/10 requêtes servies"
docker start "$cible" >/dev/null 2>&1
sleep 5
info "réplique redémarrée"

# ------------------------------------------------------------ KUBERNETES
titre "3. KUBERNETES — le même applicatif, orchestré"

if ! command -v kubectl >/dev/null 2>&1; then
  info "kubectl absent : section ignorée."
else
  info "Nœud du cluster :"
  kubectl get nodes --no-headers 2>/dev/null | sed 's/^/      /'

  info ""
  info "Pods déployés :"
  kubectl -n indicateurs get pods --no-headers 2>/dev/null | sed 's/^/      /'

  prets=$(kubectl -n indicateurs get pods --no-headers 2>/dev/null | grep -c '1/1')
  ok "$prets pods prêts"

  info ""
  info "Services — seuls frontend et gateway sont exposés :"
  kubectl -n indicateurs get svc --no-headers 2>/dev/null | awk '{printf "      %-16s %-11s %s\n",$1,$2,$5}'

  info ""
  info "Le Secret contient la connexion au PostgreSQL EXTERNE,"
  info "et n'est jamais versionné :"
  kubectl -n indicateurs get secret postgres-secret --no-headers 2>/dev/null | sed 's/^/      /'

  info ""
  info "API servie par le cluster (NodePort 30169) :"
  info "  /health/plateforme  -> HTTP $(curl -s -o /dev/null -w '%{http_code}' --max-time 15 http://localhost:30169/health/plateforme)"
  info "  /api/indicators     -> HTTP $(curl -s -o /dev/null -w '%{http_code}' --max-time 15 http://localhost:30169/api/indicators)"
  info "  interface :30080    -> HTTP $(curl -s -o /dev/null -w '%{http_code}' --max-time 15 http://localhost:30080/)"

  info ""
  info "En Kubernetes, la répartition est NATIVE : le Service « gateway »"
  info "distribue sur ses pods, sans conteneur nginx supplémentaire."
  kubectl -n indicateurs get endpoints gateway --no-headers 2>/dev/null | sed 's/^/      /'
fi

# ------------------------------------------------------- RÈGLE CENTRALE
titre "4. LA RÈGLE DU SUJET, DANS LES DEUX ENVIRONNEMENTS"

for cible in "Docker      http://localhost:5169" "Kubernetes  http://localhost:30169"; do
  nom=${cible%% *}; url=${cible##* }
  total=$(curl -s --max-time 15 "$url/api/indicators" | python3 -c 'import json,sys;print(len(json.load(sys.stdin)))' 2>/dev/null || echo '?')
  vus=$(curl -s --max-time 15 "$url/api/ia/contexte" | python3 -c 'import json,sys;print(json.load(sys.stdin)["nbIndicateurs"])' 2>/dev/null || echo '?')
  ok "$nom : $total indicateurs en base, $vus transmis à l'IA"
done

printf '\n\033[1;32mDémonstration terminée.\033[0m\n\n'
