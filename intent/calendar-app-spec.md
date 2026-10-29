# Spec: Calendar web app

- **Intent:** [calendar-app.md](calendar-app.md)
- **Author:** Hereigo
- **Date:** 2026-10-28
- **Status:** draft, pending product owner review

## 1. Summary

A single-user-role calendar web app. Each signed-in user creates, views, updates and deletes their own private events. Events are single-occurrence or all-day, or recurring by a fixed rule with an end date. Times are in Europe/Helsinki. Reminders fire at event start, in the app and as browser notifications (Notification API). No operating system notifications.

## 2. Goals and non-goals

**Goals**

1. Users sign in with Auth0 and see only their own events.
2. Users manage single, all-day and recurring events, including the four recurrence rules in section 5.
3. Reminders are delivered at event start while the app is in use.
4. The app is deployed to production.

**Non-goals (v1)**

- Shared events, invitations, or multiple users per event.
- Admin role or any role beyond "user".
- Editing or deleting a single occurrence of a series.
- Reminder offsets (e.g. "15 minutes before"). Reminders fire at start only.
- Reminders while the app is closed (no server push).
- GDPR tooling (export, erasure). Retention is indefinite.
- Calendar import/export, multiple calendars, time zones other than Europe/Helsinki.
- Week view, day view, agenda view, or any view other than month view.

## 3. Users and access

- One role: user. All users are equal. There are no admin users.
- Authentication is delegated to Auth0. The frontend uses Auth0's SPA login flow (Authorization Code with PKCE). The backend validates the Auth0 access token (issuer, audience, signature, expiry) on every API call.
- The user's identity is the token `sub` claim. Every event stores its `OwnerId` = `sub`.
- Every query and command filters by `OwnerId` on the server. A user who requests another user's event id gets `404`, not `403`, so ids are not confirmed to exist.

## 4. Event types

| Kind | Fields that matter | Notes |
|---|---|---|
| Single (timed) | Start, end (local date and time, Europe/Helsinki) | End must be after start. |
| All-day | Start date, end date (both inclusive) | No time of day. Single-day all-day is an end date equal to the start date. |
| Recurring | Start (date and time, or date for all-day), recurrence rule (section 5), until date | Occurrences are generated from the rule. |

Every event has: title (required, 1–200 chars), description (optional, up to 4000 chars), kind.

## 5. Recurrence

### 5.1 Rules

| Rule | Parameters | Occurrence dates |
|---|---|---|
| Every X days | X ≥ 1 | Start date, then start + X, start + 2X, … |
| Each X day of month | X in 1–31 | Day X of every month. |
| Each X day of week | X in Monday–Sunday | Every week on that weekday, starting from the first such day on or after the start date. |
| Each X day of year | Month and day (e.g. 14 March) | Same month and day every year. |

All recurring events require an **until date** (inclusive). The last occurrence is the last generated date ≤ until date. There is no "forever" option.

### 5.2 Month and day overflow

If a monthly or yearly occurrence's day does not exist in that month:

- Day 29 Feb in a non-leap year → 28 Feb.
- Day 30 or 31 in a shorter month → last day of that month.

Each shifted occurrence still counts as one occurrence. It is not skipped. The rule is applied per month, so the day returns to X in months that have it.

### 5.3 Editing and deleting series

- Individual occurrences cannot be edited or deleted.
- Any change to a recurring event (rule, times, title, description, until date) applies to the whole series.
- Deleting a recurring event deletes the whole series.
- Changing the start date re-anchors the rule. For "every X days" the new start is the new anchor.

### 5.4 Generation

- Occurrences are not stored. Only the rule is stored. Occurrences are computed when a date range is queried.
- Times are computed in local wall-clock time (Europe/Helsinki) and then converted to UTC. A 10:00 event is 10:00 local before and after a DST change.
- An occurrence that falls in a DST gap (e.g. 03:30 on the spring-forward day) is moved to the next valid local time (04:30 in Helsinki). This is a documented choice; see section 13.

## 6. Time zone

