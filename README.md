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
3. Set the **App Redirect URL** to `http://localhost:5050/callback` (the port
   must match `BACKEND_PORT` in your `.env` — 5050 is the default).
4. Save, then copy the generated **Client ID** and **Client Secret**.

The app runs without these, but the MAL connect/import/sync flow will fail
until they're set.

## Optional: a TMDB API key (better pictures)

MyAnimeList only publishes its own pictures, which are fairly small. With a
free [TMDB](https://www.themoviedb.org/) API key, the **Choose picture** picker
on an anime's or a series' page also offers TMDB's full-resolution posters and
backdrops — alongside MAL's own, in labelled sections — and any of them can be
picked as the picture.

1. Create a free account at https://www.themoviedb.org/ and open
   **Settings → API** (https://www.themoviedb.org/settings/api).
2. Request an API key for personal, non-commercial use, and copy the **API Key**
   — the short v3 key, not the long "API Read Access Token".
3. Set it as `TMDB_API_KEY` in your `.env` (next step), then restart the backend.

It's entirely optional. Leave it empty and the app runs exactly as it does
without it, making no TMDB request at all. **IMDb links** on the anime and
series pages work either way: they come from a separate community-maintained
id mapping the backend downloads once a week, which needs no key.

TMDB's terms allow free use for non-commercial projects, and each installation
needs its own key — this app isn't meant to be shared with one baked in. In
return TMDB requires its logo and a notice to be shown in the app. The picker
shows them wherever it offers TMDB images, and the **Credits** group at the
bottom of the Settings page always does:

<img src="frontend/assets/TMDB_Logo.svg" alt="The Movie Database (TMDB)" height="20">

*This application uses TMDB and the TMDB APIs but is not endorsed, certified, or
otherwise approved by TMDB.*

TMDB's terms also limit how long what it sends may be kept, so the app deletes a
cached list of pictures once its last fetch is more than about five months old,
and fetches it again the next time a page needs it. A list you open is refreshed
after 30 days, so only lists nobody opened get that old. A picture you have
**picked** is not part of that list: it is stored on the anime or series itself,
so it stays, and keeps showing everywhere, whatever happens to the list it came
from.

## Fixing a missing or wrong TMDB or IMDb id

The ids come from a community-maintained mapping the backend downloads once a
week, and it has gaps: a brand-new show may have no TMDB or IMDb id yet, and a few
entries name the wrong TMDB season. `backend/custom/id-mapping.json` is where
you fix that. The app reads it on top of the downloaded mapping. Run natively, an
edit applies within seconds; with Docker the folder is copied into the backend
image, so apply an edit with `docker compose up -d --build`.

By default an entry only fills a gap, and gives way as soon as the downloaded
mapping has the ids. With `"override": true` it wins even where the source has a
(wrong) value. [`backend/custom/README.md`](backend/custom/README.md) has the
format and how to find the ids.

## 2. Configure environment variables

Copy the example file and fill in your values:

```bash
cp .env.example .env
```

```dotenv
# --- MyAnimeList API ---
MAL_CLIENT_ID=            # from myanimelist.net/apiconfig
MAL_CLIENT_SECRET=

# --- TMDB (optional) ---
TMDB_API_KEY=             # v3 API key from themoviedb.org → Settings → API; leave empty to run without TMDB

# --- Ports exposed on localhost ---
BACKEND_PORT=5050         # must match the redirect URI registered with MAL above
FRONTEND_PORT=5173

# --- Postgres ---
POSTGRES_DB=animetracker
POSTGRES_USER=animetracker
POSTGRES_PASSWORD=devlocalpassword   # only guards the local database; set before your first `docker compose up`
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
- Backend API: http://localhost:5050
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

Then run the backend. It applies migrations automatically on startup and
listens on port 5050, or `BACKEND_PORT` from `.env`. Run this way, it reads
the same repo-root `.env` Docker Compose reads, so the MAL credentials, the
optional TMDB key and the Postgres settings apply with no extra setup, connecting to the
`postgres` container on `localhost:<POSTGRES_PORT>`. It looks for `.env` two
levels up from where it's started, so run it from `backend/AnimeTracker.Api`
as shown below — without one, it falls back to
`appsettings.Development.json`, which matches `.env.example`'s defaults, but
MAL won't connect:

```bash
cd backend/AnimeTracker.Api
dotnet run
```

In a separate terminal, run the frontend (Vite dev server on port 5173,
proxying `/api` calls to `localhost:5050`, or `BACKEND_PORT` from `.env`, per
`vite.config.ts`):

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
| `TMDB_API_KEY`         | TMDB v3 API key — enables TMDB pictures in the picker (optional)         | *(optional)*   |
| `BACKEND_PORT`         | Host port for the backend API; also the OAuth redirect port              | `5050`         |
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
- The optional TMDB integration only calls TMDB when a page needs a set of
  pictures it hasn't cached yet (then again after 30 days, or when you press
  **Refresh data**), never as a background sweep. A cached list of pictures that
  has gone about five months without a refresh is deleted (TMDB's terms limit how
  long its data may be kept) and fetched again if a page needs it; a picture you
  picked is not affected. TMDB's images are shown straight from its own CDN at
  full resolution, so the picker keeps each group of them closed until you open
  it. This application uses TMDB and the TMDB APIs but is not endorsed,
  certified, or otherwise approved by TMDB.
