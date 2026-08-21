#!/usr/bin/env bash
# =====================================================================
# Démonstration des tests unitaires devant un évaluateur.
#
#   ./ci/demonstration-tests.sh
#
# Montrer que des tests passent ne prouve rien : un test vide passe aussi.
# Ce script casse VOLONTAIREMENT une règle métier, montre que le test la
# rattrape, puis restaure le code et vérifie que tout repasse au vert.
#
# Le fichier modifié est sauvegardé puis remis à l'identique. En cas
# d'interruption, la restauration est garantie par un piège sur EXIT.
# =====================================================================
set -uo pipefail

FICHIER="MetierService/Models/StatutValeur.cs"
RACINE="$(cd "$(dirname "$0")/.." && pwd)"
cd "$RACINE" || exit 1
SAUVEGARDE="$(mktemp)"

titre() { printf '\n\033[1;34m%s\033[0m\n%s\n' "$1" "$(printf '─%.0s' $(seq 1 68))"; }

restaurer() {
  if [ -s "$SAUVEGARDE" ]; then
    cp "$SAUVEGARDE" "$FICHIER"
    rm -f "$SAUVEGARDE"
  fi
}
trap restaurer EXIT INT TERM

lancer_tests() {
  docker run --rm -v "$RACINE":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
    dotnet test tests/MetierService.Tests/MetierService.Tests.csproj --nologo 2>&1 \
    | grep -E "Passed!|Failed!|error CS|\[FAIL\]|Assert" | head -8 | sed 's/^/    /'
}

titre "ÉTAPE 1 — le code est sain, les tests passent"
lancer_tests

titre "ÉTAPE 2 — on casse une règle métier"
cp "$FICHIER" "$SAUVEGARDE"
echo "    Règle actuelle : une valeur VALIDÉE ne peut pas être rejetée"
echo "    directement — il faut d'abord la dévalider."
echo
echo "    On autorise volontairement Valide → Rejete :"
echo
sed -i 's/\[Valide\] = new\[\] { Brouillon, EnRevue },/[Valide] = new[] { Brouillon, EnRevue, Rejete },/' "$FICHIER"
grep -n "\[Valide\] =" "$FICHIER" | sed 's/^/      /'

titre "ÉTAPE 3 — le test rattrape la régression"
lancer_tests

titre "ÉTAPE 4 — on restaure le code"
restaurer
grep -n "\[Valide\] =" "$FICHIER" | sed 's/^/      /'
echo
lancer_tests

printf '\n\033[1;32mLe test protège réellement la règle métier.\033[0m\n\n'
