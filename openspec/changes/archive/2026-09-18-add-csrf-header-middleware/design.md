## Context

**S2** from `docs/ISSUE_TRIAGE.md:69-84` — the last open item in that doc's Phase 9 apart from S4, and the third and final hole in the same browser-attack surface S1 and S3 closed (archived change `2026-09-18-harden-host-filtering-and-framing`).

Current state, re-verified at `0cd2d77`:

- `Program.cs:219-229` — `var app = builder.Build();`, a Development-only `app.MapOpenApi();`, then `app.UseAuthorization();` and `app.MapControllers();`. Nothing between them. No `AddAntiforgery`, no `UseAntiforgery`, no `AddCors`/`UseCors` anywhere in the file.
- The mutating POSTs bind nothing from the body, so nothing about them forces a preflight: `SyncController.cs:20,39,64,73,96,110,124,142`, `MetadataRefreshController.cs:16`, `SeasonController.cs:45`, `YearController.cs:43`, `AiringController.cs:31`, `SeriesController.cs:45,134,154`, `AnimeDetailController.cs:68`, `TransferController.cs:30`. The last reads the raw body through a `StreamReader` with no content-type check, so the `text/plain` a cross-site form can send reaches it intact.
- `/callback` (`MalAuthController.cs:33`) is the only route outside `/api`, and it is a `GET`.
- `frontend/src/api/client.ts` — `performFetch` (`:79`) is the only place in the whole frontend that calls `fetch`; `fetchRaw` (`:58`) routes both its branches through it, and `fetchJson` (`:119`), `fetchVoid` (`:125`) and four raw callers that branch on status (`:476`, `:483`, `:511`, `:517`) sit above that. Confirmed by grep: no `fetch(` anywhere else under `frontend/src`.

Constraints that shaped the decisions below:

- **No HTTP-level test harness exists.** `AnimeTracker.Api.Tests` references neither `Microsoft.AspNetCore.Mvc.Testing` nor `Microsoft.AspNetCore.TestHost`; its tests construct controllers, services and middleware directly. A `WebApplicationFactory` is not cheap here — `Program.cs:234` runs `db.Database.Migrate()` at startup and 13 hosted services are registered, so it would need a live Postgres.
- **No frontend test suite exists at all.** `frontend/package.json` has no `test` script and no vitest/jsdom dependency. Nothing automated can catch a mutating call that loses the header, which is what drives D8 and the manual pass in `tasks.md`.
- The backend and frontend ship together (one Compose build, one commit), so the two halves of this change cannot get out of step in a deployed stack.

Every behavioural claim below was measured against a stand-in ASP.NET Core 10 app running the exact proposed guard, not inferred from documentation.

## Goals / Non-Goals

**Goals:**

- Refuse any non-`GET` `/api` request that cannot prove it came from the BetterMAL UI, before it reaches a controller or a service.
- Add the proof to every mutating frontend call at one choke point, with no call site touched and no existing header displaced.
- Leave `GET` exactly as it is today — see D10 for why that is a scope limit rather than a safety claim.
- Leave a regression test behind that fails against the current pipeline, in the shape the existing test project already supports.

**Non-Goals:**

