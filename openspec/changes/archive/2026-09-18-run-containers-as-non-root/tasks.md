## 1. Backend non-root user

- [x] 1.1 In `backend/AnimeTracker.Api/Dockerfile`, add `USER $APP_UID` on its own line after `COPY --from=build /app .` (`:12`) and immediately before `ENTRYPOINT` (`:13`). It must stay **after** the `apt-get` block (`:10-11`), which needs root. Do **not** add an `ARG APP_UID` — `APP_UID` is an `ENV` the `mcr.microsoft.com/dotnet/aspnet:10.0` base image already defines as `1654`, and an `ARG` would shadow it with an empty value (design D2).
- [x] 1.2 No change to `docker-compose.yml` for the backend: it already binds container port 8080, which is non-privileged. Confirm no `user:` key gets added — the fact belongs in the image (design D2).

## 2. Frontend unprivileged image

- [x] 2.1 In `frontend/Dockerfile:8`, change `FROM nginx:1.27-alpine AS runtime` to `FROM nginxinc/nginx-unprivileged:1.27-alpine AS runtime`. Leave both `COPY` lines (`:9-10`) exactly as they are — no `--chown`, deliberately: a root-owned config and SPA bundle is what makes them unwritable by the nginx user, which is the point of the change (design D5).
- [x] 2.2 In `frontend/Dockerfile`, between the new `FROM` line and the `COPY` lines, add three lines so the running container cannot add config files:
  ```dockerfile
  USER root
  RUN chown root:root /etc/nginx/conf.d && chmod 755 /etc/nginx/conf.d
  USER 101
  ```
  The `USER 101` is not optional — without it the stage ends as root and the change defeats itself. The base image includes `conf.d/*.conf` by wildcard, so a directory writable by the nginx user would let code inside the container drop a second config and reload the master to relax S1's hostname filtering or S3's framing header (design D6).
