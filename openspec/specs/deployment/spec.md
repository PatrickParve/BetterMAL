# deployment Specification

## Purpose
The deployment capability governs the three-service Docker Compose stack — auto-restarting with the machine, its durable Postgres data volume, and secrets read from a single gitignored .env — and a native Development run that reads that same .env directly rather than through Compose. Both run the backend on the one port set by BACKEND_PORT.
## Requirements
### Requirement: Docker Compose three-service stack
The system SHALL be deployable via Docker Compose with separate Postgres, backend, and frontend services.

#### Scenario: Bringing up the stack
- **WHEN** `docker compose up` is run
- **THEN** the Postgres, backend, and frontend services all start and the app is reachable locally

### Requirement: Auto-restart with the machine
The system SHALL configure each service with `restart: unless-stopped` so the app comes back up automatically when the PC boots and Docker starts, and shuts down cleanly with the PC.

#### Scenario: Recovery after reboot
- **WHEN** the PC reboots and Docker starts
- **THEN** each service restarts automatically unless it was explicitly stopped

### Requirement: Durable Postgres data volume
The system SHALL use a named volume for the Postgres data directory so data survives container rebuilds.

#### Scenario: Data survives a rebuild
- **WHEN** the containers are rebuilt
- **THEN** the Postgres data persists via the named volume

### Requirement: Secrets via gitignored .env
The system SHALL load secrets (the MAL client ID and secret, and the database connection) from a `.env` file at the repository root that is gitignored and never committed. MAL access and refresh tokens are not among them: they come from authorization and are stored in Postgres.

This SHALL hold on both run paths. Under Docker Compose, the backend receives these values from `.env` through Compose. Run natively in the Development environment, the backend SHALL read the same `.env` itself, taking each value as Docker Compose would read it, so that one file configures both paths identically. Run natively, it SHALL reach the database through the port Postgres is exposed on locally.

Outside the Development environment, the backend SHALL NOT read a `.env` file, even if one is present where it would look.

#### Scenario: Secrets stay out of the repo
- **WHEN** the repository is committed or made public
- **THEN** the `.env` file and its secrets are excluded by gitignore and not present in the repo

#### Scenario: A native run uses the same .env as Docker
- **WHEN** the backend is started natively in Development from its project folder, with a `.env` at the repository root and the Compose `postgres` service running
- **THEN** it uses that file's MAL client ID and secret, and connects to Postgres on `localhost` at `POSTGRES_PORT` with that file's database, user and password

#### Scenario: A value is read the way Docker Compose reads it
- **WHEN** a value in `.env` is followed by a whitespace-separated `# comment`, or is wrapped in one pair of quotes
- **THEN** a native run uses the value without the comment or quotes, the same value Docker Compose passes to the containers

#### Scenario: A native run with no .env
- **WHEN** the backend is started natively in Development and there is no `.env` at the repository root
- **THEN** it starts using its own Development database settings, which match `.env.example`'s defaults, and connecting to MAL stays unavailable until credentials are set

#### Scenario: A malformed port in .env
- **WHEN** the backend is started natively in Development and `.env` sets `BACKEND_PORT` or `POSTGRES_PORT` to something other than a whole number
- **THEN** it fails at startup with an error that names that key, without showing the value

#### Scenario: Outside Development, .env is never read
- **WHEN** the backend runs in any environment other than Development, including the Docker image's Production environment, and a `.env` file is present where a native run would look
- **THEN** that file is not read, and the backend's configuration is unchanged by it

### Requirement: One backend port, set by BACKEND_PORT
The system SHALL take the backend's host port from `BACKEND_PORT` in `.env`, and SHALL use 5050 when it is unset or empty. That one port SHALL be where the backend is reached on `localhost`, whether it runs under Docker Compose or natively. It SHALL also be the port in the OAuth redirect URI `http://localhost:{port}/callback`, and the port the frontend dev server proxies `/api` calls to.

#### Scenario: The default port
- **WHEN** `.env` does not set `BACKEND_PORT`, and the backend runs under Docker Compose or natively
- **THEN** the backend is reached at `http://localhost:5050` and the OAuth redirect URI is `http://localhost:5050/callback`

#### Scenario: A chosen port applies everywhere on a native run
- **WHEN** `.env` sets `BACKEND_PORT` to a port other than 5050, and the backend and the frontend dev server run natively
- **THEN** the backend listens on that port, the OAuth redirect URI uses it, and the dev server proxies `/api` to it

#### Scenario: A chosen port applies everywhere under Docker
- **WHEN** `.env` sets `BACKEND_PORT` to a port other than 5050, and the stack runs under Docker Compose
- **THEN** the backend is published on that port and the OAuth redirect URI uses it

### Requirement: Only loopback hostnames are served

Both layers that accept HTTP requests SHALL serve only requests whose `Host` header names `localhost` or `127.0.0.1`, and SHALL refuse every other hostname. This holds on both run paths and on whatever port `BACKEND_PORT` selects: the hostname is what is matched, never the port.

The backend SHALL take its allowed hostnames from the `AllowedHosts` configuration key, which SHALL list `localhost` and `127.0.0.1` rather than the wildcard `*`, so that a single setting covers the native Development run and the Docker/Production run alike. A request carrying any other `Host` SHALL be refused before it reaches a controller, so that no list entry, transfer import, or export can be reached by it.

The frontend SHALL serve the app and proxy `/api/` only under those two hostnames. A request carrying any other `Host` SHALL have its connection closed without a response, and SHALL be neither proxied to the backend nor answered with any part of the frontend.

This closes DNS rebinding: a page served from another origin that re-points its own hostname at the loopback address is refused by both layers, even though the browser treats its requests as same-origin and so never sends a CORS preflight.

