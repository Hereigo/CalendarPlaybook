
What next

1. Get the spec signed off. Decisions 1–13 in §13 are defaults the product owner hasn't reviewed. Open question 1 (production hosting: which host, who owns the database, backup policy) blocks deployment but not building. Decide whether to proceed while it stays open.
2. Step 4 of the playbook: prepare the repo for agents. Add a root CLAUDE.md (run /init, then trim it). Include the build, test and lint commands the spec names: dotnet test, npm test, npm run build.
3. Scaffold the two projects. Start with the backend:
   - a global.json pinned to 10.0.403, as §13 decision 13 requires;
   - a Minimal API solution with Features/, Recurrence/ and Infrastructure/ folders;
   - the recurrence engine first, since it is pure logic with unit tests. Those tests are the cheapest way to verify the rules in §5.
4. Install Podman (Podman Desktop or the Podman CLI) before the integration tests and the dev database are needed.

I'd start with step 2 and then the recurrence engine in step 3. Should I go ahead with those? I'd create the CLAUDE.md and backend skeleton in this repo and commit nothing until you say so.