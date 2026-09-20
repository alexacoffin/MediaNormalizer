#!/usr/bin/env bash

set -Eeuo pipefail

: "${DB_NAME:=MediaNormalizer}"
: "${DB_PORT:=1433}"
: "${DB_SERVER:=db}"
: "${MSSQL_SA_PASSWORD:?MSSQL_SA_PASSWORD must be set}"

connection_string="Server=${DB_SERVER},${DB_PORT};Initial Catalog=${DB_NAME};User ID=sa;Password=${MSSQL_SA_PASSWORD};Encrypt=False;TrustServerCertificate=True;Connection Timeout=10"

for attempt in $(seq 1 30); do
    if /tools/sqlpackage \
        /Action:Publish \
        /SourceFile:/app/MediaNormalizer.Database.dacpac \
        /TargetConnectionString:"${connection_string}" \
        /p:CreateNewDatabase=True \
        /p:BlockOnPossibleDataLoss=True; then
        exit 0
    fi

    echo "Database deployment attempt ${attempt}/30 failed; retrying in 5 seconds." >&2
    sleep 5
done

echo "Database deployment failed after 30 attempts." >&2
exit 1
