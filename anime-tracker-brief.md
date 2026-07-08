# Personal Anime Tracker — Project Brief

## Purpose & Scope

A web app for tracking my own anime watching, replacing/augmenting MyAnimeList's
own site with a UI built exactly the way I want it. **Single user, local machine
only, no plan to ever deploy publicly.** No authentication, no multi-user
support, no admin panel — every page in this app *is* the editing interface,
since there's only one person (me) who ever uses it.

My list data should live in this app's own Postgres database as the source of
truth, kept in sync with my real MyAnimeList account (both directions:
initial import pulls from MAL, ongoing edits push back to MAL).

---

## Tech Stack

- **Backend:** ASP.NET Core Web API (C#) + EF Core, Postgres
- **Frontend:** React (single-user app, no auth/session complexity needed)
- **Deployment:** Docker Compose (Postgres + backend + frontend containers),
  `restart: unless-stopped` on each service so everything comes back up
  automatically when the PC boots and Docker starts, and shuts down cleanly
  with the PC. Named volume for the Postgres data directory so data survives
  container rebuilds. Secrets (MAL client ID/secret, tokens) via `.env`,
  gitignored — this project will likely end up in a public repo as a
  portfolio piece, so secrets must never be committed.
- No RBAC, no roles, no separate admin backend — explicitly out of scope
  (see Non-Goals).

---

## External API Strategy

**Use only the official MyAnimeList API v2.** No Jikan, no other anime API —
one integration, one ID scheme, one response shape to normalize, and it's the
only API that supports writing back to my real list.

Two auth tiers on this API, used differently:

- **Public/read endpoints** (anime search, season lists, ranking/top anime,
  anime details) — require only the `X-MAL-Client-ID` header. No OAuth token
  needed for these at all.
- **User-specific endpoints** (get/update/delete my list entries) — require
  a full OAuth2 (PKCE) bearer token tied to my account.

OAuth setup: register an app at myanimelist.net/apiconfig, get a Client ID
(+ secret), redirect URI can be `http://localhost:{port}/callback` since this
never leaves my machine. Known gotcha: MAL's PKCE implementation expects
`code_challenge_method=plain`, not the `S256` most OAuth libraries default to
— must be set explicitly or the flow silently fails. Access tokens are
long-lived (~31 days), refresh tokens longer — token refresh is a background
concern, not a per-request one.

No published rate limit; MAL throttles bursts with a 403 rather than a fixed
quota. The caching strategy below keeps normal usage nowhere near whatever
that threshold actually is — live API calls should be rare, small, and paced.

---

## Data Model (entities needed)

Exact schema is an implementation detail, but the app needs at minimum:

- **AnimeMetadata** — cached copy of MAL data per anime: id, title, picture,
  MAL score, type (TV/movie/etc), status (airing/finished/not yet aired),
  total episodes (nullable — null/unknown means still airing), aired-from/to
  dates, studio, broadcast day + time (JST), popularity rank,
  `last_synced_at`, `last_score_synced_at` (see staleness tiers below).
- **UserAnimeEntry** — my relationship to an anime: status (watching, on
  hold, plan to watch, completed, dropped), episodes watched, my score,
  started date, completed date, rewatch count, `pending_sync` flag,
  `last_synced_at`.
- **ActivityLog** — timestamped record of changes (status changes, episode
  increments, score changes) — feeds the "Latest updates" feed on the
  profile page. Not optional — there's no other way to reconstruct a
  chronological feed from current-state-only columns.
- **OAuthToken** — access token, refresh token, expiry, stored in Postgres
  (not just in-memory/container filesystem) so it survives container
  restarts/rebuilds.

---

## Initial Data Load (First Boot)

No rush requirement — first boot pulls everything live via the API at a
conservative pace rather than seeding from a MAL export file.

1. One-time interactive OAuth authorization (visit MAL's authorize URL,
   log in, get redirected to the local callback) — this is the only manual
   step in the whole process.
2. Background job fetches my full list via `GET /v2/users/@me/animelist`,
   paginated (~100 entries/page).
3. For each list entry, fetch full anime metadata (picture, studio, aired
   dates, broadcast info, score) and **insert into Postgres as each fetch
   completes** — not batched at the end. This means the UI can render
   partially-populated and fill in progressively while the sync is still
   running, rather than showing nothing until it's fully done.
4. Pace requests conservatively (e.g. roughly 1 request/second) — a full
   library of a few hundred anime should take low minutes, not hours.
5. **Resumable**: if the app or PC restarts mid-sync, skip anime already
   present in `AnimeMetadata` and continue rather than restarting from zero.
6. Show a simple visible progress indicator during this first sync
   (e.g. "142 / 380 synced") so it doesn't look broken while running.

After this first sync, all page reads come from Postgres — no page should
ever block on a live API call during normal use.

---

## Ongoing Sync — Local Edits → MAL

Debounced, per-entry, not global:

- When a list entry changes (episode count, status, score, rewatch count),
  mark it `pending_sync = true` and (re)start a short timer (~5-10 seconds)
  scoped to that specific anime.
- Any further edit to that *same* anime resets its timer — so incrementing
  an episode count five times in quick succession results in one API push,
  not five.
- Edits to different anime don't block on each other's timers.
- When a timer expires with no further edits, push just that entry to MAL
  via `PATCH /v2/anime/{id}/my_list_status` with whatever changed.
- **Failure handling:** if a push fails (offline, MAL down, token expired),
  leave `pending_sync = true` and let a background retry job pick it up
  later — never silently drop a change.
- Optional manual "sync now" — processes the pending queue immediately
  instead of waiting out the debounce, for "about to close the laptop"
  moments. Only ever touches pending/changed entries, never the whole list.
- **Full reconciliation** (weekly, or manual button) — pulls the complete
  list from MAL and diffs against local data. Not part of normal operation;
  a safety net for catching drift if I ever edit directly on MAL's own
  site/app instead of through this one.

---

## Metadata & Score Refresh (Staleness Strategy)

Never call the API live from a page render. A scheduled background job
(nightly is enough) refreshes cached data based on tiered staleness
thresholds, since a score/episode-count for an actively airing show moves
much faster than one for a show that finished a year ago:

- **Currently airing, or finished within the last ~2 months:** refresh every
  1-2 days (episode counts, broadcast times, and scores are still moving).
- **Everything else:** refresh weekly or monthly — effectively static data.

Only request the specific fields needed for a refresh (e.g. just `mean`
score) to keep each call cheap. Spread refreshes across the night in small
batches rather than bursting.

**Exception — single anime detail page:** offer an on-demand "refresh" action
here specifically, since it's one API call for one anime at the exact moment
I'm looking at it, rather than N calls for the whole list.

---

## Timezone Handling

MAL/the API returns broadcast day + time in JST. Convert to local (Finland)
time for display everywhere (Main page "Airing today," Airing page weekly
view). The weekly Airing page groups shows by their **converted local day**,
not the raw JST day.

---

## Pages

### Main page
- **Currently watching** — click an entry to increment episodes watched
  (triggers the started-date / debounced-sync logic below).
- **Airing today** — only anime in my list, filtered by broadcast day
  converted to local time.
- **Current season** — only anime in my list that are airing this season.
  Filterable by popularity, MAL score, alphabetical, my score. Progress bar
  at the bottom of each card showing episode progress (watched / total, or
  watched / `?` if total is unknown — the `?` is used everywhere the total
  episode count is unknown, not just here).

### Airing page
- Week view showing time slots for anime in my list, by title + episode
  number, in local time (converted from JST broadcast data).
- Can navigate to other weeks.

### Season page
- All anime airing in the selected season (not just my list) — pulled live
  from the API on first visit to that season, then cached.
- Can change season.
- Picture + title per card.
- Filterable: popularity, score, alphabetical, my score (only meaningful
  for entries that are also in my list).
- Infinite scroll.

### Top anime page
- Ranked list: number, picture, title, my score, MAL score (right-aligned).

### My list page
- One list — grouped and ordered: Currently watching → On hold →
  Plan to watch → Completed → Dropped.
- Each entry: picture, title, type (TV/movie), progress, my score.
- If sorted by score, show rank numbers on the left.

### Profile page
- **Anime Stats:** Days, Mean Score, Watching, Completed, On-Hold, Dropped,
  Plan to Watch, Total Entries, Rewatched, Episodes — all computed from the
  local DB.
- **Latest updates** — feed from the ActivityLog table: additions, status
  changes, completions, episode-count increases, most recent first.
- **Top anime (mine):** highest-rated anime, minimum 10 shown.
  - Logic: take all anime I've scored 10. If there are 10 or more, show all
    of them (no cap at 10). If there are fewer than 10, fill the remainder
    up to 10 with my next-highest-scored anime, ties broken alphabetically.
- **All anime stats:** count of anime per score/rating value, plus overall
  mean score.
- **Opinion divergence:** two lists comparing my score against MAL's score
  for anime I've rated.
  - *"They liked it, I didn't"* — my score is 3 or more points below the
    MAL score.
  - *"I liked it, they didn't"* — MAL score is below 7 **and** my score is
    at least 2 points higher than the MAL score.

### Single anime page
- Picture, title, MAL score (respecting the hide/unhide toggle), my status,
  type, aired-from/to, studio, and links out to the anime's MAL and AniList
  pages (built as URL templates from the MAL id — no API call needed for
  these two links).
- Status editor here includes the rewatch-count field.
- On-demand "refresh" action for this anime's cached data (see staleness
  section above).

