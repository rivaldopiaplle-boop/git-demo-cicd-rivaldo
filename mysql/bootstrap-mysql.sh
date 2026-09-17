#!/usr/bin/env bash
# Applique le schéma à la base de la pile montée par docker compose.
#
# Le schéma vit dans mysql/schema.sql, et les tests d'intégration du back-end
# appliquent le même fichier : une seule source pour la structure des données.
set -euo pipefail

cd "$(dirname "$0")/.."
COMPOSE=(docker compose -f docker-compose.build.yml)
BASE="${DB_DATABASE:-taches}"
: "${MYSQL_ROOT_PASSWORD:?posez MYSQL_ROOT_PASSWORD}"

echo "Attente de MySQL…"
for essai in $(seq 1 60); do
  if "${COMPOSE[@]}" exec -T mysql mysqladmin ping -h 127.0.0.1 -uroot -p"$MYSQL_ROOT_PASSWORD" --silent >/dev/null 2>&1; then
    echo "MySQL répond."
    break
  fi
  if [ "$essai" -eq 60 ]; then
    echo "MySQL n'a pas répondu en 60 secondes." >&2
    exit 1
  fi
  sleep 1
done

"${COMPOSE[@]}" exec -T mysql mysql -uroot -p"$MYSQL_ROOT_PASSWORD" \
  -e "CREATE DATABASE IF NOT EXISTS \`$BASE\`"
"${COMPOSE[@]}" exec -T mysql mysql -uroot -p"$MYSQL_ROOT_PASSWORD" "$BASE" < mysql/schema.sql

echo "Schéma appliqué à la base « $BASE »."
