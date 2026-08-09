## 1. Airing page day headers

- [x] 1.1 In `frontend/src/pages/AiringPage.tsx`, replace the bare day number in `.airing-day__header` with a `d.M` string built from `day.localDate` by slicing month and day and stripping leading zeros — no `new Date()` parsing of the bare ISO string (see the existing comment on why).
- [x] 1.2 Check `frontend/src/pages/AiringPage.css` for a fixed header width that the longer `30.10` form would overflow; widen or let it size to content.
- [x] 1.3 Verify a week spanning a month boundary renders `30.9` / `1.10` and a single-digit date renders `3.5` with no zero padding.

## 2. Related-anime storage (backend schema)

- [x] 2.1 Add `backend/AnimeTracker.Api/Models/AnimeRelatedAnime.cs` with `AnimeId`, `RelatedAnimeId`, `RelationType`, `Title`, `PictureUrl`, `SortOrder`, and a doc comment noting `RelationType` is the raw MAL wire value and `RelatedAnimeId` is intentionally not an FK.
- [x] 2.2 Add a `RelatedAnime` navigation collection to `AnimeMetadata` and remove `PrequelMalId`, `PrequelTitle`, `SequelMalId`, `SequelTitle`.
- [x] 2.3 Configure the entity in `Data/AnimeTrackerDbContext.cs`: `DbSet`, composite key `(AnimeId, RelatedAnimeId, RelationType)`, cascade-delete FK from `AnimeId` to `AnimeMetadata`, index on `AnimeId`.
- [x] 2.4 Generate the migration (`AddAnimeRelatedAnime`) — creates the table and drops the four scalar columns. Build through the `sdk:10.0` Docker image, not the local 9.0 SDK.
- [x] 2.5 Fix every compile error from the dropped columns (grep `PrequelMalId`, `SequelMalId`, `PrequelTitle`, `SequelTitle` across the backend).

## 3. Persisting relations from MAL

- [x] 3.1 In `Services/Mal/MalMappingExtensions.ApplyTo`, replace the two `FirstOrDefault` prequel/sequel lookups with a full projection of `node.RelatedAnime` onto `target.RelatedAnime`, preserving MAL's array order as `SortOrder` and skipping edges with a null `Node`.
- [x] 3.2 Confirm `ApplyLeanTo` still never mentions relations, and update its doc comment's list of rich-only fields to say related anime rather than prequel/sequel.
- [x] 3.3 Make the relation write a wholesale replace in the persistence path (`MetadataRefreshService`, import, re-sync) so a relation MAL no longer reports is deleted — verify the tracked-collection replace actually deletes rather than orphaning rows.
- [x] 3.4 In `Services/Detail/AnimeDetailService`, widen the live-fetch trigger so a row with no related-anime entries and a `LastSyncedAt` predating the migration also refetches, so relations repopulate on first visit after deploy.

## 4. Detail API payload

- [x] 4.1 Add a `RelatedAnimeDto(int AnimeId, string Title, string? PictureUrl, string RelationType)` record in `Services/Detail/`.
- [x] 4.2 Extend `AnimeDetailDto` with `NextEpisode` (reuse `NextEpisodeEtaDto` from `Services/Dashboard/MainDashboardDto.cs`), `AniListId`, and `RelatedAnime`; drop the four prequel/sequel properties and update `FromEntity`.
- [x] 4.3 In `AnimeDetailService.GetDetailAsync`, call `scheduleService.NextAiringInstantAsync(anime, now, ct)` alongside the existing `EpisodesAiredAsOfAsync`, and convert with the same `TimeSpan.Days`/`TimeSpan.Hours` rule `MainDashboardService.ToEta` uses (sub-hour → `0d 0h`, past instant → null).
- [x] 4.4 Read the anime's `AniListId` from `AnimeAiringSyncs` in the same service and pass it through; null when no sync row exists.
- [x] 4.5 Add `.Include(a => a.RelatedAnime)` to the detail read in `Data/Repositories/AnimeMetadataRepository.cs`, ordered by `SortOrder`; confirm no lean/listing read picks it up.
- [x] 4.6 Update `frontend/src/api/types.ts`: `AnimeDetailDto` gains `nextEpisode: NextEpisodeEtaDto | null`, `aniListId: number | null`, `relatedAnime: RelatedAnimeDto[]`; remove `prequelMalId`/`prequelTitle`/`sequelMalId`/`sequelTitle`.

