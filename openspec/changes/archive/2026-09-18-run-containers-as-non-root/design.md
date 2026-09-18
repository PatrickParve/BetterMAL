## Context

**S4** from `docs/ISSUE_TRIAGE.md:86-92`, the last item in Phase 9 (`:264-266`) and the last open item in that doc. Source: SECURITY_REVIEW §8 and Recommended action 4.

Current state, verified at `56eeb6c`:

- `backend/AnimeTracker.Api/Dockerfile:8-13` — runtime stage with no `USER`, so `dotnet AnimeTracker.Api.dll` runs as root.
- `frontend/Dockerfile:8-10` — `FROM nginx:1.27-alpine` with no `USER`. The stock image's master process runs as root and drops only its workers to `nginx`.
- `frontend/nginx.conf:6,12` — two `server` blocks on port 80 (the 444 catch-all and the real block) from `2026-09-18-harden-host-filtering-and-framing`.
- `docker-compose.yml:42` — `"127.0.0.1:${FRONTEND_PORT:-5173}:80"`.

Constraints that shaped this:

- **Nothing is observably broken.** There is no known RCE and no port is published beyond `127.0.0.1`, so there is no failure to reproduce and no test that can demonstrate the benefit. The entire value is a smaller blast radius for a future bug, which makes "does the app still work identically" the only thing verification can actually check — and therefore the thing this design spends its effort on.
- **The frontend port move is the only real risk.** The image change forces a container-port change, which forces a Compose change. Those two must land together or the frontend is unreachable.
- **There is no container test harness.** Nothing in the repo builds or asserts against images; `AnimeTracker.Api.Tests` constructs services and middleware directly. Verification here is a documented manual procedure, the same call `2026-09-18-harden-host-filtering-and-framing` made for its nginx half (that change's D6).

## Goals / Non-Goals

**Goals:**

- No root process in either application container, master processes included.
- The application's own files unwritable by the process that serves them, so an RCE cannot be made persistent by patching the binaries or the SPA bundle.
- Byte-identical behaviour on both run paths, on the same host ports, including everything S1 and S3 added.
- Use each base image's built-in non-root user rather than inventing one.

**Non-Goals:**

- **S1**/**S3** (done, archived) and **S2** (CSRF header middleware, same phase, proposed separately).
- A broader container-hardening pass: `read_only: true` root filesystems, `cap_drop`, `no-new-privileges`, seccomp profiles. Each is a separate judgement call with its own failure modes, and none is what S4 describes.
- Postgres. `postgres:17-alpine` already runs as its own built-in non-root `postgres` user; its entrypoint needs root to `chown` the data volume on first run, so forcing a `user:` on it would break initialisation for no gain.
- Native Development. Not containerised, so out of reach of this change by construction.
- Anything to do with secrets, `.env`, or `ConnectionStrings`.

## Decisions

Every behavioural claim below was verified by building both images with the exact proposed edits and running them — the backend against a real `postgres:17-alpine`, the frontend with a real Vite `dist` and a real upstream behind its `/api/` proxy. Nothing here rests on documentation alone. Test containers and images were removed afterwards.

### D1. One ADDED requirement in `deployment/spec.md`

Nothing under `openspec/specs/` matches `USER`, `root`, `non-root`, `uid`, or `unprivileged` — confirmed by grep, so this is new spec territory. `deployment` already owns both Dockerfiles, the Compose stack, the nginx config, and the port rules, and it holds S1's and S3's requirements; a fourth exposure-hardening requirement belongs beside them.

`ADDED`, not `MODIFIED`: no existing requirement's behaviour changes. The frontend's container-internal port is not named by any of them — "One backend port, set by `BACKEND_PORT`" governs the backend only, and `FRONTEND_PORT` appears nowhere as a requirement — so there is nothing to restate, and `MODIFIED` with partial content would lose detail at archive time.

*Alternative considered:* a new `container-hardening` or `security` capability. Rejected for the same reason the S1/S3 change rejected it: these are statements about how the app is exposed on the host, which is what `deployment` describes.

### D2. Backend: `USER $APP_UID`, nothing else

Verified in `mcr.microsoft.com/dotnet/aspnet:10.0`:

- `APP_UID` is **an `ENV` in the base image**, not a build arg — value `1654`. The triage note calls it a build arg; that wording is wrong but the usage is identical, since `USER` expands environment variables either way. Worth recording only so nobody "fixes" it by adding an `ARG APP_UID` that would shadow the image's value with an empty one.
- It maps to a real account: `app:x:1654:1654::/home/app:/bin/sh`. No user to create.
- `ASPNETCORE_HTTP_PORTS` already defaults to `8080`, and Compose sets `ASPNETCORE_URLS: http://+:8080` — non-privileged, so binding needs no capability and no Compose change.

Then, with `USER $APP_UID` added after `COPY --from=build /app .` and the image built:

| Check | Result |
| --- | --- |
| Process identity | `uid=1654(app) gid=1654(app)` |
| `/app` contents after `COPY` | `root:root`, files `0644`, dirs `0755` |
| Files under `/app` unreadable by uid 1654 | **0** |
| `touch /app/pwned` | `Permission denied` |
| `$HOME` (`/home/app`) writable | yes |

That settles the first question the proposal was asked to check: **no `--chown` is needed.** `COPY` writes as uid 0 regardless of `USER`, but world-readable, which is all a read-only consumer needs — and `/app` being *unwritable* is the point of the change, not a problem to fix.

Placing `USER` after the `COPY` also matters: `apt-get` in the runtime stage (`:10-11`) must still run as root, and it does, since it precedes the `USER` line.

Nothing in the backend needs to write anywhere. Grepped across `backend/`, excluding `obj/`, `bin/`, and tests: no `File.Write*`, `File.Create`, `StreamWriter`, `Directory.Create`, `GetTempPath`, `GetTempFileName`, `WriteAllText`, or `WriteAllBytes`; and no `AddDataProtection`, `AddAuthentication`, `AddSession`, or `AddAntiforgery`, so the Data Protection key ring — the usual thing that surprises a newly non-root ASP.NET Core app — is never exercised. `Microsoft.AspNetCore.OpenApi`, `Microsoft.OpenApi`, and the EF Core/Npgsql packages are the whole dependency list; nothing there is a file sink. If Data Protection is ever introduced, `/home/app` is writable and is where it would look by default.

Confirmed live rather than reasoned about: the non-root container ran `db.Database.Migrate()` to completion against `postgres:17-alpine`, started its hosted services (EF command logging shows them querying `ReconciliationRunLogs`), stayed up with `restarts=0`, and answered `/api/my-list`, `/api/dashboard`, and `/api/season/bounds` with `200`. `Host: evil.example` still got `400` from S1's host filtering.

*Alternative considered:* `user: "1654"` in `docker-compose.yml` instead. Rejected — it puts the fact in the orchestration file rather than the image, so `docker run` of the image directly would still be root, and it hardcodes a uid that the base image owns.

### D3. Frontend: `nginxinc/nginx-unprivileged`, because the master is the problem

The stock `nginx:1.27-alpine` already runs its *workers* as `nginx`; its master stays root in order to bind port 80. So the finding is entirely about the master, and dropping it means giving up the privileged port. `nginxinc/nginx-unprivileged:1.27-alpine` is the upstream-maintained image for exactly that: same nginx 1.27 Alpine build, `USER 101` applied.

Verified in the image before adopting it — it needs **nothing beyond the port change**, which settles the proposal's second open question:

- `pid /tmp/nginx.pid;` is already set in its `/etc/nginx/nginx.conf`.
- All five temp paths are already redirected under `/tmp`: `proxy_temp_path`, `client_body_temp_path`, `fastcgi_temp_path`, `uwsgi_temp_path`, `scgi_temp_path`. `/tmp` is `drwxrwxrwt`.
- `/var/log/nginx/access.log` and `error.log` are symlinks to `/dev/stdout` and `/dev/stderr`, so logging needs no writable file.

Then, with the real image built (`nginx-unprivileged` base + real Vite `dist` + the 8080 config):

| Check | Result |
| --- | --- |
| `nginx -t` | syntax ok, test successful |
| Process identities | `nginx` for the master **and** all six workers — no root process |
| `Host: localhost` on `/` | `200`, `Cache-Control: no-cache`, `X-Frame-Options: DENY` |
| `Host: localhost` on `/assets/index-*.js` | `200`, `Cache-Control: public, max-age=31536000, immutable`, `X-Frame-Options: DENY` |
| `Host: localhost` on `/api/…` (real upstream) | `200`, `X-Frame-Options: DENY` inherited |
| `Host: 127.0.0.1` on `/` | identical to `localhost` |
| `Host: evil.example` on `/` and `/api/…` | `Empty reply from server` (the 444 block) |
| `dist` files unreadable by uid 101 | **0** |
| `touch /usr/share/nginx/html/pwned.js` | `Permission denied` |

So S1's Host filtering and S3's `X-Frame-Options` — including the non-obvious `add_header` repetition that change's D4 documented — all survive the port move untouched, and the SPA bundle becomes unwritable by the process serving it.

*Alternatives considered:* keeping the stock image and adding `USER nginx` by hand. Rejected — the stock `nginx.conf` writes its pid and temp files to root-owned paths, so it needs its own config rewritten *and* several `chown`s, reimplementing the unprivileged image badly. *Also considered:* keeping port 80 via `CAP_NET_BIND_SERVICE` or `net.ipv4.ip_unprivileged_port_start`. Rejected — granting a capability back to avoid a one-line port change is the wrong trade, and it keeps a privileged bind in the picture.

### D4. `listen 8080` in both `server` blocks, and no duplicate `default_server`

Both blocks move together: `listen 8080 default_server;` and `listen 8080;`. The catch-all must stay the default server for the port the real block listens on, or foreign hostnames would fall through to the real block and be served.

The collision worth checking, since the unprivileged image ships its own port-8080 config: its stock `/etc/nginx/conf.d/default.conf` **is the exact path** `frontend/Dockerfile:10` copies over, so our file replaces it and remains the only config in `conf.d`. Verified by listing the directory in the built image. A second port-8080 `default_server` would fail loudly (`duplicate default server`) rather than silently, and `nginx -t` catches it before the image ships.

The comment at `nginx.conf:2-3` says "the default server for port 80" and has to move to 8080 with the directives — it exists to stop someone reintroducing the fall-through, so a stale port number in it is worse than no comment.

### D5. Accept the IPv6 entrypoint warning rather than work around it

New, and the only visible change in behaviour: every frontend start now logs

```
10-listen-on-ipv6-by-default.sh: info: can not modify /etc/nginx/conf.d/default.conf (read-only file system?)
```

That script tries to append `listen [::]:8080;` to the config. It cannot, because the file is root-owned `0644` while the entrypoint runs as uid 101. Harmless: Compose publishes on `127.0.0.1` and Docker forwards over IPv4, so nothing needs an IPv6 listener; and the message is `info`, with `nginx -t` passing and the server starting normally in the same run.

Not worked around, because every option is worse than a log line: `--chown=101` on the `COPY` would hand the nginx user write access to its own config, which is precisely the persistence this change is removing; deleting the script edits a file the base image owns; adding the IPv6 `listen` ourselves adds an unused listener. Recorded in `tasks.md` as expected output so it is not mistaken for a failure.

### D6. Deny the running container its own config directory

The unprivileged image ships `/etc/nginx/conf.d` owned by uid 101 — the user nginx runs as — while the file installed into it is root-owned. So the running container cannot *edit* its config, but it can still *add* to it: the base `nginx.conf` includes `/etc/nginx/conf.d/*.conf` by wildcard, so a second file dropped there is loaded, and a process can signal the master it shares a uid with to reload. That is enough to switch off S1's hostname filtering or S3's framing header from inside the container — precisely the protections the previous change added.

Closed at build time, in `frontend/Dockerfile`:

```dockerfile
USER root
RUN chown root:root /etc/nginx/conf.d && chmod 755 /etc/nginx/conf.d
USER 101
```

`USER root` applies only to the `RUN` between the two lines; the stage ends back on 101, so nothing runs as root at runtime and the non-root goal is intact.

Verified on the built image:

| Check | Result |
| --- | --- |
| Container state | `running`, `restarts=0` |
| `Host: localhost` on `/api/…` through the proxy | `200`, `X-Frame-Options: DENY` |
| `Host: evil.example` | `Empty reply from server` |
| `touch /etc/nginx/conf.d/evil.conf` as uid 101 | `Permission denied` |
| New warnings or errors in the startup log | none — the same single D5 `info` line and nothing else |

This goes beyond the triage doc's fix direction, which said only "use `nginx-unprivileged`". It is in scope because it is the same idea as `/app` being unwritable — deny a process the ability to rewrite the rules it runs under — and because the gap it closes is specifically the one that would undo S1 and S3.

*Checked before adopting, since the image's own entrypoint scripts write into `conf.d`:* `10-listen-on-ipv6-by-default.sh` already failed harmlessly before this (D5) and is unaffected. `20-envsubst-on-templates.sh` guards on `[ -d "$template_dir" ] || return 0` at its line 41, and `/etc/nginx/templates` does not exist in the image, so it returns before touching `conf.d` — confirmed by inspecting both the script and the filesystem, not assumed.

*Forward-looking trade-off worth recording:* that same script's next line is `[ ! -w "$output_dir" ]` → `ERROR: $template_dir exists, but $output_dir is not writable`. So if anyone later adopts nginx's envsubst templating by adding `/etc/nginx/templates`, a root-owned `conf.d` will break it — loudly, with that explicit message, not silently. Nothing uses templating today, and the fix would be to render templates elsewhere or revert this chown.

*Alternative considered:* `--chown=101` on the `COPY` instead, to make the entrypoint's IPv6 edit succeed. Rejected for the opposite reason — it hands the nginx user write access to its own config, which is the persistence this change exists to remove.

### D7. Verified by a documented manual procedure, not a new test suite

Standing by the S1/S3 precedent. Automating this needs a Docker dependency in the build and a container fixture, to assert on a four-line change whose whole point is that nothing observable changes. `tasks.md` carries the explicit commands instead, including the `--add-host backend:127.0.0.1` trick that change recorded for standalone `nginx -t` (`proxy_pass` upstreams resolve at parse time).

## Risks / Trade-offs

- **The image changes but the Compose mapping does not (or the reverse), and the frontend is unreachable.** → This is the one genuine failure mode. Both edits are in one commit, and `tasks.md` gates completion on loading the app in a browser rather than on the containers merely being healthy — an unreachable frontend is silent from `docker compose ps`.
- **Someone reads "8080" as the host port.** → `FRONTEND_PORT` and its 5173 default are unchanged; only the right-hand side of the mapping moves. The requirement text states this explicitly because it is the likeliest misreading of the diff.
- **The mitigation is strong but not total, and could be over-trusted.** → After D6, neither container can write its application files, the SPA bundle, its nginx config, or the config directory. What remains: `/tmp` is world-writable (nginx needs it for its temp paths) and `$HOME` is writable for the backend user, so code could still run from a writable path in memory or on disk for the container's lifetime — it just cannot survive a restart by patching what the image ships. Stated plainly in the proposal so the ceiling is understood: much smaller, not zero. Closing the rest is the `read_only: true` / `cap_drop` pass listed under Non-Goals.
- **A future backend change needs to write to disk and fails as uid 1654.** → It would fail loudly at the write, not corrupt anything. `/home/app` and `/tmp` are writable; a writable app-owned path would need a volume. Recorded here because the grep that establishes "nothing writes to disk" is true today, not forever.
- **A future `USER`-before-`COPY` reordering breaks the build.** → `apt-get` at `:10-11` needs root. The `USER` line goes last in the stage, immediately above `ENTRYPOINT`, which is both correct and the conventional position.
- **`nginxinc/nginx-unprivileged` is a third-party namespace, not Docker's official `nginx`.** → It is maintained by nginx upstream (the same org behind the official image's content), tracks the same versions, and is the standard answer for this. It is pinned to `1.27-alpine`, matching the tag already in use, so the nginx version does not move with this change.
- **The `1.27-alpine` tag floats to new patch releases.** → Exactly as true of `nginx:1.27-alpine` today; this change neither improves nor worsens it, and pinning digests across all four images is a separate decision.

## Migration Plan

No data migration, no schema change, no API change, no new project dependency. Deploy is `docker compose up --build` — both images must be rebuilt, and the new frontend mapping only makes sense against the new image, so the two land in one commit. Rollback is reverting that commit and rebuilding; nothing persists state, and the Postgres volume is untouched throughout.

## Open Questions

None. Both questions the proposal flagged for verification were settled empirically before this document was written: the non-root backend user reads everything under `/app` with no `--chown` (D2), and `nginxinc/nginx-unprivileged:1.27-alpine` needs no adjustment beyond the port change to *run* correctly, since its pid, temp paths, and log sinks are already non-root-safe (D3).

Verification did surface one thing neither question asked about — that the image leaves `/etc/nginx/conf.d` writable by the user nginx runs as, which would let code inside the container append config and undo S1 and S3. That was initially recorded as an accepted limitation, then closed by D6 after confirming the three lines involved break neither the entrypoint nor any behaviour. The scope decision was the user's; the evidence for it is in D6.
