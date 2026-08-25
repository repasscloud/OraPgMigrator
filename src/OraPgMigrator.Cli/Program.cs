using System.CommandLine;
using OraPgMigrator.Cli.Commands;

var root = new RootCommand("OraPgMigrator (orapg) - Oracle to PostgreSQL schema and data migration utility.");
root.Subcommands.Add(ScanCommand.Build());
root.Subcommands.Add(DdlCommand.Build());
root.Subcommands.Add(ExportCommand.Build());
root.Subcommands.Add(ValidateCommand.Build());
root.Subcommands.Add(MigrateCommand.Build());

return await root.Parse(args).InvokeAsync();
