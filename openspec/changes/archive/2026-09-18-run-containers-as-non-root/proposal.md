## Why

Both application containers run their processes as root. There is no `USER` in `backend/AnimeTracker.Api/Dockerfile:8-13` or `frontend/Dockerfile:8-10`, and the stock `nginx:1.27-alpine` runs its master process as root. That is not a vulnerability by itself — it is an amplifier. If a bug ever lets an attacker execute code inside either container (a deserialization or path-traversal flaw in a dependency, say), root lets that code overwrite the app's own compiled binaries or the served SPA bundle, turning a transient foothold into a persistent one. Non-root does not; verified below, a non-root process cannot write `/app` or `/usr/share/nginx/html` at all.

Nothing today is reachable beyond `127.0.0.1` and there is no known RCE, so this buys nothing observable — "how you'd notice: you wouldn't." It caps the damage ceiling of a bug that does not exist yet, at close to zero cost. This is **S4** in `docs/ISSUE_TRIAGE.md:86-92`, the last item in that doc's Phase 9 (`:264-266`) and the last open item in it overall.

## What Changes

Nine lines across four files. Every claim below was verified by building and running both images before this was written (see `design.md` for the full record); nothing here is inferred from documentation.

- **Backend — one line.** Add `USER $APP_UID` to the runtime stage of `backend/AnimeTracker.Api/Dockerfile`, after `COPY --from=build /app .` (`:12`) and before `ENTRYPOINT` (`:13`). `APP_UID` is an `ENV` the `mcr.microsoft.com/dotnet/aspnet:10.0` base image already defines as `1654`, mapped to a built-in `app` user (`app:x:1654:1654::/home/app:/bin/sh`); `USER` expands it. No user to create, no `--chown`, no Compose or port change — the backend already binds only container port 8080, which is non-privileged.
- **Frontend — one base image.** Change `frontend/Dockerfile:8` from `nginx:1.27-alpine` to `nginxinc/nginx-unprivileged:1.27-alpine`, which runs the master *and* the workers as uid 101. It cannot bind privileged port 80, so it listens on 8080, which forces two mechanical follow-ons:
  - `frontend/nginx.conf:6,12` — `listen 80 default_server;` → `listen 8080 default_server;` and `listen 80;` → `listen 8080;`, plus the comment at `:2-3` that says "port 80". Nothing else in the file changes; the Host-filtering and `X-Frame-Options` behaviour from `2026-09-18-harden-host-filtering-and-framing` is untouched and was re-verified intact.
  - `docker-compose.yml:42` — `"127.0.0.1:${FRONTEND_PORT:-5173}:80"` → `"...:8080"`. Only the container-side port moves. `FRONTEND_PORT` and its 5173 default are unchanged, so the app is reached exactly as before.
- **Frontend — deny the container its own config directory.** Three more lines in `frontend/Dockerfile`: `USER root`, then `RUN chown root:root /etc/nginx/conf.d && chmod 755 /etc/nginx/conf.d`, then `USER 101`. The unprivileged image ships that directory owned by the user nginx runs as, and the base `nginx.conf` includes `conf.d/*.conf` by wildcard — so without this, code inside the container could not edit our config but could drop a *second* config beside it and reload the master it shares a uid with, switching off exactly the loopback-only filtering and framing header the previous change added. `USER root` covers only the `RUN` between the two lines; the stage ends back on 101, so nothing runs as root at runtime. This goes one step past the triage doc's fix direction, deliberately: it is the same idea as `/app` being unwritable. Verified to break neither the image's entrypoint nor any behaviour (design D6).
- **No behaviour change on either run path.** Verified end to end: the non-root backend migrated Postgres, started its hosted services, and served `/api/my-list`, `/api/dashboard`, and `/api/season/bounds` with 200; the non-root nginx served the SPA shell, a hashed asset, and a proxied `/api/` response with their existing `Cache-Control` and `X-Frame-Options` headers, and still closed `Host: evil.example` with no response. Native Development is not touched at all — this change reaches only the Docker images and the Compose file.
- **One new log line.** The unprivileged image's `/docker-entrypoint.d/10-listen-on-ipv6-by-default.sh` cannot patch our root-owned `default.conf`, so every start logs `info: can not modify /etc/nginx/conf.d/default.conf (read-only file system?)`. Cosmetic: it only wanted to add an IPv6 `listen`, and Compose publishes on IPv4 `127.0.0.1`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `deployment`: gains a requirement that both application containers run their processes as a non-root user, and that the frontend is served by an unprivileged nginx on container port 8080 while remaining reachable on the same host port. `deployment` already owns the Compose stack, both Dockerfiles, the nginx config, and the `FRONTEND_PORT`/`BACKEND_PORT` rules, and it is where S1's host filtering and S3's framing requirements already live — so this belongs beside them. Confirmed absent from `openspec/specs/`: nothing matches `USER`, `root`, `non-root`, `uid`, or `unprivileged`.

## Impact

- `backend/AnimeTracker.Api/Dockerfile` — one added line.
- `frontend/Dockerfile` — one changed line (the base image) plus three added (the `conf.d` ownership block).
- `frontend/nginx.conf` — two `listen` ports and one comment.
- `docker-compose.yml` — one port mapping.
- No source code, database, API surface, DTO, test, or dependency changes. No new NuGet or npm package. `nginxinc/nginx-unprivileged` is a new image reference but not a new project dependency, and it is the same nginx 1.27 Alpine build.
- **Rebuild required, not just restart.** `docker compose up --build` — both images change, and the frontend's port only moves once the new image and the new mapping land together. A rebuild with a stale mapping (or the reverse) leaves the frontend unreachable until both are applied, which is the one way this change can bite; it is a single commit for exactly that reason.
- Postgres is untouched: `postgres:17-alpine` already drops to its own built-in non-root `postgres` user upstream.
- Out of scope: **S1**/**S3** (done, archived) and **S2** (CSRF header middleware, same phase, proposed separately); read-only root filesystems, dropped capabilities, and `no-new-privileges` (a larger hardening pass, not this finding); and any change to secrets, `.env`, or `ConnectionStrings`.
- Honest limit, recorded so it is not mistaken for more than it is. After this change neither container can write its application files, the served SPA bundle, its nginx config, or the directory that config lives in. What stays writable is `/tmp` — nginx needs it for its temp paths — and the backend user's own home. So code that gets in can still run for the container's lifetime; what it cannot do is make itself survive a restart by patching what the image ships, or quietly relax the loopback-only and anti-framing rules. The blast radius shrinks a lot; it does not go to zero, and closing the rest is the read-only-filesystem and dropped-capabilities pass listed above as out of scope.
