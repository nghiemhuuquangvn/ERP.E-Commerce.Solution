#!/bin/sh
# Applies the PostgreSQL migrations of ManufacturingService (src/Database/Manufacturing/*.sql, filename order)
# against the "postgres" container. Creates the database first when it does not exist. Every file is idempotent,
# so this job can run on every start (docker compose up pg-migrate).
set -eu

export PGPASSWORD="${PG_PASSWORD}"
HOST="${PG_HOST:-postgres}"
USER="${PG_USER:-erp}"
DB="${PG_DATABASE:-ManufacturingDB}"

echo "pg-migrate: waiting for ${HOST}"
until pg_isready -h "$HOST" -U "$USER" -d postgres >/dev/null 2>&1; do sleep 2; done

if [ "$(psql -h "$HOST" -U "$USER" -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '$DB'")" != "1" ]; then
  echo "pg-migrate: creating database $DB"
  psql -h "$HOST" -U "$USER" -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE \"$DB\""
fi

for f in $(ls /migrations/database/Manufacturing/*.sql | sort); do
  echo "pg-migrate: $f"
  psql -h "$HOST" -U "$USER" -d "$DB" -v ON_ERROR_STOP=1 -q -f "$f"
done
echo "pg-migrate: done"
