## Context

This change fixes triage item B4. The proposal has the evidence with line references. This section covers only what shapes the design. Paths without a prefix are under `backend/AnimeTracker.Api/`.

**How configuration reaches the backend today.**
- `WebApplication.CreateBuilder(args)` (`Program.cs:31`) stacks these sources, lowest precedence first:
  - `appsettings.json`
  - `appsettings.{Environment}.json`
  - environment variables
  - command-line args
- `ASPNETCORE_`-prefixed variables such as `ASPNETCORE_URLS` also land in configuration as `urls`.
- The first reads of configuration are `AddDbContext` (`:40-41`, which reads `ConnectionStrings:Default` lazily when the context is first built) and `Configure<MalOptions>` (`:52`, which binds lazily too).
- `builder.Build()` is where Kestrel's listen addresses are resolved from the `urls` key.
- Anything added to `builder.Configuration` between `CreateBuilder` and `Build` is seen by all of them.

**The two run paths.**
- **Docker.**
  - Compose runs the backend with `ASPNETCORE_ENVIRONMENT=Production` (`docker-compose.yml:26`) and passes everything as `__`-separated env vars (`:27-30`).
  - The build context is `backend/`, and the image copies only `AnimeTracker.Api/` (`Dockerfile:3-5`), so the repo-root `.env` never exists inside the container.
  - The content root is `/app`.
- **Native.**
  - `dotnet run` applies the `http` profile of `Properties/launchSettings.json`. That profile sets `ASPNETCORE_ENVIRONMENT=Development` and `applicationUrl`, which becomes `ASPNETCORE_URLS`.
  - The content root is the working directory: the project folder under the README's `cd backend/AnimeTracker.Api && dotnet run`.
  - `appsettings.Development.json` supplies a hardcoded connection string, and nothing supplies the MAL credentials.

**How `.env` is read elsewhere.**
- Docker Compose reads it for interpolation, so its rules are the ones the loader must agree with:
  - unquoted values end at a `#` that follows whitespace
  - one pair of surrounding quotes is removed
  - `${VAR:-default}` treats an empty value as unset
- `frontend/vite.config.ts:7` reads the same file through Vite's `loadEnv`.
- The README's illustrative `.env` block (`README.md:60-74`) uses trailing `# comment`s on value lines, so a hand-edited `.env` can plausibly contain them.

**Constraints.**
- Only one local SDK is installed, 9.0.301, but the backend targets `net10.0`. So `dotnet build`, `dotnet test` and `dotnet run` fail locally.
  - Compiling and running the tests go through the `mcr.microsoft.com/dotnet/sdk:10.0` image, from a copy of `backend/` under `/private/tmp`, because Docker Desktop can't bind-mount `~/Documents`.
  - The end-to-end native check (task group 5) needs a local .NET 10 SDK. Installing one is the user's call.
- The test project is xunit with no mocking library. Test doubles are hand-written `private sealed class`es.

## Goals / Non-Goals

**Goals:**
- One default backend port, 5050, in every file that names one.
- A natively run backend (Development) picks up the MAL credentials, the callback port, its listen port and the Postgres connection from the repo-root `.env`, with no step beyond what the Docker setup already asks for.
- The Docker/Production path behaves exactly as before, apart from the `BACKEND_PORT` fallback, which only applies when `.env` omits it. Tests exercise the gate, not just a reading of the code.
- A missing `.env` changes nothing and throws nothing.

**Non-Goals:**
- Full dotenv compatibility: no `${VAR}` interpolation, no escape sequences, no multi-line values, no `export` prefix.
- Any Docker-path change beyond the fallback port. That includes `docker-compose.yml:27` interpolating a password containing `;` straight into the connection string. That problem predates this change and isn't touched.
- `dotnet user-secrets` (considered and rejected in favour of the loader).
- Registering the redirect URL with MAL.

## Decisions

### D1. 5050 in every file that names a default port

