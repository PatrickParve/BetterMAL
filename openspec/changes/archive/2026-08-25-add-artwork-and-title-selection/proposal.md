## Why

Every anime and every series in the app wears the one picture MAL happens to call its `main_picture`, and every series wears its root entry's title verbatim. Neither is a choice — they are whatever MAL's first row said, and MAL frequently makes a poor one: a key visual crowded with credits where a clean poster exists, a season-one cover standing in for a twelve-entry franchise, a root title like `Beyblade: Metal Fusion` naming a franchise nobody calls anything but `Beyblade`. MAL publishes the alternatives (`GET anime/{id}?fields=pictures`) and the app already fetches full detail for every my-list anime on a tier ladder; it simply throws that field away and never asks for it.

Separately, the series page's "Time watched" is the one watch-time figure in the app that does not count rewatches. The profile page counts every completed rewatch run and an in-progress one; the series page deliberately counts a rewatched entry exactly once, so finishing a franchise and then watching its best season three more times moves the number not at all. The two pages describe the same hours and disagree.

## What Changes

**Series page — time watched counts rewatches**
- "Time watched" on the series page counts every completed rewatch run of a main-line entry, plus the episodes watched so far in a run still in progress — the same figure the profile page's watch time already uses, via the same shared helper.
- "Time left" is unchanged: it keeps subtracting **first-run** progress from the main-line runtime, so a rewatch can never drive it negative or make a half-finished franchise read as done.
- The two stats stop hiding together. "Time watched" is shown whenever it is non-zero, including on a franchise with nothing left to watch — which is exactly the franchise whose rewatch hours are worth reading. "Time left" alone hides when it reaches zero.
- A rewatching entry still counts as fully watched and still counts as Completed everywhere else on the page (badge, progress bar, entries-completed). That rule is untouched.

