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

```sh
dotnet user-secrets set ConnectionStrings:Calendar "Server=localhost,1433;Database=Calendar;User Id=sa;Password=<password>;TrustServerCertificate=True" --project src/CalendarPlaybook.Api
dotnet user-secrets set Cidaas:BaseUrl "https://<your-tenant>.cidaas.eu" --project src/CalendarPlaybook.Api
dotnet user-secrets set Cidaas:Audience "<spa-client-id or resource URI>" --project src/CalendarPlaybook.Api

# to watch
dotnet user-secrets list --project src/CalendarPlaybook.Api
```

cidaas tenant: create a single-page (public) client with Authorization Code + PKCE. Redirect and post-logout URI `http://localhost:5173`, silent redirect URI `http://localhost:5173/silent-renew.html`, allowed web origin `http://localhost:5173`. `Cidaas:Audience` must equal the access token `aud` (the client id, unless you use a resource indicator). Put the same tenant URL and client id in `src/CalendarPlaybook.Web/.env.local` (template `.env.example`).

3. Run the API. In Development, it applies migrations on startup:

```sh
dotnet run --project src/CalendarPlaybook.Api
```

## Tests

```sh
dotnet test
```

This runs both test projects:

- `tests/CalendarPlaybook.Api.Tests`: unit tests, no dependencies.
- `tests/CalendarPlaybook.Api.IntegrationTests`: the API over HTTP against a real SQL Server 2022 Express that Testcontainers starts in Podman for each run (about 35 s, removed afterwards). cidaas is replaced by a locally generated signing key (JWKS); no cidaas tenant or user secrets are used.

### Podman as the Testcontainers endpoint (Windows)

Testcontainers speaks the Docker API. The Podman machine serves it on the named pipe `\\.\pipe\podman-machine-default`. Nothing to configure in the usual case:

- Podman registers a Docker context for that pipe, which Testcontainers discovers, and
- the test fixture falls back to that pipe when `DOCKER_HOST` is not set.

If discovery fails (another machine name, or a broken Docker context), point Testcontainers at the pipe yourself:

```ps1
[Environment]::SetEnvironmentVariable("DOCKER_HOST", "npipe://./pipe/podman-machine-default", "User")
```

or put `docker.host=npipe://./pipe/podman-machine-default` in `%USERPROFILE%\.testcontainers.properties`. `podman machine inspect` shows the pipe name under `PodmanPipe`. Rootless Podman works, including the Ryuk cleanup container; no extra settings needed.

### Without Podman

If no Podman/Docker endpoint is reachable, the integration tests are **skipped** with the message "Podman/Docker is not reachable…", and `dotnet test` still exits 0 (the summary shows `Skipped: 14` for the integration project). Start the machine with `podman machine start` and run again. Other failures (image pull, migration, SQL errors) fail the run.

In CI, set `CALENDAR_INTEGRATION_REQUIRED=true` so that a missing endpoint fails the run instead of skipping (spec §12).
