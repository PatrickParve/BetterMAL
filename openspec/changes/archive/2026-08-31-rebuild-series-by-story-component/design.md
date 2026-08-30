## Context

`SeriesGraphBuilder.BuildAsync` currently traverses the component over `SeriesRelations.TraversalSet` (story relations **plus** `alternative_version` and `alternative_setting`), then hands it to `SeriesVersionPartitioner.Partition`, which nominates every main-line-eligible member carrying a version edge as an **anchor** and assigns each remaining member to the anchor it is nearest to over story edges. Each anchor becomes a stored series; anchors linked to a series' members become its **boundary members**, forced ineligible for its main line.

The rule assumes a franchise has one version edge per telling. Real MAL data does not work that way, and the live database shows what happens when it does not:

| Franchise | Anchors | Stored today | Wanted |
|---|---|---|---|
| Clannad | `2167` Clannad, `4181` After Story, `1723` Movie | 3 series, each with a 1-entry main line; After Story is a *Sequel* extra of Clannad | 1 series, main line Clannad → After Story |
| Demon Slayer | `40456` Mugen Ressha movie, `49926` Mugen Ressha TV | 2 series, 9 members / 8 main line each, identical but for which Mugen Ressha is main line | 1 series |
| Fate/stay night | `356`, `6922`, `22297`, `25537`, `27821` … | 5 series, one rooted at *UBW Prologue* (a `tv_special`) | 1 series with a route picker |
| Fullmetal Alchemist | `121` 2003, `5114` Brotherhood | 2 series — correct | unchanged |

Clannad is the clearest failure: Clannad and After Story each carry their own `alternative_setting` special, so each is an anchor, each is at distance 0 from itself, and neither can hold the other.

What actually separates Fullmetal Alchemist 2003 from Brotherhood is not that either carries a version edge — Clannad's entries do too. It is that no `sequel`/`prequel`/`side_story`/`summary`/`spin_off` path joins them: over story relations they are two disconnected components. Clannad and After Story are joined by `sequel`; Demon Slayer's movie and TV arc are both on the same `sequel` chain through `38000` and `47778`; every Fate route hangs off `11741 --sequel--> {356, 6922, 25537, 27821}`.

Constraints carried over: builds stay bounded (400 members, 8/20 member fetches, 4/10 probes); `AnimeRelatedAnime` carries the far end's `Title`, `PictureUrl` and `MediaType` so a non-member can be rendered without a metadata row; `SeriesMember` requires an `AnimeMetadata` row; `SeriesMember`'s `(SeriesId, AnimeId)` key and `IsPrimary` already exist and are relied on by `RelationResolver`, `AnimeDetailService`, `SeriesService`, `SeriesBulkBuildBackgroundService`, `ArtworkSelectionService` and `PictureRefreshService`.

## Goals / Non-Goals

**Goals:**

- A franchise whose entries are joined by story relations is exactly one series, however many version edges its entries carry.
- Two tellings joined by nothing but a version relation are two series, each with its own page, root, main line, title and picture.
- An alternative version that is a lone entry is an ordinary extra of the series it is an alternative of, not a series of its own.
- Alternative versions that sit on the main line share one numbered watch-order position, with a picker, and entries reachable only through one of them are shown only while it is picked.
- Score averages describe the whole franchise; episode, runtime and progress figures describe the route the reader picked.
- A More tile never navigates to the page it is on.
- The seed's own series completes before a build spends anything looking outward, and only that series is persisted.
- Everything `split-series-by-version` got right — relation groups in More, the media-type filter, related non-member entries, the relation-discovery build trigger — is untouched.

**Non-Goals:**

- Ranking versions against each other or naming one canonical, beyond choosing which is pre-selected.
- Merging separate tellings anywhere: the browser, profile and search still see one series per story component.
- Changing how MAL is written to, or the detail page's own relation rendering.
- Making the picker affect anything outside the series page — browser cards and profile figures use the default pick.

## Decisions

### D1. A series is a story component; version relations are discovery-only

`SeriesRelations.TraversalSet` narrows back to the **story relations** — `sequel`, `prequel`, `side_story`, `parent_story`, `summary`, `full_story`, `spin_off`, plus the companion-media `other` case — and a series is again the connected component over that set. `VersionRelations` (`alternative_version`, `alternative_setting`) become a separate **discovery set**: followed one hop to find neighbouring tellings (D3), never used to merge, split, or expand a component.

This single rule produces every wanted outcome in the table above, and it is the rule the user's own reasoning describes: entries "all connected with the prequel/sequel … should all be in the same series", and a telling gets its own page when it "is not connected to the main line of the first".