- [x] 2.3 In `frontend/nginx.conf`, change `listen 80 default_server;` (`:6`) to `listen 8080 default_server;` and `listen 80;` (`:12`) to `listen 8080;`. Both must move together — the catch-all has to be the default server for whatever port the real block listens on, or foreign hostnames fall through and get served (design D4).
- [x] 2.4 Update the comment at `frontend/nginx.conf:2-3` so "the default server for port 80" reads 8080. It exists to stop someone reintroducing that fall-through, so a stale port in it is worse than no comment.
- [x] 2.5 Leave the rest of `nginx.conf` untouched — `server_name`, `root`, all three `location` blocks, and every `add_header`, including the `X-Frame-Options` lines repeated inside `location /assets/` and `location /`. Those repetitions are load-bearing (the S1/S3 change's D4) and task 4 re-verifies them.
- [x] 2.6 In `docker-compose.yml:42`, change the frontend mapping from `"127.0.0.1:${FRONTEND_PORT:-5173}:80"` to `"127.0.0.1:${FRONTEND_PORT:-5173}:8080"`. Only the container-side port moves; `FRONTEND_PORT` and its 5173 default stay as they are.
- [x] 2.7 Commit tasks 2.1–2.6 together with task 1.1. A new image with a stale mapping (or the reverse) leaves the frontend unreachable, and that is the only way this change can bite (design, Risks).

## 3. Verify both containers are non-root

- [x] 3.1 `docker compose up --build`. Both images must actually rebuild; a cached frontend image would still listen on 80.
- [x] 3.2 Confirm the backend process is not root: `docker compose exec backend ps -o user,pid,args` shows `app` running `dotnet AnimeTracker.Api.dll`. Expect `uid=1654(app)` from `docker compose exec backend id`.
- [x] 3.3 Confirm the backend cannot overwrite itself: `docker compose exec backend touch /app/pwned` returns `Permission denied`, while `docker compose exec backend head -c4 /app/AnimeTracker.Api.dll` succeeds. Both matter — the second is what proves the first is a hardening win and not a broken image.
- [x] 3.4 Confirm **no** nginx process is root: `docker compose exec frontend ps -o user,pid,args` shows `nginx` for the master process as well as every worker. The master is the whole point; the stock image already ran its workers as `nginx`.
- [x] 3.5 Confirm the frontend cannot overwrite what it serves: `docker compose exec frontend touch /usr/share/nginx/html/pwned.js` returns `Permission denied`, while `docker compose exec frontend head -c1 /usr/share/nginx/html/index.html` succeeds.
- [x] 3.6 Confirm the frontend cannot rewrite or extend its own config: `docker compose exec frontend touch /etc/nginx/conf.d/evil.conf` returns `Permission denied` (the directory chown from task 2.2), and so does `docker compose exec frontend touch /etc/nginx/conf.d/default.conf`. Check both: the directory being locked down says nothing about the file inside it, and it is the pair that closes the gap.
- [x] 3.7 Confirm the build's temporary `USER root` left nothing behind: `docker compose exec frontend id` reports `uid=101(nginx)`, and `docker image inspect` on the built frontend image shows a non-root `Config.User`. If either says root, the `USER 101` line in task 2.2 is missing or misplaced.
- [x] 3.8 Confirm the backend came up fully as non-root: `docker compose logs backend` shows migrations applied and no permission-related startup error, and `docker compose ps` shows it stable with no restart loop.

## 4. Verify the frontend behaves exactly as before

- [x] 4.1 Syntax-check the config. `proxy_pass` upstreams resolve at parse time, so standalone validation needs the `backend` name to resolve:
  `docker run --rm --add-host backend:127.0.0.1 -v "$PWD/frontend/nginx.conf:/etc/nginx/conf.d/default.conf:ro" nginxinc/nginx-unprivileged:1.27-alpine nginx -t`
  Expect "syntax is ok" / "test is successful". A `duplicate default server` error means another port-8080 config survived (design D4).
- [x] 4.2 Expect this line in `docker compose logs frontend` on every start, and do **not** treat it as a failure: `10-listen-on-ipv6-by-default.sh: info: can not modify /etc/nginx/conf.d/default.conf (read-only file system?)`. The entrypoint wanted to add an IPv6 `listen`; it cannot, because the config is root-owned, and nothing needs IPv6 since Compose publishes on `127.0.0.1` (design D5).
- [x] 4.3 Confirm the app is still reached on the same host port: load `http://localhost:{FRONTEND_PORT}` (default 5173) in a browser and confirm it renders and its `/api/` calls succeed. Do this in a browser, not just with `curl` — `docker compose ps` reports healthy for an unreachable frontend.
- [x] 4.4 Confirm `curl -sD- -o /dev/null -H "Host: localhost" http://127.0.0.1:{FRONTEND_PORT}/` returns 200 with **both** `X-Frame-Options: DENY` and `Cache-Control: no-cache`.
- [x] 4.5 Confirm a file under `/assets/` returns **both** `X-Frame-Options: DENY` and `Cache-Control: public, max-age=31536000, immutable`.
- [x] 4.6 Confirm a real `/api/` response through the proxy carries the inherited `X-Frame-Options: DENY`, and that `Host: 127.0.0.1` behaves the same as `Host: localhost` throughout.
- [x] 4.7 Confirm S1's filtering still closes foreign hostnames: `curl -sS -H "Host: evil.example" http://127.0.0.1:{FRONTEND_PORT}/` gives "Empty reply from server", and the same for an `/api/` path. Also confirm the backend still answers `400` to `curl -H "Host: evil.example" http://127.0.0.1:{BACKEND_PORT}/api/my-list`.
- [x] 4.8 Confirm a non-default `FRONTEND_PORT` still works: set it to another value in `.env`, `docker compose up -d`, and confirm the app is reached there.

## 5. Verify nothing else regressed

- [x] 5.1 Exercise the app for real against the rebuilt stack: load My List, open an anime detail page, and make one edit that writes through to the backend. This is the check that the non-root backend can still do everything, not just start.
- [x] 5.2 Confirm the native Development path is untouched: run the backend natively and the Vite dev server, and confirm `http://localhost:{BACKEND_PORT}` serves and the dev proxy still reaches `/api`. Nothing in this change should have touched it — this is a guard against an accidental edit outside the four files.
- [x] 5.3 Confirm the Postgres volume survived the rebuild and the data is still there (the named volume is untouched by this change, but the rebuild is the moment to notice if it were not).
- [x] 5.4 Run `dotnet test backend/AnimeTracker.Api.Tests/AnimeTracker.Api.Tests.csproj` and confirm the suite is green. No test changes here, so this is purely a check that nothing was edited by accident.

## 6. Land it

- [x] 6.1 Update `docs/ISSUE_TRIAGE.md`: mark S4 (`:86-92`) done and mark Phase 9 item 18 for S4 (`:264-266`) done, following the convention the resolved entries already use (e.g. the S1/S3 entry at `:83-85` and item 17 at `:259-262`) — including the pointer to this change name and date. Fix the duplicate `18.` numbering in that plan list while there, since both S2 and S4 are numbered 18.
- [x] 6.2 Note in the same doc that this closes the last open item in Phase 9 and in the triage overall, if the doc tracks that anywhere.
