Product:      OraPgMigrator
Solution:     OraPgMigrator.sln
Repository:   ora-pg-migrator
Binary:       orapg.exe
CLI command:  orapg

So usage stays clean:

orapg scan
orapg ddl
orapg export
orapg validate
orapg migrate

I would avoid ora2pg specifically because the established Ora2Pg project already exists.

Recommended naming
Application name:
OraPgMigrator

Description:
Oracle to PostgreSQL schema and data migration utility

Repository:
ora-pg-migrator

Solution:
OraPgMigrator.sln

Executable:
orapg.exe

Then your .NET projects remain readable:

OraPgMigrator.sln

src/
├── OraPgMigrator.Cli/
├── OraPgMigrator.Core/
├── OraPgMigrator.Oracle/
├── OraPgMigrator.Postgres/
└── OraPgMigrator.Infrastructure/

tests/
├── OraPgMigrator.Core.Tests/
├── OraPgMigrator.Oracle.Tests/
├── OraPgMigrator.Postgres.Tests/
└── OraPgMigrator.IntegrationTests/

And when published as a self-contained Windows executable:

.\orapg.exe scan --schema LEGACY ...

If it's installed somewhere on %PATH%:

orapg scan
orapg ddl
orapg export
orapg validate

OraPgMigrator / orapg.exe would be my choice. It says exactly what it does without making the command annoying to type.