- All user-facing times are Europe/Helsinki (EET UTC+2, EEST UTC+3 during DST).
- Timed events store `StartUtc` and `EndUtc`, plus a `TimeZone` field set to `Europe/Helsinki`. The field makes the zone explicit if it ever changes.
- All-day events and recurrence start dates are stored as local dates (no UTC conversion). They do not shift with DST.
- The backend uses IANA zone ids (`Europe/Helsinki`). The deployment target must have time zone data available to .NET.

## 7. Notifications

- When an event (or occurrence) starts, the app shows an in-app notification and, if the user has granted permission, a browser notification (Notification API).
- Notifications are scheduled in the browser for occurrences in the next 24 hours, read from the API when the app loads.
- All-day events notify at 00:00 Helsinki time on their start date.
- If the app is closed, no notification is delivered. This is a stated limitation of v1.
- The user can deny browser permission. The in-app notification still works while the app is open.

## 8. Functional requirements

| ID | Requirement |
|---|---|
| FR-1 | User signs in through Auth0 and is redirected back to the calendar. |
| FR-2 | User sees a month view with navigation to previous and next month and a "today" marker (from ui-template). |
| FR-3 | Month view shows all occurrences (single, all-day, recurring) for the visible range. |
| FR-4 | User creates a single, all-day, or recurring event from a form. |
| FR-5 | User opens an event and edits it. For a recurring event, the form states that the change applies to the whole series. |
| FR-6 | User deletes an event. For a recurring event, the confirmation states that the whole series is deleted. |
| FR-7 | User can see the upcoming notification list. |
| FR-8 | User signs out. |

## 9. API (Minimal API, Vertical Slices)

Base path: `/api`. All endpoints require a valid Auth0 bearer token.

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/events?from=YYYY-MM-DD&to=YYYY-MM-DD` | Returns expanded occurrences in the date range (inclusive). Each item has `eventId`, `occurrenceStartUtc`, `occurrenceEndUtc` or dates for all-day, `kind`, `title`. Max range: 370 days. |
| GET | `/api/events/{id}` | Returns the event definition (not occurrences). |
| POST | `/api/events` | Creates an event. Returns `201` with the event. |
| PUT | `/api/events/{id}` | Replaces the event definition. Applies to the whole series. |
| DELETE | `/api/events/{id}` | Deletes the event and its series. Returns `204`. |
| GET | `/api/notifications?horizonHours=24` | Returns occurrences starting in the next N hours (max 48). |

Errors use RFC 9457 problem details. Validation errors return `400`. Unknown or foreign ids return `404`. Missing or invalid token returns `401`.

### 9.1 Vertical slices

Each feature is a folder with its own request, validation, handler, and endpoint mapping:

```
Features/
  Events/
    CreateEvent/
    UpdateEvent/
    DeleteEvent/
    GetEvent/
    ListOccurrences/
  Notifications/
    ListUpcoming/