Story components are disjoint by construction, so nearest-anchor assignment, tie-shared members, boundary members and the "a one-entry telling is still a series" carve-out (`split-series-by-version` D1, D2, D7) all disappear. `SeriesVersionPartitioner` is deleted; a much smaller component splitter replaces it.

*Alternatives considered.* Keeping anchors but grouping story-connected anchors into one telling (rejected: it is the story-component rule stated indirectly, and still leaves anchors deciding which members are shared). Keeping `alternative_version` in the traversal set and merging everything (rejected: that is the pre-`split-series-by-version` behaviour, which put Fullmetal Alchemist 2003 and Brotherhood on one page — the problem that change existed to solve).

### D2. A version neighbour either folds in as an extra or opens its own series

A **version neighbour** is an anime linked to a member by a version relation but outside the component. Each one is classified once, at build time, and stored as its own kind of membership:

| Condition | `MembershipKind` | Stored? | Tile links to |
|---|---|---|---|
| in the story component | `Core` | member | `/anime/<id>` |
| version neighbour, cached row, **no story relation of its own** | `FoldedVersion` | member | `/anime/<id>` |
| version neighbour, cached row, **has story relations of its own** | `NeighbourTelling` | member | `/series/<id>` |
| version neighbour, **no cached metadata row** | — | related entry only | `/anime/<id>` |

A neighbour with no story relation of its own has a story component of exactly itself, so it can never be a series: Clannad Movie, Fate/Prototype and Clannad's two `alternative_setting` specials all fold in as ordinary extras. A neighbour that does have story relations heads a component of its own — Brotherhood, Prisma☆Illya, Fate/Grand Order — so its tile opens that series, built on demand if it is not stored yet.

The decision needs only the neighbour's **cached** relation rows, so it costs no fetch. A neighbour with no cached row at all cannot be a `SeriesMember` (the row is required), so it falls through to the related-entry projection `split-series-by-version` D5 already built, rendered from `AnimeRelatedAnime`, and self-heals into a member on a later build once its row is cached.

Both member kinds are counted — they enter the all-member averages, the member count and the browser card figures exactly as extras do today. Neither is ever main line, and a `NeighbourTelling` is never expanded through, which is what keeps a foreign telling's own entries off this page.

*Alternative considered.* Showing a `NeighbourTelling` as an uncounted related entry (rejected by the user: Brotherhood on the 2003 page should count like any other extra).

### D3. Two-phase traversal, seed component only

**Phase 1** exhausts the seed's story component: breadth-first over the narrowed traversal set, spending the fetch budget on uncached members and re-expanding lean members exactly as today, honouring contradicted edges, the member cap and the probe budget. Because version relations are no longer traversed, no budget can be spent on a foreign telling before the seed's own is complete — the risk `split-series-by-version` accepted under "traversing `alternative_setting` widens components sharply" is removed rather than mitigated.

**Phase 2** walks each member's version relations, in both directions, one hop. Far ends already in the component are ignored (their version edge only feeds D4). The rest are classified per D2 from cached rows, with no fetches.

Only the seed's component is classified and persisted. A neighbouring telling reaches storage when its tile is followed — the read endpoint builds an unstored series for the anime it is opened with — or when the bulk build reaches it from the user's list, or when a relation discovery enqueues it. So the "one build writes every telling" transaction (`split-series-by-version` D3) is gone, and with it the risk of persisting a telling that a seed-distant, budget-truncated traversal only half-saw.

**Seed resolution.** A build whose phase 1 yields a single anime, where that anime has version neighbours, re-seeds from the version neighbour with the largest story component and builds *that*, folding the original seed in per D2. Opening `/series/1723` (Clannad Movie) therefore lands on the Clannad series rather than "not part of a series". With no version neighbour either, the build returns null as it does today.

### D4. Version slots and branches inside a main line

Main-line classification is unchanged — the highest-ranked `sequel`/`prequel` chain, reduced to eligible members — except that the `forcedIneligibleIds` boundary-member hack is dropped, since a telling's members are never in another telling's component.

Over the resulting main line, define:

- **Version slot** — a connected component, of size ≥ 2, of the undirected version edges *among main-line members*. Its members are the slot's **alternatives**. Its key is the lowest MAL id among them.
- **Branch** — for each alternative `h`, the main-line members whose shortest path to `h` over `sequel`/`prequel` edges, **with every other alternative of every slot removed from the graph**, is strictly shorter than to any other alternative. Removing the sibling alternatives is what stops a branch tail being "reached" through a sibling head.
- **Trunk** — every main-line member that is not an alternative and not strictly nearest to one: it ties between two or more alternatives, or reaches none. Trunk entries are always visible.

