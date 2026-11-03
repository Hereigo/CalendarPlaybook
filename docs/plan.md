# Plan: DeleteEvent slice (`DELETE /api/events/{id}`)

- **Spec:** `intent/calendar-app-spec.md` §9 (`DELETE /api/events/{id}` deletes the event and its series, returns `204`; `404` unknown or foreign id; `401` no valid token), §5.3 (whole series only), §9.1 (slice layout), §11 (owner enforcement, no titles in logs), §12 (series delete integration row).
- **Status:** implemented. `dotnet test` from the repo root with Podman up: 98 unit + 49 integration tests passed, 0 skipped.
- **Previous steps (done):** Infrastructure + CreateEvent; integration-test harness; GetEvent; UpdateEvent.

## Decisions

- **Hard delete, one row.** A recurring event is a single row (occurrences are computed), so deleting it removes the whole series. There is no soft delete or trash; the spec does not ask for one.
- **Owner filter and 404:** load by `OwnerId == sub && Id == id`, then remove; no match returns `404`, so foreign and unknown ids take the same path. A repeated delete returns `404` the second time.
- **`204` with no body**, as the spec says. Only the event id and kind are logged.
- **No unit tests:** the slice has no pure logic (no validator, no mapping); the integration tests cover it end to end.

## Steps (done)

1. `Features/Events/DeleteEvent/`: `DeleteEventHandler`, `DeleteEventEndpoint` (`MapDeleteEvent`), wired in `Program.cs`. No request type or validator: the only input is the route id.
2. Integration tests in `tests/CalendarPlaybook.Api.IntegrationTests/Features/Events/DeleteEvent/DeleteEventTests.cs`: `204` and the event is gone (`GET` returns `404`), recurring series removed while another event survives, second delete returns `404`, foreign and unknown ids return `404` with the owner's row intact, non-GUID id returns `404`, six `401` token cases.

## Verification

`dotnet test` from the repo root (Podman running; check the summary for `Skipped`).

## Open points

- Next slices: `ListOccurrences` (`GET /api/events?from&to`, 370-day limit, recurrence expansion via `Recurrence/`), `Notifications/ListUpcoming`.
- Extract the shared request/validator/mapping that `UpdateEvent` borrows from `CreateEvent` and `GetEvent` (`Features/Events/Shared/` or `Infrastructure/Events/`).
- No optimistic concurrency on `PUT` (last write wins); raise with the product owner if wanted.
- CI: set `CALENDAR_INTEGRATION_REQUIRED=true` and make Podman available when the pipeline is set up.
- Spec §3 does not say that a token without `sub` returns `401` (implemented in `AuthSetup`); consider a line there (`intent/` edits need your approval).
- CORS: add once the SPA origin is known.
- `System.Security.Cryptography.Pkcs` is pinned to 10.0.14 to override a vulnerable transitive 9.0.11 from SqlClient. Drop the pin once SqlClient brings a patched version.
