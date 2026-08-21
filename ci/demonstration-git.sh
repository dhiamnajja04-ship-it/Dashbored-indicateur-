#!/usr/bin/env bash
# =====================================================================
# Démonstration du travail versionné, en ligne de commande.
#
#   ./ci/demonstration-git.sh
#
# Montre ce qu'un évaluateur veut vérifier : que le travail est réparti sur
# la durée, sur une branche personnelle, avec des messages exploitables — et
# qu'aucun secret n'a été versionné.
# =====================================================================
set -uo pipefail
cd "$(cd "$(dirname "$0")/.." && pwd)" || exit 1

titre() { printf '\n\033[1;34m%s\033[0m\n%s\n' "$1" "$(printf '─%.0s' $(seq 1 68))"; }

titre "1. Où l'on travaille"
printf '  branche courante : \033[1m%s\033[0m\n' "$(git branch --show-current)"
echo "  dépôt distant    : $(git remote get-url origin 2>/dev/null)"
echo
echo "  Toutes les branches :"
git branch -a --format='      %(refname:short)  →  %(objectname:short)  %(contents:subject)' | head -6

titre "2. Volume du travail"
echo "  commits sur la branche      : $(git rev-list --count HEAD)"
echo "  fichiers versionnés         : $(git ls-files | wc -l)"
echo "  premier commit              : $(git log --reverse --format='%ad' --date=short | head -1)"
echo "  dernier commit              : $(git log -1 --format='%ad' --date=short)"
echo
echo "  Répartition par jour :"
git log --format='%ad' --date=short | sort | uniq -c | awk '{printf "      %s : %s commit(s)\n",$2,$1}'

titre "3. Historique récent"
git log --oneline -12 | sed 's/^/  /'

titre "4. Un message de commit, en entier"
echo "  Les messages expliquent le POURQUOI, pas seulement le quoi :"
echo
git log -1 --format='%B' --skip=2 | head -14 | sed 's/^/      /'

titre "5. Ce qui a le plus changé"
git log --format='' --name-only | sort | uniq -c | sort -rn | head -8 \
  | awk '{printf "      %3s modifications  %s\n",$1,$2}'

titre "6. Aucun secret versionné"
echo "  Fichiers sensibles exclus par .gitignore :"
grep -E "appsettings.Development|secret" .gitignore | sed 's/^/      /'
echo
suspects=$(git ls-files | grep -icE "appsettings\.Development\.json|\.env$|id_rsa" || true)
if [ "$suspects" -eq 0 ]; then
  printf '  \033[32m✓\033[0m aucun fichier de secret dans le dépôt\n'
else
  printf '  \033[31m✗\033[0m %s fichier(s) suspect(s)\n' "$suspects"
fi
mdp=$(git grep -icE "password=(?!A_COMPLETER|<)" -- '*.cs' '*.json' 2>/dev/null | wc -l)
echo "  mots de passe en dur hors gabarits : $mdp"

titre "7. Synchronisation avec GitHub"
git fetch origin --quiet 2>/dev/null || true
local_h=$(git rev-parse --short HEAD)
dist_h=$(git rev-parse --short origin/"$(git branch --show-current)" 2>/dev/null || echo '—')
echo "  local   : $local_h"
echo "  distant : $dist_h"
avance=$(git rev-list --count origin/"$(git branch --show-current)"..HEAD 2>/dev/null || echo 0)
if [ "$avance" -eq 0 ]; then
  printf '  \033[32m✓\033[0m branche synchronisée\n'
else
  printf '  \033[33m!\033[0m %s commit(s) pas encore poussé(s)\n' "$avance"
fi

printf '\n'
