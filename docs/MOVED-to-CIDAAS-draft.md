Checking cidaas token claims (aud/iss) before writing the plan.

Tests are updated. Now the SPA: swapping packages.

Frontend builds. Now docs, spec and guardrails.

Now the intent/spec files — these go through the Edit tool so the guard hook can ask you for each.

Only your local, untracked `.env` / `.env.local` still hold Auth0 values. I'll leave those for you to fill with cidaas values. Running the backend tests now.

Auth0 is gone from the solution and cidaas sign-in replaces it. All automated checks pass: `dotnet test` (125 unit and 67 integration tests, none skipped), `npm test` and `npm run build`. I haven't signed in against a real cidaas tenant yet, and nothing is committed.

**Backend**
- [AuthSetup.cs](src/CalendarPlaybook.Api/Infrastructure/Auth/AuthSetup.cs) now has `AddCidaas`, which reads two settings:
  - `Cidaas:BaseUrl` is your tenant URL. The API gets the issuer and signing keys from that tenant's discovery document.
  - `Cidaas:Audience` must match the token's `aud`. By default cidaas puts the client id there.
- The rest works as before: `sub` is still the owner id, a token without `sub` gets 401, and the app won't start if either setting is missing.
- In the tests I renamed the stand-in tenant and issuer and dropped the `auth0|` prefix from test user ids.

**Frontend**
- `@auth0/auth0-react` is replaced by `oidc-client-ts` + `react-oidc-context`. Sign-in uses Authorization Code with PKCE, and tokens are kept in memory only, as the spec requires. The settings are in the new `src/auth.ts`.
- Tokens renew in the background through a new `silent-renew.html` page, which I added as a second Vite entry point.
- The new variables are `VITE_CIDAAS_BASE_URL`, `VITE_CIDAAS_CLIENT_ID` and an optional `VITE_CIDAAS_RESOURCE`.

**Docs and spec:** I replaced Auth0 with cidaas in the intent, the spec, `CLAUDE.md`, `docs/local-setup.md`, both `.env.example` files and the hook message. `docs/plan.md` has the new plan and the tenant setup.

**What you need to do**
1. In cidaas, create a single-page app client with PKCE:
   - redirect and post-logout URI `http://localhost:5173`
   - silent redirect URI `http://localhost:5173/silent-renew.html`
   - allowed origin `http://localhost:5173`

2. Your local `.env` and `src/CalendarPlaybook.Web/.env.local` still have the old `VITE_AUTH0_*` values. I didn't touch them because they hold your own settings; switch them to the `VITE_CIDAAS_*` names.

3. Replace the Auth0 user secrets:
```sh
dotnet user-secrets set Cidaas:BaseUrl "https://<tenant>.cidaas.eu" --project src/CalendarPlaybook.Api
```
```sh
dotnet user-secrets set Cidaas:Audience "<spa-client-id>" --project src/CalendarPlaybook.Api
```
4. Sign in once and check that the token's `iss` and `aud` match these two settings.

Events created under Auth0 users won't show up for cidaas users, because their owner ids differ. That only affects local test data, so I didn't migrate it.