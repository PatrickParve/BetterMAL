Paths without a prefix are under `backend/AnimeTracker.Api/`. Line numbers are as of `94828d2`. Check each one against the file before editing.

The spec delta is `specs/deployment/spec.md`. Groups 3, 5 and 6 check its scenarios. Committing and archiving are done by hand after group 7, so no task here covers them.

**Build and test in the SDK container.** The only local SDK is 9.0, and the backend targets .NET 10. Docker Desktop can't bind-mount `~/Documents`, so copy the source first. From the repo root:

```bash
rsync -a --delete --exclude 'bin/' --exclude 'obj/' backend/ /private/tmp/bm-build/
docker run --rm -v /private/tmp/bm-build:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 \
  bash -c "dotnet test AnimeTracker.Api.Tests/AnimeTracker.Api.Tests.csproj"
```

**Never print secrets.** No check below echoes `.env`, `docker compose config` output in full, or the MAL client ID.

## 1. One default port: 5050 (design D1)

- [x] 1.1 In `Properties/launchSettings.json:8`, change `applicationUrl` to `http://localhost:5050`.
- [x] 1.2 In `appsettings.json:15`, change `Mal:CallbackPort` to `5050`.
- [x] 1.3 In `Services/Mal/MalOptions.cs:13`, change the `CallbackPort` default to `5050`. The doc comment above it stays as it is.
- [x] 1.4 In `docker-compose.yml`, change both `${BACKEND_PORT:-5000}` fallbacks (`:30` `Mal__CallbackPort`, `:32` `ports`) to `${BACKEND_PORT:-5050}`.
- [x] 1.5 In `frontend/vite.config.ts:8`, change the fallback `'5000'` to `'5050'`.

## 2. The `.env` loader (design D2–D7)

- [x] 2.1 Create `Services/Infrastructure/DevDotEnvOverlay.cs`, a `public static class` in namespace `AnimeTracker.Api.Services.Infrastructure`. Add a class doc comment saying what it's for: a natively run backend reads the repo-root `.env` that Docker Compose reads, and it does nothing outside Development.
- [x] 2.2 Add `internal static Dictionary<string, string> Parse(IEnumerable<string> lines)`, following the D4 rules:
  - trim each line, and skip blank lines and lines starting with `#`
  - split at the first `=`, and skip lines with no `=` or an empty key
  - quoted value: one matching `"` or `'` pair is removed, anything after the closing quote is ignored, and there are no escapes
  - unquoted value: it ends at the first `#` preceded by a space or tab, and is then trimmed
  - keys are ordinal (case-sensitive), and a later duplicate wins
- [x] 2.3 Add `internal static Dictionary<string, string?> ToConfiguration(IReadOnlyDictionary<string, string> env)`, following the D5 table:
  - an empty value is treated as absent
  - `MAL_CLIENT_ID` sets `Mal:ClientId`, and `MAL_CLIENT_SECRET` sets `Mal:ClientSecret`
  - `BACKEND_PORT` sets `Mal:CallbackPort` and `urls` = `http://localhost:<port>`
  - `ConnectionStrings:Default` is set only when `POSTGRES_DB`, `POSTGRES_USER` and `POSTGRES_PASSWORD` are all present. It is built with `Npgsql.NpgsqlConnectionStringBuilder { Host = "localhost", Port = POSTGRES_PORT ?? 5432, Database, Username, Password }.ConnectionString`.
  - a `BACKEND_PORT` or `POSTGRES_PORT` that doesn't parse as an integer throws `InvalidOperationException`. The message names the key and `.env`, but not the value.
- [x] 2.4 Add `public static void Apply(IConfigurationBuilder configuration, IHostEnvironment environment)`:
  - return immediately unless `environment.IsDevelopment()`, before any filesystem access (D3)
  - resolve `Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", ".env"))` (D6), and return if the file doesn't exist
  - otherwise call `configuration.AddInMemoryCollection(ToConfiguration(Parse(File.ReadAllLines(path))))`
- [x] 2.5 In `Program.cs`, directly after `var builder = WebApplication.CreateBuilder(args);` (`:31`):
  - call `DevDotEnvOverlay.Apply(builder.Configuration, builder.Environment);`
  - add `using AnimeTracker.Api.Services.Infrastructure;` if it's missing (the imports are alphabetical)
  - add a short comment: run natively (Development), the backend reads the repo-root `.env` that Docker Compose reads, and it must come before anything reads configuration
- [x] 2.6 Compile with the container command above, using `dotnet build AnimeTracker.Api/AnimeTracker.Api.csproj` in place of `dotnet test`. It builds with no new warnings.

