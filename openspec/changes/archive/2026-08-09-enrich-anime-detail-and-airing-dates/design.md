## Context

Four of the five asks are pure presentation over data the app already holds; one needs a schema change.

Already available, no new fetching:

- **Next-episode countdown** — `IEpisodeScheduleService.NextAiringInstantAsync` already returns the earliest stored future air instant, and `MainDashboardService.ToEta` already turns that into a `NextEpisodeEtaDto(Days, Hours)` for the currently-watching carousel. The detail service just isn't calling it.
- **Add-to-watching / add-to-list** — `PATCH /api/anime/{id}/entry` (`UserAnimeEntryEditService`) already creates the entry when it's absent, defaulting to `PlanToWatch`, and applies the date/activity/sync rules. Both buttons are one `updateEntry` call each; no endpoint work.
- **AniList link** — `AnimeAiringSync.AniListId` already holds AniList's `Media.id` for every anime whose airing data has been fetched. That is exactly the id `anilist.co/anime/{id}` wants. This is why the earlier attempt failed: it was built from the MAL id, and AniList's ids are unrelated.
- **Airing-page header** — `day.localDate` is a full `yyyy-MM-dd` string; the page currently slices out only the day. The month is right there.

The one gap is **related anime**. `MalMappingExtensions.ApplyTo` reads `node.RelatedAnime` — which `MalClient.FullDetailAnimeFields` already requests — but keeps only `FirstOrDefault(r => r.RelationType == "prequel")` and the same for `"sequel"`, flattened into four scalar columns on `AnimeMetadata`. Everything else MAL returns is discarded at mapping time. The More overlay and the main-series button need those edges persisted.

## Goals / Non-Goals

**Goals:**

- Persist every `related_anime` edge from a full-detail fetch, keyed by raw MAL relation string.
- Serve the detail page one payload carrying relations, next-episode ETA, and the AniList id — no extra round-trips from the client.
- Keep the countdown honest: derived from stored AniList rows only, never estimated, consistent with the "no estimation" rule the aired-count and weekly-schedule reads already follow.
- Leave the lean-vs-full upsert split intact: a season/top-anime browse must never clear relations.

**Non-Goals:**

- No TMDB integration. SeriesGraph gets a title-search URL.
- No live client-side ticking countdown. The ETA is computed server-side at request time and rendered as a static `2d 7h`; it goes stale only until the next page load, which matches how the currently-watching carousel already behaves.
- No recursive franchise graph. The More overlay lists direct MAL relations of the current anime, one hop, nothing transitive.
- No backfill job. Relations populate through the existing on-visit full-detail refresh.

## Decisions

### 1. Related anime move to their own table; the four scalar columns are dropped

New entity, one row per edge:

```csharp
public class AnimeRelatedAnime
{
    public int AnimeId { get; set; }        // owner (MAL id)
    public int RelatedAnimeId { get; set; } // MAL id of the related anime
    public string RelationType { get; set; } = "";  // raw MAL wire value
    public string Title { get; set; } = "";
    public string? PictureUrl { get; set; }
    public int SortOrder { get; set; }      // MAL's own ordering within the anime's related_anime array
}
```

Composite PK `(AnimeId, RelatedAnimeId, RelationType)` — MAL can list the same anime under two relations, so the relation has to be part of the key. `AnimeId` is a plain FK to `AnimeMetadata` with cascade delete; `RelatedAnimeId` is deliberately **not** an FK: MAL routinely relates an anime we've never cached, and requiring the target row to exist would fail the whole upsert. The stored `Title`/`PictureUrl` are what let the overlay render a relation to an anime we don't have a metadata row for; clicking through then triggers the detail page's existing live-fetch path.

`PrequelMalId`/`PrequelTitle`/`SequelMalId`/`SequelTitle` come off `AnimeMetadata` and the detail DTO derives them from the table instead (`first row with RelationType == "prequel"` by `SortOrder`). Keeping both would mean two places to update and a way for them to disagree.

*Alternative considered:* a JSON column on `AnimeMetadata`, matching how `Genres` is stored (`text[]`). Rejected — `Genres` is a flat string list; relations are structured records that we filter and group by relation type, and a table keeps that a query rather than an in-memory scan of deserialized JSON. A JSON column would also make the "same anime under two relations" case awkward to key.

*Alternative considered:* keep the four columns and add the table alongside. Rejected as dual sources of truth for the same fact.

