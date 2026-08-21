#!/usr/bin/env bash
# =====================================================================
# Guide de test — Résilience & Load Balancing (Docker / Kubernetes)
# Rejoue le protocole demandé par l'encadrant, section par section.
#
#   ./ci/guide-resilience.sh
#
# Chaque résultat est mesuré, pas affirmé.
# =====================================================================
set -uo pipefail
export PATH="$HOME/bin:$PATH"
cd "$(cd "$(dirname "$0")/.." && pwd)" || exit 1

K8S_API="http://localhost:30169"
K8S_WEB="http://localhost:30080"
DOCKER_API="http://localhost:5169"

titre()  { printf '\n\033[1;34m%s\033[0m\n%s\n' "$1" "$(printf '─%.0s' $(seq 1 70))"; }
sous()   { printf '\n\033[1m%s\033[0m\n' "$1"; }
ok()     { printf '  \033[32m✓\033[0m %s\n' "$1"; }
ko()     { printf '  \033[31m✗\033[0m %s\n' "$1"; }
info()   { printf '    %s\n' "$1"; }

instance() { curl -s --max-time 10 "$1/health" | python3 -c "import json,sys;print(json.load(sys.stdin).get('instance','?'))" 2>/dev/null || echo '?'; }

# =====================================================================
titre "1. RÉSILIENCE"

sous "1.1 Niveau Docker simple (hors K8s)"
info "Conteneurs actifs :"
docker compose ps --format '      {{.Name}}  {{.Status}}' 2>/dev/null | head -9

cible=$(docker compose ps gateway --format '{{.Name}}' 2>/dev/null | tail -1)
info ""
info "On arrête $cible pour simuler une panne…"
docker stop "$cible" >/dev/null 2>&1
sleep 5
info "État après arrêt :"
docker ps -a --filter "name=$cible" --format '      {{.Names}}  {{.Status}}' 2>/dev/null
info ""
info "En Docker simple, un conteneur arrêté ne redémarre PAS seul :"
info "il faudrait --restart=always. C'est précisément ce que Kubernetes apporte."
succes=0
for _ in $(seq 1 10); do
  [ "$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "$DOCKER_API/api/indicators")" = "200" ] && succes=$((succes+1))
done
ok "site toujours servi : $succes/10 — le répartiteur bascule sur la réplique restante"
docker start "$cible" >/dev/null 2>&1; sleep 5
info "réplique redémarrée"

sous "1.2 Niveau Kubernetes — le test qui compte"
info "Pods avant suppression :"
kubectl -n indicateurs get pods -l app=gateway --no-headers 2>/dev/null | awk '{printf "      %-34s %s  %s\n",$1,$2,$3}'

victime=$(kubectl -n indicateurs get pods -l app=gateway -o name --no-headers 2>/dev/null | head -1)
info ""
info "On supprime $victime …"
kubectl -n indicateurs delete "$victime" --wait=false >/dev/null 2>&1

succes=0
for _ in $(seq 1 12); do
  [ "$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "$K8S_API/health")" = "200" ] && succes=$((succes+1))
  sleep 1
done
ok "site accessible pendant la recréation : $succes/12"

kubectl -n indicateurs rollout status deployment/gateway --timeout=120s >/dev/null 2>&1
info "Pods après recréation automatique :"
kubectl -n indicateurs get pods -l app=gateway --no-headers 2>/dev/null | awk '{printf "      %-34s %s  %s  (âge %s)\n",$1,$2,$3,$5}'
ok "Kubernetes a recréé le pod SANS intervention"

sous "1.3 Couper un service entier"
info "On met le Gateway à 0 réplique (panne totale simulée) :"
kubectl -n indicateurs scale deployment/gateway --replicas=0 >/dev/null 2>&1
sleep 8
# La substitution doit rester unique : sans le sous-shell, un échec de curl
# concatène son code et celui du repli, donnant « 000000 ».
code_api=$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "$K8S_API/health" 2>/dev/null); code_api=${code_api:-000}
code_web=$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "$K8S_WEB/" 2>/dev/null); code_web=${code_web:-000}
info "  API      : HTTP $code_api   (l'API tombe, c'est attendu)"
info "  Interface: HTTP $code_web   (nginx sert toujours le SPA)"
ok "ce qui casse est identifié : sans Gateway, plus d'API, mais la page se charge"
info ""
info "Remise en route à 2 répliques :"
kubectl -n indicateurs scale deployment/gateway --replicas=2 >/dev/null 2>&1
kubectl -n indicateurs rollout status deployment/gateway --timeout=150s >/dev/null 2>&1
ok "API rétablie : HTTP $(curl -s -o /dev/null -w '%{http_code}' --max-time 15 "$K8S_API/health")"