Worked through the live data: Demon Slayer's slot is `{40456, 49926}`, both branches are the head alone, and `38000`, `47778`, `51019`, `55701`, `59192`, `62546`, `62547` all tie at equal distance from both heads, so they are trunk — the picker changes one position and nothing else. Fate's slot is `{356, 6922, 22297, 25537}`; `10087` and `11741` tie and are trunk; `27821` is at distance 1 from `22297` and 2 from the rest, so *UBW Prologue* is in the UBW branch; `28701` is in the UBW branch, `33049`/`33050` in the Heaven's Feel branch. Clannad has no slot at all — its Movie is a `FoldedVersion` extra, not a main-line member — so no picker appears.

**Ordering.** The topological watch order runs over a graph in which each slot is **contracted to a single node**, so every alternative of a slot receives the same `Order` and a branch tail is ordered around the slot rather than after it — which is what puts *UBW Prologue* before the picker and *UBW 2nd Season* after it. The existing sort already tolerates `sequel` cycles by emitting the remainder in `OrderKey` order, and that tolerance covers a cycle contraction could introduce.

`SeriesMember` gains `VersionSlotKey` (nullable int, set on alternatives) and `BranchHeadAnimeId` (nullable int, set on alternatives and their branch members, null on trunk), so the projection needs no re-walk.

*Alternative considered.* Deriving branches at read time from relation rows (rejected: the same BFS, run on every read, over data the build already has in hand).

### D5. The default pick

Per slot, the pre-selected alternative is chosen server-side in this order:

1. The branch with the most of the user's watched episodes across its members; ties fall to the branch with more entries Completed or Rewatching. Skipped when every branch is at zero.
2. The **best** head: highest `MalScore`; when the two best heads are within **0.25**, the one with the lower `PopularityRank` wins. Heads with no score sort last.
3. Earliest `AiredFrom`, then lowest MAL id.

Rule 2 is what the user asked for — "the best (calculate somehow between MAL score and popularity averages of the alternatives)" — expressed as a score comparison with popularity as the near-tie break rather than a blended index, because a blend has to invent weights and, on Fate, a popularity-weighted blend hands the pick to the 2006 TV series purely because it is the oldest and most-listed. Comparing heads gives UBW TV (8.19, far more listed than Heaven's Feel's 8.11 within the 0.25 band), which is the route a reader is usually pointed at.

The chosen head is returned per slot as `defaultBranchHeadAnimeId`. The reader's own pick overrides it and is remembered per series in the page's restorable view state, beside the type filter and per-group open state.

### D6. Averages span every route; totals follow the pick, and the server computes both

Per the user's decision, `SeriesScoresDto` is unchanged: `MalMain`/`MineMain` average over **every** main-line member, alternatives included, so the headline scores never move when the picker does.

`SeriesStatsDto`'s pick-dependent figures — episode total, runtime total, aired episodes, watched episodes/seconds, rewatched seconds, entries completed, `HasUnknownEpisodeCounts`, `MainLineCompletedByMe`, longest gap — are computed over **trunk plus picked branches**.

The server computes a full `SeriesStatsDto` for **each admissible combination of picks**, returned as `statsByPick: { branchHeadAnimeIds: int[], stats: SeriesStatsDto }[]`, with `SeriesDto.stats` remaining the default combination so the browser card, the profile and the first paint are unaffected. Switching the picker is then an index lookup, not a refetch and not a client-side re-derivation.

This is deliberately not client-side arithmetic: `MainLineCompletedByMe`, `HasUnknownEpisodeCounts` and the longest gap are not additive over branches, so a client that summed per-branch contributions would have to re-implement `EpisodesAndRuntime`, `MainLineSettledByMe` and `LongestGap` in TypeScript and keep them in step with the C#. The combination count is the product of the slot sizes — one slot of two to five alternatives in every case observed — and is capped at 24, beyond which slots after the first keep their default pick.

*Alternatives considered.* A `?branch=` query parameter re-read on every pick (rejected: a network round trip on a toggle). Per-branch additive figure blocks summed client-side (rejected: the non-additive figures above).

### D7. The tile's link target is carried on the wire

`SeriesEntryDto` gains `OpensOwnSeries`, projected straight from `MembershipKind == NeighbourTelling`. `SeriesExtraTile` links to `/series/<id>` when it is true and `/anime/<id>` otherwise, replacing today's inference from `relationGroup`.

That inference is the bug behind "clicking Kyou-hen takes me back to the same page": the relation group is a display bucket, and a group is `AlternativeSetting` whenever MAL says so — or whenever the group-inheritance BFS hands it down — regardless of whether the far end is a telling of its own. `MembershipKind` answers the question the link actually asks.

### D8. Schema: three columns, no key change

