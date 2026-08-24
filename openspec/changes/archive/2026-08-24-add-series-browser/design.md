## Context

Series already exist as first-class stored objects: `Series` + `SeriesMember` rows built by `SeriesGraphBuilder`, read by `SeriesService.GetSeriesAsync` for the series page, and joined wholesale by `SeriesRankingLookup` for the profile's Top series strip. What has never existed is a *browse* surface over them.

Two existing surfaces are the shape this page has to fit between:

- **`SeriesRankingLookup` / `SeriesRankingIndex`** (profile): loads every member of every stored series in one `SeriesMembers ⋈ Series ⋈ AnimeMetadata ⋈ UserEntry` join, computes eligibility and the two main-line averages in memory, and returns the whole set unordered — the client ranks and filters locally. This is already almost exactly the query the Series page needs.
- **`SeasonPage` / `SearchPage`** (browse): a header with title and a sort/filter cluster, a fluid `AnimeCard` grid, and hand-rolled infinite scroll over an `IntersectionObserver` sentinel, with view controls in the URL so back-navigation restores them.

The constraint that shapes most of the decisions below: the page must be **read-only over stored data**. Opening it must never trigger a series build or a MAL fetch, because it touches every series at once and building even a fraction of them on a visit would be unbounded work. The Settings page's "Build all series from my list" and the profile's on-read backfill already exist to fill the store in; this page shows what they produced.

Referenced throughout: `openspec/specs/series-page/spec.md` (composition, main line, status pill, personal badge, average reveal rules), `openspec/specs/season-browser/spec.md` (browse page shape), `openspec/specs/score-visibility/spec.md`, `openspec/specs/page-header-design/spec.md`, `openspec/specs/page-state-restoration/spec.md`.

## Goals / Non-Goals

**Goals:**
- One page listing every stored series with at least one member in my list, as cards in the Season/Search grid form.
- Each card carries the figures the series page's header carries, reduced to card scale: picture, both main-line averages, status pill, personal progress badge, year span, main-line episode total, entry count.
- MAL averages on a card obey exactly the rule the series page applies to its own main-series average — one shared computation, not a second implementation that could drift.
- Seven sort orders over the whole list, switching instantly, each with a defined tie-break and a defined place for series the sort key can't rank.
- Opening the page costs no MAL calls and no series builds.

**Non-Goals:**
- Filters (type, status, in-my-list). The request asks for sort only, and every series listed is by definition in my list. Adding filters later is a spec addition, not a rework.
- Building or rebuilding series from this page. A series missing from the store stays missing until Settings' bulk build or a page visit builds it; the page's empty state points at that action, as the profile's Top series section already does.
- Search-within-series, pagination controls, or a list/table view alternative to the grid.
- Changing what a series *is* — composition, main-line classification, and status precedence are untouched.

## Decisions

### D1 — One whole-list read, sorted on the client

`GET /api/series/list` returns **every** eligible series in one response, unordered-but-deterministic, and the client sorts. Server-side sorting with offset paging was the alternative.

Client-side wins here for three reasons:

1. **Alphabetical sort must use the display title.** `pickDisplayTitle` prefers the English title when there is one, and that preference lives on the client (`navigation-and-search`'s "English title preferred for display"). A server-side alphabetical sort would order by a title the user isn't reading.
2. **Switching sort must not refetch.** Every sort key is derived from data already in the payload; a round-trip per sort change would be pure latency for zero new information.
3. **Precedent.** Top series already returns its whole set and re-ranks locally for exactly this reason (`TopSeriesSectionDto`'s own doc comment says so). A second, differently-shaped series listing endpoint would be the inconsistency.

Payload size is the trade-off. A large list yields on the order of several hundred series at roughly 300 bytes each — a few hundred KB, one read, cached by `usePageData` for the session's back-navigation. That is the same order as the Top series response already in flight on every profile visit.

The server still returns a **stable default order** (my main-line average descending, then title) so the response is deterministic and a client that did nothing with it would still render sensibly.

### D2 — Widen the existing ranking projection rather than add a second lookup

`SeriesRankingLookup` already loads the exact join this page needs; it just doesn't project the airing dates, media-type-independent episode fields, and per-member watched counts the card requires. Three options were on the table: a second parallel lookup, a generic lookup with two projections, or widening the one that exists.

**Widen it.** `SeriesRankingMemberProjection` gains `AiredFrom`, `AiredTo`, and `AiringStatus` is already there; `EpisodesWatched`, `TotalEpisodes`, and `AverageEpisodeDurationSeconds` are already there for the rewatch math. The added columns cost the profile's read nothing measurable (same join, same rows, a few more scalars) and the alternative is two queries that must be kept in step by hand — precisely the drift `SeriesAverages` was extracted to prevent.

The new aggregation lives in `SeriesRankingIndex` as a `ListedSeries()` method beside `EligibleSeries()` and `RewatchedSeries()`, so all three series-wide aggregations read one loaded index.

### D3 — Eligibility is "at least one member in my list", and nothing more

`EligibleSeries()` (Top series) applies a **watched-coverage rule**: a franchise whose main line has two or more aired entries needs at least two of them in my list, or its averages would rank a whole franchise on one entry's viewing. That rule exists because Top series *ranks by an average*.

This page does not apply it. The request is explicit — exclude a series only when *not a single entry* is in my list — and the page is a browse surface, not a ranking: a franchise I've seen one season of is exactly the kind of thing I'd open this page to find. `ListedSeries()` therefore filters on `members.Any(m => m.EntryStatus is not null)` alone.

Consequence worth stating: the Series page and the profile's Top series strip will list different sets. That is correct, and each spec says why.

### D4 — The MAL reveal boolean is computed server-side, once, by the shared helper

The series page's reveal rule 1 is: every finished-airing main-line entry is Completed or Dropped **and** no main-line entry is currently airing. `SeriesAverages.MainLineSettledByMe` already implements the first half and `SeriesRankingIndex.EligibleSeries()` already composes the second (`mainLineSettledByMe && !mainLineAiring`) to produce `MalRevealed`.

`ListedSeries()` reuses that same composition verbatim, and the card passes the resulting boolean to `<ScoreValue completed={...}>` — identical to what both the series page and the Top series strip do today. The card therefore cannot reveal a score the series page would blur, because the same expression decides both.

The card only ever shows the **main-line** averages, so rule 2 (the across-all-entries group rule) has nothing to apply to here and is not implemented on this surface.

### D5 — Status and the personal badge: extract, don't reimplement

`SeriesService.ComputeStatus` takes `List<AnimeMetadata>` — navigation-property-bearing entities the list projection deliberately doesn't load. Rather than duplicating its four-step precedence, extract the precedence into a static helper over airing-status strings (`SeriesStatusRules.Compute(mainLineStatuses, allStatuses)`) and have `SeriesService` call it too. One implementation, two callers, no behavioural change to the series page.

The **personal badge** is the mirror case in the other direction: the series page computes it *client-side* (`completionBadge` in `SeriesPage.tsx`) precisely so an in-place row edit updates it without a refetch. This page has no in-place editing, and the badge needs the whole main line's watched counts and aired-episode counts — data the card payload would otherwise not need to carry per member.

So the badge is computed **server-side** here, into a `progressBadge` field with the same four outcomes (`Completed` | `CaughtUp` | `Behind` + count | `None`) and the same precedence the series page's `completionBadge` applies. Two implementations of one rule is a real cost; it is accepted because the alternative — shipping every main-line member's watched/aired/status triple to the client for every series on the page — would multiply the payload by the average main-line size for a figure the client would then compute identically anyway. The spec pins both to the same stated precedence and the tasks pair them with tests over the same cases.

### D6 — Aired-episode counts: one batch read, over ids

The behind-count needs, for each currently-airing main-line member, how many episodes have actually broadcast. `IEpisodeScheduleService.EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata>, ...)` does this in one database read — but takes entities, and its implementation only ever reads `a.Id`.

Add an id-taking overload (`IReadOnlyCollection<int>`) and make the entity overload delegate to it. The list projection then passes the ids of its currently-airing members directly, with no entity load and no per-member query. This is the honest signature for what the method does; the entity overload stays for its existing four callers.

Members whose airing status is `currently_airing` and that have **no** stored aired row come back absent from the dictionary — that is "unknown", and per the series page's rule an unknown broadcast count means **no badge at all** rather than an invented count.

### D7 — Episode total is the main line's, marked as a lower bound

"The episodes out number" is the **main-line** episode total, matching the series page's `Main series episodes` stat and matching the card's main-line averages — so every figure on the card describes the same member set. Computed exactly as `SeriesStatsDto.MainLineEpisodeTotal` is: a member with a known `TotalEpisodes` contributes it in full, one without contributes its known aired-so-far count instead, and either way an unknown sets `hasUnknownEpisodeCounts` so the card renders `62+ ep` rather than claiming an exact figure. The `Unknown` label for a zero-total-with-unknowns follows `formatEpisodeTotal`'s existing branch.

The **entry count** is the opposite scope on purpose: every member, extras included, matching `SeriesBadge`'s `entryCount` everywhere else in the app.

### D8 — Seven sorts, each with a defined tail

Every sort has a defined answer for series the key can't rank, so nothing silently disappears or lands in an arbitrary spot:

| Sort | Key | Direction | Unrankable go |
|---|---|---|---|
| Alphabetical | display title, case-insensitive | ascending | — |
| MAL average | main-line MAL average | descending | last |
| My average | main-line my-score average | descending | last |
| Status | `Airing` → `Ongoing` → `Upcoming` → `Finished` | fixed order | — |
| Newest | first-aired date | descending (latest start first) | last |
| Oldest | first-aired date | ascending (earliest start first) | last |
| My progress | main-line watched ÷ main-line aired-so-far | descending | see below |

Every sort's final tie-break is display title ascending, so the order is total and stable across re-sorts.

Three points worth pinning:

- **Newest/oldest** read the way the words do — "Newest" puts the most recently *started* series first. (The request's parentheticals had these swapped; confirmed with the user.)
- **Sorting by a hidden MAL average still sorts by the real value.** The season page already sorts by MAL score while the toggle hides it, and `score-visibility`'s browse-card exception accepts that. Doing otherwise would mean the sort control silently stops working when the toggle is on.
- **My progress** is watched-against-*aired*, not watched-against-total, so being current on a running series ranks alongside having finished a done one — which is what "completed to 0 watched" means about my watching rather than about the franchise's release schedule. A series with nothing aired at all has no ratio; it sorts after every series with a real 0, ahead of nothing, marked by its own flag rather than by a fake zero.

### D9 — Route, page shell, and restoration

- Route `/series` for the browser; the existing series page keeps `/series/:animeId`. React Router matches the static segment first, so no ambiguity and no change to the detail route.
- Sort lives in the URL (`?sort=`), as on Season and Search, so a shared link and a back-navigation both land on the same order.
- The page follows `page-header-design`: an `h1` inside a page-specific header wrapper with zeroed margin, and the sort control in a filter/sort cluster on the shared control height.
- Data goes through `usePageData` on a fixed key, so back-navigation restores the loaded list without refetching.
- The grid renders incrementally through the same `IntersectionObserver` sentinel the Season page uses, over the already-loaded array rather than over network pages — several hundred cards mounted at once is the only real cost of D1's whole-list read, and this removes it. The rendered count lives in `useRestorableState`, not `useState`, so a deep-scrolled restore has the cards to scroll back to (`page-state-restoration`'s scroll requirement is otherwise unsatisfiable here).

### D10 — Card composition reuses `AnimeCard`

The card is `AnimeCard` with `to={/series/${rootAnimeId}}` — the same override the search page's series results already use — and a series-specific meta block as `children`, so it inherits the grid's fluid sizing, hover treatment, title truncation, and click target for free. Inside the link, since all of it is passive metadata per `navigation-and-search`'s clickable-cards requirement; `ScoreValue`'s reveal button already stops its own propagation.

Chips use `ScoreChip size="compact"` (the tile-card density the timeline and extras tiles use), the status pill reuses the series page's four status colours, and the progress badge reuses its three badge colours — one visual language across both series surfaces.

## Risks / Trade-offs

- **Whole-list payload grows with the store** → Bounded by the number of *franchises* with a list member, not by list size, and each series is a flat ~300-byte record. If it ever became a problem the endpoint can take an offset without any client-visible change to the card or the sorts; nothing in D1 forecloses that.
- **The progress badge is implemented twice** (server here, client on the series page — D5) → Accepted deliberately, for the payload reason stated there. Mitigated by pinning both to one stated precedence in the spec and testing both against the same case set, including the unknown-broadcast-count case that must produce *no* badge.
- **The page lists nothing on a store where no series has been built** → This is the likely first-run state and it must not read as an error. The empty state says series are still being discovered and links to Settings' "Build all series from my list", reusing the wording the profile's Top series section already uses for the identical situation.
- **The Series page and Top series list different sets** (D3) → Intentional; both specs state their own rule and why it differs.
- **A partial or truncated series shows figures computed over the members it has** → Its averages, episode total, and entry count are all lower bounds. The card does not surface partial/truncated state (the series page does, next to its Rebuild control). Accepted: a per-card warning badge on a browse grid is noise for a condition the user resolves one series at a time.
- **Sorting by MAL average while scores are hidden orders cards by values on screen as blurs** → Established precedent (D8); a user who finds it revealing can turn the sort off, and the alternative silently breaks the control.

## Migration Plan

No data migration, no schema change, no new tables or columns. The endpoint reads rows that already exist; the route is additive; the navbar gains one link. Rollback is removing the route and the navbar link — nothing else stores or depends on this page's state beyond per-session history snapshots.

The one shared-code edit that touches existing behaviour is D5's extraction of `ComputeStatus` and D6's added overload; both are pure refactors with the existing call sites' tests as their regression net.

## Open Questions

None blocking. Two deferred by choice: whether the page eventually wants the Season page's `Type`-style filters (status, in-my-list-completeness), and whether a partial/truncated marker belongs on the card — both additive, neither shaping anything decided above.
