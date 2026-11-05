# Plan: current state

Last refreshed: 2026-11-05.

## Current slice: ListOccurrences (`GET /api/events?from=&to=`), done

- **Spec:** `intent/calendar-app-spec.md` §9 (`GET /api/events?from=YYYY-MM-DD&to=YYYY-MM-DD` returns expanded occurrences, range inclusive, each item has `eventId`, `occurrenceStartUtc`, `occurrenceEndUtc` or dates for all-day, `kind`, `title`, max range 370 days), §5 (rules, month/day overflow, generation), §5.4 and §6 (wall-clock times per occurrence, DST gap and repeated hour), §10 (columns, indexes), §11 (owner enforcement, no titles in logs), §12 (range-limit integration row).
- **Status:** implemented. `dotnet test` from the repo root with Podman up: 148 unit + 67 integration tests passed, 0 skipped (2026-11-05).
- **Previous slices (done):** Infrastructure + CreateEvent; integration-test harness; GetEvent; UpdateEvent; DeleteEvent; legacy import side task (see below).

## Decisions

The spec leaves these open; each is a default, none changes a §13 decision. Proposed spec wording is in Open points.

- **Response is a bare JSON array** of items, ordered by local start date, all-day before timed, then start time, title, event id. Item fields: `eventId`, `kind`, `title`, `occurrenceStartUtc`/`occurrenceEndUtc` (timed only, serialized with `Z`), and `startDate`/`endDate` (Europe/Helsinki local dates, `endDate` inclusive) on **every** item. The local dates go beyond the spec's "dates for all-day": they let the month view place a timed occurrence in a cell without time-zone maths in the browser.
- **`from` and `to` are local Helsinki dates**, `yyyy-MM-dd` only, both inclusive. A bad format, a missing value, `to` before `from`, or a year outside 0002–9998 returns `400` with the parameter name as the error key (`from`/`to`).
- **370 days = 370 calendar days counting both ends** (`to - from + 1 <= 370`). So 2027-01-01..2028-01-05 passes and ..2028-01-06 returns `400` on `to`. The other reading (`to - from <= 370`) would allow one more day.
- **What counts as in range:** a timed single event when its instants overlap the local-day range (`start < end of to` and `end > start of from`, so an event that ends exactly at 00:00 on `from` is not in range); an all-day event when its date span overlaps (a multi-day event starting before `from` is returned with its full span); a recurring event once per occurrence date from `RecurrenceEngine` (a timed occurrence is on one local day, so `startDate == endDate`).
- **Recurring times** come from the occurrence date plus `StartLocalTime`/`EndLocalTime` through `HelsinkiTime.ToUtc`, so 10:00 stays 10:00 local across DST and a start in the gap moves to the next valid time.
- **One query, then pure expansion.** SQL narrows the rows by owner and kind-specific range predicates (uses the `(OwnerId, StartUtc)` and `(OwnerId, StartDate)` indexes); `ListOccurrencesHandler.Expand` applies the exact rules in memory and is unit-tested without a database. Only counts are logged.
- **No paging or result cap.** The range cap bounds the work; the result is whatever one owner's events expand to.
- **Fix found on the way (GetEvent):** `GET /api/events/{id}` returned `startUtc`/`endUtc`/`createdAtUtc`/`updatedAtUtc` without a `Z` when read from SQL Server (the `DateTime` kind is unspecified there), unlike the `POST` response. They are now marked UTC; `GetEventTests` asserts it.

## Open points

Each needs a decision or a spec edit (`intent/` edits ask for confirmation via a hook). Nothing here is implemented yet.

- **Spec §3:** add that a token without a usable `sub` is rejected with `401` (the code does this in `Infrastructure/Auth/AuthSetup.cs`).
- **Spec §9:** propose wording for the item shape (bare array, local `startDate`/`endDate` on every item), ordering, `from`/`to` as local inclusive dates, "370 days counting both ends", and `401` for a token without `sub`.
- **Performance:** the §12 row (p95 under 300 ms for a 42-day range) is not measured. The integration project has no timing test.
- **Next slice:** `Notifications/ListUpcoming` (`GET /api/notifications?horizonHours=24`, max 48; all-day events notify at 00:00 Helsinki on their start date; spec §7). It can reuse `ListOccurrencesHandler.Expand` after extracting it to a shared place.
- **Shared code:** extract the request/validator/mapping that `UpdateEvent` borrows from `CreateEvent` and `GetEvent` (`Features/Events/Shared/` or `Infrastructure/Events/`), together with the expansion above.
- **Concurrency:** no optimistic concurrency on `PUT` (last write wins). Raise with the product owner if wanted.
- **CI:** set `CALENDAR_INTEGRATION_REQUIRED=true`, make Podman available, add the vulnerability scan (spec §11), and run both `dotnet test` and the web `npm test` / `npm run build` (spec §15.12). No CI exists yet.
- **Lint:** none configured. Add a lint command to CLAUDE.md once one is chosen.
- **Security pin:** `System.Security.Cryptography.Pkcs` is pinned to 10.0.14 to override a vulnerable transitive 9.0.11 from SqlClient. Drop the pin once SqlClient brings a patched version.

## Side task (done): legacy Calendarium data import

- `tools/CalendarPlaybook.LegacyImport`: a one-off console tool that imports `DATA_FROM_OLD_PROJECT/database_backup.json` (124 rows, git-ignored) into `Events` for one owner `sub`. It is a data migration, **not** the user-facing import that spec §1 keeps out of scope. The spec is unchanged.
- Mapping defaults (agreed with the user): `Once` → Single/AllDay, `Yearly` → YearDay, `Monthly` → MonthDay, `EveryXdays` → EveryDays, all anchored on the old `Started` date. Old time 00:00/00:01 → all-day; other times → timed, end = start + 1 h (capped 23:59). `UntilDate = 2099-12-31`. Old description → title. Stray `EveryXDays` on non-interval rows is ignored with a warning.
- Unit tests: `tests/CalendarPlaybook.Api.Tests/LegacyImport/`. Dry run against the backup: 124 would import, 0 invalid.
