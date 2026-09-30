# Development

How to run BetterMAL without Docker, run the tests, and find your way around
the code.

## Running natively

Install:

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [Node.js 22](https://nodejs.org/) or newer
- PostgreSQL 17. The simplest way is to run only the database from Docker
  Compose:

  ```bash
  docker compose up -d postgres
  ```

Set up `.env` at the repo root as in the [README](../README.md#2-configure).
The backend and the frontend's dev server both read it.

Start the backend from its project folder, which is where it looks for `.env`:

```bash
cd backend/AnimeTracker.Api
dotnet run
```

It listens on `BACKEND_PORT` (5050) and applies database migrations when it
starts.

In a second terminal, start the frontend:

```bash
cd frontend
npm install
npm run dev
```

Vite serves the app on `FRONTEND_PORT` (5173) and passes `/api` calls on to the
backend. MyAnimeList's sign-in returns to that port, so open the app there.

## Tests and checks

The backend tests use xUnit with an in-memory database, so they need no
Postgres:

```bash
cd backend
dotnet test
```

The frontend's type check and build, and the linter:

```bash
cd frontend
npm run build
npm run lint
```

## Database migrations

The backend applies EF Core migrations when it starts. To add one after changing
the model, with the [`dotnet-ef`](https://learn.microsoft.com/ef/core/cli/dotnet)
tool installed:

```bash
cd backend/AnimeTracker.Api
dotnet ef migrations add <Name>
```

## Rebuilding the Docker images

`docker compose up -d --build` rebuilds whatever changed and leaves the database
alone. To rebuild one service, or to rebuild without the cache:

```bash
docker compose up -d --build backend
docker compose build --no-cache backend frontend && docker compose up -d
```

`backend/custom/` is copied into the backend image, so an edit to
`id-mapping.json` needs a rebuild too.

To start over with an empty database, which deletes everything the app holds:

```bash
docker compose down -v
```

## Project layout

```
backend/
  AnimeTracker.Api/         ASP.NET Core Web API
    Controllers/            the HTTP endpoints under /api
    Services/               the app's logic, one folder per area (Sync, Setup, Series, Airing, Tmdb, …)
    Data/                   EF Core context and repositories
    Models/                 database entities
    Migrations/             EF Core migrations
  AnimeTracker.Api.Tests/   xUnit tests
  custom/                   hand-made corrections to the id mapping
frontend/
  src/pages/                one component per page
  src/components/           shared UI
  src/api/                  calls to the backend
docker-compose.yml          postgres, backend and frontend
```

The C# projects and the database are named `AnimeTracker`.
