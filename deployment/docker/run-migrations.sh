#!/bin/bash
# Applies every service's hand-written SQL migration file against the SQL Server
# container started by docker-compose.yml (service "sqlserver"), so the whole
# platform can be bootstrapped with `docker compose up` alone -- no separately
# installed host SQL Server required.
#
# Each service's own 001_Create*.sql already does
#   IF NOT EXISTS (...) CREATE DATABASE X; GO
#   USE X; GO
# so this script never needs a hardcoded folder-name -> database-name map; it just
# runs every *.sql file it finds, in numeric filename order, against the server.
# deployment/sql/init-databases.sql runs first as a defensive extra (creates all 15
# databases up front even before any per-service migration runs).
#
# Runs as the one-shot "db-migrate" compose service (see docker-compose.yml),
# using the mssql/server image itself since it already ships sqlcmd -- no extra
# image to build or pull.
set -uo pipefail

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
HOST="${SQL_HOST:-sqlserver}"
FAILED=0
SQL_ERRORS=0

echo "Waiting for SQL Server ($HOST) to accept connections..."
until "$SQLCMD" -S "$HOST" -U sa -P "$SA_PASSWORD" -C -I -Q "SELECT 1" > /dev/null 2>&1; do
    sleep 2
done
echo "SQL Server is up."

echo
echo "== deployment/sql/init-databases.sql =="
"$SQLCMD" -S "$HOST" -U sa -P "$SA_PASSWORD" -C -I -i /migrations/init/init-databases.sql \
    || { echo "  !! init-databases.sql failed"; FAILED=1; }

for dir in /migrations/database/*/; do
    service=$(basename "$dir")
    files=$(find "$dir" -maxdepth 1 -iname "*.sql" | sort)
    if [ -z "$files" ]; then
        continue
    fi

    echo
    echo "== $service =="
    while IFS= read -r f; do
        echo "  -> $(basename "$f")"
        # sqlcmd without -b exits 0 even when a statement fails, so count the "Msg N, Level"
        # lines it prints to give an honest summary (on a database that already exists the
        # non-idempotent 001 scripts report "already exists" -- expected, not fatal).
        out=$("$SQLCMD" -S "$HOST" -U sa -P "$SA_PASSWORD" -C -I -i "$f" 2>&1); rc=$?
        printf '%s\n' "$out"
        n=$(printf '%s\n' "$out" | grep -cE '^Msg [0-9]+, Level (1[1-9]|2[0-9])')
        SQL_ERRORS=$((SQL_ERRORS + n))
        if [ "$rc" -ne 0 ]; then
            echo "     !! FAILED (continuing -- see docs/architecture/database-map.md" \
                 "and the Promotion/PromotionService folder-naming note before assuming" \
                 "this migration is safe to ignore)"
            FAILED=1
        fi
    done <<< "$files"
done

echo
if [ "$FAILED" -ne 0 ]; then
    echo "One or more migrations reported an error -- review the log above."
    exit 1
fi
if [ "$SQL_ERRORS" -ne 0 ]; then
    echo "Finished, but $SQL_ERRORS SQL error(s) were printed above (grep '^Msg ' the log)." \
         "Errors such as 1913/2714 (already exists) are expected on a pre-existing database."
    exit 0
fi
echo "All migrations applied successfully."
