## Why

Nothing in the request pipeline distinguishes a click in the BetterMAL UI from a POST issued by a page on another origin. `Program.cs:219-229` goes straight from `builder.Build()` to `app.UseAuthorization()` → `app.MapControllers()` with no antiforgery or origin check, and the mutating POSTs bind nothing from the request body — which makes them CORS "simple requests" that need no preflight. A hostile page open in another tab can therefore run

```html
<form action="http://localhost:5050/api/sync/held/accept" method="POST"></form>
<script>document.forms[0].submit()</script>
```

and the browser sends it. CORS never enters into it: the attacker never reads the response, only triggers the side effect. That reaches `AcceptAllHeldAsync` and pushes held removals to MAL. The same shape declines unsent local edits (`/api/sync/held/decline`), accepts a pending reconciliation diff, imports a crafted transfer file, or burns MAL's daily call cap through the refresh-all endpoints. This is **S2** in `docs/ISSUE_TRIAGE.md:69-84`, and the last open item in that doc's Phase 9 apart from S4.

## What Changes

- **Backend — a guard middleware.** A new `CrossSiteRequestGuard` in `backend/AnimeTracker.Api/Services/Infrastructure/`, registered in `Program.cs` between the Development/OpenAPI block and `app.UseAuthorization()` (`:227`). Any request whose path is under `/api` and whose method is not `GET` is answered `403 Forbidden` and short-circuited unless it carries `X-Requested-With: BetterMAL`. A browser will not attach a custom header to a cross-site request without a preflight, and a preflight cannot succeed here, since no `Access-Control-Allow-Origin` is ever sent.
- **Frontend — send the header once, centrally.** `frontend/src/api/client.ts` adds that header to every non-`GET` request in `performFetch` (`:79`), the single point every call passes through. It is *merged* into `init.headers`, never overwriting, so `importData` (`:577`) keeps its own `Content-Type: application/json`. No call site changes: `fetchJson`, `fetchVoid`, and the raw `fetchRaw` callers that branch on status (`:476`, `:483`, `:511`, `:517`) all inherit it. Nothing outside `client.ts` calls `fetch` — confirmed by grep across `frontend/src`.
- **GET is untouched** — but not on the usual grounds. Covering every `/api` request was considered and ruled out on evidence: `App.tsx:82` and `SettingsPage.tsx:1149` reach `GET /api/mal-auth/start` through a plain `<a href>`, a top-level navigation that cannot carry a custom header, so a blanket rule would break connecting to MyAnimeList. The exemption instead rests on nothing `GET`-reachable pushing to MAL or deleting list data — which this change turns from an audit result into a spec requirement with a test behind it, since `GET /api/sync/held` already writes on read and shows the codebase drifts that way.
- Verified at `0cd2d77`: the endpoints this closes are `SyncController.cs:20,39,64,73,96,110,124,142`, `MetadataRefreshController.cs:16`, `SeasonController.cs:45`, `YearController.cs:43`, `AiringController.cs:31`, `SeriesController.cs:45,134,154`, `AnimeDetailController.cs:68`, and `TransferController.cs:30` — the last of which reads the raw body with no content-type check, so a `text/plain` cross-site POST reaches it too.
- **Not a behaviour change for anything real.** `OPTIONS` and `HEAD` requests to `/api` paths now get `403` instead of the `405` they get today — measured, not assumed, by driving both through a stand-in app. Nothing maps either method and there is no CORS middleware to intercept them, so this is the same outcome under a different status code: a cross-origin preflight was never going to succeed here, since no `Access-Control-Allow-Origin` is ever sent.

## Capabilities

### New Capabilities

None. This is one new requirement on an existing capability.

### Modified Capabilities

- `deployment`: gains a requirement that mutating API requests must be provably same-origin — the backend refuses non-`GET` `/api` requests without a fixed custom header, and the web client sends it on every such request. Confirmed absent from `openspec/specs/`: nothing matches `CSRF`, `antiforgery`, `X-Requested-With`, `preflight`, or `cross-site`. `deployment` is where S1's host filtering and S3's `X-Frame-Options` requirements already live (archived change `2026-09-18-harden-host-filtering-and-framing`), and this closes the third hole in the same browser-attack surface, so it belongs beside them rather than in a new capability.

## Impact

- `backend/AnimeTracker.Api/Services/Infrastructure/CrossSiteRequestGuard.cs` — new, small; a static guard with a `RequestDelegate`-shaped entry point so a test can drive it directly, the way `HostFilteringTests` drives the real host-filtering middleware.
- `backend/AnimeTracker.Api/Program.cs` — one registration line plus a comment, in the pipeline block at `:219-229`.
- `frontend/src/api/client.ts` — a header merge inside `performFetch` (`:79-92`). No other frontend file changes.
- `backend/AnimeTracker.Api.Tests/Configuration/` — new tests: the header present passes through, absent or wrong is `403` with the next delegate never invoked, `GET` passes with no header, and non-`/api` paths are untouched. Plus a pinning test over the `[HttpGet]` routes under `/api`, so a new read endpoint cannot join the exemption without review. No new package: the existing suite constructs middleware against `DefaultHttpContext`.
- No database, API surface, DTO, or dependency changes. No CORS policy, no antiforgery token, no cookie or session state — the fix needs no server-side state and nothing to issue or rotate.
- Risk sits entirely in the frontend half: if any mutating call were to miss the header, it would fail with `403` rather than fail silently. That risk is bounded by making `performFetch` the only place it is added, and by a manual pass over every mutating action (My List edits, sync, held accept/decline, transfer import) before archive — there is no frontend test suite in this repo to catch it otherwise.
- Residual, and deliberately left open: a cross-site `GET` can still burn MyAnimeList quota and self-clear agreeing held changes through cache-filling reads. Non-destructive, and its response stays unreadable. Closing it properly means Fetch Metadata (`Sec-Fetch-Site`) rather than this header, since that survives the `/api/mal-auth/start` navigation — a separate proposal, recorded in design D10/D11.
- Out of scope: **S1**/**S3** (done, archived), **S4** (non-root containers, same phase, not yet proposed), PATCH/PUT/DELETE-specific work and any CORS policy (both already effectively protected by the absence of `Access-Control-Allow-Origin`), and token- or cookie-based CSRF defences.