## 5. Detail page — countdown

- [x] 5.1 Extend `formatAiringStatus` in `AnimeDetailPage.tsx` to take the ETA and append ` · next in {days}d {hours}h` whenever it is non-null, driven by the ETA's presence rather than by `airingStatus`.
- [x] 5.2 Verify: currently-airing with a future row shows counts plus countdown; not-yet-aired with a stored premiere shows a countdown; currently-airing with only past rows shows no countdown; finished-airing shows none.

## 6. Detail page — action buttons

- [x] 6.1 Replace the single Edit button with a three-button vertical stack under the progress bar: Add to watching, then Add to list / Edit, then Refresh data. Move the status text and progress bar above the stack unchanged.
- [x] 6.2 Wire Add to watching to `updateEntry(animeId, { status: 'Watching' })` and Add to list to `updateEntry(animeId, { status: 'PlanToWatch' })`, both applying the result via the existing `setDetail(prev => ({ ...prev, entry: saved }))` callback.
- [x] 6.3 Add a shared pending flag that disables the stack while an add is in flight (mirroring `incrementPending`), so a double-click sends one request.
- [x] 6.4 Show Add to list only while `detail.entry` is null; render Edit in that same slot otherwise, so there are always exactly three buttons.
- [x] 6.5 In `AnimeDetailPage.css`, generalize `.anime-detail-page__edit` / `__refresh` into a shared action-button class and lay the three out as a column matching the picture width.

## 7. Detail page — More overlay and main-series link

- [x] 7.1 Add `frontend/src/components/RelatedAnimeOverlay.tsx` on top of the shared `Modal`: takes the non-prequel/sequel relations, groups by relation type in a fixed display order (side story, alternative version, summary, spin-off, character, other, then unrecognized), renders each group as a heading plus `<Link>` rows with thumbnail and title, and closes on navigation.
- [x] 7.2 Add a relation-label helper that prettifies the raw wire value (underscores to spaces, capitalize) the way `formatSource` already does, so an unrecognized relation renders readably without a lookup map.
- [x] 7.3 In `AnimeDetailPage.tsx`, partition `relatedAnime` into prequel / sequel / parent-story / everything-else; keep the prequel and sequel buttons fed by the first of each by sort order.
- [x] 7.4 Render a More button immediately left of the prequel/sequel buttons when the everything-else bucket is non-empty, opening the overlay; render nothing when it is empty.
- [x] 7.5 Render a Main series button next to More when a `parent_story` relation exists, linking to that anime's detail page.
- [x] 7.6 Add `RelatedAnimeOverlay.css` styled consistently with the existing overlays, with a scrolling body for long relation lists.

## 8. External links row

- [x] 8.1 Move the external links out of the info `<dl>` into their own row of three link buttons (MyAnimeList, AniList, SeriesGraph), each `target="_blank" rel="noreferrer"`.
- [x] 8.2 Build the AniList href from `aniListId` when present, else `https://anilist.co/search/anime?search={encodeURIComponent(title)}`; build the SeriesGraph href as `https://seriesgraph.com/show/search/{encodeURIComponent(title)}`, using the same `pickDisplayTitle` result the page renders.
- [x] 8.3 Style the row in `AnimeDetailPage.css` as bordered pills matching `.anime-detail-page__related-link` (accent border on hover), replacing the bare accent-text link styling.
- [x] 8.4 Verify a title containing spaces, `&`, or `#` produces a working search URL for both sites.

## 9. Verification

- [x] 9.1 Build the backend through the `sdk:10.0` Docker image and the frontend with nvm's node v22; both clean.
- [x] 9.2 Apply the migration against a snapshot of the dev database and confirm the drop-plus-create runs without data loss elsewhere.
- [x] 9.3 Walk the detail page for: an anime not in my list, one already watching, a currently-airing anime with a future episode, a finished anime, a side-story movie with a parent story, and an anime with no relations at all.
- [x] 9.4 Confirm relations repopulate on first detail-page visit after the migration, and that a season or top-anime browse afterwards does not clear them.
- [x] 9.5 Update `CODE_GUIDE.md` for the new table, the changed detail payload, and the related-anime mapping change.

