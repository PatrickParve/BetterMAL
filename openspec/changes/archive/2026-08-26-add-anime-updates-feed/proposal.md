## Why

Franchise news reaches my list today only if I happen to open the right detail page. `MetadataRefreshService` already writes a `RelationDiscovery` row every time a newly-announced sequel appears in a my-list anime's relation set — its own doc comment calls it "the write side of a future 'updates' view (this change writes these; nothing reads them yet)". Nothing reads them, so a sequel announced for a show I am watching is invisible until I stumble onto it.

Two gaps keep that log from being readable news. Announced anime are almost never in my list, and the tiered refresh is my-list-only, so once an announcement is recorded the anime is never fetched again and its episode count and premiere date never arrive. And `not_yet_aired` currently shares the 1-day tier with `currently_airing`, which is both wasteful for a show premiering next year and blind to the fact that a show premiering next week is the one worth watching closely.

## What Changes

- **A new "Updates" section on the Home page**, between "Currently watching" and the Airing today / Followed shows airing row. Full page width, one horizontally-scrolling row of horizontal cards (image left, text right), newest leftmost. Each card shows the anime's picture, title, its episode count when known, and its premiere date when known, plus what the update is and which of my shows it is affiliated with. Empty-state text when there is nothing. A button opens the full updates history overlay.
- **Six kinds of update are recorded**, in two families. Facts *becoming known*: a new anime **announced** as related to one of my shows, its **total episode count** becoming known, its **premiere date** becoming known. And the schedule *moving*: a **premiere date changed**, a **broadcast slot changed**, and **episodes moved** — an upcoming episode's air date shifting, the break-week case.
- **Facts that become known at the same moment form one card, not three.** A sequel announced with its episode count and premiere date already attached is one "announced" card carrying both. A count released three weeks after the announcement is its own, later card.
- **News about my own entries and about their franchises is shown.** An update is eligible when the anime it concerns is itself a non-Dropped entry of mine, or is connected by a story relation to one. The first clause matters for an anime I found somewhere and added before it had any data — its count and date are exactly what I am waiting for, franchise or not. Eligibility is evaluated at read time, so dropping a show retires its news without a cleanup pass, and adding one surfaces news already recorded.
- **The Home section shows the last 30 days only**; older updates leave the row but are kept and remain in the history overlay.
- **An updates history overlay**, matching the profile page's Full edit history overlay: whole log newest-first, with a title search and a from/to date filter.
- **A considered staleness ladder for anime that have not aired**, replacing the blanket 1-day tier: daily inside 30 days of a known premiere (or once a premiere date has passed while MAL still says `not_yet_aired`), weekly when the premiere is further out, and every 3 days when no premiere date is known at all — the missing date being both the most valuable fact outstanding and the one with no signal to time the wait against.
- **BREAKING (spec-level)**: the tiered refresh stops being strictly my-list-only. It gains one narrow carve-out — anime that have not aired and are story-related to a non-dropped my-list entry — because without it an announcement can never mature into an episode count or a premiere date. Finished and currently-airing anime outside my list remain excluded, so an anime leaves the carve-out the moment it starts airing.
- Newly-discovered relation edges are resolved by fetching the related anime once, and an announcement is recorded **only when that anime has not finished airing** — a relation MAL adds to a finished 2005 OVA is a data correction, not news.
- Schedule changes are recorded **only while an anime has not finished airing**, and an episode counts as moved only when its **local calendar day** changes — so MAL tidying an old show's records, and AniList nudging an air time by twenty minutes, are neither of them news.
- An anime's **total episode count changing** between two known values stays out: it is MAL bookkeeping (specials recounted, cours merged), and nothing a viewer does depends on it until the show is over.
- Relation discoveries recorded before this change ships are marked processed and announce nothing, the same way the baseline import records no activity.

## Capabilities

### New Capabilities
- `anime-updates`: what counts as an update, how simultaneous facts group into one, which updates are eligible to be shown, how announcements are resolved, the Home section's 30-day window and layout, and the history overlay.

### Modified Capabilities
- `metadata-refresh`: the not-yet-aired staleness ladder replaces the shared 1-day tier; the scheduled job's candidate set gains unaired anime story-related to a non-dropped my-list entry; newly-discovered related anime get a one-time resolving fetch under the existing pacer and daily cap.
- `main-dashboard`: the Home page gains the Updates section, positioned below Currently watching and above the Airing today / Followed shows airing row.

## Impact

- **Backend models/schema**: new `AnimeUpdate` entity and table, carrying the previous premiere date, previous broadcast day/time, and moved-episode number with its two dates; `RelationDiscovery` gains a `ProcessedAt` column; migration backfills every existing discovery as processed.
- **Backend services**: `Services/Metadata/RefreshTiers` (not-yet-aired ladder), `MetadataRefreshService` (candidate set, plus count/date/slot detection on both the batch and on-demand paths), `MetadataRefreshBackgroundService` (announcement resolution shares the tick, pacer, and daily cap), `Services/Airing/EpisodeScheduleRefreshService` (moved-episode detection around the per-episode row replace); new `Services/Updates/` for detection, eligibility and read models.
- **Backend API**: `GET /api/dashboard` gains an `updates` array; new `GET /api/updates/history`.
- **Frontend**: new `UpdatesSection` and `UpdatesHistoryOverlay` components plus CSS, wired into `HomePage`; `api/types.ts` and `api/client.ts` gain the new DTOs and calls.
- **Docs**: `CODE_GUIDE.md` sections 2, 3 (`Services/Metadata/`), 4 (`components/`) and the background-jobs table.
- No change to MAL write sync, list editing, or the activity log.
