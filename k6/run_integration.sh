#!/usr/bin/env bash
# Tests d'intégration contre la pile déjà montée par docker compose.
#
#   BASE_URL=http://localhost:8080 bash run_integration.sh
#
# Deux tests, deux angles : k6 mesure la tenue en charge de l'API, Chrome rejoue
# le parcours d'un visiteur dans l'interface.
set -euo pipefail

cd "$(dirname "$0")"
BASE_URL="${BASE_URL:-http://localhost:8080}"
# Depuis un conteneur, « localhost » désigne le conteneur : k6 a besoin d'une
# autre adresse pour joindre la pile.
K6_BASE_URL="${K6_BASE_URL:-$BASE_URL}"

echo "Attente de l'API derrière le front ($BASE_URL/api/sante)…"
for essai in $(seq 1 90); do
  if curl -fsS "$BASE_URL/api/sante" | grep -q '"base":"disponible"'; then
    echo "L'API répond et la base est disponible."
    break
  fi
  if [ "$essai" -eq 90 ]; then
    echo "L'API n'a pas répondu en 90 secondes." >&2
    exit 1
  fi
  sleep 1
done

echo
echo "── Charge : k6"
docker run --rm -i --network host -e BASE_URL="$K6_BASE_URL" grafana/k6:1.0.0 run - < charge.js

echo
echo "── Parcours : Chrome"
npm install --no-audit --no-fund --silent
BASE_URL="$BASE_URL" node navigateur.mjs
