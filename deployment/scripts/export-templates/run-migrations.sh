#!/bin/bash
# Applies every hand-written SQL migration in ./Database, in numeric filename order,
# against the "sqlserver" container. Each file does its own
#   IF NOT EXISTS (...) CREATE DATABASE AuthDB; GO
#   USE AuthDB; GO
# so no separate init step is needed (unlike the source monorepo, which
# defensively creates all 15 of its databases up front — this export only has one).
set -uo pipefail

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
HOST="${SQL_HOST:-sqlserver}"
FAILED=0

echo "Waiting for SQL Server ($HOST) to accept connections..."
until "$SQLCMD" -S "$HOST" -U sa -P "$SA_PASSWORD" -C -I -Q "SELECT 1" > /dev/null 2>&1; do
    sleep 2
done
echo "SQL Server is up."

echo
echo "== Database =="
for f in $(find /migrations/database -maxdepth 1 -iname "*.sql" | sort); do
    echo "  -> $(basename "$f")"
    if ! "$SQLCMD" -S "$HOST" -U sa -P "$SA_PASSWORD" -C -I -i "$f"; then
        echo "     !! FAILED"
        FAILED=1
    fi
done

echo
if [ "$FAILED" -ne 0 ]; then
    echo "One or more migrations reported an error -- review the log above."
    exit 1
fi
echo "All migrations applied successfully."
