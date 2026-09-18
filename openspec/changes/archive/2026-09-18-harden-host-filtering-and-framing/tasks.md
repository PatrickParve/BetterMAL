## 1. Backend host filtering (S1)

- [x] 1.1 In `backend/AnimeTracker.Api/appsettings.json:8`, change `"AllowedHosts": "*"` to `"AllowedHosts": "localhost;127.0.0.1"`. No `Program.cs` change — `WebApplication.CreateBuilder` already wires `HostFilteringMiddleware` off this key. Leave `appsettings.Development.json` alone; it sets no `AllowedHosts`, so this one edit covers both run paths.

## 2. Backend regression test (S1)

- [x] 2.1 Add `backend/AnimeTracker.Api.Tests/Configuration/HostFilteringTests.cs`. Load the shipped `appsettings.json` from `AppContext.BaseDirectory` with `ConfigurationBuilder`, assert the file exists first (design D5 — it is copied to the test output, so the test reads the real file), then bind `AllowedHosts` into `HostFilteringOptions` by splitting on `;`.
- [x] 2.2 In that test, drive a real `Microsoft.AspNetCore.HostFiltering.HostFilteringMiddleware` over a `DefaultHttpContext`. **Its third constructor argument is `IOptionsMonitor<HostFilteringOptions>`, not `IOptions<>`** — `Options.Create(...)` will not compile, so add a small file-scoped `IOptionsMonitor` test double. No new NuGet package: the ASP.NET Core framework reference flows through the existing `ProjectReference`.
- [x] 2.3 Cover both directions: `Host: evil.example` and `attacker.test:5050` give `400` with the next delegate never invoked (nothing reaches a controller); `localhost`, `localhost:5050`, and `127.0.0.1:5050` all pass through. Confirm the test fails against `"*"` before task 1.1 and passes after — if it passes both ways it is testing nothing.
- [x] 2.4 Run `dotnet test backend/AnimeTracker.Api.Tests/AnimeTracker.Api.Tests.csproj` and confirm the full suite is green, not just the new test.

## 3. nginx host filtering and framing (S2 file, one pass)

- [x] 3.1 In `frontend/nginx.conf`, add a catch-all first: `server { listen 80 default_server; server_name _; return 444; }`. Keep it above the real block for readability.
- [x] 3.2 Narrow the existing block to `server_name localhost 127.0.0.1;` and drop `listen 80 default_server` from it (`listen 80;` only — the catch-all is the default server). Leave `root`, `index`, and all three `location` blocks otherwise unchanged.
- [x] 3.3 Add `add_header X-Frame-Options DENY always;` at the `server` level of the real block.
- [x] 3.4 Also add `add_header X-Frame-Options DENY always;` **inside** `location /assets/` and `location /`, alongside each one's existing `Cache-Control`. This is not redundant: a location that declares any `add_header` drops every inherited one, so without this the header never reaches either (design D4, verified). Do **not** add it to `location /api/`, which declares no `add_header` and inherits correctly.
- [x] 3.5 Add a brief comment above the repeated directives recording *why* they are repeated, so a future edit does not "tidy" them away (design D4 risk).

## 4. Verify nginx behaviour

- [x] 4.1 Syntax-check the file. `nginx -t` resolves `proxy_pass` upstreams at parse time, so standalone validation needs the `backend` name to resolve:
  `docker run --rm --add-host backend:127.0.0.1 -v "$PWD/frontend/nginx.conf:/etc/nginx/conf.d/default.conf:ro" nginx:1.27-alpine nginx -t`
  Expect "syntax is ok" / "test is successful". A `duplicate default server` error here means another port-80 config exists (design risk).
- [x] 4.2 With the stack up (`docker compose up --build`, frontend on `FRONTEND_PORT`, default 5173), confirm `curl -sS -H "Host: evil.example" http://127.0.0.1:5173/` closes the connection ("Empty reply from server") and that the same holds for `.../api/...` — neither proxied nor served.
- [x] 4.3 Confirm `curl -sD- -o /dev/null -H "Host: localhost" http://127.0.0.1:5173/` returns 200 with **both** `X-Frame-Options: DENY` and `Cache-Control: no-cache`.
- [x] 4.4 Confirm a file under `/assets/` returns **both** `X-Frame-Options: DENY` and `Cache-Control: public, max-age=31536000, immutable`.
- [x] 4.5 Confirm a real `/api/` response carries the inherited `X-Frame-Options: DENY`, and that `Host: 127.0.0.1` works the same as `Host: localhost` throughout.

## 5. Verify both run paths still work end to end

- [x] 5.1 Docker Compose: `docker compose up --build`, then load the app in a browser at `http://localhost:{FRONTEND_PORT}` and confirm it renders and its `/api/` calls succeed — this exercises nginx→backend proxying, where `Host: $host` is port-stripped to `localhost` and must pass the new backend filter.
- [x] 5.2 Native Development: run the backend natively and the Vite dev server, confirm `http://localhost:{BACKEND_PORT}` serves and the dev proxy (`changeOrigin: true`) still reaches `/api`. Sanity-check that `curl -H "Host: evil.example" http://127.0.0.1:{BACKEND_PORT}/api/...` now returns `400`.
- [x] 5.3 Confirm the OAuth redirect `http://localhost:{BACKEND_PORT}/callback` is unaffected (hostname is `localhost`; `AllowedHosts` ignores the port).

## 6. Land it

- [x] 6.1 Update `docs/ISSUE_TRIAGE.md`: mark S1 (`:61-67`) and S3 (`:86-91`) done, and mark Phase 9 item 17 (`:265-268`) done, following the existing convention used by the resolved entries (e.g. B5 at `:56`, PF6 at `:259`) — including the pointer to this change name and date.

