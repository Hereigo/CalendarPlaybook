# Plan: Integration test harness + CreateEvent coverage

- **Spec:** `intent/calendar-app-spec.md` §3 (Auth0 token validation, `OwnerId` = `sub`), §9 (POST, problem details, 400/401), §10 (SQL Server 2022 Express in Podman, EF migrations), §11 (owner enforcement), §12 (Integration rows: owner isolation, auth, WeekDay round-trip; "Auth0 is mocked with a locally generated JWKS")
- **Status:** implemented. `dotnet test` passes from the repo root: 89 unit + 13 integration tests, the integration tests against a Testcontainers SQL Server 2022 on Podman. With the Podman machine stopped, the 13 are skipped and the run exits 0; with `CALENDAR_INTEGRATION_REQUIRED=true` they fail.
- **Previous step (done):** Infrastructure + CreateEvent slice, 89 unit tests. Not yet run against a live SQL Server or Auth0 tenant.

## Goal

A reusable integration-test harness (real HTTP pipeline, real JwtBearer validation, real SQL Server) that every later slice plugs into, and the first tests with it for `POST /api/events`.

## Decisions

- **Separate project `tests/CalendarPlaybook.Api.IntegrationTests/`**, not a folder in the unit-test project. Reasons: different dependencies (Testcontainers, Mvc.Testing) stay out of the unit project; unit tests run on their own with `dotnet test tests/CalendarPlaybook.Api.Tests`; the integration project can use xUnit v3, which has runtime skipping (`Assert.Skip`), without migrating the 89 unit tests off xUnit v2.
- **One SQL Server container per test run** (xUnit assembly fixture), same digest-pinned `mssql/server:2022` image as `compose.yaml`, `MSSQL_PID=Express`. The fixture applies the EF migrations once (`Database.MigrateAsync`), so the migrations themselves are under test. Each test uses fresh random `sub` values, so tests never see each other's rows and need no cleanup.
- **Test host:** `WebApplicationFactory<Program>` in environment `Testing`. That keeps `MigrateInDevelopment`/`DevSqlContainer` and user secrets out of the run; the connection string and `Auth0:Domain`/`Auth0:Audience` come from the factory (`UseSetting`), never from secrets.
- **Auth0 replacement:** the fixture generates an RSA key at start, publishes it as a JWKS inside a static OpenID configuration (`JwtBearerOptions.Configuration`, issuer `https://<test domain>/`), so no network call to Auth0. `Program`/`AuthSetup` are unchanged; JwtBearer still validates issuer, audience, signature and lifetime. A `TestTokens` helper mints tokens with any `sub`, and variants (wrong key, wrong audience, wrong issuer, expired).
- **No Podman → skip, with an override.** If Testcontainers finds no Docker endpoint, every integration test is skipped with the message "Podman/Docker not reachable: … See docs/local-setup.md". `dotnet test` stays green, so the Stop hook remains reliable without changes. Any other failure (image pull, migration, SQL error) fails the run. Setting `CALENDAR_INTEGRATION_REQUIRED=true` (for CI, spec §12) turns the skip into a failure.
- **Podman endpoint on Windows:** Testcontainers finds the Podman machine through the Docker context Podman registers; the fixture also falls back to `npipe://./pipe/podman-machine-default` when `DOCKER_HOST` is unset. Manual `DOCKER_HOST` is documented in `docs/local-setup.md`. Rootless Podman runs Ryuk fine; no extra setting.
- **xUnit v3 3.2.2, not 4.x:** 4.x runs on Microsoft.Testing.Platform only, which `dotnet test` on the .NET 10 SDK rejects in VSTest mode. Moving the whole repo to MTP (`global.json` `test.runner`) would also move the xUnit v2 unit project; not worth it now.
- **Stop hook unchanged:** skipped tests count as passed, so it stays green without Podman. With Podman up, the run takes ~35 s longer (hook timeout is 300 s).

## Steps

1. New project `tests/CalendarPlaybook.Api.IntegrationTests` (xunit.v3, Microsoft.AspNetCore.Mvc.Testing 10.0.14, Testcontainers.MsSql), added to `CalendarPlaybook.slnx`.
2. `Infrastructure/`: `SqlServerFixture` (container + migrations + skip detection), `CalendarApiFactory` (`WebApplicationFactory<Program>`, settings, test JWKS), `TestTokens`, `IntegrationTest` base (HttpClient per `sub`, `CalendarDbContext` for DB assertions).
3. `Features/Events/CreateEvent/CreateEventTests.cs`:
   - 201 with `Location: /api/events/{id}`; row persisted with `OwnerId` = `sub`; timed single event stored as UTC from Helsinki local time; all-day stored as local dates.
   - 400 `application/problem+json` with `errors` for invalid input (empty title, end before start, missing until date); 400 for a numeric enum value.
   - 401 with no token, wrong signing key, wrong audience, wrong issuer, expired token.
   - Owner isolation: A and B both create; each row carries only its creator's `sub`; an `ownerId` in the body is ignored. (GetEvent does not exist yet, so assertions are at DB level.)
   - WeekDay rule round-trip through SQL Server for all 7 days (string names, Sunday included).
4. Verify: `dotnet test` with Podman running (integration tests executed against SQL Server), and with Podman stopped (tests skipped with the message, run still green; with `CALENDAR_INTEGRATION_REQUIRED=true` it fails).
5. Docs: `docs/local-setup.md` (Podman endpoint, how to run each test project), `CLAUDE.md` Commands section, this file (status, open points).

## Verification

`dotnet test` from the repo root, with Podman running and with it stopped.

## Open points

- Integration tests for the next slices: GetEvent/UpdateEvent/DeleteEvent owner isolation (`404` for a foreign id), series update/delete, range limit (spec §12 Integration rows).
- CI: set `CALENDAR_INTEGRATION_REQUIRED=true` and make Podman available when the pipeline is set up.
- A valid token without a `sub` claim makes `GetOwnerId()` throw (500, not 401). Cover with a test once the expected behaviour is agreed; out of scope for this step.
- CORS: add once the SPA origin is known.
- `System.Security.Cryptography.Pkcs` is pinned to 10.0.14 to override a vulnerable transitive 9.0.11 from SqlClient. Drop the pin once SqlClient brings a patched version.
- `201 Location` points at `/api/events/{id}`, which returns 404 until the GetEvent slice exists.
