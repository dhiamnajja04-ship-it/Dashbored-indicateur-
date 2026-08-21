#!/usr/bin/env bash
# =====================================================================
# Tests unitaires des règles métier.
#
#   ./ci/tests-unitaires.sh
#
# Aucun SDK .NET n'est requis sur la machine : tout s'exécute dans un
# conteneur. Ces tests ne touchent ni la base ni le réseau — ils portent sur
# la logique pure, celle qui décide de ce que voit le modèle IA.
# =====================================================================
set -uo pipefail

IMAGE="mcr.microsoft.com/dotnet/sdk:8.0"
RACINE="$(cd "$(dirname "$0")/.." && pwd)"
ECHECS=0

for projet in MetierService IaService; do
  printf '\n\033[1;34m%s.Tests\033[0m\n' "$projet"
  docker run --rm -v "$RACINE":/src -w /src "$IMAGE" \
    dotnet test "tests/${projet}.Tests/${projet}.Tests.csproj" --nologo 2>&1 \
    | grep -E "Passed!|Failed!|error|Duration" | sed 's/^/  /'
  # shellcheck disable=SC2181
  [ "${PIPESTATUS[0]}" -eq 0 ] || ECHECS=$((ECHECS + 1))
done

echo
if [ "$ECHECS" -eq 0 ]; then
  printf '\033[1;32mTous les tests unitaires passent.\033[0m\n'
else
  printf '\033[1;31m%s projet(s) en échec.\033[0m\n' "$ECHECS"
  exit 1
fi