These files change:
- `Properties/launchSettings.json` `applicationUrl`
- `appsettings.json` `Mal:CallbackPort`
- the `MalOptions.CallbackPort` property default
- both `docker-compose.yml` fallbacks
- the `frontend/vite.config.ts` fallback
- the six README references, and `CODE_GUIDE.md:1049`

`MalOptions` and `vite.config.ts` aren't in the triage entry's evidence. Both are fallbacks that only apply when the other sources are silent, so leaving `5000` there keeps the drift B4 is about.

`.env.example` already says `5050`.

*Alternatives considered:*
- **Keep `5000` and change `.env.example`.** Rejected. macOS's AirPlay Receiver holds port 5000, which is why `.env.example` moved away from it.

### D2. A static `DevDotEnvOverlay` in `Services/Infrastructure/`, called once from `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

// Natively run (Development), read the repo-root .env that Docker Compose reads …
DevDotEnvOverlay.Apply(builder.Configuration, builder.Environment);
```

The class has three parts, each testable alone:
- `Parse(IEnumerable<string> lines)` returns `Dictionary<string, string>`. It is `internal`, and D4 has the rules.
- `ToConfiguration(IReadOnlyDictionary<string, string> env)` returns `Dictionary<string, string?>`. It is `internal`, pure, and D5 has the mapping.
- `Apply(IConfigurationBuilder configuration, IHostEnvironment environment)` is `public`. It checks the gate (D3), resolves the path (D6), reads the file if it exists, and calls `configuration.AddInMemoryCollection(...)` with the mapped keys. If the gate is closed or the file is missing, it returns without adding a source.

`InternalsVisibleTo` already exposes `internal` members to `AnimeTracker.Api.Tests` (`AnimeTracker.Api.csproj`).

*Alternatives considered:*
- **Inline the logic in `Program.cs`.** Rejected. Top-level statements can't be unit-tested, and `Program.cs` runs `Database.Migrate()` on startup, which rules out `WebApplicationFactory` without a real Postgres.
- **A NuGet dotenv package** (e.g. `DotNetEnv`). Rejected. It writes into process environment variables rather than configuration, and it adds a dependency for about 30 lines of parsing.
- **An extension method** (`builder.Configuration.AddDevDotEnv(builder.Environment)`). Equivalent. A plain static call keeps the Development gate visible at the call site's name and avoids introducing an extensions class for one method.

### D3. The Development check is inside `Apply`, before any filesystem access

- `Apply` first checks `environment.IsDevelopment()`, and returns immediately when it's false.
- This is the real guard for the Docker path. The file's absence in the container (`/app/../../.env` resolves to `/.env`) is a second layer, and the design doesn't rely on it.
- Keeping the check inside `Apply`, not at the call site, means the tests call the same method `Program.cs` calls. They exercise the gate against a real `WebApplicationBuilder` built with `EnvironmentName = "Production"` (D8).

### D4. Parsing rules: `KEY=VALUE`, matching Compose on the forms a hand-edited `.env` uses

For each line:
1. Trim it. Skip it if it's empty or starts with `#`.
2. Split at the **first** `=`, so passwords may contain `=`. Skip the line if it has no `=` or the trimmed key is empty.
3. Take the text after `=`:
   - **Quoted:** trimmed, it starts with `"` or `'` and the same quote appears again later. The value is the text between the two quotes, and anything after the closing quote is ignored. No escapes.
   - **Unquoted:** the value ends at the first `#` preceded by a space or tab, then it is trimmed. A `#` with no whitespace before it, as in `pa#ss`, stays part of the value.
4. Keys are case-sensitive (ordinal). A later line wins over an earlier one with the same key.

`File.ReadAllLines` handles a UTF-8 BOM and CRLF line endings.

