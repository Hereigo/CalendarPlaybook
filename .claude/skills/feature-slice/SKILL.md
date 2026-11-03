---
name: feature-slice
description: How to add or change a backend feature slice (endpoint) in CalendarPlaybook.Api - folder layout, ownership filtering, validation, problem details, enums, and tests. Use when creating or editing anything under src/CalendarPlaybook.Api/Features/.
---

# Adding a feature slice

Spec: §9 (API), §9.1 (slices), §3 and §11 (security). Read the matching spec rows first.

## Layout

One folder per slice, e.g. `Features/Events/CreateEvent/`:

- `CreateEventRequest.cs` / `...Response.cs` - records, no EF types.
- `CreateEventValidator.cs` - all input rules from spec §4, §5.1, §9 (lengths, ranges, max date range 370 days, horizon max 48 h).
- `CreateEventHandler.cs` - the logic. Takes `DbContext`, the owner id, and the request.
- `CreateEventEndpoint.cs` - `MapPost(...)` extension method, wired from `Program.cs`. Thin: bind, validate, call handler, map result.

Slices do not call each other. Shared domain logic goes to `Recurrence/` (pure, no EF/HTTP). Shared persistence goes to `Infrastructure/`.

## Rules every slice follows

1. **Owner filter.** Every query starts with `.Where(e => e.OwnerId == ownerId)`; `ownerId` is the token `sub`. Never trust an owner id from the request body.
2. **Foreign or unknown id → `404`**, never `403`. Use the same code path for both cases.
3. **Errors are RFC 9457 problem details.** Validation → `400` (`Results.ValidationProblem`). Missing/invalid token → `401` (via auth middleware, `RequireAuthorization()` on the group).
4. **Enums as strings** (`Kind`, `RuleType`, `DayOfWeek`) via `JsonStringEnumConverter`. Use `System.DayOfWeek`. Reject undefined enum values.
5. **Times.** Timed events: UTC in DB, `TimeZone = "Europe/Helsinki"`. All-day and recurrence start: `DateOnly`, no conversion. Recurring timed: `StartLocalTime`/`EndLocalTime`, converted per occurrence (§5.4, DST gap → next valid time).
6. **Recurrence is computed**, never stored. Edits and deletes apply to the whole series.
7. **Logging.** Never log titles or descriptions. Log ids and kinds only.
8. **EF Core** LINQ only; no raw SQL from input.

## Tests (required before done)

- Unit tests for the validator and any pure logic in `tests/CalendarPlaybook.Api.Tests/<same path>`.
- Integration tests in `tests/CalendarPlaybook.Api.IntegrationTests/<same path>`: happy path, `400`, `401`, and owner isolation (user B gets `404` on user A's id). Derive from `IntegrationTest`, use a fresh `NewSub()` per user and `CreateClientFor(sub)`; see `Features/Events/CreateEvent/CreateEventTests.cs`.
- `dotnet test` must pass. The Stop hook enforces this.
