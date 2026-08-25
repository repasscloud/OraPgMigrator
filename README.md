# OraPgMigrator

A Windows CLI (`orapg.exe`) for migrating an Oracle schema to PostgreSQL: schema
scan, editable manifest, PostgreSQL DDL generation, and streaming CSV data
export.

See [oracle-to-postgresql-migration-claude-code-spec.md](oracle-to-postgresql-migration-claude-code-spec.md)
for the full specification and [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md)
for engineering decisions made while building it.

## Solution layout

```
OraPgMigrator.slnx
src/
  OraPgMigrator.Cli             CLI (System.CommandLine) — publishes as orapg.exe
  OraPgMigrator.Core            Neutral domain model, mapping/conversion abstractions
  OraPgMigrator.Oracle          Oracle.ManagedDataAccess.Core schema/data reader
  OraPgMigrator.Postgres        DDL generation, identifier policy, type mapping
  OraPgMigrator.Infrastructure  CSV, JSON, filesystem/output paths, env resolver, logging
tests/
  OraPgMigrator.Core.Tests
  OraPgMigrator.Oracle.Tests
  OraPgMigrator.Postgres.Tests
  OraPgMigrator.IntegrationTests
```

## Build & test

Requires the .NET 10 SDK (all projects target `net10.0`).

```bash
dotnet restore OraPgMigrator.slnx
dotnet build OraPgMigrator.slnx
dotnet test OraPgMigrator.slnx
```

Restores use committed `packages.lock.json` files per project for
reproducible builds. After changing a `PackageReference`, run
`dotnet restore` to refresh the lock file and commit it alongside the
`.csproj` change; CI restores with `--locked-mode` and fails if a lock
file is out of date.

## Publish the Windows executable

```powershell
dotnet publish src\OraPgMigrator.Cli -c Release -r win-x64 -o .\publish
```

produces a single native `.\publish\orapg.exe` — self-contained, Native
AOT-compiled, and requiring no separate runtime files or `.dll`s
alongside it. CI publishes this on every PR (`publish-windows` job in
[ci.yml](.github/workflows/ci.yml)) and uploads it as the `orapg-win-x64`
artifact.

Native AOT is win-x64-only for now (set via a `RuntimeIdentifier`-gated
`PublishAot` in
[OraPgMigrator.Cli.csproj](src/OraPgMigrator.Cli/OraPgMigrator.Cli.csproj)).
Publishing for `osx-x64`, `osx-arm64`, or `linux-x64` still produces a
single self-contained file, just via the trimmed self-extracting
single-file host rather than Native AOT.

## Usage

```powershell
$env:MY_ORA_HOST="oracle.internal"
$env:MY_ORA_PORT="1521"
$env:MY_ORA_SID="ORCL"
$env:MY_ORA_USER="migration_user"
$env:MY_ORA_PASSWORD="secret"

# 1. Scan a schema
orapg scan `
    --oracle-host-env MY_ORA_HOST `
    --oracle-port-env MY_ORA_PORT `
    --oracle-sid-env MY_ORA_SID `
    --oracle-user-env MY_ORA_USER `
    --oracle-password-env MY_ORA_PASSWORD `
    --schema LEGACY `
    --output C:\data_export

# 2. Edit C:\data_export\manifest.csv as needed, then generate DDL
orapg ddl `
    --manifest C:\data_export\manifest.csv `
    --metadata C:\data_export\metadata.json `
    --output C:\data_export\ddl

# 3. Export data
orapg export `
    --manifest C:\data_export\manifest.csv `
    --metadata C:\data_export\metadata.json `
    --oracle-host-env MY_ORA_HOST `
    --oracle-port-env MY_ORA_PORT `
    --oracle-sid-env MY_ORA_SID `
    --oracle-user-env MY_ORA_USER `
    --oracle-password-env MY_ORA_PASSWORD `
    --output C:\data_export\data

# Or run scan -> ddl -> export in one step:
orapg migrate --oracle-host-env MY_ORA_HOST ... --output C:\data_export
```

Run `orapg <command> --help` for the full option list. Exit codes are documented
in [ExitCodes.cs](src/OraPgMigrator.Cli/ExitCodes.cs).