**Picture selection**
- A new capability: the app stores MAL's **full picture set** per anime, not just its main picture, and lets a picture be chosen as that anime's own.
- Picture sets are fetched **only for anime on my list**. They ride along with full-detail fetches that already happen (initial import, the nightly tiered my-list refresh, a detail page's own TTL fetch), costing no additional MAL requests. An anime already fresh but with no picture set yet is backfilled by a visit-triggered fetch, single-flighted and paced like every other visit-triggered fetch. An anime **not** on my list is never fetched for pictures; adding it to my list makes it eligible, and its detail page fetches on the next read.
- A **Choose picture** button appears on the detail page of a my-list anime that has more than one picture, and opens an overlay showing every picture. Clicking one makes it that anime's picture **everywhere** — home, my list, season, year, top, airing, search, recap, profile, series page, related-anime tiles. Clicking the one MAL calls its main picture clears the choice rather than storing a redundant override.
- The same for a **series**: its picture may be any picture belonging to any of its main-line members, chosen from a **Choose picture** button on the series page, and it applies everywhere the series is shown — the Series browser cards, search results, the series page header, the profile's Top series.
- A choice **survives MAL syncs**. Nothing MAL sends can overwrite a chosen picture; MAL's own main picture is kept alongside so the choice can be cleared back to it.
- The series page fetches the picture sets its picker needs on visit, bounded to a small per-visit budget so a sixty-member franchise cannot fire sixty MAL calls, and finishes the job across visits.

**Series title selection**
- A series' title may be chosen from the titles of its main-line members — each member's MAL title and English title are offered — or set to a **trimmed** version of one of them.
- Trimming is contiguous only: the chosen text must appear as an unbroken run inside one of the offered titles. `Beyblade: Metal Fusion` may become `Beyblade` or `Metal Fusion`; it may not become `Beyblade Fusion` or anything not literally inside it. Leading and trailing punctuation and whitespace are trimmed before the check, so `Beyblade` passes against `Beyblade: Metal Fusion`. The rule is enforced server-side, not only in the UI.
- A chosen title replaces the derived one everywhere the series is named, and the series stays findable by every member title regardless — searching `Metal Fusion` still finds a series titled `Beyblade`.
- Rebuilding a series keeps its chosen title and picture. So does a rebuild that absorbs a neighbouring series into it.

## Capabilities

### New Capabilities
- `artwork-selection`: what a picture set is, when it is fetched and for which anime, how a chosen picture is stored and applied everywhere, how it survives MAL syncs and series rebuilds, and how it is cleared. Covers both anime pictures and series pictures.
- `series-identity`: a series' displayed title and picture as chosen values rather than derived ones — the title options offered, the contiguous-trim rule, what happens on rebuild and on absorption, and the fact that search still matches member titles.

### Modified Capabilities
- `series-page`: "Time watched" counts rewatch runs; "Time watched" and "Time left" no longer hide together; the header carries **Choose picture** and **Choose title** controls.
- `anime-detail`: the detail page carries a **Choose picture** control for a my-list anime with more than one picture, and its response carries the anime's picture set.
- `series-browser`: a card shows the series' chosen picture and chosen title.
- `navigation-and-search`: a series search result shows the series' chosen picture and chosen title, while still matching on member titles.
- `mal-api-integration`: full-detail requests for a my-list anime include the `pictures` field, and the returned set is persisted; the field is omitted for anime not on my list.
- `metadata-refresh`: the tiered my-list refresh keeps picture sets fresh as part of its existing fetch; a visit-triggered picture backfill is defined for rows that predate the field.
- `data-persistence`: `AnimeMetadata` gains the picture set, its fetch timestamp, MAL's own main picture, and the chosen picture; `Series` gains its chosen title and chosen picture.

## Impact

**Backend**
- `Models/AnimeMetadata.cs` — `PictureUrls`, `PicturesSyncedAt`, `MalPictureUrl`; `PictureUrl` becomes "the picture to display" rather than "MAL's main picture".
- `Models/Series.cs` — `SelectedTitle`, `SelectedPictureUrl`.
- `Services/Mal/MalMappingExtensions.cs` — the single guard: `ApplyTo` and `ApplyLeanTo` write `MalPictureUrl` always and `PictureUrl` only when unoverridden; a fetch that did not ask for `pictures` must not clear a stored set.
- `Services/Mal/MalClient.cs` / `Dto/MalAnimeNode.cs` — a with-pictures field set and the `pictures` wire shape.
- `Services/Metadata/MetadataRefreshService.cs` — the my-list batch and the on-demand refresh choose the with-pictures field set for an anime that has an entry.
- `Services/Detail/AnimeDetailService.cs` + `AnimeDetailDto` — picture set and chosen picture on the response; a `picturesFetchPending` flag mirroring the existing related-anime backfill handshake.
- `Services/Series/SeriesService.cs` — rewatch-inclusive `MyWatchedSeconds`; picture options and title options on the response; the resolved series identity.
- `Services/Series/SeriesGraphBuilder.cs` — chosen title/picture survive a rebuild and an absorption.
- `Services/Search/SeriesSearchIndex.cs`, `Services/Series/SeriesRankingIndex.cs` — series identity read through one shared resolver instead of three separate root projections.
- `Services/Relations/RelationResolver.cs` — prefer a cached metadata row's picture over the denormalized copy on the relation row, so an override reaches related-anime tiles.
- `Services/Watching/WatchMath.cs` — no new maths; the series page starts calling the rewatch-inclusive helper the profile already uses.
- New controller actions for setting and clearing an anime's picture, a series' picture, and a series' title, plus the two picture backfill triggers.

**Frontend**
- New `PicturePickerOverlay` component (shared by the detail page and the series page) and a `SeriesTitlePickerOverlay`.
- `pages/AnimeDetailPage.tsx` — the Choose picture button beside Refresh data; the pending-picture backfill call.
- `pages/SeriesPage.tsx` — Choose picture and Choose title in the header; the time-stats visibility split.
- `api/types.ts`, `api/client.ts` — the new fields and endpoints.

**Data**
- One migration adding four columns to `AnimeMetadata` and two to `Series`. Backfill: `MalPictureUrl` is set from the existing `PictureUrl` (which is, today, exactly MAL's main picture), so no row starts out overridden and every page renders identically the moment the migration lands.

**Non-goals** (flagged, deliberately not in scope)
- Pushing a chosen picture or title to MyAnimeList. MAL has no API for either, and this app's only write to MAL stays the list-status PATCH.
- Fetching picture sets for anime that are not on my list, including series members that are not, and including anime merely browsed on the Season, Year, Top, or Search pages. Those keep MAL's main picture.
- Recording picture and title choices in the activity log. That log is about my relationship with an anime, not about cached presentation.
- Uploading or linking arbitrary artwork. The options are exactly what MAL publishes for the anime.
- Choosing a picture per surface (a wide banner for one page, a poster for another). One picture per anime, one per series.
- A series title composed from several members, or with words that appear in none of them. The contiguous-trim rule is the whole freedom on offer.
- Overriding an individual anime's *title*. Only series titles are choosable.