## 3. Tests (design D8)

- [x] 3.1 Create `backend/AnimeTracker.Api.Tests/Services/Infrastructure/DevDotEnvOverlayTests.cs`. Add `Parse` tests for each of these:
  - comment and blank lines are skipped
  - the README form `BACKEND_PORT=5050         # must match the redirect URI…` gives `5050`
  - `pa#ss` is kept whole
  - `"a # b"` and `'x'` lose their quotes, and the `#` inside the quotes is kept
  - `a=b=c` gives the value `b=c`
  - a line with no `=` is skipped
  - whitespace around the key and value is trimmed
  - of two lines with the same key, the later wins
- [x] 3.2 Add `ToConfiguration` tests:
  - **The full `.env.example` key set, with filled-in values**, gives exactly the keys `Mal:ClientId`, `Mal:ClientSecret`, `Mal:CallbackPort`, `urls` and `ConnectionStrings:Default`. `urls` is `http://localhost:5050`.
  - **The connection string**, parsed back with `NpgsqlConnectionStringBuilder`, has `Host` `localhost` and the given port, database, username and password.
  - **No `POSTGRES_PORT`** gives port 5432.
  - **A password containing `;` and `=`** round-trips.
  - **A missing or empty `POSTGRES_PASSWORD`** (and separately, user or DB) gives no `ConnectionStrings:Default`.
  - **An empty `MAL_CLIENT_ID=` or `BACKEND_PORT=`** gives no key.
  - **`BACKEND_PORT=abc` or `POSTGRES_PORT=abc`** throws `InvalidOperationException`. The message contains the key name but not the value.
- [x] 3.3 Add `Apply` tests against a real builder:
  - **Setup.**
    - Create a unique temp directory holding `backend/AnimeTracker.Api/` and a `.env` at its root.
    - Build with `WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = …, ContentRootPath = <tmp>/backend/AnimeTracker.Api, Args = ["--hostBuilder:reloadConfigOnChange=false"] })`. The arg stops file watchers from holding the temp directory.
    - Record `builder.Configuration.Sources.Count` before calling `Apply`.
  - **Development with `.env`:** the count grows by one, and `Mal:ClientId`, `Mal:CallbackPort` and `ConnectionStrings:Default` return the file's values.
  - **Production with the same `.env`:** the count is unchanged, and `Mal:ClientId` and `ConnectionStrings:Default` are null.
  - **Development with no `.env`:** the count is unchanged, and nothing throws.
  - **Cleanup:** dispose `builder.Configuration` and delete the temp directory, in `Dispose` or `finally`.
- [x] 3.4 Run the full test suite with the container command. Every test passes, both new and existing.

## 4. `.env.example`, README and CODE_GUIDE

- [x] 4.1 In `.env.example:19`, set `POSTGRES_PASSWORD=devlocalpassword`, matching `appsettings.Development.json:9`.
- [x] 4.2 In `README.md:45-46`, the redirect URL becomes `http://localhost:5050/callback`, and the parenthetical ends "5050 is the default".
- [x] 4.3 In the README's `.env` block:
  - `:66` becomes `BACKEND_PORT=5050`
  - `:72` becomes `POSTGRES_PASSWORD=devlocalpassword`, and its trailing comment is replaced with one that says it only guards the local database and should be set before the first `docker compose up`. Postgres keeps the password it was created with (design Risks).
- [x] 4.4 In `README.md:94`, "Backend API" becomes `http://localhost:5050`.
- [x] 4.5 Rewrite the native-run paragraph at `README.md:160-162`. It should say:
  - the backend applies migrations on startup and listens on port 5050, or `BACKEND_PORT`
  - run this way, it reads the same repo-root `.env` as Docker Compose, so the MAL credentials and the Postgres settings apply with no extra setup, and it connects to the `postgres` container on `localhost:<POSTGRES_PORT>`
  - it looks for `.env` two levels up from where it's started, so run it from `backend/AnimeTracker.Api` as shown
  - without a `.env`, it falls back to `appsettings.Development.json`, which matches `.env.example`'s defaults, but MAL won't connect

  Drop the "already points at the postgres container's exposed port" wording.
- [x] 4.6 In `README.md:170`, the Vite proxy target becomes `localhost:5050` (or `BACKEND_PORT` from `.env`).
- [x] 4.7 In the README's env-var table (`:191`), the `BACKEND_PORT` default becomes `` `5050` ``.
- [x] 4.8 In `CODE_GUIDE.md`:
  - `:60-61`: `MalOptions` is fed from `.env` through docker-compose env vars under Docker, and through `DevDotEnvOverlay` when run natively
  - `### Program.cs` (`:216-221`): add that, before any registration, `DevDotEnvOverlay.Apply` layers the repo-root `.env` over configuration in Development only
  - `:1049`: `dotnet run` (backend, port 5050)