#### Scenario: A rebound hostname reaches the backend

- **WHEN** a request arrives at the backend with a `Host` header of `evil.example`, with or without a port
- **THEN** it is refused before reaching a controller, and no list entry, transfer import, or export is read or written

#### Scenario: A rebound hostname reaches the frontend

- **WHEN** a request arrives at the frontend with a `Host` header of `evil.example`
- **THEN** the connection is closed with no response, and the request is neither proxied to `/api/` nor served any frontend file

#### Scenario: The documented access paths keep working

- **WHEN** the app is reached at `localhost` or `127.0.0.1` on the port `BACKEND_PORT` selects, under Docker Compose or run natively
- **THEN** the backend serves the request as before, and the frontend serves the app and proxies `/api/` as before

### Requirement: The frontend refuses to be framed

Every response the frontend serves SHALL carry `X-Frame-Options: DENY`, so that no other origin can embed the UI in a frame and overlay it to capture clicks. This SHALL hold for the SPA shell, for hashed assets, and for responses proxied through `/api/`, and SHALL NOT displace the cache-control behaviour those responses already have.

#### Scenario: The SPA shell is framed

- **WHEN** a response for `/` is served
- **THEN** it carries `X-Frame-Options: DENY` alongside its existing `Cache-Control: no-cache`

#### Scenario: A hashed asset is framed

- **WHEN** a response for a file under `/assets/` is served
- **THEN** it carries `X-Frame-Options: DENY` alongside its existing `Cache-Control: public, max-age=31536000, immutable`

#### Scenario: A proxied API response

- **WHEN** a response is proxied through `/api/`
- **THEN** it carries `X-Frame-Options: DENY`

#### Scenario: Another origin embeds the app

- **WHEN** a page on another origin loads the app in an `<iframe>`
- **THEN** the browser refuses to render the frame

### Requirement: Mutating API requests must prove they came from the app

The backend SHALL refuse any request whose path is under `/api` and whose method is not `GET` unless it carries the header `X-Requested-With: BetterMAL`. A refused request SHALL be answered `403 Forbidden` and SHALL be stopped before any controller or service runs, so that nothing it asks for is read, written, or pushed to MyAnimeList. The value SHALL be matched exactly: a missing header, an empty one, or any other value is refused.

The web client SHALL send that header on every non-`GET` request it makes to the backend. It SHALL be added at the single point every request passes through, rather than at each call site, and SHALL be merged with whatever headers the caller already set, so that a request carrying its own `Content-Type` keeps it.

`GET` SHALL be exempt on both sides: the backend SHALL NOT require the header on a `GET`, and the client SHALL NOT add it to one, so that reads go out exactly as they do today and stay collapsible by URL alone.

This exemption is a deliberate limit on what the requirement covers, not a claim that reads have no effects. Some `GET` endpoints do write — `GET /api/sync/held` clears held changes MyAnimeList already agrees with, and several browse reads fill caches — so a cross-site `GET` can still trigger those effects and the outbound MyAnimeList calls they make. What it cannot do is read the response, or reach anything that pushes a change to MyAnimeList or deletes list data.

That last point is what the exemption rests on, so it SHALL hold rather than merely happen to be true: no endpoint served over `GET` under `/api` SHALL push a change to MyAnimeList, delete a list entry, or discard unsent local edits. Filling a cache or reconciling state as a side effect of a read is permitted. This SHALL be enforced against drift, so that adding or renaming a `GET` route under `/api` cannot quietly widen what a cross-site request can reach.

This closes cross-site request forgery. The mutating endpoints bind nothing from the request body, so a cross-site form submit or a `no-cors` fetch is a CORS "simple request" that needs no preflight and reaches them today. A browser will not attach a custom header to a cross-site request without first sending a preflight, and no preflight can succeed here, because no `Access-Control-Allow-Origin` is ever sent. The header is therefore only ever present on a request the app itself made.

#### Scenario: A hostile page submits a cross-site form

- **WHEN** a page on another origin submits a form or issues a `no-cors` fetch to `POST /api/sync/held/accept`, carrying no `X-Requested-With` header
- **THEN** the backend answers `403 Forbidden`, the controller and its service never run, and no held change is accepted or pushed to MyAnimeList

#### Scenario: A cross-site request guesses at the header

- **WHEN** a non-`GET` request to an `/api` path carries `X-Requested-With` with any value other than `BetterMAL`
- **THEN** it is refused with `403 Forbidden` before reaching a controller

#### Scenario: The app's own mutations keep working

- **WHEN** the web client edits a list entry, runs a sync, accepts or declines a held change, or reorders a ranking
- **THEN** the request carries `X-Requested-With: BetterMAL` and is served exactly as before

#### Scenario: An import carries both its own header and this one

- **WHEN** the web client posts a transfer file to `/api/transfer/import`, a call that sets its own `Content-Type: application/json`
- **THEN** the request carries both that `Content-Type` and `X-Requested-With: BetterMAL`, and the import runs

#### Scenario: A read needs no header

- **WHEN** a `GET` request is made to any `/api` path with no `X-Requested-With` header
- **THEN** it is served as before, unaffected by this check

#### Scenario: A new read endpoint could widen the exemption

- **WHEN** a `GET` route under `/api` is added, renamed, or removed
- **THEN** the change fails its tests until the new set of read endpoints is reviewed and recorded, so that no `GET` gains a destructive effect unnoticed

#### Scenario: A method the app never uses

- **WHEN** an `OPTIONS` or `HEAD` request is made to an `/api` path without the header
- **THEN** it is refused with `403 Forbidden`, which is a refusal either way — no route maps either method

