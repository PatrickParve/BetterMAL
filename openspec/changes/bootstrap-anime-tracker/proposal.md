## Why

MyAnimeList's own site is the only way I currently browse and edit my anime list, and its UI doesn't work the way I want. I want a personal, single-user web app running locally that presents my list exactly how I like it, while keeping MyAnimeList as the canonical account it stays in sync with (import once, push edits back). This is also a portfolio piece, so it must be self-contained and deployable via Docker with no secrets committed.

## What Changes

- Introduce a greenfield application: ASP.NET Core Web API (C#) + EF Core + Postgres backend, React frontend, orchestrated with Docker Compose.
- Integrate the official MyAnimeList API v2 as the single external data source — public/read endpoints via `X-MAL-Client-ID`, user endpoints via OAuth2 (PKCE, `code_challenge_method=plain`).
- Make the local Postgres database the source of truth: no page read ever blocks on a live API call.
- One-time interactive MAL authorization, then a resumable, progressive first-boot import of my full list.
- Debounced, per-entry background sync that pushes local edits back to MAL, with a retry queue and a full-reconciliation safety net.
- Tiered, scheduled background refresh of cached metadata/scores plus on-demand refresh for a single anime.
- Seven main pages (Main, Airing, Season, Top anime, My list, Profile, Single anime), a global navbar with type-ahead search, a global hide/unhide MAL-score toggle, and an operational settings page.
- **Non-goals (explicitly out of scope):** authentication/login, multi-user, RBAC, a separate admin backend, public deployment, and any anime API other than MAL v2.

## Capabilities

### New Capabilities
- `mal-api-integration`: MAL API v2 client with dual auth (client-id for read endpoints, OAuth2 PKCE for user endpoints), one-time authorization flow, token persistence and refresh, and burst/403 throttling behavior.
- `data-persistence`: Postgres schema and EF Core model for AnimeMetadata (including genres, synopsis, background, and prequel/sequel relations), UserAnimeEntry, ActivityLog, OAuthToken, and persisted manual top-anime selections, establishing the local DB as the source of truth read by every page.
- `initial-import`: Resumable, progressively-inserted, paced first-boot import of the full MAL list with a visible progress indicator.
- `metadata-refresh`: Scheduled background refresh of cached metadata/scores using staleness tiers, plus on-demand single-anime refresh.
- `list-editing`: Editing of a user's anime entries (episode increment, status, score, rewatch count) via a shared modal overlay with start/complete date rules (no date fields in the editor), unknown-episode handling, add-to-list creation, and ActivityLog writes.
- `mal-write-sync`: Debounced per-entry push of local edits to MAL, retry-on-failure queue, manual "sync now", and weekly/manual full reconciliation.
- `main-dashboard`: Main page — Currently watching (horizontal carousel with plus-to-increment and next-episode countdown), Airing today (local-time filtered, with empty state), and Current season with filters and progress bars.
- `airing-schedule`: Weekly airing view of my list in local time, laid out as seven day-columns grouped by converted local broadcast day, with week navigation and empty-day/empty-week states.
- `season-browser`: Season page listing all anime airing in a selected season (live-fetched then cached), with filters, infinite scroll, and cards showing title, picture, episode count, and type.
- `library-views`: My list page (grouped by status with status filter tabs and quick-filters, MAL score column, per-row edit, ranked when sorted by score) and Top anime page (ranked global list with my/MAL scores and a conditional Add/Edit action per row).
- `profile-stats`: Profile page — anime stats, latest-updates activity feed with a full edit-history overlay, my top anime with default fill plus manual selection, all-anime score distribution, and opinion-divergence lists.
- `anime-detail`: Single anime page — layout with rank/MAL-score and my-score/rewatch boxes, info and synopsis/background boxes, prequel/sequel links when present, external MAL/AniList links, an overlay status editor with rewatch count, and on-demand refresh.
- `navigation-and-search`: Navbar layout (Home, Seasonal, Top, Airing, Profile, Settings gear) with center type-ahead search (local-first, live fallback) and clickable anime cards everywhere.
- `score-visibility`: Global hide/unhide MAL-score toggle that blurs every MAL score with per-score, non-persisted reveal.
- `deployment`: Docker Compose stack (Postgres + backend + frontend), named volume for DB data, `restart: unless-stopped`, and gitignored `.env` secrets.

### Modified Capabilities
<!-- None — this is a greenfield project; no existing specs to modify. -->

## Impact

- **New codebase:** backend (ASP.NET Core Web API, EF Core), frontend (React), and infrastructure (Docker Compose, Postgres).
- **External dependency:** official MyAnimeList API v2 (registered app at myanimelist.net/apiconfig).
- **Configuration/secrets:** `.env` (MAL client ID/secret, tokens, DB connection) — gitignored; repo intended to be public.
- **Persistent state:** Postgres named volume holding all list data, cached metadata (including genres, synopsis, background, and prequel/sequel relations), manual top-anime selections, activity log, and OAuth tokens.
- **Background processing:** hosted/scheduled services for import, write-sync, retry, and metadata refresh.