Recurrence/          (pure domain logic, no EF or HTTP references)
Infrastructure/      (EF Core DbContext, Auth0 config, migrations)
```

The recurrence engine lives in `Recurrence/` and has no dependency on EF Core or HTTP. This makes it testable in isolation (section 12).

## 10. Data model

SQL Server via EF Core. Local development runs SQL Server 2022 or later in a Docker container. Production runs SQL Server. Both use the same EF Core migrations. Tables:

**Events**

| Column | Type | Notes |
|---|---|---|
| Id | uniqueidentifier | PK |
| OwnerId | nvarchar(255) | Auth0 `sub`. Indexed. |
| Title | nvarchar(200) | 1–200 chars |
| Description | nvarchar(4000) NULL | up to 4000 chars |
| Kind | nvarchar(16) | `Single`, `AllDay`, `Recurring` |
| StartUtc | datetime2(3) NULL | UTC. Timed events and timed recurring events |
| EndUtc | datetime2(3) NULL | UTC. Timed single events |
| StartDate | date NULL | All-day events and all-day recurring events. |
| EndDate | date NULL | Inclusive. All-day single events. |
| TimeZone | nvarchar(64) | `Europe/Helsinki` |
| RuleType | nvarchar(16) NULL | `EveryDays`, `MonthDay`, `WeekDay`, `YearDay` |
| Interval | int NULL | X for `EveryDays` |
| DayOfMonth | int NULL | `MonthDay`, `YearDay` |
| Month | int NULL | `YearDay` |
| DayOfWeek | int NULL | `WeekDay` (ISO 1–7) |
| UntilDate | date NULL | Inclusive. Required for `Recurring`. |
| CreatedAtUtc | datetime2(3) | UTC |
| UpdatedAtUtc | datetime2(3) | UTC |

Timed recurring events also need a local start time. Store it in `StartLocalTime` (`time`) with `StartDate` as the first local date.

Constraints: `CHECK` that `Kind` and the nullable columns agree. Index on `(OwnerId, StartUtc)` and `(OwnerId, StartDate)`.

Migrations are EF Core migrations, applied on startup in development (against the Docker container) and by an explicit step in production. There is one migration set, for SQL Server only.

## 11. Security

- Auth0 access tokens only. No tokens in local storage. The SPA keeps tokens in memory with Auth0's SDK.
- The backend enforces `OwnerId` on every query and command. The frontend is not trusted.
- Input validation on all DTOs. Title and description length limits. Date ranges limited (section 9).
- EF Core parameterized queries only. No raw SQL built from input.
- HTTPS only. HSTS on. CORS allows only the SPA origin.
- Logs do not contain event titles or descriptions.
- Secrets (Auth0 client secret if used, connection strings) come from environment or secret store. Not committed. The local Docker SQL Server `sa` password is also kept out of the repo (environment variable or .NET user secrets).
- Dependency updates and a vulnerability scan run in CI.

## 12. Test plan

The Test stage requires a verification command to pass before the work is reported done. Proposed command: `dotnet test` for the backend and `npm test` plus `npm run build` for the frontend. Both run in CI. CI must have Docker available so the integration tests can start SQL Server.

| Level | What | Key cases |
|---|---|---|
| Unit | Recurrence engine | Every X days across year boundary; monthly day 31 in Feb, Apr, Jun, Sep, Nov; yearly 29 Feb in non-leap years; weekday rule first occurrence; until date inclusive; until date before start (rejected). |
| Unit | Time zone conversion | 10:00 local before and after DST start and end; DST gap (03:30 on spring-forward day). |
| Unit | Validation | Title length; end before start; missing until date; X ≤ 0; day of month outside 1–31. |
| Integration | API with SQL Server (Docker container, via Testcontainers) | Owner isolation (user B cannot read or change user A's events); series update changes all occurrences; series delete removes all; range limit. |
| Integration | Auth | Missing token 401; token for another audience rejected. |
| E2E | Frontend | Sign in; create a recurring event; see it in month view; edit series; delete series; sign out. |
| Performance | API latency | `GET /api/events` over a 42-day range against SQL Server in Docker, with a single user's typical data set. Fails the build if p95 is 300 ms or more. |
| Accessibility | UI, WCAG 2.1 AA | Automated scan (axe or equivalent) of the month view, event form, and delete confirmation. Zero violations. Keyboard-only run of create, edit, delete and sign-out. |

Auth0 is mocked in integration tests with a locally generated JWKS.

## 13. Decisions taken in this spec (for product owner review)

These were not explicit in the intent. Each is a reasonable default and is easy to change.

1. **"Each X day of week"** means one chosen weekday, repeating every week. Not a set of weekdays.
2. **"Each X day of year"** means the same month and day every year.
3. **Until date is required** for every recurring event, because the intent allows only "until a specific date".
4. **All-day events** can span multiple days (start date to end date, inclusive). If only single-day all-day is wanted, drop `EndDate` for all-day events.
5. **DST gap** (a time that does not exist on spring-forward day) moves to the next valid time.
6. **Notifications** use the in-app and browser Notification API only. No operating system notifications. No server push, so nothing fires when the app is closed.
7. **Reminder offset** is not in v1.
8. **Ownership errors** return `404`, not `403`.
9. **Database** is SQL Server in both development (Docker container) and production. SQLite is not used. The container image tag is pinned in the dev compose file, so dev and prod run the same major version.
10. **.NET SDK** must be 10.0.403 or later.
11. **Views.** v1 has month view only. The month view is the ui-template layout. Week, day and agenda views are out of scope.
12. **Accessibility and performance.** The UI meets WCAG 2.1 AA. API p95 latency for `GET /api/events` over a 42-day range is under 300 ms for a single user's data.
13. **Policy review defaults.** No internal brand, security, compliance, or UX policies apply. The spec is reviewed against OWASP ASVS Level 1 and the OWASP Top 10 for security (§11), WCAG 2.1 AA for UX (decision 12), and the intent's compliance statement (no GDPR, indefinite retention). Brand follows `ui-template/`. Earlier SDKs are not supported. The backend repo enforces this with `global.json`, and the verification command fails on an older SDK.

## 14. Open questions

1. **Production hosting.** The intent says "deployed to production" but names no host, OS, or hosting model. SQL Server needs a host: a VM, a managed service (for example Azure SQL, which changes the Docker-only dev parity), or a container with a persistent volume. Which host, who owns the database, and what is the backup and restore policy?
2. **Week and day views.** Resolved: month view only for v1. Week and day views are out of scope (see §2 and §13 decision 11).
3. **Organizational skills.** Resolved: no internal policies apply beyond what is written in this spec and the intent. The review uses the defaults in §13 decision 13.
4. **Accessibility and performance targets.** Resolved: WCAG 2.1 AA for the UI, and API p95 under 300 ms for a 42-day range on a single user's data (see §13 decision 12).
5. **Deadline.** None set.

## 15. Acceptance criteria

Each criterion maps to a test in section 12.

1. A signed-out user is redirected to Auth0 login. An API call without a token returns 401.
2. A user creates a single timed event and sees it on the correct local day in month view.
3. A user creates an all-day event spanning three days and sees it on each of those days.
4. A user creates "every 3 days" from 1 March until 31 March. The occurrences are 1, 4, 7, … 31 March.
5. A user creates "day 31 of each month" starting 1 January until 30 June. February, April and June show the occurrence on the last day of that month. The shift is shown, not skipped.
6. A user creates "each 29 February" (yearly) starting 1 January 2027 until 31 December 2029. The 2027 and 2029 occurrences are on 28 February. The 2028 occurrence is on 29 February (leap year).
7. A user creates "10:00 every Monday" that crosses the spring DST change. Each occurrence is at 10:00 Helsinki time.
8. A user edits a recurring event's title. All occurrences show the new title.
9. A user deletes a recurring event. No occurrence remains.
10. User A cannot read, edit or delete user B's events. Attempts return 404.
11. A reminder notification appears in the app at start time for a timed event, and as a browser notification if permission is granted.
12. The verification command passes in CI before the release is deployed.
13. The app is deployed to production (pending open question 1).
14. The month view, event form, and delete confirmation have no WCAG 2.1 AA violations in the automated scan. Create, edit, delete and sign-out can be completed with the keyboard only.
15. `GET /api/events` for a 42-day range has a p95 under 300 ms against SQL Server in Docker, with a single user's data.

## 16. Traceability to intent

| Intent item | Spec section |
|---|---|
| Authorization | 3, 11, FR-1, FR-8 |
| Add, update, delete events | 8, 9, FR-4 to FR-6 |
| Event types | 4 |
| Recurring types | 5.1 |
| Recurrence end: until date | 5.1 |
| Edit whole series only | 5.3 |
| Feb 29 and day 30/31 shift | 5.2 |
| Europe/Helsinki | 6 |
| Private events only | 3 |
| In-app and browser notifications at start | 7 |
| React frontend from ui-template | 8 (FR-2), 13 |
| .NET 10, Minimal API, Vertical Slices, EF Core with SQL Server | 9, 9.1, 10, 13 (decision 9) |
| Auth0 | 3, 11 |
| Indefinite retention, no GDPR | 2 (non-goals) |
| Deployed to production | 15, 14 |
