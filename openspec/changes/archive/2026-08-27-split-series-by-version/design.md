## Context

A series is today the connected component of the stored related-anime graph over `SeriesRelations.TraversalSet`, which includes `alternative_version`. `SeriesGraphBuilder.BuildAsync` traverses that component from a seed anime, classifies a main line, assigns a watch order and per-media-type extras order, and persists one `Series` row whose members are keyed by `SeriesMember.AnimeId` — the schema itself enforcing "an anime belongs to at most one series". `SeriesService.ProjectAsync` recomputes scores, stats and grouping on every read.

Three consequences motivate this change:

1. Fullmetal Alchemist (2003) and Brotherhood are one component, so one page shows both retellings; `ClassifyMainLineChain` picks whichever sequel chain scores higher, and the loser's seasons become extras. Fate is worse — its routes and alternative settings fan out further.
2. Extras are grouped by media type (`SeriesMediaTypeOrder`), which tells the reader what format an entry is but not how it relates to the franchise.
3. Relations outside the traversal set — `character`, `alternative_setting`, non-companion `other` — are stored on `AnimeRelatedAnime` and shown on the detail page, but never reach the series page at all.

Constraints worth naming up front: builds are bounded (400 members, 8/20 member fetches, 4/10 probes) and must stay bounded; `AnimeRelatedAnime` already carries the far end's `Title`, `PictureUrl` and `MediaType`, so a related anime can be rendered without a metadata row; and `SeriesMember.AnimeId` being the primary key is depended on by `RelationResolver`, `AnimeDetailService`, `ArtworkSelectionService`, `PictureRefreshService`, `SeriesSearchLookup` and `SeriesRankingIndex`.

## Goals / Non-Goals

**Goals:**

- Each alternative version and alternative setting of a franchise is its own series, with its own root, main line, watch order, title, picture, page and browser card.
- A version's page shows the other versions it is directly linked to, and every entry it shares with them, but nothing exclusive to them.
- Extras are grouped by their relationship to the main line, with media type moved to a separate multi-select filter.
- Everything MyAnimeList relates to a main-line entry is present on the page, in or out of my list, without spending a fetch to get it there.
- A refresh that discovers a new relation reaches the series page without waiting for a visit.
- Build cost stays bounded, and every stored series heals itself on its next read.

**Non-Goals:**

- Deciding which version of a franchise is "canonical" or ranking versions against each other.
- Merging versions back together anywhere — the profile, the browser and search all see the narrower series.
- Making related non-member entries count toward averages, stats, or any figure.
- Any change to how MyAnimeList is written to, or to the detail page's own relation rendering.

## Decisions

### D1. Versions are split by partitioning the component, not by dropping edges from the traversal set

The obvious move — remove `alternative_version` from `TraversalSet` — does not work. Two versions are frequently also joined by story edges through a shared entry: an OVA that is a `side_story` of both FMA 2003 and Brotherhood keeps the component connected over `side_story` alone, so both versions still land in one series, now without even the relation that explains why.

Instead, the build keeps traversing the whole component — `alternative_version` and `alternative_setting` both **added** to the traversed set, so every version of a franchise is discovered — and then partitions the members into one series per version.

