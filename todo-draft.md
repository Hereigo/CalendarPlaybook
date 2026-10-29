The intent is captured and a spec draft already exists, so you're between Step 2 (intent) and Step 3 (spec) of the playbook. Nothing is blocking code yet, but the spec needs decisions from you before you approve it.

Where things stand

- intent/calendar-app.md is a draft with status draft, and it has no open questions. Its change is staged but not committed.
- intent/calendar-app-spec.md is a full spec draft, marked "pending product owner review". git status shows it as clean, so it's already committed.
- The intent points at the new stack (.NET 10 Minimal API, Vertical Slices, EF Core with SQLite, React on ui-template, Auth0). The existing MVC 5 project is deprecated and is not the target.

Next steps, in order

1. Answer the three blocking spec questions (section 14 of the spec):
   - Which host runs production. SQLite needs a persistent volume, so this affects the design.
   - Whether the new backend and frontend go in this repo or a new one. This decides where CLAUDE.md and the project scaffolding live.
   - Whether month view alone is enough for v1. The ui-template only has a month view.

   Two smaller items to confirm while you're there: the notification wording in the intent says "OS local notifications", but the spec chose browser Notification API (decision 6), and the accessibility and performance targets are proposed defaults only.
2. Commit the staged intent change, then update the spec to match your answers and set its status to approved.
3. Run organizational skill review (brand, security, compliance, UX) on the spec, if your team has those skills. The playbook recommends this before approval, and none has been run yet.
4. Scaffold the project and run /init to generate CLAUDE.md. Add the build and test commands from spec section 12 (dotnet test, npm test, npm run build).
5. Write plan.md in plan mode from the approved spec, then implement, starting with the recurrence engine in Recurrence/. It has no EF or HTTP dependencies and is the easiest part to test in isolation.

I haven't changed any files. If you tell me the answers to the three questions, I can update the spec, commit the intent, and scaffold the CLAUDE.md next.