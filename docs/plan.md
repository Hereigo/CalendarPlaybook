# Plan: Infrastructure + CreateEvent slice

- **Spec:** `intent/calendar-app-spec.md` §3, §4, §5.1, §5.4, §6, §9 (POST), §9.1, §10, §11, §12 (unit rows: validation, time zone)
- **Status:** implemented; `dotnet test` passes (89 tests). Not yet run against a live SQL Server or Auth0 tenant.

## Goal

Add the persistence and auth foundation (EF Core + SQL Server, Auth0 JWT, problem details, string enums) and the first slice, `POST /api/events`, so the remaining slices build on it.

## Decisions taken while planning

- Timed recurring events store `StartUtc`/`EndUtc` = NULL (§10 paragraph wins; column note corrected).
- A local time in the autumn repeated hour maps to the earlier offset (EEST, +3). Added to §5.4.
- Unit tests only in this step; the Testcontainers + JWKS integration harness comes next.

## Steps

1. Packages: EF Core SqlServer + Design, JwtBearer (10.0.x).
2. `Recurrence/HelsinkiTime.cs`: local → UTC (gap → forward by DST delta, overlap → earlier offset), UTC → local.
3. `Infrastructure/`: `Event` entity, `EventKind`, `RuleType`, `EventConfiguration` (string enums, column types, CHECKs, indexes), `CalendarDbContext`, Auth0 JWT setup (`sub` → owner id), DB setup + migrate in Development, `InitialCreate` migration. `compose.yaml` for SQL Server 2022 Express in Podman.
4. `Features/Events/CreateEvent/`: request/response records, validator, handler (pure `ToEntity` + `HandleAsync`), endpoint.
5. `Program.cs`: problem details, `JsonStringEnumConverter`, auth, `/api/events` group with `RequireAuthorization()`.
6. Unit tests: `HelsinkiTime`, validator (§12 validation row), `ToEntity` mapping.

## Verification

`dotnet test` from the repo root.

## Open points

- Integration test harness (Testcontainers SQL Server on Podman, local JWKS): happy path, 400, 401, owner isolation. Next piece of work.
- CORS: add once the SPA origin is known.
- `System.Security.Cryptography.Pkcs` is pinned to 10.0.14 to override a vulnerable transitive 9.0.11 from SqlClient. Drop the pin once SqlClient brings a patched version.
- `201 Location` points at `/api/events/{id}`, which returns 404 until the GetEvent slice exists.
