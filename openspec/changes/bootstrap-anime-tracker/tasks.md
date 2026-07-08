## 1. Project scaffolding & deployment (`deployment`)

- [x] 1.1 Create repo layout: `/backend` (ASP.NET Core Web API), `/frontend` (React), `/docker` or root `docker-compose.yml`
- [x] 1.2 Add `.gitignore` (ignore `.env`, build artifacts) and a `.env.example` documenting MAL client ID/secret, callback port, and DB connection — no real values
- [x] 1.3 Write `docker-compose.yml` with `postgres`, `backend`, `frontend` services, each `restart: unless-stopped`
- [x] 1.4 Add a named volume for the Postgres data directory; wire the DB connection string from `.env`
- [x] 1.5 Add Dockerfiles for backend and frontend; verify `docker compose up` brings up all three services and the app is reachable locally
- [x] 1.6 Confirm data survives a `docker compose down` + rebuild (volume persistence check)
- [x] 1.7 On startup, detect first-run state (no valid OAuthToken present); if so, surface a "Connect to MAL" prompt/button that starts the OAuth flow, rather than auto-starting anything.

## 2. Data layer & persistence (`data-persistence`)

- [x] 2.1 Add EF Core + Npgsql; configure `DbContext` and Postgres connection
- [x] 2.2 Define `AnimeMetadata` entity (id, title, picture, MAL score, type, status, total episodes nullable, aired-from/to, studio, broadcast day/time JST, popularity rank, `last_synced_at`, `last_score_synced_at`)
- [x] 2.3 Define `UserAnimeEntry` entity (status, episodes watched, my score, started/completed dates, rewatch count, `pending_sync`, `last_synced_at`)
- [x] 2.4 Define `ActivityLog` entity (timestamp, anime ref, change type, change detail)
- [x] 2.5 Define `OAuthToken` entity (access token, refresh token, expiry)
- [x] 2.6 Create initial EF Core migration; apply migrations automatically on backend startup
- [x] 2.7 Establish read repositories/services so all page reads come from Postgres (no live API on read path)

## 3. MAL API integration & auth (`mal-api-integration`)

- [x] 3.1 Implement typed `IMalClient` with read methods (attach `X-MAL-Client-ID`) and user methods (attach `Authorization: Bearer`)
- [x] 3.2 Add a delegating handler for per-endpoint auth selection, request pacing, and `403` backoff-and-retry
- [x] 3.3 Implement one-time interactive OAuth2 PKCE flow: build authorize URL with `code_challenge_method=plain`, handle `http://localhost:{port}/callback`, exchange code for tokens
- [x] 3.4 Persist tokens to the `OAuthToken` table; load them on startup so they survive restarts
- [x] 3.5 Implement background token refresh using the refresh token ahead of expiry
- [x] 3.6 Add read wrappers: anime search, season list, top/ranking, anime details (client-id only)
- [x] 3.7 Add user wrappers: get `@me` animelist (paginated), `PATCH`/`DELETE` `my_list_status` (bearer token)

## 4. Initial import (`initial-import`)