### 2. `ApplyTo` replaces the relation set wholesale; `ApplyLeanTo` never touches it

`ApplyTo` is the full/rich upsert and already overwrites every detail-only field. Relations follow the same rule: delete the anime's existing rows, insert what MAL just returned. That is what makes "relation removed upstream" resolve correctly on the next refresh.

This needs the relation write to happen where a `DbContext` is in scope, not inside the static mapping extension. `ApplyTo` will project the edges onto `target.RelatedAnime` (the navigation collection) and the callers — `MetadataRefreshService`, import, re-sync — persist that collection; the EF configuration handles the delete/insert as a tracked collection replace. `ApplyLeanTo` continues to not mention relations at all, so the existing "lean never clobbers rich" guarantee holds unchanged.

### 3. Detail read gains three fields, no new endpoint

`AnimeDetailDto` gains:

- `NextEpisode: NextEpisodeEtaDto?` — reusing the existing record from `Services/Dashboard/MainDashboardDto.cs` rather than defining a second days/hours shape. `AnimeDetailService` calls `scheduleService.NextAiringInstantAsync(anime, now, ct)` alongside the `EpisodesAiredAsOfAsync` call it already makes, and converts with the same rounding rule the dashboard uses (`TimeSpan.Days` / `TimeSpan.Hours`, so sub-hour resolves to `0d 0h` rather than disappearing).
- `AniListId: int?` — read from `AnimeAiringSyncs` by `AnimeId`. One extra keyed lookup on a page that is already doing several.
- `RelatedAnime: List<RelatedAnimeDto>` — flat list of `(AnimeId, Title, PictureUrl, RelationType)` ordered by `SortOrder`. The frontend does the prequel/sequel/parent/other partitioning, since it is the thing that decides which button each bucket feeds.

`AnimeMetadataRepository`'s detail read needs `.Include(a => a.RelatedAnime)`. Every other page read uses the lean projection and is untouched.

*Alternative considered:* returning pre-grouped buckets from the server (`prequel`, `sequel`, `parentStory`, `more`). Rejected — the grouping is a UI decision (which relations get a dedicated button vs. land in the overlay), and baking it into the DTO means a spec change to the API every time the UI regroups.

### 4. Countdown formatting lives with the status string

`formatAiringStatus` in `AnimeDetailPage.tsx` already composes the status line from status + aired counts; the countdown is appended there as ` · next in {days}d {hours}h` whenever `nextEpisode` is non-null. Driving it off `nextEpisode != null` rather than off `airingStatus` is deliberate: a `not_yet_aired` anime with a stored premiere date gets a countdown, and a `currently_airing` anime whose future episodes were never fetched correctly gets none. Airing status is MAL's, the airing rows are AniList's, and the rows are the ones that actually know.

### 5. Button stack is one column; identity is fixed, only the middle slot swaps

Three buttons in a vertical stack under the progress bar, reusing the existing `.anime-detail-page__edit` / `__refresh` button styling as a shared `__action` class:

```
┌──────────────────────┐
│ Add to watching      │
├──────────────────────┤
│ Add to list  / Edit  │   ← same slot, swaps on entry existence
├──────────────────────┤
│ Refresh data         │
└──────────────────────┘
```

The status text and progress bar stay above the stack where they are now. Both add actions share one `pending` state flag that disables the stack while a request is in flight, reusing the `incrementPending` pattern already in the file, and both reuse the existing `setDetail(prev => ({...prev, entry: saved}))` callback so the page updates in place.

### 6. External links become a labelled row; AniList falls back to search

Three link buttons styled like the related-anime buttons (bordered pill, `--accent-border` on hover) rather than the current bare accent-coloured text, moved out of the info `<dl>` — a link row is not a definition-list term/value pair and being inside one is why it looks out of place today.

URL construction:

| Link | URL |
|---|---|
| MyAnimeList | `https://myanimelist.net/anime/{malId}` |
| AniList (id known) | `https://anilist.co/anime/{aniListId}` |
| AniList (no id) | `https://anilist.co/search/anime?search={encodeURIComponent(title)}` |
| SeriesGraph | `https://seriesgraph.com/show/search/{encodeURIComponent(title)}` |

