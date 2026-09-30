# BetterMAL

A self-hosted front end for your MyAnimeList list. It keeps your list in its own
database, adds the views MyAnimeList doesn't have (an airing calendar, whole
franchises in watch order, recaps and stats), and syncs every change back to
your MyAnimeList account.

It runs on your own computer with Docker, for one person, with no accounts of
its own.

## Features

- **Home**: what you're watching, what airs today, and this season at a glance.
- **Airing calendar**: a week grid of when the shows on your list air, with
  per-episode dates from AniList.
- **Seasons, years and Top Anime**: browse any season or year, or
  MyAnimeList's rankings, and add anime to your list from there.
- **Series**: each franchise in watch order on a timeline, with how far behind
  you are and your scores next to MyAnimeList's.
- **My list**: grouped by status, with sorting and filters by score, type and
  more.
- **Recap and Profile**: recap any season, year or run of years; see your
  stats, favourite seasons, where your scores differ most from MyAnimeList's,
  and rank your favourites.
- **Updates**: news about the shows on your list, such as a sequel announced, a
  start date or episode count released, or a broadcast moving.
- **Hide scores**: hide MyAnimeList's scores everywhere, so they don't sway
  yours.
- **Pictures**: choose any MyAnimeList picture for an anime or a series, or,
  with a TMDB key, a full-size poster or backdrop.
- **Two-way sync**: your edits reach MyAnimeList within seconds, and changes
  made elsewhere wait for your review before they're applied.
- **Files**: back up your list, and move your ranking, chosen pictures and edit
  history to another computer.

**Stack:** ASP.NET Core 10 (C#) with EF Core, PostgreSQL 17, React 19 with
TypeScript and Vite, Docker Compose.

## Quick start

You need [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or
another Docker with Compose) and a MyAnimeList account.

### 1. Create a MyAnimeList API app

1. Go to https://myanimelist.net/apiconfig and create an app with App Type
   **other**.
2. Set the **App Redirect URL** to `http://localhost:5050/callback`.
3. Save, and copy the **Client ID** and the **Client Secret**.

### 2. Configure

```bash
cp .env.example .env
```

Open `.env` and fill in `MAL_CLIENT_ID` and `MAL_CLIENT_SECRET`. Everything
else can stay as it is (see [Configuration](#configuration)).

### 3. Start it

```bash
docker compose up -d --build
```

Open http://localhost:5173 and press **Connect to MyAnimeList**. The first run
then reads your list and fetches the details of every anime on it, about one
anime a second, so a long list takes a while. You can close the tab in the
meantime: the app opens by itself when it's ready.

To update after pulling new changes, run the same command again.
`docker compose down` stops the app, and your data stays in a Docker volume.

## Optional: better pictures from TMDB

With a free TMDB API key, the picture picker also offers TMDB's full-size
posters and backdrops. See [guides/TMDB.md](guides/TMDB.md).

## Configuration

All settings live in `.env`, which is git-ignored.

| Variable            | What it is                                                     | Default            |
| ------------------- | -------------------------------------------------------------- | ------------------ |
| `MAL_CLIENT_ID`     | Your MyAnimeList app's Client ID                               | *(required)*       |
| `MAL_CLIENT_SECRET` | Your MyAnimeList app's Client Secret                           | *(required)*       |
| `TMDB_API_KEY`      | TMDB v3 API key, for TMDB pictures                             | *(empty)*          |
| `BACKEND_PORT`      | Port of the backend, and part of the redirect URL you gave MAL | `5050`             |
| `FRONTEND_PORT`     | Port you open the app on                                       | `5173`             |
| `POSTGRES_DB`       | Database name                                                  | `animetracker`     |
| `POSTGRES_USER`     | Database user                                                  | `animetracker`     |
| `POSTGRES_PASSWORD` | Database password; change it before the first start            | `devlocalpassword` |
| `POSTGRES_PORT`     | Port the database is reachable on from your computer           | `5434`             |

If you change `BACKEND_PORT`, change your MyAnimeList app's redirect URL to
match.

Everything listens on `localhost` only. The app has no login, so don't expose
it to a network.

## More

- [guides/SETTINGS.md](guides/SETTINGS.md): what syncs by itself, and what each
  action on the Settings page does.
- [guides/DEVELOPMENT.md](guides/DEVELOPMENT.md): running without Docker, the
  tests, and how the code is laid out.
- [backend/custom/README.md](backend/custom/README.md): correcting an anime's
  TMDB or IMDb id.

## Credits

- Anime data and your list: the
  [MyAnimeList API](https://myanimelist.net/apiconfig/references/api/v2).
- Episode air dates: the [AniList](https://anilist.co/) API.
- The MyAnimeList-to-TMDB/IMDb id mapping:
  [Fribb/anime-lists](https://github.com/Fribb/anime-lists).
- Pictures (optional): [TMDB](https://www.themoviedb.org/). This application
  uses TMDB and the TMDB APIs but is not endorsed, certified, or otherwise
  approved by TMDB.

BetterMAL is an independent project, not affiliated with or endorsed by
MyAnimeList or AniList.

## License

[MIT](LICENSE). The TMDB logo in `frontend/assets` is TMDB's trademark and isn't
covered by it. The services above have their own terms, which apply when you
use them through this app.
