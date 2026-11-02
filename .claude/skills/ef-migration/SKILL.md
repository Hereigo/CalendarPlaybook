---
name: ef-migration
description: How to change the database schema in CalendarPlaybook - EF Core entity config, migrations, SQL Server in Podman, secrets. Use when touching Infrastructure/, the DbContext, entity configuration, or migrations.
---

# Schema changes

Spec: §10 (data model). The column list, nullability and CHECK constraints there are the contract.

## Steps

1. Change the entity and its `IEntityTypeConfiguration<T>` in `Infrastructure/`.
   - Enums: `.HasConversion<string>()` with a max length (`Kind` 16, `RuleType` 16, `DayOfWeek` 9).
   - Dates: `DateOnly` → `date`, `TimeOnly` → `time`, UTC instants → `datetime2(3)`.
   - Keep the `Kind`-vs-nullable-columns `CHECK` and the `DayOfWeek` value `CHECK` in sync with §10.
   - Indexes: `(OwnerId, StartUtc)` and `(OwnerId, StartDate)`.
2. Add a new migration: `dotnet ef migrations add <PascalCaseName> --project src/CalendarPlaybook.Api`.
3. Read the generated migration. Check column types, constraints and that nothing is dropped by accident.
4. Never edit an existing migration (a hook blocks it). To fix a mistake, add another migration. Only the `ModelSnapshot` may be regenerated.
5. Run `dotnet test`.

## Local database

- SQL Server 2022 Express in Podman (`MSSQL_PID=Express`), image tag pinned in the compose file.
- The `sa` password and connection string come from environment variables or `dotnet user-secrets`. Never put them in `appsettings*.json`, compose files or `launchSettings.json` (a hook blocks it). Use `${MSSQL_SA_PASSWORD}`-style references.
- Migrations apply on startup in Development only. Production applies them with an explicit step.