---

## Navbar

- **Search** (center): type-ahead, debounced, queries local cache first,
  falls back to a live API search only for titles not yet cached. Dropdown
  shows up to 5 matches with picture + title.
- **Left buttons:** Home, Seasonal anime, Top anime, Airing.
- **Right buttons:** Profile, hide/unhide MAL score toggle (global).

---

## Global Features

- **Hide/unhide MAL score toggle:** global switch. When hidden, every MAL
  score everywhere is replaced with a blur that doesn't leak the value.
  Each blurred score has its own small "unhide" control that reveals just
  that one score in place; navigating away re-hides it (the unhidden state
  is not persisted).
- **Anime cards are clickable everywhere** they appear, linking to that
  anime's view page.

---

## Core Business Logic Rules

- **Start/complete date logic:** if I set episodes-watched on an anime that
  was previously at 0 or not yet in my list, set `started_at` to today.
  `completed_at` is set only when status is explicitly changed to
  Completed. If status is changed *away* from Completed to anything else,
  clear `completed_at`.
- **Unknown total episodes:** displayed as e.g. `26/?`. An anime with an
  unknown total episode count cannot be marked Completed (since "complete"
  is undefined until the total is known).
- **Rewatch count:** editable field in the status editor, independent of
  watch status.
- Every field change described above should also write an ActivityLog entry
  and trigger the debounced MAL sync described earlier.

---

## Settings / Utility Page

Not an admin panel — a small operational page for plumbing, not content:

- Sync status: which entries are pending, last successful sync time, any
  failed/retrying entries.
- Manual "resync now" / full reconciliation trigger.
- Re-authorize with MAL (in case the refresh token ever becomes invalid).
- Force-refresh a specific anime's cached metadata on demand.

---

## Explicit Non-Goals

- No authentication/login system, no multi-user support, no RBAC — this is
  a single-user local app.
- No separate admin backend UI — every list page already is the editing
  surface.
- No public deployment plan — local Docker Compose only.
- No use of Jikan or any API other than the official MyAnimeList API v2.
