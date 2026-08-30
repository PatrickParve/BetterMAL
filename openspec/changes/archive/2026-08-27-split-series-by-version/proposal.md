## Why

Franchises that exist in more than one telling — Fullmetal Alchemist (2003) beside Brotherhood, the several Fate routes and their alternative settings — collapse into a single series today, because `alternative_version` is traversed as a story relation. The result is one page mixing two retellings, whose main line is whichever version happened to have the longer sequel chain, and whose extras are a jumble of both versions' side content. The two are separate stories that happen to share a source; they deserve separate pages.

The same page has two further gaps: extras are grouped by media type, which says nothing about how an entry relates to the franchise, and anything MyAnimeList relates to the main line by a relation the series traversal ignores — `character`, `alternative_setting`, a non-companion `other` — is absent from the page entirely, whether or not it is in my list.

## What Changes

- **BREAKING** `alternative_version` and `alternative_setting` no longer merge anime into one series. Each version becomes its own series with its own page, root, main line, title, picture, and card in the series browser.
- **BREAKING** An anime may belong to more than one series. Where two versions share a related entry — an OVA that is a side story of both — that entry is a member of both series and appears once on each page. `SeriesMember`'s key becomes `(SeriesId, AnimeId)`, and each membership records whether it is that anime's *primary* series, which every existing "the series for this anime" lookup resolves to.
- A version's page shows the other versions themselves — as tiles in an **Alternative version** / **Alternative setting** group that link through to those versions' own series pages — but never the entries exclusive to them. Only shared entries appear on both.
- A version whose own component is a single entry is still a series, because the version it is an alternative of counts as a member of it.
- **BREAKING** The More section is grouped by each extra's **relationship to the main line** — Prequel, Sequel, Side story, Parent story, Summary, Full story, Spin-off, Alternative version, Alternative setting, Character, Adaptation, Other — replacing today's media-type groups.
- Media type becomes a separate row of multi-select **type buttons** above the More section (TV, Movie, OVA, ONA, Special, Music, PV, …), narrowing which extras are shown across every group. It joins the More section's restorable view state.
- The More section shows **everything** MyAnimeList relates to a main-line entry, including relations the series traversal does not follow. Those entries are shown from the relation rows already stored — title, picture, media type — so no fetch is needed and nothing is missing. They are shown, not counted: they do not enter averages, stats, member counts, or the browser's card figures.
- A metadata write that discovers a new relation on a member enqueues that anime's series for a background rebuild, so a newly announced entry reaches the series page without waiting for a visit or the 30-day staleness window.
- "Build all series from my list" and every other build path use the same rules, and every stored series is rebuilt once on its next read.

## Capabilities

### New Capabilities
- `series-versions`: how a franchise's alternative versions and alternative settings become separate series — which anime anchor a version, how members are assigned to one version or shared between several, which series is an anime's primary one, and how the versions are persisted and re-identified across rebuilds.

### Modified Capabilities
- `series-page`: composition stops merging over `alternative_version`; the root, main line, and watch order are per version; extras are grouped by relation to the main line rather than by media type; a media-type filter is added; non-traversed relations of main-line entries are shown as related entries; the More section's restorable state gains the type filter; "Build all series from my list" targets primary memberships.
- `series-browser`: a franchise with alternative versions lists one card per version; card figures are computed over that version's members, with a shared member counting toward every version that holds it.
- `series-identity`: title, picture, and the offered titles are resolved per version series from that version's own main line; absorption across a version split keeps the choices on the version that inherits the stored series.
- `relation-confidence`: the ranked prequel/sequel fallback that walks a stored series resolves through an anime's primary series, and "in the same series" becomes "sharing any series".
- `metadata-refresh`: a fetch that adds a relation edge to a cached anime enqueues a series build for it.

## Impact

**Schema / data.** `SeriesMember` primary key `AnimeId` → `(SeriesId, AnimeId)`; new `SeriesMember.RelationGroup` and `SeriesMember.IsPrimary`. `SeriesGraphBuilder.ClassificationRevisedAt` is bumped so every stored series rebuilds on its next read. `Series.RootAnimeId` remains unique per series.

**Backend.** `SeriesRelations` (traversal set, new version-edge set, relation-group naming and precedence), `SeriesGraphBuilder` (component traversal, version partition, per-version classification and persistence, greedy identity matching), `SeriesService` (projection: relation groups, related non-members, primary resolution), `SeriesDto`/`SeriesEntryDto` (relation group, related entries, version links), `SeriesMediaTypeOrder` → a relation-group order, `SeriesRankingIndex`/`SeriesListService`, `SeriesBulkBuildBackgroundService`, `SeriesSearchLookup`, `ArtworkSelectionService`, `PictureRefreshService`, `RelationResolver`, `AnimeDetailService`, and the metadata-refresh write path that raises relation discoveries.

**Frontend.** `SeriesPage.tsx` (relation groups, type-button row, related entries, version links), `SeriesExtraTile`, `SeriesBrowserPage`, and the series API types.

**Behaviour users will notice.** Franchises with alternative versions split into several series cards; series counts and averages change for those franchises; chosen titles and pictures survive on one side of a split only; the profile's Top series and Most rewatched by series see the new, narrower series.