- [x] 4.9 Run `git grep -nE "\b5000\b|5273|changeme"`. The only remaining hits should be unrelated: `ConnectionStatusNotice.tsx`'s 5000 ms interval, and `ISSUES.md`'s historical description of #15.

## 5. Check the Docker path by hand (no .NET 10 SDK needed)

- [x] 5.1 From the repo root, check the port with `docker compose config | grep -nE "published|Mal__CallbackPort"`. The grep prints only those lines, never the full output, which contains secrets. Run it three ways; a shell variable overrides `.env` for Compose, so `.env` itself isn't edited:
  - as is: the backend's published port and `Mal__CallbackPort` match `.env`'s `BACKEND_PORT`
  - prefixed with `BACKEND_PORT=`: both are `5050` (the spec's "The default port" scenario)
  - prefixed with `BACKEND_PORT=5055`: both are `5055` (the spec's "A chosen port applies everywhere under Docker" scenario)
- [x] 5.2 Run `docker compose up -d --build backend`, then open http://localhost:5173. The app loads past "Can't reach the backend", and `docker compose logs backend` shows no startup error. The overlay is compiled into the image, but the Production gate keeps it from running.

## 6. Check the native run by hand (needs a local .NET 10 SDK)

- [x] 6.1 Run `dotnet --list-sdks`. If no `10.x` SDK is listed, stop and ask the user whether to install one. If they decline, leave 6.2–6.5 unchecked and say so when reporting.
- [x] 6.2 Stop the Docker backend (`docker compose stop backend`) so port 5050 is free, and keep `postgres` running. Then run `cd backend/AnimeTracker.Api && dotnet run`. The log shows "Now listening on: http://localhost:<BACKEND_PORT>", and startup migration completes with no Npgsql authentication error, which shows the `.env` connection string reached the compose Postgres.
- [x] 6.3 Check the connection and the OAuth URL:
  - `curl -s http://localhost:5050/api/mal-auth/status` returns a JSON `state`, which is a database read.
  - `curl -s -o /dev/null -w '%{redirect_url}\n' http://localhost:5050/api/mal-auth/start | sed -E 's/(client_id|code_challenge|state)=[^&]*/\1=<redacted>/g'` shows `redirect_uri=http%3A%2F%2Flocalhost%3A5050%2Fcallback`.
  - `curl -s -o /dev/null -w '%{redirect_url}' http://localhost:5050/api/mal-auth/start | grep -qE 'client_id=[^&]+' && echo client_id set` prints `client_id set`.
- [x] 6.4 Stop it, then check that the `urls` overlay beats the launch settings:
  - run `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5999 dotnet run --no-launch-profile`
  - the log shows it listening on `BACKEND_PORT` from `.env`, not 5999
  - stop it
- [x] 6.5 With the native backend running again (6.2), run the frontend from `frontend/` with `PATH="$HOME/.nvm/versions/node/v22.23.1/bin:$PATH" npm run dev`. http://localhost:5173 loads the app, not "Can't reach the backend". Stop both, then `docker compose up -d backend` to restore the Docker stack.

## 7. Validate and record

- [x] 7.1 Run `openspec validate fix-native-dev-setup --strict`, and fix anything it reports.
- [x] 7.2 In `docs/ISSUE_TRIAGE.md` (local, gitignored), following the pattern PF2 and N2 used:
  - replace the B4 entry (`:48-56`) with a short blockquote pointing to the resolved file
  - update the Summary table's "Still present" count, and add B4 to a "Resolved (code fixes)" row for the date it lands
  - mark Phase 7 item 14 done. Phase 7 stays open for SP5.
  - update the ISSUES #15 row (`:366`)
- [x] 7.3 In `docs/ISSUE_TRIAGE_RESOLVED_2026-09-13.md`, add a `## B4.` section under a code-fixes heading for that date, adding the heading if it's missing. Name this change. Record:
  - the `.env` loader replaced the triage entry's `user-secrets` direction, and why: one source shared with Docker
  - 5050 everywhere, including the `MalOptions` default and the Vite fallback the entry didn't list
  - `BACKEND_PORT` also drives the native listen port
  - the password default now matches `appsettings.Development.json`
  - what remains: `dotnet run` must start from the project folder, and the MAL redirect URL is registered by hand