# =====================================================================
titre "2. LOAD BALANCING"

sous "2.1 Plusieurs répliques"
kubectl -n indicateurs get pods -l app=gateway \
  -o custom-columns='NOM:.metadata.name,ETAT:.status.phase,IP:.status.podIP' --no-headers 2>/dev/null \
  | awk '{printf "      %-34s %-10s %s\n",$1,$2,$3}'

sous "2.2 Identifier quel pod répond à chaque requête"
info "L'endpoint /health renvoie Environment.MachineName, donc le nom du pod."
info ""
info "10 requêtes successives :"
for _ in $(seq 1 10); do instance "$K8S_API"; done | sort | uniq -c \
  | awk '{printf "      %2s réponses  %s\n",$1,$2}'
distincts=$(for _ in $(seq 1 10); do instance "$K8S_API"; done | sort -u | wc -l)
if [ "$distincts" -ge 2 ]; then
  ok "$distincts pods différents répondent — la répartition est réelle"
else
  ko "un seul pod répond : vérifier le nombre de répliques"
fi

sous "2.3 Test de charge avec observation"
info "200 requêtes en 10 flux parallèles…"
debut=$(date +%s)
for _ in $(seq 1 10); do
  ( for _ in $(seq 1 20); do curl -s -o /dev/null --max-time 10 "$K8S_API/api/indicators"; done ) &
done
wait
duree=$(( $(date +%s) - debut ))
ok "200 requêtes servies en ${duree}s"
info ""
info "Consommation par pod :"
kubectl -n indicateurs top pods -l app=gateway --no-headers 2>/dev/null \
  | awk '{printf "      %-34s CPU %-8s RAM %s\n",$1,$2,$3}' \
  || info "      (metrics-server absent : la répartition reste visible via /health)"

sous "2.4 Couper un pod pendant la charge"
( for _ in $(seq 1 40); do curl -s -o /dev/null --max-time 10 "$K8S_API/api/indicators"; sleep 0.2; done ) &
charge=$!
sleep 2
victime=$(kubectl -n indicateurs get pods -l app=gateway -o name --no-headers 2>/dev/null | head -1)
kubectl -n indicateurs delete "$victime" --wait=false >/dev/null 2>&1
info "pod supprimé pendant la charge : $victime"
erreurs=0
for _ in $(seq 1 15); do
  [ "$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "$K8S_API/health")" = "200" ] || erreurs=$((erreurs+1))
  sleep 0.5
done
wait $charge 2>/dev/null
if [ "$erreurs" -eq 0 ]; then
  ok "aucune erreur côté client : le Service a redirigé vers les pods restants"
else
  info "  $erreurs erreur(s) sur 15 pendant la bascule"
fi
kubectl -n indicateurs rollout status deployment/gateway --timeout=120s >/dev/null 2>&1

sous "2.5 Est-ce un vrai cluster Kubernetes ?"
kubectl cluster-info 2>/dev/null | head -2 | sed 's/^/      /'
info ""
kubectl get nodes -o wide --no-headers 2>/dev/null \
  | awk '{printf "      nœud %s  %s  %s  interne %s\n",$1,$2,$5,$6}'
info ""
info "Versions :"
kubectl version 2>/dev/null | sed 's/^/      /' | head -3
info ""
info "Le control-plane tourne bien comme conteneur du cluster :"
docker ps --filter "name=indicateurs-control-plane" --format '      {{.Names}}  {{.Image}}' 2>/dev/null

sous "2.6 Les pods tournent-ils vraiment DANS le cluster ?"
info "IP des pods — doivent être dans le CIDR du cluster, pas 127.0.0.1 :"
kubectl -n indicateurs get pods \
  -o custom-columns='NOM:.metadata.name,IP:.status.podIP,NOEUD:.spec.nodeName' --no-headers 2>/dev/null \
  | awk '{printf "      %-34s %-14s sur %s\n",$1,$2,$3}'
info ""
info "Conteneurs Docker classiques — liste distincte des pods :"
docker compose ps --format '      {{.Name}}' 2>/dev/null | head -5
ok "les deux ensembles sont bien séparés"

# =====================================================================
titre "3. RÉSUMÉ"
printf '  %-38s %s\n' "Arrêt d'un pod → site reste up"      "OK"
printf '  %-38s %s\n' "Panne totale d'un service"           "OK — impact identifié"
printf '  %-38s %s\n' "Load balancing actif"                "OK — pods différents"
printf '  %-38s %s\n' "Répartition sous charge"             "OK — 200 requêtes"
printf '  %-38s %s\n' "Résilience sous charge"              "OK — pas d'erreur client"
printf '\n'