`SeriesMember` gains `MembershipKind` (string, `Core`/`FoldedVersion`/`NeighbourTelling`, backfilled `Core`), `VersionSlotKey` (nullable int) and `BranchHeadAnimeId` (nullable int). The `(SeriesId, AnimeId)` key and `IsPrimary` stay: a `FoldedVersion` neighbour can legitimately belong to two components at once — Fate/Prototype is `alternative_setting` to Fate/stay night and `alternative_version` to Fate/strange Fake, and both are real series — so single membership cannot be re-imposed, and reverting the key would be migration churn for no gain.

`IsPrimary` becomes a rule rather than a distance calculation: a `Core` membership is always primary; a `NeighbourTelling` membership never is (that anime's own series is its home, built on demand); a `FoldedVersion` membership is primary when the anime has no `Core` membership anywhere, resolved between two folds by the larger component and then the lower root MAL id.

### D9. Identity matching is scoped to core members

`MatchTellingsToStoredSeries` collapses to a single-component match: among stored series overlapping this component, the one with the largest overlap keeps its `Id`, chosen title and chosen picture; the rest are deleted with title/picture adoption, exactly as absorption works today. Clannad's three stored rows therefore collapse into one that inherits the largest.

Overlap and staleness are computed over **`Core` members only**. Without that scope, building Fullmetal Alchemist 2003 would see Brotherhood — a counted `NeighbourTelling` member of it — overlap the stored Brotherhood series and delete it.

### D10. Every stored series rebuilds once, on its next read

`ClassificationRevisedAt` is bumped to this change's ship date. `SeriesService.NeedsBuild` already rebuilds anything older, so the 256 stored series re-derive themselves under the new rules on first read, and "Build all series from my list" already includes them. No data migration walks existing rows.

## Risks / Trade-offs

- **Merging back makes some pages much longer** — Fate/Zero, Fate/stay night, the UBW films and Heaven's Feel become one page with a 10-entry main line. → The picker is exactly the mitigation: at most trunk plus one route is ever on screen, and the trunk is two entries.
- **Averages still span every route while totals do not** — a reader could read "8.21 average" and "50 episodes" as describing the same set. → The user's explicit choice; the page labels the score averages as covering the whole main line, and the totals move visibly with the picker.
- **`alternative_setting` is no longer traversed, so a neighbouring setting reaches storage only on demand** — the browser will not list Prisma☆Illya until it is visited or bulk-built. → Its tile always resolves (the read endpoint builds on visit), the bulk build covers everything in the user's list, and the relation-discovery trigger covers new ones. This is the same on-demand guarantee `split-series-by-version` D10 relied on, now applied consistently.
- **`AdjacentAnimeSet` reads `SeriesRelations.TraversalSet`** for the metadata-refresh adjacency, and narrowing that set would silently stop refreshing alternative versions of a listed anime. → It is repointed at the union of the traversal and version sets, so refresh adjacency is unchanged by this change.
- **`MembershipKind` classification depends on what happens to be cached** — a neighbour whose row is not cached is a related entry this build and a member the next. → It is displayed either way, in the same group, with the same tile; only the link target and its inclusion in the counts change, and both settle once the row is cached.
- **A member equidistant from two alternatives lands in the trunk and is always shown** — for a franchise where the alternatives genuinely diverge but MAL records a symmetric edge, an entry may show under both picks. → Preferable to hiding it under both; trunk is the safe default and matches Demon Slayer, where every shared entry genuinely belongs to both.
- **The `statsByPick` combination cap** — a main line with three or more slots keeps later slots on their default figures. → No observed franchise has more than one slot; the picker still switches which entries are shown.
- **Franchise figures change for the second release running** — averages, totals and Top series entries move again. → Unavoidable while correcting a shipped rule; the rebuild stamp makes it happen uniformly on first read.

## Migration Plan

1. EF migration: add `SeriesMember.MembershipKind` (string, non-null, default `Core`), `VersionSlotKey` (nullable int) and `BranchHeadAnimeId` (nullable int). No key change, no data backfill beyond the column default.
2. Ship the narrowed traversal set, the component splitter, the slot/branch resolver, the projection and the bumped `ClassificationRevisedAt` together; stored rows stay readable until each series' first read rebuilds it.
3. Frontend ships in the same release: version slots, branch visibility, per-pick numbering and figures, the persisted pick, and the server-supplied tile target.
4. Delete `SeriesVersionPartitioner` and its tests; keep `SeriesMainLineEligibility`, `SeriesRelationGroupOrder`, `RelationGroup` and the related-entry projection unchanged.
5. Rollback is the migration down plus the previous build. A series rebuilt under these rules is re-derived by the old rules on its next read, since the old stamp is older still.

## Open Questions

None outstanding. The four decisions this change opened — how alternatives on a main line are shown, what feeds the figures, whether a neighbouring telling counts, and which alternative is pre-selected — were settled with the user and are recorded in D4, D6, D2 and D5.