*Why go beyond bare `KEY=VALUE`:* the loader exists so that native and Docker read one file the same way. Suppose Compose strips `  # pick your own password` or a pair of quotes from `POSTGRES_PASSWORD` and the loader doesn't. Native then authenticates with a different password from the one the volume was created with, and that is B4 again, with no error pointing at the cause. Both rules are a few lines. Interpolation and escapes stay out: nothing in the repo uses them, and a partial version would be worse than none.

### D5. Key mapping

| `.env` | Configuration key | Rule |
|---|---|---|
| `MAL_CLIENT_ID` | `Mal:ClientId` | if non-empty |
| `MAL_CLIENT_SECRET` | `Mal:ClientSecret` | if non-empty |
| `BACKEND_PORT` | `Mal:CallbackPort` and `urls` = `http://localhost:<port>` | if non-empty; must parse as an integer |
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_PORT` | `ConnectionStrings:Default` | only if DB, user and password are all non-empty; port is `POSTGRES_PORT` if non-empty (must parse as an integer), else `5432` |

- **Empty means absent.** This matches Compose's `:-` fallbacks. An unfilled `MAL_CLIENT_ID=` line, as `.env.example` ships it, adds nothing, rather than overwriting some other source with `""`.
- **The connection string is built with `NpgsqlConnectionStringBuilder`**, with `Host = "localhost"` and the four fields above. The builder quotes values containing `;`, `=` or quotes, which string interpolation wouldn't.
  - The host is `localhost`, not `postgres`, because native dev reaches the compose Postgres through its published port (`docker-compose.yml:12`).
  - The port fallback of `5432` mirrors that same line's `${POSTGRES_PORT:-5432}`.
- **Partial Postgres keys add no connection string.** `appsettings.Development.json` stays in effect. Compose can't start that Postgres without all three either.
- **A port that isn't an integer throws** `InvalidOperationException`, naming the key (for example "`BACKEND_PORT` in .env must be a port number"). Otherwise the error would surface later and far away: `MalOptions` binding fails on the first OAuth call, and `NpgsqlConnectionStringBuilder` fails on `Port`. The message names the key and `.env` but not the value. A missing file still never throws.
- **`BACKEND_PORT` also sets `urls`.** Without it, Kestrel's port comes only from `launchSettings.json`. A non-default `BACKEND_PORT` would then send the Vite proxy and the OAuth redirect to a port the backend isn't on. Compose avoids that because `BACKEND_PORT` drives both the published port and the callback port. The launch profile's `http://localhost:5050` stays as the value when `.env` is missing or omits the key.
  - *Alternative:* `builder.WebHost.UseUrls(...)`. Rejected. It would split the overlay across two mechanisms, and the mapping would stop being one dictionary that the tests can assert on.

### D6. `.env` is found at `<ContentRootPath>/../../.env`

- The path is `Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", ".env"))`.
- Under the documented invocation, the content root is `backend/AnimeTracker.Api/`, so the path is the repo root.
- The content root is the same base ASP.NET Core resolves `appsettings*.json` against. So `.env` and the Development settings file are found from the same starting point.

*Alternatives considered:*
- **Walk up the directory tree** until a `.env` or `docker-compose.yml` turns up. Rejected. It's more machinery than the one documented layout needs, and it could pick up an unrelated `.env` above the repo.
- **`AppContext.BaseDirectory`** (`bin/Debug/net10.0/`). Rejected. The depth changes with configuration and target framework.
- **`[CallerFilePath]`**, the source path baked in at compile time. Rejected as too clever. It also silently points at the build machine's path.

### D7. The overlay is added last, so in Development `.env` beats env vars and command-line args for its keys