**Version anchors.** An anchor is a member that carries an `alternative_version` or `alternative_setting` edge, in either direction, to another member, and that is **main-line-eligible** under the existing eligibility predicate (not `special`/`music`/`pv`, not a recap, not another member's side content). Restricting anchors to eligible members is what stops an uncensored OVA cut or a recap special's alternate edit — both routinely tagged `alternative_version` — from shattering a franchise into a series per special. Eligibility is computed over the whole undivided component, where it is well defined and independent of which chain wins.

**Assignment by story distance.** Let `d(m, a)` be the shortest path from member `m` to anchor `a` over **story edges only** — the traversal set minus the two version relations. Each member belongs to every anchor in `argmin_a d(m, a)`. So:

- Shamballa is at distance 1 from FMA 2003 and unreachable from Brotherhood, so it is 2003's alone.
- An OVA that is a side story of both is at distance 1 from each, ties, and is a member of **both** series.
- A member that reaches no anchor over story edges — possible when a sub-franchise hangs off an anchor by a version edge only — falls back to distance over all edges, so it still lands somewhere rather than being dropped.

**Boundary members.** Each series additionally holds, as members, the anchors directly linked to any of its own members by a version edge. They are the tiles in the Alternative version / Alternative setting groups. They are never main line in a series they are only a boundary of, and they are never expanded through — that is exactly what keeps a foreign version's exclusive entries off this page.

*Alternatives considered.* Dropping `alternative_version` from the traversal set (fails, as above). Splitting at read time from the anime id the page was opened with, leaving one stored row (rejected: the browser, the profile's Top series and search would each have to re-derive the split, and the two versions could never carry different chosen titles or pictures). Nearest-anchor assignment with ties broken arbitrarily instead of shared (rejected: contradicts the requirement that a shared entry appears on both pages).

### D2. One anime, many series — `SeriesMember` keyed by `(SeriesId, AnimeId)`, with one primary membership

Shared members mean the schema can no longer enforce single membership. The key becomes composite. Every field on the row is already per-series (`IsMainLine`, `Order`, `FavouriteRank`), so nothing else about the row changes.

The many callers that ask "which series is this anime in" need a single answer. Rather than teach each of them a rule, the build records it: `SeriesMember.IsPrimary`, true on exactly one of an anime's memberships. The primary is the series whose anchor is nearest by the D1 metric, ties broken by the anchor's aired-from date and then its MAL id; a boundary member's primary is its own series, never the one it is a boundary of. `FindSeriesIdAsync`, the detail page's Series link, `RelationResolver`'s series-neighbour fallback and the artwork/picture services all filter on `IsPrimary`; the "are these two in the same series" check becomes "do they share any series", which is the honest generalisation.

*Alternative considered.* Computing the primary at read time from stored distances (rejected: it would need the distances persisted anyway, and every caller would pay a join).

### D3. A build persists every version in the component, in one transaction

The build has already computed the whole partition, and the Alternative version tile has to link somewhere, so writing only the seed's version would leave dangling links and a browser list that fills in one visit at a time. All versions are persisted together.

**Identity across rebuilds** generalises today's largest-overlap rule to a greedy matching: rank every (new version, stored overlapping series) pair by overlap size, and assign greedily, each stored series claimed at most once. The version with the largest overlap with the old combined series keeps its `Id`, its chosen title and its chosen picture; the other versions are new rows. Stored series that overlap the component and go unclaimed are deleted, with title/picture adoption into the claiming series exactly as today.

*Alternative considered.* Persisting only the seed's version and enqueueing the others (rejected: dangling links, and a rebuild of one version could contradict a sibling written by an earlier build).

### D4. Extras group by relation to the main line, resolved directionally, with a fixed precedence

An extra's group is the relation it bears **towards the main line**, which is directional: `M --summary--> X` makes X a *Summary*, while `X --summary--> M` makes X the *Full story*. The groups are Alternative version, Alternative setting, Prequel, Sequel, Parent story, Side story, Full story, Summary, Spin-off, Character, Adaptation, Other, displayed in that order.

Resolution, so that no entry is ever in two groups:

1. If the extra has a relation edge, in either direction, to any main-line member of this series, its group is the highest-precedence such relation in the order above. Version relations are first in the order precisely so a boundary anchor that also carries a sequel edge still reads as an alternative version.
2. Otherwise — an extra reached only through another extra — it inherits the group of the extra that reaches it, breadth-first outward from the main line. A special of a side story reads as Side story, which is what "relationship towards the main series" means for it.
3. Otherwise, Other.

A group is named by the **relation**, never by the far end's media type. A commercial reaches the page over a non-companion `other` edge and therefore lands in Other along with every other `other` edge, rather than earning a `cm` group of its own — media type is what the filter buttons are for, and letting it override the relation would put one media type in a group named after a format while every other group is named after a relation.

`SeriesMember.RelationGroup` stores the result, so the projection groups without re-walking the graph, and `SeriesMember.Order` becomes the position within that group (aired-from ascending, as today within a media-type group). `SeriesMediaTypeOrder` is replaced by a relation-group order; media type keeps its meaning only in the new filter.

### D5. Related non-members are projected from relation rows, never stored and never counted

"Everything related to the main entry" must include relations the traversal deliberately refuses to follow — `character`, non-companion `other`, `adaptation`, anything unrecognised. Admitting them as members would be wrong twice over: traversing them fuses unrelated franchises (the reason they are excluded today), and each one with no cached row would cost a fetch out of a budget of 8, leaving big franchises permanently partial and rebuilding on every visit.

So they are not members. On every read, the projection collects the non-traversed relations of the series' **main-line** members and emits them as *related entries*, rendered from `AnimeRelatedAnime.Title`, `PictureUrl` and `MediaType`, enriched from `AnimeMetadata` and `UserAnimeEntry` when those exist. That is free, complete on the first read, and needs no fetch. They appear in the same relation groups as the extra members, sorted the same way, and are excluded from every average, stat, count and member cap. A related entry whose id is already a member of this series is dropped, so nothing is listed twice.

*Alternative considered.* Storing them as non-expanded leaf members (rejected: `SeriesMember` requires an `AnimeMetadata` row, so a leaf with no cached row could not be stored at all without either a fetch or a schema change, and neither buys anything the read-time projection does not already give).

### D6. Type buttons filter the More section only

Media type becomes a row of multi-select buttons above the More section, offering only the types actually present among that series' extras and related entries. None selected means no narrowing. The filter composes with the "in my list" filter and the per-group open/collapse state rather than replacing them, and joins them in the page's restorable state.

It deliberately does not touch the timeline: the main line is a numbered watch order, and hiding entry 3 of 5 would make the numbering lie. Group headings show the count admitted by the current type filter, and a group left with nothing is not rendered while a type filter is active — with a dozen possible relation groups, a column of empty headings is noise.

### D7. A single-entry version is still a series

`BuildAsync` returns null when a component has one member, which is right: an anime related to nothing is not a franchise. That rule survives untouched, because a version's boundary anchors are counted as its members — an alternative version with no other relations at all still has two members, itself and the version it is an alternative of, and is stored.

### D8. Relation discovery enqueues a background build

`RelationDiscovery` rows already record every newly discovered relation edge on a cached anime, and `ISeriesBuildTrigger` already drives a background build queue used by search and the profile. Wherever a discovery is recorded, the owning anime is enqueued on that trigger; the background service builds its series with the visit budget. New members therefore reach the page without a visit and without the 30-day staleness window, and the work is naturally deduplicated by the trigger's own tracking. Everything else on the page — episode counts, scores, airing status — is already recomputed on every read and needs nothing.

### D9. Every stored series rebuilds once, on its next read

`SeriesGraphBuilder.ClassificationRevisedAt` is bumped to this change's ship date. `SeriesService.NeedsBuild` already treats any series built before that stamp as needing a build, so every franchise re-derives itself under the new rules on first read, and "Build all series from my list" already includes such series among its targets. No data migration walks the existing rows; the schema migration only changes the key and adds the two columns.

### D10. An alternative version's tile links by anime id, so it can never be dead

The series page is routed `/series/:animeId` and its read endpoint builds a series for that anime when none is stored. An Alternative version or Alternative setting tile therefore links to `/series/<that anchor's anime id>` unconditionally: if that telling has already been stored — which D3 makes the normal case, since one build persists every telling it partitions — the page is served from storage, and if it has not, the visit builds it exactly as any first visit to a series does. An anchor always carries a version relation to another member, so its component is never the one-member case the endpoint reports as "no series", and the link always resolves.

This is why the tile needs no stored series id on the wire and no detail-page fallback: there is no state in which the link points at nothing.

*Alternative considered.* Carrying the neighbouring telling's series id in the DTO and falling back to the anime's detail page when it is null (rejected: it adds a field and a branch to guard against a case the on-demand build already handles, and the fallback would silently send the user somewhere other than where the tile says it goes).

## Risks / Trade-offs

- **Traversing `alternative_setting` widens components sharply** — Gundam and Fate link many settings, so a build may reach the 400-member cap and be marked truncated, having spent fetch budget on foreign versions. → The partition still yields correct, small per-version series from whatever was traversed; the cap is the existing safety ceiling and truncation already suppresses automatic re-traversal. Breadth-first order means the seed's own version is complete long before the cap is near.
- **A franchise's stats change the day this ships** — averages, episode totals and Top series entries all narrow to one version. → Intended, and stated in the proposal; the rebuild-on-read stamp makes it happen uniformly rather than franchise by franchise.
- **A chosen title or picture survives on one side of a split only** — the version that inherits the stored series keeps it, the new versions start unchosen. → Unavoidable without asking the user which version the old choice described; the choice is one control away on the version that lost it.
- **Shared members are counted by both versions** — a shared OVA's runtime and rewatch time contribute to both series' figures, so summing across series double-counts it. → Accepted: each page's figures are right for that page, which is what they are read for. No surface sums across series.
- **`(SeriesId, AnimeId)` invalidates the assumption behind several single-row lookups** — a missed call site silently reads an arbitrary membership. → `IsPrimary` gives every such call site one correct answer, and the call sites are enumerated in the proposal's Impact section so none is left to chance.
- **Relation-group inheritance depends on breadth-first order** — an extra reachable from two differently-grouped extras takes whichever is nearer. → Deterministic given a stable BFS ordering seeded from the main line in watch order; ties fall to the lower MAL id.
- **Related non-members can be stale** — a relation row's cached title or picture predates a rename. → It is what the detail page already shows for the same edge, so the two agree; a cached metadata row, when one exists, wins.

## Migration Plan

1. EF migration: `SeriesMember` key `AnimeId` → `(SeriesId, AnimeId)`; add `RelationGroup` (nullable string) and `IsPrimary` (bool, default true, backfilled true for every existing row since each anime has exactly one today).
2. Ship the builder, service and projection changes together with the bumped `ClassificationRevisedAt`; existing rows stay readable until each series' first read rebuilds it.
3. Frontend ships with the same release — the DTO gains relation groups and related entries, and the old media-type grouping is removed in the same change.
4. Rollback is the migration down plus the previous build; stored series rebuilt under the new rules are re-derived by the old rules on their next read, since the old stamp is older still.

## Open Questions

None outstanding. Both questions this design opened have been resolved above: `cm` is grouped under Other (D4), and the Alternative version tile links by anime id so it can never be dead (D10).