The AniList fallback matters more than it looks: `AniListId` is only populated for anime whose airing data has been fetched, which in practice means my-list anime that were airing or upcoming when added. A finished show browsed from search will usually have no id, so an id-only link would be missing on most pages. Search always resolves to something.

SeriesGraph keys off TMDB ids (`seriesgraph.com/show/42509-steinsgate`, and `/show/42509` resolves too). There is no MAL→TMDB mapping in the app and adding one means a TMDB API key plus title matching that can silently hit the wrong show — worse than a search page that shows the user the candidates. Confirmed with the user: title search.

### 7. More overlay reuses the shared `Modal`

New `components/RelatedAnimeOverlay.tsx` built on `components/Modal.tsx`, which already does Escape, click-outside, and `role="dialog"`. It takes the non-prequel/sequel relations, groups by relation type in a fixed display order (side story, alternative version, summary, spin-off, character, other, then anything unrecognized), and renders each group as a heading plus `<Link>` rows with thumbnail and title. Navigation closes the overlay via `onClose` on click.

Relation labels are prettified from the raw wire value with the same underscore-to-space-and-capitalize transform `formatSource` already uses for `source`, so an unrecognized relation renders readably instead of needing a map entry.

The **Main series** button is separate from the overlay: it renders next to More whenever a `parent_story` relation exists, since the ask is a one-click path back, not another popup.

## Risks / Trade-offs

- **Relations are empty until each anime is refreshed** → The migration drops the four scalar columns and creates the table empty, so every anime loses its prequel/sequel buttons until its next full-detail fetch. Mitigation: `AnimeDetailService` already live-fetches full detail when `Genres` is unpopulated; extend that trigger so a row with no related-anime entries *and* a `LastSyncedAt` older than the migration also refetches — or simply accept it, since the buttons return the first time each page is opened. Recommended: widen the existing trigger to `Genres` empty **or** the row was last synced before the migration, so the first visit after deploy repopulates. Do not add a bulk backfill pass; it would be thousands of paced MAL calls for a cosmetic recovery.
- **Dropping four columns is not reversible by rollback alone** → Migration down-path recreates the columns but cannot restore values. Mitigation: the data is a cache of MAL, fully rebuildable by refresh; no user-authored data is at risk. Take a DB snapshot before applying, as with any destructive migration.
- **`RelatedAnimeId` has no FK, so nothing stops a row pointing at an anime we never cache** → That is the intended behavior, but it means the overlay can link to a detail page that has to live-fetch on open. Mitigation: that path already exists and works (it is how sequel links to un-cached anime behave today).
- **Countdown goes stale on a page left open** → A page open for hours shows an ETA computed at load. Mitigation: accepted — same behavior as the currently-watching carousel, and days/hours granularity makes drift invisible for most of the window. A ticking client-side countdown can be added later without a server change, since the payload could carry the instant instead of the pair.
- **SeriesGraph search may not find the anime** → Title mismatch between MAL's romaji title and SeriesGraph's TMDB-sourced title. Mitigation: use the same `pickDisplayTitle` result the page is already showing, so the query matches what the user sees; the destination is a search page where they can pick.
- **A very large `related_anime` array on a long franchise** → One-hop only and MAL caps what it returns, so the overlay is a list of tens at worst. The overlay body scrolls; no pagination.

## Migration Plan

1. Add `AnimeRelatedAnime` entity, `DbSet`, and EF configuration (composite key, cascade FK on `AnimeId`, index on `AnimeId`).
2. Remove the four prequel/sequel properties from `AnimeMetadata`.
3. `dotnet ef migrations add AddAnimeRelatedAnime` — creates the table and drops the four columns in one migration. Build via the `sdk:10.0` Docker image, per the project's local-build note.
4. Deploy; apply the migration.
5. Relations repopulate per-anime on first detail-page visit through the widened live-fetch trigger.

Rollback: revert the migration (recreates the columns, empty) and redeploy the previous build; the previous build's live-fetch-when-`Genres`-empty path does **not** repopulate the scalar columns for rows that already have genres, so a rollback needs a one-off `UPDATE ... SET LastSyncedAt = NULL` on affected rows, or accepts missing prequel/sequel buttons until each is refreshed. Worth knowing before deploying, not worth pre-building tooling for.

## Open Questions

None blocking. Two settled with the user during proposal: SeriesGraph uses a title-search link rather than a TMDB integration; "main series" resolves via MAL's `parent_story` relation rather than by walking prequels.
