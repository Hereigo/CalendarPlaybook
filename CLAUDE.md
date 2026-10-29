# CLAUDE.md

Calendar web app: single-user-role, private events, recurrence, Europe/Helsinki time.
Source of truth: `intent/calendar-app.md` (intent) and `intent/calendar-app-spec.md` (spec, sections 1–16).
Read the spec section that matches your task before changing behaviour.

## Layout

- `global.json` pins the .NET SDK to 10.0.403 or later patch. Earlier SDKs are not supported.
- `src/CalendarPlaybook.Api/` - .NET 10 Minimal API, Vertical Slices.
  - `Features/` - one folder per slice (`Events/CreateEvent`, `Events/ListOccurrences`, `Notifications/ListUpcoming`, ...).
  - `Recurrence/` - pure domain logic. No EF Core or HTTP references. Keep it that way.
  - `Infrastructure/` - EF Core DbContext, Auth0 config, migrations (SQL Server only).
- `tests/CalendarPlaybook.Api.Tests/` - xUnit tests. `Recurrence/` has unit tests for the engine.
- `ui-template/` - starting markup for the React frontend (`Calendar.htm`, `Calendar.css`). The frontend is not scaffolded yet.
- `Continue_here.md` - handoff notes. Update or delete it when its steps are done.

## Commands

Run from the repo root.

- Backend build: `dotnet build`
- Backend tests: `dotnet test` (the verification command for backend work)
- Backend run: `dotnet run --project src/CalendarPlaybook.Api`
- Frontend (not yet scaffolded): `npm test`, `npm run build`. Both are verification commands in CI once the frontend exists.
- Integration tests need Podman running, since they start SQL Server 2022 Express via Testcontainers.
- Lint: none configured yet.

## Conventions

- Timed events are stored in UTC with `TimeZone = Europe/Helsinki`. All-day events and recurrence start dates are local dates (`DateOnly`), with no UTC conversion.
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
- Decisions 1–13 in spec §13 are defaults pending product-owner review. Do not silently change them.
