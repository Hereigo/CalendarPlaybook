# Intent: Calendar web app

- **Originator:** Hereigo
- **Date:** 2026-10-28
- **Status:** draft
- **Source:** idea

## Problem

Need to create calendar web app.

## Proposed outcome

Calendar app with authorization and ability to add, update, delete different types of events with different types of repeating.

## Affected users

- Single user role. All users are equal; there are no admin users.

## Affected systems

- Frontend (React): ui-template files used as the starting markup:
    - ../ui-template/Calendar.htm
    - ../ui-template/Calendar.css
- Backend (.NET 10): new project, Minimal API, Vertical Slices architectural pattern, EFCore with SQLite. This repo (ASP.NET MVC 5) is deprecated and is not the target.
- Data: EFCore with SQLite
- External: Auth0 auth provider.

## Calendar scope

- Event types: single, all-day, recurring-types (every X days, each X day of month, each X day of week, each X day of year)
- Recurrence end: until a specific date only.
- Recurrence editing: individual occurrences cannot be edited or deleted; any change applies to the whole series.
- Recurrence edge cases: if a monthly or yearly rule's day does not exist in a given month (Feb 29 in a non-leap year, or day 30/31 in a shorter month), the occurrence shifts to Feb 28 (or the last day of that month, for days 30/31).
- Time zone: Europe/Helsinki (EET, UTC+2; EEST, UTC+3 during daylight saving time)
- Sharing and permissions: no shared events - all private
- Notifications and reminders: in-app, plus browser or operating system local notifications, sent at event start.

## Constraints

- Platform: React frontend, .NET 10 backend, SQLite db
- Security and compliance: Auth0 provider and modern security best practices for web apps. Data retention: indefinite. No GDPR requirements.
- Performance: not yet intended
- Accessibility: not yet intended
- Deadline or release window: not yet intended

## Success criteria

- The app is deployed to production.

## Open questions

- None.