- `AddInMemoryCollection` after `CreateBuilder` puts the overlay above every default source.
- So in Development, a shell `Mal__ClientId=…` or `--urls …` is overridden for the five keys the overlay sets. That only happens when `.env` has a non-empty value for the matching key.
- This is accepted. `.env` is documented as the one place these values live (`README.md:76-77`, and the `deployment` spec's "Secrets via gitignored .env"), and this single-user setup has no workflow that overrides them from the shell.

*Alternatives considered:*
- **Insert the source just below the environment-variables source**, which is the usual dotenv precedence. Rejected. It means finding that source's index in `builder.Configuration.Sources` by type, which is brittle for a case nobody uses. Anyone who needs an override edits `.env`.

### D8. Tests

The new file is `backend/AnimeTracker.Api.Tests/Services/Infrastructure/DevDotEnvOverlayTests.cs`.

- **`Parse`:**
  - comment lines and blank lines are skipped
  - a trailing ` # comment` is stripped, in the README's form: `BACKEND_PORT=5050         # must match…`
  - `#` without whitespace before it is kept
  - double- and single-quoted values lose their quotes, and a `#` inside quotes is kept
  - `=` inside a value is kept
  - a line with no `=` is skipped
  - whitespace around keys and values is trimmed
  - the later duplicate wins
- **`ToConfiguration`:**
  - The full `.env.example` key set, with filled-in values, produces exactly `Mal:ClientId`, `Mal:ClientSecret`, `Mal:CallbackPort`, `urls` and `ConnectionStrings:Default`.
  - The connection string is checked by parsing it back with `NpgsqlConnectionStringBuilder`: `Host=localhost`, plus the port, database, username and password.
  - With no `POSTGRES_PORT`, the port is 5432.
  - A password containing `;` and `=` round-trips.
  - Missing or empty password, user or DB gives no `ConnectionStrings:Default`.
  - Empty `MAL_CLIENT_ID` and `BACKEND_PORT` give no key.
  - A non-integer `BACKEND_PORT` or `POSTGRES_PORT` throws, and the message names the key.
- **`Apply`, against a real builder:**
  - Setup: a temp directory laid out as `<tmp>/backend/AnimeTracker.Api/` plus `<tmp>/.env`. A `WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = …, ContentRootPath = <tmp>/backend/AnimeTracker.Api })`.
  - **Development:** after `Apply`, `builder.Configuration` returns the `.env` values, and `Sources.Count` has grown by one.
  - **Production, with the same `.env` present:** `Sources.Count` is unchanged, and `Mal:ClientId` and `ConnectionStrings:Default` are still null.
  - **Development, with no `.env`:** `Sources.Count` is unchanged, and nothing throws.
  - Each test deletes its temp directory.

The real builder is used, not a fake `IHostEnvironment`, so the gate is tested on the same environment object `Program.cs` passes in.

## Risks / Trade-offs

- **[`dotnet run` started from a directory other than the project folder]** puts the content root elsewhere. The loader then finds no `.env` and silently falls back to `appsettings.Development.json`. → The README documents the `cd backend/AnimeTracker.Api && dotnet run` invocation, and this design doesn't change it. IDE launches (Rider, VS Code's C# Dev Kit) use the project folder.
- **[`POSTGRES_PASSWORD` changed in `.env` after the Postgres volume was created]**: Postgres keeps the original, so native and Docker both fail to authenticate. → This already applies to Docker, and the loader doesn't make it worse. The fix is the same for both: change `.env` back, or recreate the volume.
- **[Existing Docker users with no `BACKEND_PORT` in `.env`]** move from host port 5000 to 5050 and need to update their MAL app's redirect URL. → `.env.example` has shipped `BACKEND_PORT=5050`, so a `.env` copied from it already sets the port. The proposal's Impact section calls this out.
- **[The Compose parsing rules drift for an exotic value]** (escapes, interpolation, `export`). → These are out of scope (Non-Goals), and nothing in the repo uses them. The README's `.env` block, which is the realistic source of hand edits, is covered by D4 and its tests.
- **[The end-to-end native check can't run on this machine]** without a .NET 10 SDK. → The parser, mapping and gate are covered by tests in the `sdk:10.0` container. The live check (listen port, OAuth `client_id`/`redirect_uri`, DB connection) is a hand-check task that needs the SDK installed. If it isn't, that task is left to the user.