- [x] 4.1 Implement a hosted background import job triggered after successful authorization
- [x] 4.2 Page through `GET /v2/users/@me/animelist` (~100/page)
- [x] 4.3 For each entry, fetch full metadata at ~1 req/s and insert into Postgres as each completes (progressive)
- [x] 4.4 Make import resumable by skipping anime already present in `AnimeMetadata`
- [x] 4.5 Expose import progress (synced / total) via an endpoint and show a visible indicator in the UI — endpoint done (`GET /api/import/status`); the UI indicator itself is blocked on section 9 (frontend shell doesn't exist yet)

## 5. List editing & business rules (`list-editing`)

- [x] 5.1 Implement entry-edit service (episode count, status, score, rewatch count)
- [x] 5.2 Set `started_at` = today when episodes move above 0 from 0/new; do not overwrite an existing start date
- [x] 5.3 Set `completed_at` only on status → Completed; clear it when status changes away from Completed
- [x] 5.4 Enforce that anime with unknown total episodes cannot be marked Completed; expose `watched/?` display data
- [x] 5.5 Make rewatch count editable independently of status
- [x] 5.6 On every tracked change, write an `ActivityLog` row and trigger the debounced sync — sync trigger is a minimal seam (`IEntrySyncScheduler` / `DebouncedEntrySyncScheduler`, ~7s debounce + push, covering 6.1/6.2); the retry queue, manual "sync now", and reconciliation from section 6 are not implemented

## 6. Write-back sync to MAL (`mal-write-sync`)

- [x] 6.1 On entry change, set `pending_sync = true` and start/restart a per-anime debounce timer (~5–10s), independent per anime
- [x] 6.2 On timer expiry, `PATCH /v2/anime/{id}/my_list_status` with changed fields; on success clear `pending_sync` and set `last_synced_at`
- [x] 6.3 On push failure, leave `pending_sync = true` (never drop) and let a background retry job drain pending entries later
- [x] 6.4 Implement manual "sync now" that flushes only pending entries immediately — endpoint done (`POST /api/sync/now`); settings-page button is section 17
- [x] 6.5 Implement full reconciliation (weekly schedule + manual trigger) pulling the full MAL list and diffing against local — weekly job + `POST /api/sync/reconcile` done; settings-page button is section 17
- [x] 6.6 Adjust the debounce constant from ~7s to exactly 8 seconds to match the finalized decision
- [x] 6.7 Change reconciliation to diff only entries with `pending_sync = false`, excluding any entry that is `pending_sync = true` at diff time, and persist the resulting diff instead of applying it (see 19.7 for storage)
- [x] 6.8 Add Accept (apply the persisted diff) and Cancel (discard it) endpoints/actions for the pending reconciliation diff — endpoints done (`POST /api/sync/reconcile/accept`, `POST /api/sync/reconcile/cancel`, `GET /api/sync/reconcile/pending`); settings-page UI is section 17

## 7. Metadata & score refresh (`metadata-refresh`)

- [x] 7.1 Implement a nightly scheduled refresh job (no live calls on the render path)
- [x] 7.2 Select refresh candidates by staleness tier (airing/recent → 1–2 days; else weekly/monthly)
- [x] 7.3 Request only needed fields (e.g. `mean`) and spread work into small batches across the night
- [x] 7.4 Implement on-demand single-anime refresh endpoint (one call, updates cached record + timestamps) — endpoint done (`POST /api/anime/{id}/refresh`); detail-page button is section 16
- [x] 7.5 Restrict the nightly refresh candidate query to `AnimeMetadata` rows with a corresponding `UserAnimeEntry` (my list only); Season/Top-Anime browsing must not feed this job
- [x] 7.6 Replace the two-tier placeholder with four staleness tiers: airing → 1 day; finished <60 days → 3 days; finished 60 days–1 year ago or not yet aired → weekly; finished 1+ years ago → monthly
- [x] 7.7 Cap the nightly job at a fixed batch size (e.g. 500 calls/night), selecting most-stale-first across tiers, letting overflow roll to the next night automatically
- [x] 7.8 Implement a lean-field upsert (title, picture, episode count, type, score, rank/popularity) separate from the full-detail upsert, and ensure a lean update never nulls out existing rich detail fields on a row — `MalMappingExtensions.ApplyLeanTo`/`ToLeanAnimeMetadata` ready for sections 13/14's season/top-anime browsing to call; those pages don't exist yet

## 8. Timezone conversion (cross-cutting: `main-dashboard`, `airing-schedule`)

- [x] 8.1 Add a JST→local (`Europe/Helsinki`, DST-aware) conversion utility for broadcast day/time
- [x] 8.2 Provide converted broadcast day/time to airing-today filtering and the weekly grouping (group by converted local day) — `IBroadcastLocalTimeConverter.AirsOnLocalDate`/`ResolveForWeek` ready for sections 11.2/12.2 to call; those pages don't exist yet

## 9. Frontend shell, navbar & search (`navigation-and-search`)

- [x] 9.1 Scaffold the React app with routing for all pages — `react-router-dom` added; `AppShell.tsx` routes `/`, `/season`, `/top`, `/airing`, `/my-list`, `/profile`, `/settings`, `/anime/:id` to placeholder pages, ready for sections 11–17 to fill in
- [x] 9.2 Build the navbar: left buttons (Home, Seasonal, Top, Airing), right controls (Profile, hide/unhide toggle, Settings gear icon) — `components/Navbar/Navbar.tsx`
- [x] 9.3 Build centered type-ahead search: debounced, local-cache-first using word-boundary prefix matching (title start or any word start), live API fallback when nothing matches locally, dropdown of up to 5 with picture + title — `components/SearchBar.tsx` backed by a new `GET /api/anime/search` endpoint (`AnimeSearchController`/`AnimeSearchService`) doing the local-prefix-then-live-fallback matching server-side; live-fallback failures degrade to no results rather than a 500
- [x] 9.4 Create a reusable clickable anime card component linking to the anime detail page (used everywhere) — `components/AnimeCard.tsx`; ready for sections 11/13/14/16 to consume, not yet wired into any page
- [x] 9.5 Build the reusable editor overlay/modal used for all edit and add-to-list actions (opens on top of the page, closes on Esc/click-outside), with fields for episodes watched, status, score, and rewatch count — no start/finish date fields — `components/EntryEditorOverlay.tsx` + `context/EntryEditorContext.tsx` (`useEntryEditor().openEditor(...)`), backed by the existing entry-edit endpoint; ready for sections 11/14/16 to open it, not yet triggered by any page

## 10. Global score visibility (`score-visibility`)

- [x] 10.1 Add global hide/unhide state wired to the navbar toggle — `context/ScoreVisibilityContext.tsx`, toggle button in the navbar
- [x] 10.2 Render MAL scores blurred when hidden without leaking the value into the DOM text — `components/ScoreValue.tsx`; verified in a real browser that the numeric value is absent from DOM text while hidden
- [x] 10.3 Add per-score in-place reveal that is transient and re-hides on navigation — local `revealed` state in `ScoreValue`, naturally resets on route unmount; verified end-to-end (hide → reveal → navigate away and back → re-hidden)

## 11. Main page (`main-dashboard`)

- [x] 11.1 Build "Currently watching" as a horizontal card carousel with left/right arrows; clicking a card opens the detail page; a plus control next to the episode count increments episodes (applies edit/sync rules) — `GET /api/dashboard` (`MainDashboardService`) + `components/CurrentlyWatchingCarousel.tsx`
- [x] 11.2 Build "Airing today" section (my list only, filtered by converted local broadcast day) — `components/AiringTodayList.tsx`, backed by the dashboard endpoint's `airingToday` list (uses `IBroadcastLocalTimeConverter.ResolveForDate`)
- [x] 11.3 Build "Current season" section (my list airing this season) with filters (popularity, MAL score, alphabetical) and per-card progress bar (`watched/total` or `watched/?`) — `components/CurrentSeasonSection.tsx` + shared `components/ProgressBar.tsx`; "this season" = my-list anime with `AiringStatus == currently_airing`
- [x] 11.4 Show a next-episode countdown ("Next ep: in X days, Y h") on each currently-watching card, computed from the cached broadcast schedule via the JST→local converter; omit it when there is no known upcoming broadcast — `IBroadcastLocalTimeConverter.NextBroadcastInstant` (new), consumed by `MainDashboardService`
- [x] 11.5 Add the "Airing today" empty state (small "nothing airing today" message) and render each row as a small image + "time: Title" — done in `AiringTodayList.tsx`

## 12. Airing page (`airing-schedule`)

- [x] 12.1 Build the weekly time-slot view of my list (title + episode number, local time)
- [x] 12.2 Group shows by converted local day; support navigation to other weeks
- [x] 12.3 Lay out seven day-columns (day-label headers, variable slot counts of time + small image + title + ep number); show empty day-columns and a centered "nothing airing this week" message when the week is empty

## 13. Season page (`season-browser`)

- [x] 13.1 Build the season view listing all anime for a season (not just my list)
- [x] 13.2 Fetch a season live on first visit, then serve from cache indefinitely once that season has fully finished airing; support changing season
- [x] 13.3 Add filters (popularity, score, alphabetical, my score) and infinite scroll
- [x] 13.4 Season card shows title, picture, episode count (`?` when unknown), and type
- [x] 13.5 For the current and upcoming season only, re-fetch lean listing fields on the first visit of a new local calendar day since the last fetch; same-day revisits serve from cache (uses 19.5/7.8's lean upsert)

## 14. My list & top anime pages (`library-views`)

- [ ] 14.1 Build the My list page: one list grouped/ordered Watching → On hold → Plan to watch → Completed → Dropped, each showing picture, title, type, progress, my score
- [ ] 14.2 Show rank numbers (`#1`) on the left when My list is sorted by score
- [ ] 14.3 Build the Top anime page: ranked rows (number, picture, title, my score, right-aligned MAL score)
- [ ] 14.4 Add the MAL score column (hide-toggle aware) and an edit button (opens the editor overlay) to each My list row
- [ ] 14.5 Add My list status filter tabs (All, Watching, Completed, Plan to watch, On hold, Dropped) and a quick-filter control (MAL score, my score, alphabetical)
- [ ] 14.6 Add the conditional Top anime list-action button per row — "Add" when the anime is not in my list (adds it with status Plan to watch, then flips in place to "Edit"), "Edit" (opens the editor overlay on the same page) when it is
- [ ] 14.7 Re-fetch the Top Anime ranking's lean listing fields on the first visit of a new local calendar day since the last fetch; same-day revisits serve from cache; never proactively fetched if never visited

## 15. Profile page (`profile-stats`)

- [ ] 15.1 Compute and display anime stats from local DB (Days, Mean Score, Watching, Completed, On-Hold, Dropped, Plan to Watch, Total Entries, Rewatched, Episodes)
- [ ] 15.2 Build the "Latest updates" feed from `ActivityLog`, most recent first
- [ ] 15.3 Build "My top anime" with the minimum-of-10 fill rule (all 10s uncapped; fill the remainder up to 10 by next-highest score as the default)
- [ ] 15.4 Build all-anime score distribution (count per score value + overall mean)
- [ ] 15.5 Build opinion-divergence lists ("They liked it, I didn't" ≥3 below MAL; "I liked it, they didn't" MAL <7 and mine ≥2 above)
- [ ] 15.6 Add the "My top anime" edit control (top-right) opening a selection view over the tied next-highest-scored anime; persist the manual choice and have it take precedence over the default fill (see 19.3)
- [ ] 15.7 Make the "Latest updates" box scrollable and add its top-right control opening a full edit-history overlay (closes on Esc/click-outside)

## 16. Single anime page (`anime-detail`)

- [ ] 16.1 Build the detail view (picture, title, MAL score respecting hide toggle, my status, type, aired-from/to, studio)
- [ ] 16.2 Add external MAL and AniList links built from the MAL id (URL templates, no API call)
- [ ] 16.3 Add the progress bar (`watched/total` or `watched/?`) with status and an edit button that opens the overlay editor (episodes, rewatch count, score; no start/finish date fields; applies edit/sync rules)
- [ ] 16.4 Add the on-demand refresh action for this anime
- [ ] 16.5 Lay out the detail header: large picture on the left, two side-by-side boxes ("rank + MAL score", "my score + rewatch count"), an info box (type, studio, aired-from/to, genre, other short fields), and a synopsis/background box
- [ ] 16.6 Show prequel/sequel link buttons in the top-right, only when the related anime exist, linking to their detail pages (see 19.2)

## 17. Settings / utility page

- [ ] 17.1 Show sync status: pending entries, last successful sync time, failed/retrying entries
- [ ] 17.2 Add manual "resync now" and full-reconciliation triggers
- [ ] 17.3 Add re-authorize-with-MAL action (recover from invalid refresh token)
- [ ] 17.4 Add force-refresh of a specific anime's cached metadata
- [ ] 17.5 Show the pending reconciliation diff (if any) with Accept and Cancel actions, wired to 6.8's endpoints

## 18. Verification

- [ ] 18.1 Verify no page render triggers a live API call (reads come from Postgres)
- [ ] 18.2 Verify a full-library import completes progressively and resumes correctly after a mid-sync restart
- [ ] 18.3 Verify debounced sync coalesces rapid edits into one push, and a failed push is retried (never dropped)
- [ ] 18.4 Verify timezone conversion for "airing today" and weekly grouping across a day boundary
- [ ] 18.5 Verify secrets stay out of git (`.env` ignored) and data survives a container rebuild
- [ ] 18.6 Verify the currently-watching plus control increments while a card click navigates, and the next-episode countdown renders from cached broadcast data
- [ ] 18.7 Verify the profile top-anime manual selection persists and takes precedence over the default next-highest auto-fill
- [ ] 18.8 Verify the Top anime "Add" button creates a new list entry that syncs to MAL, and "Edit" opens the editor overlay
- [ ] 18.9 Verify the per-anime debounce window is exactly 8 seconds and a burst of edits still coalesces into one push
- [ ] 18.10 Verify the nightly refresh only ever selects my-list anime, respects the four staleness tiers, and stops at the batch cap
- [ ] 18.11 Verify Season/Top-Anime browsing never triggers a full-detail fetch and never overwrites existing rich fields with a lean update
- [ ] 18.12 Verify search matches a query against both a title's start and a mid-title word start, and falls back live only when nothing matches locally
- [ ] 18.13 Verify reconciliation excludes `pending_sync = true` entries from its diff, and that Accept applies while Cancel discards without changing local data

## 19. Data-model & backend additions (from the pages brief)

> These extend the already-completed data layer (section 2), import (section 4),
> list-editing (section 5), and refresh (section 7). They are added as new tasks so
> the completed work stays intact; they are prerequisites for tasks 15.6, 16.5, 16.6.

- [x] 19.1 Extend `AnimeMetadata` (and its EF mapping) to store genres, synopsis, and background text for the detail page
- [x] 19.2 Store related prequel/sequel references (MAL id + title) on `AnimeMetadata`, populated from MAL's related-anime data
- [x] 19.3 Add persistence for manual top-anime selections (which tied next-highest-scored anime fill the remaining top-10 slots) plus an endpoint to read/update it — `TopAnimeSelection` entity/repository + `GET`/`PUT /api/top-anime/selection`
- [x] 19.4 Add an EF Core migration for the new metadata fields and top-anime selection storage; apply on startup — migration `ExtendMetadataAndAddTopAnimeAndReconciliationDiff`, applied automatically via the existing `db.Database.Migrate()` startup call
- [x] 19.5 Extend the initial import and metadata-refresh field sets to populate genres, synopsis, background, and related-anime references — `MalClient`'s full-detail field set now includes `genres,synopsis,background,related_anime`, mapped in `MalMappingExtensions.ApplyTo`; import and on-demand/full-tier refresh all share this mapping already
- [x] 19.6 Add a create-entry (add-to-list) path that creates a `UserAnimeEntry` (default status Plan to watch) for an anime not yet in my list and pushes it to MAL via the existing debounced sync — already satisfied by the existing `PATCH /api/anime/{id}/entry` endpoint (creates with `WatchStatus.PlanToWatch` when missing, logs `Added`, and triggers debounced sync); no code change needed
- [x] 19.7 Add persisted storage for the pending reconciliation diff (survives restart, cleared on Accept/Cancel) plus its EF Core migration — `PendingReconciliationDiff`/`PendingReconciliationDiffEntry` entities, included in the same migration as 19.4