- **S4** (non-root containers) — same phase, proposed separately. **S1**/**S3** — done and archived.
- Antiforgery tokens, cookies, or any session-based CSRF defence. The triage doc's fix direction is the custom-header check specifically, because it needs no server-side state and nothing to issue or rotate.
- A CORS policy. Adding one could only *loosen* what is currently refused by default.
- Changes to `PATCH`/`PUT`/`DELETE` routes as such — they are covered by the same rule as every other non-`GET` method, and needed no separate treatment to begin with, since a non-simple method already requires a preflight that cannot succeed here.
- Protection against a non-browser client. `curl` can set any header it likes. This closes CSRF — a browser being used as a confused deputy — and nothing else.

## Decisions

### D1. The requirement goes in `deployment/spec.md` as ADDED

Nothing under `openspec/specs/` matches `CSRF`, `antiforgery`, `X-Requested-With`, `preflight`, or `cross-site` — confirmed by grep, so this is new spec territory rather than a wording change. `deployment` already holds S1's "Only loopback hostnames are served" and S3's "The frontend refuses to be framed", which are the same kind of rule about how the app is exposed to a browser. `ADDED`, not `MODIFIED`: no existing requirement changes behaviour, and `MODIFIED` with partial content loses detail at archive time.

*Alternative considered:* split it — the client half into `frontend-data-loading`, the backend half into `deployment`. Rejected: one mechanism whose two halves are useless apart, and `frontend-data-loading`'s Purpose is explicitly about *read* discipline (de-duplication, per-load reads, mutation responses), which this is not.

*Alternative considered:* a new `security` capability. Rejected for the same reason the S1/S3 design rejected it — it would hold one requirement that `deployment` already has neighbours for.

### D2. A fixed custom header, not a token, a cookie, or an origin check

The guard needs to distinguish "the BetterMAL UI issued this" from "some other page caused the browser to issue this". A custom request header does it with no state anywhere:

- A cross-site `fetch` that sets a custom header stops being a CORS simple request, so the browser sends a preflight first. The preflight gets no `Access-Control-Allow-Origin` back — there is no CORS middleware in the pipeline — so the browser never sends the real request.
- A cross-site `<form>` submit cannot set headers at all, so it can never carry the value.
- A `no-cors` fetch cannot set a custom header either; that mode allows only the CORS-safelisted ones.

So the header is present only on a request the app itself made. An antiforgery token would need issuance, storage and rotation for an app that has no sessions and no login; an `Origin`/`Referer` check would have to enumerate the hostname-and-port combinations `BACKEND_PORT` allows, and `Origin` is absent on some same-origin requests. Both are more moving parts for the same outcome.

The value is a fixed string, `X-Requested-With: BetterMAL`. It is not a secret and does not need to be — its security comes from the browser refusing to attach it cross-site, not from being unguessable.

### D3. A named guard in `Services/Infrastructure/`, not an inline lambda in `Program.cs`

`Services/Infrastructure/` is where cross-cutting helpers that `Program.cs` calls already live (`DevDotEnvOverlay`, `RefreshGate`). A `static class CrossSiteRequestGuard` with `public const string HeaderName`/`HeaderValue` and a `Task Invoke(HttpContext, RequestDelegate)` registers as `app.Use(CrossSiteRequestGuard.Invoke);` — verified to compile against that `IApplicationBuilder.Use` overload — and a test can call `Invoke` directly with a `DefaultHttpContext`, which an inline lambda could not.

### D4. Registered immediately before `UseAuthorization()`, and that is early enough

The guard goes after the Development/OpenAPI block and directly above `app.UseAuthorization()` (`:227`), which reads as the first thing in the pipeline proper.

The non-obvious part: `WebApplication` inserts its automatic `UseRouting` *before* user middleware, so by the time the guard runs, an endpoint has already been **selected**. That does not matter, because endpoints are **executed** by the automatic `UseEndpoints` at the very end. Measured, not assumed: with the guard short-circuiting a `POST`, a flag set inside the controller action stayed `false`, and flipped to `true` only once the header was supplied. So "never reaches the controller/service" holds literally.

The host-filtering middleware from S1 is installed ahead of all user middleware by `CreateBuilder`'s startup filter, so a foreign `Host` is still refused first. The two guards do not interact.

### D5. Matched on `/api` by segment, and on the method by `HttpMethods.IsGet`

`request.Path.StartsWithSegments("/api")` and `!HttpMethods.IsGet(request.Method)`. Both measured against the stand-in app:

- `/apifoo/notapi` is **not** matched — `StartsWithSegments` compares whole segments, so a path that merely starts with the same characters is not caught.
- `/API/PROBE/ACCEPT` **is** matched. This matters: `StartsWithSegments` defaults to `OrdinalIgnoreCase`, and ASP.NET routing is case-insensitive too, so a guard that compared case-sensitively would leave an uppercase bypass.

Scoping to `/api` rather than every path keeps `/callback` — the only non-`/api` route, and a `GET` — plainly out of the way, and means adding a non-`GET` route outside `/api` later is a deliberate act rather than a silent exemption.

### D6. Refusals are `403` with the app's own `{ "error": ... }` body

`403 Forbidden` is the status the triage doc names. The body follows the `{ "error": ... }` convention that `readErrorReason` (`client.ts:108`) already parses into `ApiError.reason`, so the one realistic way a *legitimate* request gets refused — a tab still running a pre-upgrade bundle — surfaces a sentence the UI can show instead of a bare `"… responded with 403"`. The message says only that the request was refused; there is nothing useful to disclose to an attacker, and the attacker cannot read the response anyway.

### D7. Tests drive the guard directly, mirroring `HostFilteringTests`

`AnimeTracker.Api.Tests/Configuration/CrossSiteRequestGuardTests.cs`, built like the S1 suite next to it: construct a `DefaultHttpContext`, set method/path/headers, call `CrossSiteRequestGuard.Invoke` with a `next` that flips a local flag, then assert on `context.Response.StatusCode` *and* on the flag. Asserting the flag is the point — it is what encodes "the controller never ran", and a status-code-only assertion would still pass if the guard set 403 and then called through. No new package, and the suite fails today because the guard does not exist.

### D8. The frontend header is merged in `performFetch`, using `new Headers(...)`

`performFetch` (`:79`), not `fetchRaw` (`:58`), because `performFetch` is the single line in the frontend that actually calls `fetch` — `fetchRaw`'s GET branch returns clones of a promise `performFetch` created. Adding it lower is impossible to bypass by construction, which is what D8 is buying given that no frontend test can check it.

The merge is `const headers = new Headers(init?.headers); headers.set(...)`, then `fetch(input, { ...init, headers })`. `new Headers` normalizes all three shapes `RequestInit.headers` accepts (a `Headers`, a `Record<string, string>`, an array of pairs), so `importData`'s `Content-Type: application/json` (`:581`) and the several other JSON callers keep theirs. It runs only on the non-`GET` branch, so `GET` requests go out byte-for-byte as they do today and the `inFlightGets` keying by URL (`:56`) is untouched.

`markUpdatesSeen` (`:342`) sends with `keepalive: true` from a `pagehide` handler; keepalive fetches carry custom headers normally — the keepalive restriction is on body size, not headers.

### D9. Both proxies forward the header unchanged, with one trap avoided

nginx `proxy_pass` (`frontend/nginx.conf:24`) forwards client request headers by default, and Vite's dev proxy (`vite.config.ts:16-19`) does too. The trap: nginx silently drops request headers containing **underscores** unless `underscores_in_headers on`. `X-Requested-With` is hyphenated, so it passes — but this is exactly why the name must not be "improved" into `X_Requested_With` later.

### D10. `GET` stays exempt — a blanket rule is not safe here

The usual justification for exempting reads ("`GET` is read-only") does not hold in this codebase, so it is worth stating what the exemption actually rests on.

**The premise is false.** `GET /api/sync/held` (`SyncController.cs:85`) calls `HeldChangeService.GetHeldAsync`, which clears `PendingSync`/`HeldForReviewAt` on every held change MyAnimeList already agrees with and commits at `HeldChangeService.cs:107` — a documented side effect (that change's own D8a), preceded by one outbound MAL read per held item. `Services/Search`, `Services/Season` and `Services/Library` each also contain a `SaveChangesAsync` reachable from a `GET`.

**So extending the guard to every `/api` request was considered seriously**, and rejected on evidence rather than cost. The cost is genuinely low: every frontend read goes through `performFetch`, including the two file downloads, which fetch a blob via `fetchDownload` (`client.ts:549`) rather than navigating an anchor; and a constant header would not disturb `inFlightGets` (`client.ts:56`), which keys on URL alone.

**What rules it out is that two `GET`s under `/api` are reached by top-level browser navigation, not by `fetch`:** `App.tsx:82` and `SettingsPage.tsx:1149` both render `<a href="/api/mal-auth/start">`, and that route (`MalAuthController.cs:29`) redirects the browser to MyAnimeList's authorize page. An anchor navigation cannot carry a custom header, so a blanket rule would break connecting to MyAnimeList — the app's primary onboarding action — and would need a path allowlist to work at all. That allowlist is the same rot-prone carve-out the blanket rule was meant to remove, so it buys no durability, and getting it wrong breaks the login flow.

**The exemption therefore stands on two things that are actually true:**

- Nothing reachable by `GET` pushes a change to MyAnimeList or deletes list data. Every endpoint in S2's damage list — accept/decline held, accept a reconciliation diff, import a transfer file — is non-`GET`, so the destructive half of the finding is closed completely.
- A cross-site `GET` still cannot read its response; CORS blocks that independently of this change.

What remains open is the weakest item on S2's damage list: a hostile page can burn MyAnimeList quota, and silently self-clear agreeing held changes, through cache-filling `GET`s. Non-destructive and unreadable, but real — recorded in Risks, and in the spec as an explicit limit on what the requirement covers.

### D11. The invariant the exemption depends on gets a test, not an audit

The first bullet above is an *audit result*, not an enforced property: it is true at `0cd2d77` because it was checked, and nothing would notice the day a `GET` acquires a destructive effect or a new destructive `GET` route is added. Since `GET /api/sync/held` shows the codebase already drifts toward writing reads, leaving that as a one-time check is the part of this change most likely to age badly.

So the audit becomes a pinning test — `CrossSiteRequestGuardTests` gains a case that reflects over every `ControllerBase` in the API assembly, collects each `[HttpGet]` route template under `api/`, and asserts the set equals a checked-in list.

It cannot decide by reflection whether an action is destructive — nothing can. What it does is force the question at the only moment anyone can answer it: adding or renaming a `GET` under `/api` fails the test until someone updates the list, and the failure message says why the list exists. That converts "we checked once" into "it stays checked", which is the actual future-proofing available here.

*Alternative considered:* asserting on `Sec-Fetch-Site: cross-site` instead, which browsers send automatically and page script cannot forge, and which would distinguish the app's own navigation to `/api/mal-auth/start` from a hostile one. It is a stronger signal and would allow covering `GET` properly. Rejected for this change: it is a different defence mechanism from the one the triage doc specifies, and it belongs in its own proposal rather than smuggled in beside this one. Noted in Open Questions as the natural next step if the residual `GET` exposure is ever worth closing.


## Risks / Trade-offs

- **A mutating call that misses the header fails with `403` instead of working, and no test can catch it** → the header is added at the one place every request funnels through (D8), so missing it would require bypassing `performFetch` entirely, which nothing in `frontend/src` does. Backed by a manual pass over every mutating action before archive (`tasks.md`), since there is no frontend test suite.
- **A browser tab left open across the upgrade keeps running the old bundle and gets `403` on its next mutation** → real but self-clearing: `nginx.conf:44` serves `index.html` with `Cache-Control: no-cache`, so a reload always revalidates and picks up the new content-hashed bundle. D6's error body means the stale tab shows a sentence rather than a bare status. Not worth a compatibility window that would leave the hole open on purpose.
- **`OPTIONS` and `HEAD` to `/api` change from `405` to `403`** (measured on the stand-in app, both before and after) → no route maps either method and nothing in the app or its containers issues them; it is a refusal either way.
- **The header is not a secret, so anything that is not a browser can set it** → intended. This closes CSRF only; a non-browser client on the loopback interface was never constrained by it, and S1's host filtering plus Compose's `127.0.0.1`-only publishing are what keep the API off the network.
- **Cross-site `GET`s can still cause writes and outbound MyAnimeList calls** → the residual part of S2 this change does not close (D10). Bounded: no `GET` pushes to MyAnimeList or deletes an entry, and the response stays unreadable cross-site. Left as an audit task (5.x) rather than widened into this change.
- **Same-origin XSS would defeat it**, since injected script can set any header the app can → out of scope, and not made worse by this change.
- **A future non-`GET` route outside `/api` would be unguarded** → deliberate (D5). The only route outside `/api` today is the `GET /callback` OAuth redirect.

## Migration Plan

Not a migration — no data, no config, no API surface changes. Both halves ship in the same commit and the same Compose build, so a deployed stack never runs one without the other. Rollback is reverting that commit; nothing persists that would need undoing.

## Open Questions

None blocking. Two things to record for later:

- The header name and value exist as a C# constant on one side and a TypeScript literal on the other, with nothing tying them together mechanically — renaming either means changing both, and the tests in D7 assert the backend's half only.
- If the residual cross-site `GET` exposure (D10) is ever worth closing, the route to it is Fetch Metadata (`Sec-Fetch-Site`), not this header — it is forgery-proof and, unlike a custom header, survives the top-level navigation to `/api/mal-auth/start`. Separately, `GET /api/sync/held` writing on read is a violation of `GET`'s safety semantics in its own right; fixing that would shrink the exposure without any new mechanism, and D11's test keeps it visible.
