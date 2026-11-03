# CLAUDE.md

Calendar web app: single-user-role, private events, recurrence, Europe/Helsinki time.
Source of truth: `intent/calendar-app.md` (intent) and `intent/calendar-app-spec.md` (spec, sections 1–16).
Read the spec section that matches your task before changing behaviour.

## Layout

- `global.json` pins the .NET SDK to 10.0.403 or later patch. Earlier SDKs are not supported.
- `src/CalendarPlaybook.Api/` - .NET 10 Minimal API, Vertical Slices. `Program.cs` wires problem details, string enums, Auth0 and the `/api/events` group.
  - `Recurrence/` - pure domain logic (engine, `HelsinkiTime` local↔UTC). No EF Core or HTTP references. Keep it that way.
  - `Features/` (spec §9.1) - one folder per slice. Done: `Events/CreateEvent`, `Events/GetEvent`, `Events/UpdateEvent`, `Events/DeleteEvent`, `Events/ListOccurrences`. Next: `Notifications/ListUpcoming`.
  - `Infrastructure/` (spec §9.1) - `CalendarDbContext`, `Events/` (entity, enums, configuration), `Auth/` (JWT, `GetOwnerId()`), `Database/`, `Migrations/` (SQL Server only).
- Local SQL Server 2022 Express in Podman, two options (use one, both bind port 1433). Both need `MSSQL_SA_PASSWORD` in the environment or `.env` (template: `.env_example`).
  - `podman run ... --name calendar-sql` from `docs/podman-sql-notes.md`. In Development the app runs `podman start calendar-sql` on startup (`DevSqlContainer:Name` in `appsettings.Development.json`) and waits until SQL Server accepts connections.
  - `compose.yaml` (`podman compose up -d`). Its container has a different name, so the app does not start it; start it yourself.
- Config keys (user secrets or environment, never committed): `ConnectionStrings:Calendar`, `Auth0:Domain`, `Auth0:Audience`, `Cors:AllowedOrigin` (SPA origin; `http://localhost:5173` in Development).
- `docs/plan.md` - the current plan (spec-sync skill).
- `docs/` - also `local-setup.md` (prerequisites, secrets, run), `AI-SDLC-Playbook-howto.md` (the SDLC process) and `podman-sql-notes.md`.
- `tests/CalendarPlaybook.Api.Tests/` - xUnit unit tests, mirroring the source folders.
- `tests/CalendarPlaybook.Api.IntegrationTests/` - xUnit v3 (3.x, VSTest; 4.x needs Microsoft.Testing.Platform) API tests through `WebApplicationFactory<Program>`, real JwtBearer against a local JWKS (`Infrastructure/TestTokens`), SQL Server via Testcontainers. New slices derive from `IntegrationTest` and use a fresh `NewSub()` per user.
- `docs/ui-template/` - starting markup for the React frontend (`Calendar.htm`, `Calendar.css`).
- `src/CalendarPlaybook.Web/` - Vite + React + TypeScript SPA with Vitest. Month grid with prev/next navigation and today marker (`monthGrid.ts`). Auth0 sign-in (`@auth0/auth0-react`, in-memory tokens), `api.ts` client for `GET /api/events` and `Calendar.tsx` month view with occurrences. Config in `.env.local` (template `.env.example`: `VITE_AUTH0_*`, `VITE_API_URL`). Not yet done: event forms (create/edit/delete), notifications.
- `Continue_here.md` - handoff notes, when a session leaves work unfinished. Update or delete it when its steps are done.
- `.claude/` - guardrails. Hooks need Node.js on PATH.
  - `settings.json` + `hooks/` - asks before edits to `intent/`; blocks edits to existing EF migrations and secrets in config files; the Stop hook runs `dotnet test` when backend files changed and blocks "done" if it fails.
  - `skills/` - `feature-slice` (adding an endpoint), `ef-migration` (schema changes), `spec-sync` (`docs/plan.md`, spec divergence, handoff).

## Commands

Run from the repo root.

- Backend build: `dotnet build`
- Backend tests: `dotnet test` (the verification command for backend work). Runs unit and integration tests.
  - Unit only: `dotnet test tests/CalendarPlaybook.Api.Tests`. Integration only: `dotnet test tests/CalendarPlaybook.Api.IntegrationTests`.
- Backend run: `dotnet run --project src/CalendarPlaybook.Api`
- Frontend (run in `src/CalendarPlaybook.Web`): `npm install`, `npm run dev`, `npm test`, `npm run build`. The last two are the verification commands for frontend work.
- Integration tests need the Podman machine running: Testcontainers starts a throwaway SQL Server 2022 Express container per run (~35 s) and applies the migrations. Without Podman they are skipped and `dotnet test` stays green, so check the summary for `Skipped`. `CALENDAR_INTEGRATION_REQUIRED=true` makes them fail instead (CI).
- Lint: none configured yet.

## Conventions

- Timed events are stored in UTC with `TimeZone = Europe/Helsinki`. All-day events and recurrence start dates are local dates (`DateOnly`), with no UTC conversion.
- Enums (`Kind`, `RuleType`, `DayOfWeek`) are stored and sent as string names, never numbers: EF Core `.HasConversion<string>()`, `JsonStringEnumConverter` in the API. Use `System.DayOfWeek` in code; no custom weekday enum.
- Recurrence is computed at query time. Only the rule is stored, never the occurrences.
- Any change to a recurring event applies to the whole series. Individual occurrences are never edited or deleted.
- Every query and command filters by `OwnerId` (Auth0 `sub`). A foreign or unknown id returns `404`, never `403`.
- Errors use RFC 9457 problem details. Validation errors return `400`. A missing or invalid token returns `401`.
- Logs must not contain event titles or descriptions.
- EF Core parameterized queries only. No raw SQL built from input.
- Secrets (Auth0 config, connection strings, `sa` password) come from environment or user secrets. Never commit them.
- Match the surrounding code's style. Use file-scoped namespaces and records for DTOs and rules.

## Workflow

- Do not commit unless asked.
- Before a change is reported done, the relevant verification command must pass.
- The decisions in spec §13 are defaults pending product-owner review. Do not silently change them.
