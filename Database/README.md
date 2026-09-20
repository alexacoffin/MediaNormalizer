# MediaNormalizer database

This directory contains the SQL Server database project for MediaNormalizer. It builds a DACPAC containing the `ctlg` and `dbo` schemas, normalization tables, constraints, indexes, and catalog seed data.

## Compose deployment

Create a local environment file and set a strong SQL Server password:

```powershell
Copy-Item .env.example .env
```

Build and start the services:

```powershell
docker compose up --build -d
```

Compose starts three services:

- `db` runs SQL Server 2022 with the edition selected by `MEDIA_NORMALIZER_DB_PID` and stores data in the `media-normalizer-db-data` volume. Use a properly licensed edition for production.
- `db-init` builds the database project, installs the pinned SqlPackage tool, waits for SQL Server, and publishes the DACPAC once.
- `media-normalizer` starts after `db-init` completes successfully.

The database is exposed to the host at `127.0.0.1:${MEDIA_NORMALIZER_DB_PORT}`. The default port is `11433`.

To rerun schema deployment after changing SQL files:

```powershell
docker compose build db-init
docker compose run --rm db-init
```

The deployment is idempotent and blocks changes that SqlPackage identifies as potentially data-lossy; it does not delete existing database data. The SQL Server volume can be removed only when intentionally resetting the local database:

```powershell
docker compose down -v
```

Do not use that command against a production Compose project without explicit approval.

## Direct DACPAC build

```powershell
dotnet build Database/MediaNormalizer.Database.sqlproj --configuration Release
```

The DACPAC is written to `Database/bin/Release/MediaNormalizer.Database.dacpac` and is ignored by Git through the existing build-output rules.
