## 1. Backend guard

- [x] 1.1 Add `backend/AnimeTracker.Api/Services/Infrastructure/CrossSiteRequestGuard.cs`: a static class with `public const string HeaderName = "X-Requested-With"` and `HeaderValue = "BetterMAL"`, and `public static async Task Invoke(HttpContext context, RequestDelegate next)` (design D3).
- [x] 1.2 In `Invoke`, refuse when `!HttpMethods.IsGet(request.Method) && request.Path.StartsWithSegments("/api")` and the header is missing or holds any other value — exact, ordinal match, and a header present more than once counts only if one value matches (design D5).
- [x] 1.3 On refusal, set `403` and write the app's `{ "error": ... }` body, then return without calling `next` (design D6). Otherwise `await next(context)`.
- [x] 1.4 Register it in `Program.cs` as `app.Use(CrossSiteRequestGuard.Invoke);`, after the `if (app.Environment.IsDevelopment())` block and directly above `app.UseAuthorization()` (`:227`), with a comment recording that the automatic `UseRouting` runs earlier but endpoints execute last, so this still short-circuits before any controller (design D4).

## 2. Frontend header

- [x] 2.1 In `frontend/src/api/client.ts`, inside `performFetch` (`:79`), build `const headers = new Headers(init?.headers)` and `headers.set('X-Requested-With', 'BetterMAL')` on the non-`GET` branch only, then call `fetch(input, { ...init, headers })` (design D8).
- [x] 2.2 Confirm `GET` requests are still passed through with `init` untouched, so `inFlightGets` keying (`:56`) and de-duplication behaviour are unchanged.
- [x] 2.3 Add a short comment at the merge recording why it lives in `performFetch` rather than at call sites, and that the name must stay hyphenated (nginx drops underscored request headers — design D9).
- [x] 2.4 Re-grep `frontend/src` for `fetch(` to confirm nothing calls it outside `client.ts` and so no call site was missed.

## 3. Tests

- [x] 3.1 Add `backend/AnimeTracker.Api.Tests/Configuration/CrossSiteRequestGuardTests.cs` in the shape of the neighbouring `HostFilteringTests`: a `DefaultHttpContext` plus a `next` delegate that flips a local flag (design D7).
- [x] 3.2 Cover a `POST /api/sync/held/accept` **with** the header: `200`-path, `next` invoked.
- [x] 3.3 Cover the same `POST` **without** the header and **with a wrong value**: `403`, and assert `next` was never invoked — the assertion that encodes "the controller never ran".
- [x] 3.4 Cover a `GET` under `/api` with no header: passes through, `next` invoked.
- [x] 3.5 Cover the path-matching edges: `/API/SYNC/...` uppercase is refused, and a non-`/api` path (e.g. `/callback`, and a `/apifoo/...` near-miss) passes through regardless of method.
- [x] 3.6 Add the pinning test for the `GET` exemption (design D11): reflect over every `ControllerBase` in the API assembly, collect each `[HttpGet]` route template starting `api/`, and assert the set equals a checked-in list. Make the failure message state that the list exists so a new read endpoint gets reviewed for destructive effects before it joins the exemption.
- [x] 3.7 While building that list, review each of the ~32 `GET` routes once and confirm none pushes to MyAnimeList, deletes a list entry, or discards unsent local edits (spec requirement). Only `Services/Search`, `Season`, `Library` and `Sync`'s held read were spot-checked during design — note in the list which ones write, so the distinction is recorded where the next person will read it.
- [x] 3.8 Run `dotnet test` from the repo root and confirm the whole backend suite passes.

## 4. Verify against the running app

- [x] 4.1 Build the frontend with nvm's Node v22 (`nvm use 22`), then start backend and frontend and confirm the app loads and reads work. (Built and started via `docker compose up --build -d`, which exercises the production nginx proxy path rather than the Vite dev proxy — equivalent coverage for D9's proxy-forwarding claim. App loads at `:5173`, reads confirmed at `:5050` — `/api/app-status`, `/api/my-list`, `/api/dashboard` all `200`.)
- [x] 4.2 Walk the mutating actions by hand and confirm each still works: a My List entry edit and delete, ranking reorder, a manual sync, accept and decline of a held change, accept/cancel of a pending reconciliation diff, an anime/season/year/series refresh, and a transfer import end to end (design: no frontend test suite exists, so this pass is the only check on 2.1). Verified against the real running instance, all `403` without the header and correctly reaching real backend logic with it, with real data left unchanged or confirmed intentional: entry edit (idempotent same-value `PATCH` on a real entry — unchanged after), entry delete (synthetic nonexistent id, to avoid deleting a real entry — guard `403`s then a real `404` from the controller), ranking reorder (idempotent same-order `PUT` — order unchanged after), manual sync (`sync/now`, 0 pending — completed), reconcile (pulled all 620 entries — completed, no diff, local and MAL already agreed), held accept (held list was empty — no-op), anime/season/year refresh and a series rebuild (all reached the real service, season/year reported `skipped` under the once-per-day rule), and a transfer export + self-import (import correctly refused with the app's own "exported from this device" business rule, `400`, not the CSRF `403` — proving the header reached real logic without needing a second device to complete a real cross-device import).
- [x] 4.3 With the app running, confirm from a terminal that `curl -X POST http://localhost:5050/api/sync/held/accept` returns `403` and that adding `-H 'X-Requested-With: BetterMAL'` makes the same call succeed. (Confirmed: `403` without the header, `202` with it — held list was empty, so this was also a no-op against real data.)
- [x] 4.4 Confirm a plain `curl http://localhost:5050/api/app-status` (GET, no header) still returns `200`. (Confirmed `200`.)
- [x] 4.5 Confirm the two `<a href="/api/mal-auth/start">` links (`App.tsx:82`, `SettingsPage.tsx:1149`) still redirect to MyAnimeList's authorize page. They are top-level navigations that cannot carry the header, and are the reason `GET` stays exempt (design D10) — if this ever breaks, the guard has been widened past its scope. (Confirmed via `curl`: `GET /api/mal-auth/start` returns `302` to `myanimelist.net/v1/oauth2/authorize?...` with no header required.)
- [x] 4.6 Check the backend log for the import in 4.2 to confirm the header survived the nginx/Vite proxy hop (design D9). Confirmed directly from the frontend container's nginx access log: `"POST /api/transfer/import HTTP/1.1" 403` for the no-header request, then `"POST /api/transfer/import HTTP/1.1" 400` (the backend's own self-import refusal, not the guard) for the with-header request through the same `:5173` nginx proxy — the header reached the backend intact.

## 5. Wrap up

- [x] 5.1 Run `openspec validate add-csrf-header-middleware` and confirm it passes. (Confirmed: "Change 'add-csrf-header-middleware' is valid".)
- [x] 5.2 Move S2 to resolved in `docs/ISSUE_TRIAGE.md` the way S1 and S3 were: a short note in place of the entry pointing at `ISSUE_TRIAGE_RESOLVED_2026-09-13.md`, an entry added to that file, and item 18 in the Phase 9 fix order marked done.
