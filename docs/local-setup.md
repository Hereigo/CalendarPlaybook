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

1. Start SQL Server. Set `MSSQL_SA_PASSWORD` in the environment or in `.env` (copy `.env.example` from the repo root), then follow `docs/podman-sql-notes.md` to create the `calendar-sql` container. The app starts that container on its own in Development.

2. Set the secrets with user secrets (never commit them):

```sh
dotnet user-secrets set ConnectionStrings:Calendar "Server=localhost,1433;Database=Calendar;User Id=sa;Password=<password>;TrustServerCertificate=True" --project src/CalendarPlaybook.Api
dotnet user-secrets set Auth0:Domain "<your-tenant>.auth0.com" --project src/CalendarPlaybook.Api
dotnet user-secrets set Auth0:Audience "<api-identifier>" --project src/CalendarPlaybook.Api
dotnet user-secrets set Cors:AllowedOrigin "http://localhost:5173" --project src/CalendarPlaybook.Api

# to watch
dotnet user-secrets list --project src/CalendarPlaybook.Api
```

3. Set up the frontend once. In `src/CalendarPlaybook.Web`, run `npm install`, then copy `.env.example` to `.env.local` and fill in `VITE_AUTH0_DOMAIN`, `VITE_AUTH0_CLIENT_ID`, `VITE_AUTH0_AUDIENCE` (from the Auth0 SPA application) and `VITE_API_URL`.

4. Run the app. In Development, it applies migrations on startup, starts the Vite dev server and opens `http://localhost:5173`:

```sh
dotnet run --project src/CalendarPlaybook.Api
```

Frontend checks (in `src/CalendarPlaybook.Web`): `npm test` and `npm run build`.

## Hosting (API + UI on one site)

`dotnet publish` builds the React app too (`npm ci` and `vite build --mode hosted`) and puts it in `wwwroot` of the publish output. Outside Development the API serves it from the same origin: `/api/*` is the API, other paths without a file extension return `index.html`.

```sh
dotnet publish src/CalendarPlaybook.Api -c Release -o publish
```

- The build machine needs Node.js and the `VITE_AUTH0_*` values (in `src/CalendarPlaybook.Web/.env.local` or as environment variables). `.env.hosted` makes API calls relative, so `VITE_API_URL` is ignored.
- Add the site URL to the Auth0 SPA application's Allowed Callback, Logout and Web Origins URLs.
- `Cors:AllowedOrigin` is not needed for the same-origin setup.
- Hosting config (`ConnectionStrings:Calendar`, `Auth0:Domain`, `Auth0:Audience`) goes in environment variables on the host (`ConnectionStrings__Calendar`, ...) or in `src/CalendarPlaybook.Api/appsettings.Production.json`. That file is git-ignored but `dotnet publish` copies it, so it reaches shared hosting where environment variables cannot be set. Never put these values in `appsettings.json`, which is committed.
- IIS needs the ASP.NET Core 10 Hosting Bundle and an app pool with "No Managed Code". Without the bundle, publish self-contained: `-r win-x64 --self-contained`.
- `-p:SkipSpaBuild=true` publishes the API without the UI.

## Tests

```sh
dotnet test
```

This runs both test projects:

- `tests/CalendarPlaybook.Api.Tests`: unit tests, no dependencies.
- `tests/CalendarPlaybook.Api.IntegrationTests`: the API over HTTP against a real SQL Server 2022 Express that Testcontainers starts in Podman for each run (about 35 s, removed afterwards). Auth0 is replaced by a locally generated signing key (JWKS); no Auth0 tenant or user secrets are used.

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

If no Podman/Docker endpoint is reachable, the integration tests are **skipped** with the message "Podman/Docker is not reachable…", and `dotnet test` still exits 0 (check the summary for a non-zero `Skipped` count on the integration project). Start the machine with `podman machine start` and run again. Other failures (image pull, migration, SQL errors) fail the run.

In CI, set `CALENDAR_INTEGRATION_REQUIRED=true` so that a missing endpoint fails the run instead of skipping (spec §12).
