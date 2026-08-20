#!/usr/bin/env bash
# =====================================================================
# Vérifie qu'une plateforme démarrée se comporte comme attendu.
#
#   ./ci/verifier-plateforme.sh [URL_API]
#
# Utilisé par la CI ET utilisable en local. Le but n'est pas de tester chaque
# ligne de code, mais de garantir qu'aucune régression ne casse :
#   - le CRUD,
#   - le workflow de validation,
#   - et surtout la RÈGLE CENTRALE : l'IA ne voit que les valeurs validées.
#
# Sort en code 1 au premier échec, pour que la CI s'arrête net.
# =====================================================================
set -uo pipefail

API="${1:-http://localhost:5169}"
SUCCES=0
ECHECS=0

verifier() {
  local libelle="$1" attendu="$2" obtenu="$3"
  if [ "$attendu" = "$obtenu" ]; then
    printf '  \033[32mOK\033[0m   %-52s %s\n' "$libelle" "$obtenu"
    SUCCES=$((SUCCES + 1))
  else
    printf '  \033[31mECHEC\033[0m %-52s attendu=%s obtenu=%s\n' "$libelle" "$attendu" "$obtenu"
    ECHECS=$((ECHECS + 1))
  fi
}

code() { curl -s -o /dev/null -w '%{http_code}' --max-time 20 "$@"; }
corps() { curl -s --max-time 20 "$@"; }
json() { python3 -c "import json,sys;d=json.load(sys.stdin);print($1)"; }

echo "▸ Santé"
verifier "GET /health"                200 "$(code "$API/health")"
verifier "GET /health/plateforme"     200 "$(code "$API/health/plateforme")"

echo "▸ Lecture"
verifier "GET /api/indicators"        200 "$(code "$API/api/indicators")"
verifier "GET /api/organisations"     200 "$(code "$API/api/organisations")"
verifier "GET /api/periodes"          200 "$(code "$API/api/periodes")"
verifier "GET /api/utilisateurs"      200 "$(code "$API/api/utilisateurs")"

echo "▸ CRUD complet sur un indicateur jetable"
CODE_TEST="CI-$RANDOM"
CREE=$(curl -s --max-time 20 -X POST "$API/api/indicators" -H 'Content-Type: application/json' \
  -d "{\"code\":\"$CODE_TEST\",\"nom\":\"Indicateur CI\",\"unite\":\"%\",\"valeurCible\":50,\"anneeReference\":2025,\"categorieId\":1}")
ID=$(echo "$CREE" | json "d['id']" 2>/dev/null || echo "")
verifier "POST /api/indicators renvoie un id" "oui" "$([ -n "$ID" ] && echo oui || echo non)"

if [ -n "$ID" ]; then
  verifier "GET  /api/indicators/{id}"  200 "$(code "$API/api/indicators/$ID")"
  verifier "PUT  /api/indicators/{id}"  204 "$(code -X PUT "$API/api/indicators/$ID" -H 'Content-Type: application/json' \
    -d "{\"id\":$ID,\"code\":\"$CODE_TEST\",\"nom\":\"Modifie par la CI\",\"unite\":\"%\",\"categorieId\":1}")"
  verifier "  -> modification persistee" "Modifie par la CI" "$(corps "$API/api/indicators/$ID" | json "d['nom']")"

  echo "▸ Workflow de validation"
  VAL=$(curl -s --max-time 20 -X POST "$API/api/indicators/$ID/valeurs" -H 'Content-Type: application/json' \
    -d '{"valeur":42,"organisationId":1,"periodeId":1,"pays":"Tunisie","degreDeFiabilite":"haute","saisiePar":"CI"}')
  VID=$(echo "$VAL" | json "d['id']" 2>/dev/null || echo "")

  # Règle de sûreté : le serveur refuse toute auto-validation par le client.
  verifier "POST valeur -> isValid forcé à false" "False" "$(echo "$VAL" | json "d['isValid']")"

  AVANT=$(corps "$API/api/ia/contexte" | json "d['nbValeursValidees']")
  verifier "PATCH .../validate" 200 "$(code -X PATCH "$API/api/indicators/values/$VID/validate" \
    -H 'Content-Type: application/json' -d '{"utilisateur":"CI"}')"
  APRES=$(corps "$API/api/ia/contexte" | json "d['nbValeursValidees']")

  echo "▸ RÈGLE CENTRALE : le périmètre de l'IA suit la validation"
  verifier "valider ajoute la valeur au périmètre IA" "$((AVANT + 1))" "$APRES"

  verifier "PATCH .../devalidate" 200 "$(code -X PATCH "$API/api/indicators/values/$VID/devalidate" \
    -H 'Content-Type: application/json' -d '{"utilisateur":"CI"}')"
  RETOUR=$(corps "$API/api/ia/contexte" | json "d['nbValeursValidees']")
  verifier "dévalider la retire du périmètre IA" "$AVANT" "$RETOUR"

  echo "▸ Nettoyage"
  verifier "DELETE /api/indicators/{id}" 204 "$(code -X DELETE "$API/api/indicators/$ID")"
  verifier "  -> relecture après suppression" 404 "$(code "$API/api/indicators/$ID")"
fi

echo
echo "  $SUCCES réussite(s), $ECHECS échec(s)"
[ "$ECHECS" -eq 0 ] || exit 1
