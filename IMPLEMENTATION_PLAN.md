# OraPgMigrator — Implementation Plan & Decisions

This document records engineering decisions made while building the MVP, and any
conflicts found between `notes.md` and the spec
(`oracle-to-postgresql-migration-claude-code-spec.md`).

## Conflicts between notes.md and spec

- `notes.md` says the solution file should be `OraPgMigrator.sln`. The task
  instructions explicitly require `OraPgMigrator.slnx` (the new XML-based solution
  format). **Decision: use `OraPgMigrator.slnx`** per the more specific/explicit
  instruction in the prompt, which overrides both `notes.md` and the spec's `.sln`
  references.
- Spec section 6 uses the project prefix `OraclePgMigrator.*`; `notes.md` and the
  task instructions use `OraPgMigrator.*`. **Decision: use `OraPgMigrator.*`**
  (matches notes.md, the requested folder layout, and the requested binary name
  `orapg.exe`).

## Solution layout

```
OraPgMigrator.slnx
src/
  OraPgMigrator.Cli            -> exe, AssemblyName=orapg
  OraPgMigrator.Core           -> neutral domain model, mapping/conversion abstractions
  OraPgMigrator.Oracle         -> Oracle.ManagedDataAccess.Core based schema/data reader
  OraPgMigrator.Postgres       -> DDL generation, identifier policy, Postgres-specific mapping
  OraPgMigrator.Infrastructure -> CSV, JSON, filesystem/output path, env resolver, logging helpers
tests/
  OraPgMigrator.Core.Tests
  OraPgMigrator.Oracle.Tests
  OraPgMigrator.Postgres.Tests
  OraPgMigrator.IntegrationTests
```

Dependency direction: Cli -> {Oracle, Postgres, Infrastructure, Core}; Oracle/Postgres/Infrastructure -> Core.
Core has no dependency on Oracle.ManagedDataAccess.Core or Npgsql (keeps target-agnostic domain model per spec §56).

## Target framework

`net9.0`, console app, Windows is the primary runtime (`RuntimeIdentifiers` includes
`win-x64`); build/test on macOS/Linux dev hosts still works since Oracle/Npgsql
managed drivers are cross-platform. Windows-specific behaviors (UNC paths etc.) are
implemented using platform-neutral `System.IO` APIs that behave correctly when
published for `win-x64`.

## MVP scope (this pass)

Implements spec §58 items 1-17: connect, scan, manifest.csv, metadata.json,
report.txt, per-table CREATE TABLE DDL, core type mapping, CSV export with
streaming, NULL handling (`\N` token), local + UNC output paths, logging, unit
tests for mapping/manifest/identifiers/csv.

Deferred (structured for, not implemented yet): PK/FK/index/sequence DDL files,
COPY load scripts, parallel export, resume, validate command, dry-run, trigger/
identity detection, hash verification, views/procedures reporting beyond basic
counts. These are represented as explicit TODO/NotImplemented seams in the CLI
command layer only (not inside Core migration logic), so `orapg migrate`,
`orapg validate` exist as commands with clear "not yet implemented" behavior
rather than silently pretending to work.

## Data type mapping policy

Implemented per spec §12-§15/§43 as `OracleToPostgresTypeMapper` in
`OraPgMigrator.Postgres`. NUMBER precision policy: <=4 smallint, <=9 integer,
<=18 bigint, scale>0 or unknown precision -> numeric(p,s)/numeric. Unknown/
unsupported Oracle types throw `UnsupportedOracleTypeException` rather than
guessing, unless a user-supplied type-map override exists.

## Identifier policy

Default `--identifier-case lower`, PostgreSQL reserved-word detection list is a
built-in table sourced from the current reserved keyword list; collisions after
normalization are reported as errors during DDL generation, not silently
resolved.

## Status (as implemented)

- `scan`, `ddl`, `export` fully implement the MVP workflow end-to-end (spec §58,
  §62 "Definition of Done") and were smoke-tested against hand-built
  manifest.csv/metadata.json fixtures (real Oracle connectivity could not be
  exercised in this environment, but connection/error-path handling was verified
  against an unreachable host).
- `validate` implements row-count comparison only (spec §36); chunk/hash
  validation (spec §37) is not implemented.
- `migrate` orchestrates `scan` -> `ddl` -> `export` by re-invoking each
  command's own `System.CommandLine` parser with synthesized arguments, so the
  three commands share one implementation with no duplicated logic. It does not
  orchestrate a `load` or `validate` stage.
- PK/FK/index/sequence DDL generation, `\copy` load-script generation and a
  dependency-ordered `load-all.sql`, and table-granularity `--resume` are all
  implemented (not deferred), since they fit naturally into the same
  `PostgresDdlGenerator`/`CopyScriptGenerator`/`MigrationState` abstractions the
  MVP needed anyway.
- Not implemented: parallel export beyond `--parallel` bounded concurrency
  scheduling (implemented), dry-run mode, trigger/identity-to-IDENTITY
  conversion (triggers are detected and reported only), hash-based validation.

## Notable engineering decisions

- **`Directory.Build.props` sets `RollForward=LatestMajor`.** This dev/build
  environment only has the .NET 10 shared runtime installed, not net9.0. The
  roll-forward policy lets the net9.0 framework-dependent build/test/run locally
  against .NET 10; it does not change the declared `net9.0` TargetFramework and
  has no effect on a self-contained Windows publish.
- **`OutputPathNormalizer` only rewrites unambiguous Windows-style input**: UNC
  paths (`//server/share` or `\\server\share`) and drive-letter paths with
  forward slashes (`C:/data`). A plain POSIX-style path is left untouched. An
  earlier version rewrote every `/` to `\` unconditionally, which corrupted
  local dev/test paths on non-Windows hosts and was never actually required by
  the spec (only UNC-with-forward-slashes normalization is specified).

## Security

Passwords only ever flow through `OracleConnectionSettings` and Oracle
`OracleConnection` connection strings inside `OraPgMigrator.Oracle`; they are
never written to metadata.json/manifest.csv/report.txt/logs. `ToString()` /
logging of `OracleConnectionSettings` masks the password.
