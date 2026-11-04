
**Loose ends to settle along the way:**

- Add a line to spec §3 about tokens without a `sub` (needs your approval for `intent/` edits).
- Add CI with `CALENDAR_INTEGRATION_REQUIRED=true`.

**Still open:**

- Performance is not measured. The spec asks for a p95 under 300 ms over a 42-day range, and there is no timing test yet.
- I’d like to propose spec wording for §9 covering the item shape, ordering, range semantics and the 401 for a token without sub. Edits to intent/ need your approval.
- Notifications/ListUpcoming is the last backend slice. After that comes the shared-code extraction.

- The notifications list (FR-7) is still not built. It needs the backend Notifications/ListUpcoming slice first.