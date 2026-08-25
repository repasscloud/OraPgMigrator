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

Every Oracle connection value (host, port, SID, user, password) is always read
from an environment variable — the CLI never accepts these as bare command-line
strings. By default it reads a fixed set of variable names, so if you just set
those, no `--oracle-*-env` flags are needed at all:

| Setting  | Default variable | Direct flag       | Custom-name flag         |
|----------|-------------------|--------------------|---------------------------|
| Host     | `ORAPG_HOST`      | `--oracle-host`    | `--oracle-host-env`      |
| Port     | `ORAPG_PORT`      | `--oracle-port`    | `--oracle-port-env`      |
| SID      | `ORAPG_SID`       | `--oracle-sid`     | `--oracle-sid-env`       |
| User     | `ORAPG_USER`      | `--oracle-user`    | `--oracle-user-env`      |
| Password | `ORAPG_PASSWORD`  | `--oracle-password`| `--oracle-password-env`  |

Precedence per setting: the direct flag (e.g. `--oracle-host`) wins if given;
otherwise the `*-env` flag names an environment variable to read; otherwise the
default variable name above is read. If none of those resolve to a set,
non-empty value, the command fails immediately, naming exactly which variable
is missing. Port falls back to `1521` if left fully unset (no flag, no
`ORAPG_PORT`). SID falls back to nothing if unset, since it's mutually
exclusive with `--oracle-service-name` — supply exactly one of the two.

`--schema`, `--output`, `--manifest`, and `--metadata` are plain strings with no
`-env` equivalent and no defaults; the app never assumes them. Populate them
from an environment variable directly in the shell if you want, e.g.
`--output $env:DDL_EXPORT_PATH` — PowerShell substitutes the value before orapg
ever sees it.

```powershell
# Simplest form: rely on the default ORAPG_* variable names
$env:ORAPG_HOST="oracle.internal"
$env:ORAPG_PORT="1521"
$env:ORAPG_SID="ORCL"
$env:ORAPG_USER="migration_user"
$env:ORAPG_PASSWORD="secret"

# 1. Scan a schema
orapg scan `
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
    --output C:\data_export\data

# Or run scan -> ddl -> export in one step:
orapg migrate --output C:\data_export
```

If your variables use different names, point at them explicitly with the
`*-env` flags — the value passed must be the *name* of a set environment
variable, never a literal value:

```powershell
$env:MY_ORA_HOST="oracle.internal"
$env:MY_ORA_PORT="1521"
$env:MY_ORA_SID="ORCL"
$env:MY_ORA_USER="migration_user"
$env:MY_ORA_PASSWORD="secret"

orapg scan `
    --oracle-host-env MY_ORA_HOST `
    --oracle-port-env MY_ORA_PORT `
    --oracle-sid-env MY_ORA_SID `
    --oracle-user-env MY_ORA_USER `
    --oracle-password-env MY_ORA_PASSWORD `
    --schema LEGACY `
    --output C:\data_export
```

Run `orapg <command> --help` for the full option list. Exit codes are documented
in [ExitCodes.cs](src/OraPgMigrator.Cli/ExitCodes.cs).
