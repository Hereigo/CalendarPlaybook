# CalendarPlaybook local setup

How to run the calendar web app on your machine. The app is built with .NET following [The AI-native SDLC playbook](https://claude.com/resources/articles/the-ai-native-sdlc-playbook).

- Intent: `intent/calendar-app.md`
- Spec: `intent/calendar-app-spec.md`
- Current plan: `docs/plan.md`
- How the SDLC process works here: `docs/AI-SDLC-Playbook-howto.md`

## Prerequisites

- .NET SDK 10.0.403 or later (pinned in `global.json`)
- Podman (local SQL Server 2022 Express)
- Node.js on PATH (Claude Code hooks in `.claude/`)

## Steps

1. Start SQL Server. Set `MSSQL_SA_PASSWORD` in the environment or in `.env` (copy `.env_example`), then follow `docs/podman-sql-notes.md` to create the `calendar-sql` container. The app starts that container on its own in Development.
2. Set the secrets with user secrets (never commit them):

   ```bash
   dotnet user-secrets set ConnectionStrings:Calendar "Server=localhost,1433;Database=Calendar;User Id=sa;Password=<password>;TrustServerCertificate=True" --project src/CalendarPlaybook.Api
   dotnet user-secrets set Auth0:Domain "<your-tenant>.auth0.com" --project src/CalendarPlaybook.Api
   dotnet user-secrets set Auth0:Audience "<api-identifier>" --project src/CalendarPlaybook.Api
   ```

3. Run the API. In Development, it applies migrations on startup:

   ```bash
   dotnet run --project src/CalendarPlaybook.Api
   ```

## Tests

```bash
dotnet test
```
