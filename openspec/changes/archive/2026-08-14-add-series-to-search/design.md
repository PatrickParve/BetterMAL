## Context

Search today (`AnimeSearchService`) has two entry points, both anime-only:

- `SearchAsync` — navbar type-ahead. Merges the local metadata cache (`GetSearchIndexAsync`) with a live MAL search, ranks prefix-matches ahead of contains-matches, orders by popularity, returns 5.
- `SearchPageAsync` — full `/search` page. Sourced purely from a live MAL search (up to 100 candidates), sortable, chunked client-side.

Series live in two tables — `Series` (id, `RootAnimeId`, `BuiltAt`, partial/truncated flags) and `SeriesMember` (anime id PK, series id, main-line flag, order) — and are built lazily by `SeriesGraphBuilder`, triggered **only** from `SeriesService.ResolveAsync`, i.e. only when someone opens a series page. A build costs MAL fetches (`VisitFetchBudget` = 8, `RebuildFetchBudget` = 20) and is single-flighted through `RefreshGate`.

Constraints that shape this design:

- The type-ahead fires on every debounced keystroke. Nothing on that path may add a MAL round-trip.
- A series has no stored title of its own; the series' identity for display is its root anime's metadata (`series-page` spec: "The series SHALL take its title and its main picture from the root").
- The search page's `totalCount` semantics are already subtle (it is capped to what one response can deliver, because the frontend fetches once and reveals client-side). Adding rows must not break that.
- The app already has a signal-plus-drain background pattern (`IAiringRefreshTrigger` + `AiringRefreshTriggerBackgroundService`) to copy rather than invent.

## Goals / Non-Goals

**Goals:**

- A query that matches a franchise surfaces that franchise's series in both the navbar dropdown and the `/search` results page.
- A series result is unmistakably a series (badge on the line beneath the title) and navigates to `/series/{rootAnimeId}`.
- Zero added MAL latency on the search request path.
- Franchises that have never had their series page opened become searchable over time, without the user having to do anything deliberate.

**Non-Goals:**

- Building a series graph for arbitrary MAL search hits synchronously (latency and fetch budget make this untenable).
- A separate "series search" page, a series-only filter, or sorting series among anime by score/popularity on the results page.
- Persisting series titles or a series search index as denormalised columns — matching reads the members' existing metadata.
- Changing how series are composed, what counts as main line, or the series page itself.

## Decisions

### 1. Match a series through its members' titles, not a stored series title

A series matches the query when **any of its members'** `Title` or `EnglishTitle` matches, using the same prefix/contains/exact rules the anime search already applies. The result is then presented using the **root** member's title and picture.

*Why:* there is no series title column, and member-wide matching is what makes "shingeki", "attack on titan", and "final season" all resolve to the same franchise. Restricting to the root's title would miss any query phrased against a later season — the common case for long franchises.

*Alternative considered:* match only the root's title. Simpler and cheaper, but "attack on titan final season" — a real query shape — would return nothing.

*Cost:* this needs one `SeriesMembers ⋈ AnimeMetadata` projection (anime id, series id, title, english title). The metadata table is already loaded whole for the search index on every type-ahead, so this is the same order of work; it is a single additional query returning at most a few thousand rows on a mature install.

### 2. Rank a series by its best-matching member, then place series ahead of anime

Within the matched set, each series takes the **strongest match quality** of any of its members (exact > prefix > contains) and, as a tie-break, the **best popularity rank** among the members that matched. Series are then emitted **before** anime results in both surfaces.

*Why:* pinning is the point of the feature — the user searching a franchise name wants the franchise offered first, and the dropdown only shows 5 rows. Interleaving series by popularity among anime would frequently bury them below the very entries they aggregate.

*Alternative considered:* rank series and anime in one popularity-ordered list. Rejected: unstable placement makes the feature feel accidental, and the badge is easier to read as a header-ish first row.

### 3. Cap series results at 2 in the dropdown, 3 on the results page

The dropdown's total stays 5, of which at most 2 may be series; the results page prepends at most 3 series ahead of the anime candidates.

*Why:* a query like "gundam" or "fate" can match many stored series. Without a cap, series would push every anime result out of a 5-row dropdown. Two leaves at least three anime rows always visible.

### 4. Series appear only in the default (relevance) ordering on the results page

On `/search`, series rows are prepended to the relevance-ordered results. When the user selects Popularity, MAL score, Alphabetical, or My score, series rows are **omitted** entirely.

*Why:* those sorts are defined over per-anime numbers a series does not have (a series has four different averages, computed at read time, and no single popularity rank). Fabricating one to make a series sortable would misrepresent it. Relevance — "what best answers this query" — is exactly where a franchise belongs.

*Alternative considered:* keep series pinned to the top under every sort. Rejected: a user who explicitly asked for "sorted by MAL score" seeing unsortable rows pinned above the sort is confusing, and the count line would disagree with the visible ordering.

### 5. `totalCount` counts only anime; series rows are extra

`SearchPageDto.TotalCount` keeps its current meaning (the number of *anime* this response can deliver). Series rows ride along in a separate `series` array on the DTO rather than being mixed into `items`.

