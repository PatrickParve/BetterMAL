## Why

Six rough edges, all in the "the data or the plumbing is already there, the app just doesn't use it" category. Adding a finished show to my list never fetches its episode airing dates, so its aired count stays unknown until the next full backfill happens to sweep it up. A movie's runtime reads as "115 min" instead of "1h 55min". The schedule page can only be moved a week at a time or a month at a time through the date picker's own arrows — reaching a different year takes dozens of clicks. The detail page offers "Add to watching" for an anime that is already being watched. The More overlay's media-type backfill only starts when the overlay opens, so the first thing you see on a big franchise is a wall of "Unknown". And when the backend goes down mid-session, every page silently keeps showing stale data with no indication anything is wrong.

## What Changes

**Airing data coverage**

- Adding *any* anime to my list triggers an immediate airing-data fetch for it, not just one whose MAL airing status is currently-airing or not-yet-aired. A finished show gets its full episode history on add, the same data the one-time backfill would eventually give it.

**Anime detail page**

- Durations of 60 minutes or more render as hours and minutes (`1h 55min`, `2h`) rather than a large minute count. The existing `/ep` suffix rule is unchanged, so a long-episode series reads `1h 5min/ep`.
- **Add to watching** is hidden when the anime's status is already Watching, joining Completed and Dropped. An anime being watched shows only **Edit** and **Refresh data**.
- The related-anime media-type backfill starts when the detail page loads, not when the **More** button is clicked, so media types are usually already resolved by the time the overlay opens. The backfill only runs when at least one related entry has no cached media type, so a fully-cached anime costs nothing extra.

**Airing page**

- The "Jump to week" date input is replaced by month and year dropdowns, making any week of any year two clicks away instead of one arrow-press per month. Changing only the year keeps the same point in the year.

**Connection status**

- A dismissible notice appears whenever the app can't reach its backend — a failed network request or a server error — and clears itself once a request succeeds again. While it's up, the app polls the health endpoint so the notice disappears on its own when the backend comes back, without needing a page interaction.

## Capabilities

### New Capabilities

- `connection-status`: app-wide detection and display of "the backend is unreachable", covering which failures count, how the notice behaves, and how it clears.

### Modified Capabilities

- `episode-airing-data`: the add-to-list refresh trigger fires for every anime added, not only currently-airing/not-yet-aired ones.
- `anime-detail`: duration renders in hours and minutes past the hour mark; "Add to watching" is hidden for a Watching entry as well as Completed/Dropped; the related-anime media-type backfill is triggered on page load rather than on overlay open.
- `airing-schedule`: week navigation gains month and year jump controls in place of the date picker.

## Impact

**Backend**

- `Services/Entries/UserAnimeEntryEditService.cs` — drop the airing-status condition on the add-triggered airing refresh.
- No schema change, no new endpoints. `GET /api/health` already exists and is what the connection poll uses.

**Frontend**

- `pages/AnimeDetailPage.tsx` — duration formatting, the hidden-button condition, and moving the related-anime backfill into a load-time effect.
- `pages/AiringPage.tsx` + `.css` — month/year jump controls replacing the date input.
- New `api/connectionStatus.ts` (a subscribable up/down store) wired into `api/client.ts`'s two fetch helpers, plus a new `components/ConnectionStatusNotice.tsx` mounted in `AppShell.tsx`.

**External dependencies**

- None. No new API integrations; the wider add-trigger uses the AniList client and its existing request pacing.
