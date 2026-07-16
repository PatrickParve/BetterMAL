# Anime Tracker

A personal anime-tracking web app that replaces/augments MyAnimeList's own UI
with one built exactly to taste. It's a **single-user, local-machine app**:
no accounts, no auth, no multi-tenancy — every page *is* the editing
interface. Your list lives in this app's own Postgres database as the source
of truth, and stays synced with your real MyAnimeList account in both
directions (initial import from MAL, ongoing edits pushed back to MAL).

**Stack:** ASP.NET Core (C#) + EF Core API, Postgres, React + TypeScript
frontend, all wired together with Docker Compose.

## Prerequisites

Pick one of the two setups below.

**Docker (recommended)** — this is the only setup you need for this option:

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (includes
  Docker Compose)

**Running services natively without Docker** — install all of:

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/) (ships with npm)
- [PostgreSQL 17](https://www.postgresql.org/download/) (or run just the
  `postgres` service from Docker Compose and run backend/frontend natively —
  see below)

Check what you already have installed:

```bash
docker --version && docker compose version
dotnet --version
node --version && npm --version
```

## 1. Get a MyAnimeList API key

The app talks to the official MyAnimeList API v2, which requires your own
app credentials:

1. Log into MyAnimeList and go to https://myanimelist.net/apiconfig
2. Create a new app (App Type: **other**).
3. Set the **App Redirect URL** to `http://localhost:5000/callback` (the port
   must match `BACKEND_PORT` in your `.env` — 5000 is the default).
4. Save, then copy the generated **Client ID** and **Client Secret**.

The app runs without these, but the MAL connect/import/sync flow will fail
until they're set.

## 2. Configure environment variables

Copy the example file and fill in your values:

```bash
cp .env.example .env
```

```dotenv
# --- MyAnimeList API ---
MAL_CLIENT_ID=            # from myanimelist.net/apiconfig
MAL_CLIENT_SECRET=

# --- Ports exposed on localhost ---
BACKEND_PORT=5000         # must match the redirect URI registered with MAL above
FRONTEND_PORT=5173

# --- Postgres ---
POSTGRES_DB=animetracker
POSTGRES_USER=animetracker
POSTGRES_PASSWORD=changeme   # pick your own password
POSTGRES_PORT=5434
```

`.env` is gitignored and must never be committed — it's the only place
secrets live.

## 3. Run it

### Option A: Docker Compose (recommended)

From the repo root:

```bash
docker compose up -d --build
```

This builds and starts three containers: `postgres`, `backend`, and
`frontend`. The backend applies EF Core migrations automatically on startup,
so the database schema is created for you — no manual migration step needed.

- Frontend: http://localhost:5173
- Backend API: http://localhost:5000
- Postgres: exposed on `localhost:5434` (for a local DB client, if wanted)

All three services use `restart: unless-stopped`, so they come back up
automatically whenever Docker restarts (e.g. on machine boot). Data persists
across rebuilds in the named `postgres-data` volume.

To stop everything:

```bash
docker compose down
```

To view logs:

```bash
docker compose logs -f backend   # or frontend / postgres
```

#### Rebuilding the Docker images

Whenever you change backend or frontend code, the running containers are
still using the old image until you rebuild. From the repo root:

```bash
docker compose up -d --build
```

This rebuilds any service whose source changed and recreates its container,
leaving `postgres` (and its data volume) untouched. It's safe to run any
time, including with no changes — Docker just reuses cached layers.

To target a single service instead of rebuilding everything:

```bash
docker compose build backend    # or frontend
docker compose up -d backend    # recreate just that container with the new image
```

To force a full rebuild with no cached layers (useful if you suspect a stale
layer, e.g. after a base-image update):

```bash
docker compose build --no-cache backend frontend
docker compose up -d
```

Notes:
- These commands build the image using each service's `Dockerfile`
  (`backend/AnimeTracker.Api/Dockerfile`, `frontend/Dockerfile`) — you never
  need to run `docker build` by hand.
- `docker compose build` sends your source as a build context (no bind
  mount), so it works the same everywhere `docker compose` runs, regardless
  of where the repo lives on disk.
- The backend image targets .NET 10 and is compiled *inside* the build
  stage, so you don't need the .NET 10 SDK installed locally to build it —
  only Docker.

### Option B: Run natively (faster iteration during development)

Start just Postgres via Docker (simplest way to get a matching database):

```bash
docker compose up -d postgres
```

Then run the backend (applies migrations automatically on startup, listens
on port 5000 using `appsettings.Development.json`, which already points at
the `postgres` container's exposed port):

```bash
cd backend/AnimeTracker.Api
dotnet run
```

In a separate terminal, run the frontend (Vite dev server on port 5173,
proxying `/api` calls to `localhost:5000` per `vite.config.ts`):

```bash
cd frontend
npm install
npm run dev
```

## 4. Connect your MyAnimeList account

On first run, open the app (http://localhost:5173) and use the "Connect to
MAL" prompt/settings page — it kicks off a one-time OAuth flow at
`/api/mal-auth/start`. Once authorized, the app pulls your existing list from
MAL and keeps it synced from then on.

## Environment variable reference

| Variable              | Description                                                              | Default        |
| ---------------------- | ------------------------------------------------------------------------- | -------------- |
| `MAL_CLIENT_ID`        | MyAnimeList API app Client ID                                            | *(required)*   |
| `MAL_CLIENT_SECRET`    | MyAnimeList API app Client Secret                                        | *(required)*   |
| `BACKEND_PORT`         | Host port for the backend API; also the OAuth redirect port              | `5000`         |
| `FRONTEND_PORT`        | Host port for the frontend (Docker only)                                 | `5173`         |
| `POSTGRES_DB`          | Postgres database name                                                   | `animetracker` |
| `POSTGRES_USER`        | Postgres user                                                            | `animetracker` |
| `POSTGRES_PASSWORD`    | Postgres password                                                        | *(required)*   |
| `POSTGRES_PORT`        | Host port Postgres is exposed on                                        | `5434`         |

## Project structure

```
backend/    ASP.NET Core Web API (C#), EF Core migrations, MAL integration
frontend/   React + TypeScript app (Vite)
```

## Notes

- This app is designed for personal, local-only use — it has no
  authentication layer, since it assumes a single trusted user on their own
  machine. It is not intended to be deployed publicly as-is.
- MAL enforces no fixed rate limit but throttles bursts; the app paces and
  caches requests to stay well under any practical threshold.