*Why:* `items` is typed `AnimeBrowseItemDto` and consumed by `AnimeCard`; a mixed array would force a discriminated union through code that currently just maps. A separate array keeps the anime path byte-identical and makes "prepend series, then the grid" trivial on the frontend. It also preserves the carefully-reasoned `totalCount` semantics (capped to `limit`) without having to decide whether series count toward it.

*Alternative considered:* one polymorphic `results` array with a `kind` discriminator. Cleaner conceptually, but it changes the type of every existing consumer for no user-visible gain, and the chunked-reveal logic (`items.slice(0, visibleCount)`) would need series excluded from chunking anyway, since they are always all shown.

For the **dropdown**, the shape is different: results are already a flat 5-row list rendered by one component, so there a `kind` discriminator (`"anime" | "series"`) on a single array is the lighter option, and series simply sort first. The two surfaces differ deliberately — each takes the shape its consumer already wants.

### 6. Background build on a miss, via the existing trigger pattern

When a search's **top-ranked anime match** has no row in `SeriesMembers`, its anime id is enqueued on a new `ISeriesBuildTrigger`, drained by a `SeriesBuildTriggerBackgroundService` that calls `ISeriesService.GetSeriesAsync(animeId)` in a scope of its own — mirroring `IAiringRefreshTrigger`/`AiringRefreshTriggerBackgroundService` exactly.

Guards on the enqueue side:

- Only the single top-ranked anime match per search, never the whole result set.
- Only when the query is at least 3 characters, so a half-typed query does not queue builds for whatever it happens to prefix-match.
- An in-memory set of recently-enqueued anime ids (bounded, cleared on restart) suppresses duplicates, so a debounced type-ahead typing "attack on titan" one character at a time enqueues each candidate at most once.
- Enqueue is fire-and-forget: it never blocks, never fails the search, and its outcome is invisible to the response.

*Why:* it makes the feature self-healing on a fresh install without putting `SeriesGraphBuilder`'s MAL fetches on the request path. `SeriesService` already single-flights through `RefreshGate`, so a queued build racing a user opening the series page is already safe. Reusing the established trigger pattern means no new concurrency primitive to reason about.

*Alternative considered (rejected):* build inline for the top match. Adds up to 8 MAL fetches to a keystroke-frequency endpoint — unacceptable.

*Alternative considered (rejected):* a startup/nightly job that builds series for the whole local cache. Far more MAL traffic than the feature justifies, and most of it for franchises the user will never search.

### 7. The badge sits below the title, carrying the entry count

Both surfaces render, on the line beneath the title, a "Series" pill followed by the member count (e.g. `SERIES · 4 entries`). On the results page this occupies the same slot `AnimeCardMeta` uses for `TYPE · N ep`, so series cards and anime cards keep identical geometry in the grid.

*Why:* the placement was specified by the user; reusing the meta-line slot means no layout work and no grid irregularity. The entry count is the one number that makes a series row immediately meaningful next to the individual seasons beside it.

### 8. Entry count is the full member count

The count shown is every member of the series — main line and extras alike — matching what the series page lists in total.

*Why:* it is the count already available from `SeriesMembers` without a projection, and "4 entries" reading smaller than the series page's visible entry list would be a bug report waiting to happen.

## Risks / Trade-offs

- **A fresh install shows almost no series** → the background build (decision 6) fills the store as the user searches; the first search for a franchise typically shows anime only, a later one shows the series. This is a deliberate trade against per-search MAL cost, and is invisible once the store warms.
- **Member-wide title matching can produce a surprising hit** (a query matching an obscure OVA surfaces the whole franchise) → mitigated by ranking series on their best member match and capping the count; a franchise surfacing for one of its own entries' titles is defensible behaviour regardless.
- **Query cost grows with series count** → the join is over `SeriesMembers` (bounded by 60 members per series, and by however many series have been built) and runs alongside a full-table read the search already performs. If it ever matters, the member-title projection is a natural candidate for the same in-memory caching the search index would get.
- **A series whose root metadata is lean** (no picture, no English title) renders a thin row → falls back exactly as anime rows already do (`pickDisplayTitle`, placeholder picture).
- **Background builds could queue behind a slow MAL** → the queue is unbounded but fed at most one id per search with dedupe; a backlog delays discovery, never a response. Failures are logged and dropped, as in the airing trigger service.
- **Series omitted under non-relevance sorts may read as a bug** → the results page keeps the same default sort it has today (relevance), so series are visible unless the user actively changes sort; documented in the spec as intended.

## Migration Plan

No schema change, no data migration. Backend and frontend deploy together; an older frontend against the newer backend simply ignores the added fields (dropdown items with `kind: "series"` would render as anime rows, so ship them in one release rather than staging the backend first).

Rollback is a straight revert — nothing is written that would outlive it, other than series rows the background trigger built, which are exactly the rows a series-page visit would have built anyway.

## Open Questions

None blocking. Two deferred: whether the dropdown should show a series' member count inline once the roster grows past two digits, and whether `/search` should eventually offer a "series only" filter — both are additive and out of scope here.