## 10. Detail page polish (info-box relocation, layout, formatting)

- [x] 10.1 Move the MyAnimeList/AniList/SeriesGraph link row back inside `.anime-detail-page__info-grid`, as the last `<dl>` item — the cell the plain MyAnimeList link occupied before task 8 — instead of its own section below the box.
- [x] 10.2 Lay `Add to watching` and `Add to list`/`Edit` side by side in one row (each `flex: 1 1 0`); keep `Refresh data` as a full-width row beneath them.
- [x] 10.3 Add a `formatAiredRange` helper in `AnimeDetailPage.tsx`: when `airedFrom === airedTo` (movie/special that aired in one day), render that single date instead of a `from – to` range.
- [x] 10.4 Attempted storing `MediaType` on `AnimeRelatedAnime` populated from `edge.Node.MediaType`; reverted after confirming against the live MAL API that `related_anime` nodes never carry a media type (no field-selection support beyond id/title/picture) — rolled back the `AddAnimeRelatedAnimeMediaType` migration and the model/mapping change.
- [x] 10.5 Add `MediaType` to `RelatedAnimeDto` (backend record), sourced instead by looking up each related anime's id against our own `AnimeMetadata` cache in `AnimeDetailService.GetDetailAsync` (present only for related anime we've separately cached); add `mediaType` to the frontend `RelatedAnimeDto` type and render it (uppercased, matching `AnimeCardMeta`'s convention, "Unknown" when absent) beneath the title of each row in `RelatedAnimeOverlay.tsx`.
- [x] 10.6 Extend `formatDuration` in `AnimeDetailPage.tsx` to take `totalEpisodes` and append `/ep` to the minutes value unless `totalEpisodes === 1`.
- [x] 10.7 Add `Rating` and season fields to `AnimeDetailDto` (backend): `Rating` straight from `AnimeMetadata.Rating`; `SeasonYear`/`Season` derived via `SeasonCalendar.GetSeasonFor(anime.AiredFrom)` when `AiredFrom` is set, else null. Add both to `types.ts`.
- [x] 10.8 Render Rating (formatted G/PG/PG-13/R/R+/Rx) and Season (`"{Label} {Year}"` as a `<Link to="/season?year={y}&season={s}">`) as new `<dl>` entries in the info box; omit the season entry when `AiredFrom` is unknown.
- [x] 10.9 Rebuild backend + frontend, apply the follow-up migration to the dev DB, and re-verify the detail page (info-box link placement, side-by-side buttons, single-day aired movie, per-episode duration on a series vs. a movie, rating, and a season link that navigates to the right season page) in the browser.

## 11. Follow-up polish (season link style, watched/dropped buttons, related-anime backfill, link centering)

- [x] 11.1 Remove the underline from the info box's Season link in `AnimeDetailPage.css`; keep the accent-color hover as the only affordance.
- [x] 11.2 Hide the "Add to watching" button when `detail.entry.status` is `Completed` or `Dropped`, leaving `Edit` (and `Refresh data`) as the remaining actions.
- [x] 11.3 Add `IAnimeDetailService.RefreshRelatedMediaTypesAsync` + `AnimeDetailService` implementation: backfill up to a capped number (20) of this anime's not-yet-cached related entries via `IMetadataRefreshService.RefreshOneAsync` (one paced MAL call each), then return the refreshed `RelatedAnimeDto` list. Expose via `POST /api/anime/{id}/related-anime/refresh` on `AnimeDetailController`.
- [x] 11.4 Wire the frontend: `client.ts` gets `refreshRelatedAnimeMediaTypes`; clicking More opens the overlay immediately and kicks off the backfill call in the background, merging the refreshed list into `detail.relatedAnime` on completion; `RelatedAnimeOverlay` shows a small "Loading media types…" note while the backfill request is in flight.
- [x] 11.5 Center the text in `.anime-detail-page__related-link` (used by the MyAnimeList/AniList/SeriesGraph pills and the prequel/sequel/More/Main-series buttons).
- [x] 11.6 Rebuild backend + frontend (no schema change), redeploy, and verify in the browser: season link has no underline, a Completed/Dropped anime shows no "Add to watching" button, opening More on a franchise with many uncached relations progressively resolves "Unknown" labels, and the external-link pills render with centered text.
