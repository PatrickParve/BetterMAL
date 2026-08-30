## 1. Schema and model

- [x] 1.1 Add `MembershipKind` (string, non-null, default `"Core"`), `VersionSlotKey` (nullable int) and `BranchHeadAnimeId` (nullable int) to `SeriesMember`; keep the `(SeriesId, AnimeId)` key and `IsPrimary`
- [x] 1.2 Configure the three columns in `AnimeTrackerDbContext` and add the EF migration; verify the model snapshot builds and the migration applies against the local Postgres
- [x] 1.3 Add a `MembershipKind` enum (`Core`, `FoldedVersion`, `NeighbourTelling`) beside `RelationGroup`, with the same string round-tripping the projection uses for relation groups

## 2. Relation sets

- [x] 2.1 Narrow `SeriesRelations.TraversalSet` back to the story relations; keep `StoryTraversalSet` as its alias or fold the two into one, and keep `VersionRelations` as the discovery-only set
- [x] 2.2 Repoint `AdjacentAnimeSet` at the union of the traversal and version sets so metadata-refresh adjacency is unchanged by the narrowing
- [x] 2.3 Audit every other reader of `TraversalSet` (`SeriesService.ProjectRelatedEntriesAsync`, `AnimeUpdateService`) and confirm the narrowing gives the intended set at each site — related entries now legitimately include version relations to non-members

## 3. Component splitting and version neighbours

- [x] 3.1 Delete `SeriesVersionPartitioner` and its tests
- [x] 3.2 Add a `SeriesVersionNeighbours` helper that, given a component's members and their cached relation rows, returns each version neighbour with its `MembershipKind` — `FoldedVersion` when it carries no story relation of its own, `NeighbourTelling` when it does, and excluded entirely when it has no cached metadata row
- [x] 3.3 Unit-test the classifier against the live shapes: Clannad Movie and the two Clannad `alternative_setting` specials fold in; Brotherhood and Prisma☆Illya open their own series; an uncached neighbour is excluded

## 4. Traversal

- [x] 4.1 Rewrite `SeriesGraphBuilder.TraverseAsync` as phase 1 only — story relations, breadth-first from the seed, existing fetch/probe budgets, lean-member expansion, contradicted-edge handling and the member cap unchanged
- [x] 4.2 Add phase 2: follow each member's version relations one hop, classify neighbours via 3.2, admit `FoldedVersion` and `NeighbourTelling` as members, never expand through them, and spend no fetch or probe budget
- [x] 4.3 Add seed resolution: when phase 1 yields a single anime that has version neighbours, rebuild from the neighbour with the largest story component and fold the original seed in; keep the null return when it has none
- [x] 4.4 Test that a build seeded inside Fate/stay night traverses only its story component, and that a build seeded from Clannad Movie resolves to the Clannad component

## 5. Main line, slots and branches

- [x] 5.1 Drop the `forcedIneligibleIds` boundary-member parameter from `ClassifyMainLineChain`; version neighbours are outside the component and are extras by construction
- [x] 5.2 Add a `SeriesVersionSlots` resolver: version slots as connected groups of size ≥ 2 over version edges among main-line members, keyed by the lowest MAL id; branches by strictly-nearest alternative over `sequel`/`prequel` with every other alternative removed; everything else trunk
- [x] 5.3 Change `TopologicalMainLineOrder` to order with each slot contracted to one node, so every alternative shares an `Order` and branch entries sort around the slot; keep the cycle fallback
- [x] 5.4 Compute the default alternative per slot: most watched episodes in the branch, then more entries Completed/Rewatching, then higher `MalScore` with `PopularityRank` breaking a ≤ 0.25 gap, then earliest `AiredFrom`, then lowest MAL id
- [x] 5.5 Exclude version neighbours from relation-group inheritance in `ResolveExtraGroups`, both as sources and as targets
- [x] 5.6 Test the slot/branch resolver on the live shapes: Demon Slayer's one slot with an all-trunk remainder; Fate's four alternatives with UBW Prologue in the UBW branch and the Heaven's Feel films in theirs; Clannad producing no slot

## 6. Persistence and identity

- [x] 6.1 Rewrite `PersistAsync` for a single series: persist the seed's component plus its version-neighbour members, writing `MembershipKind`, `VersionSlotKey`, `BranchHeadAnimeId`, `RelationGroup`, `IsMainLine` and `Order`
- [x] 6.2 Replace `MatchTellingsToStoredSeries` with a single-component match — largest overlap over `Core` members keeps the identifier, chosen title and chosen picture; every other overlapping stored series is deleted with title/picture adoption; never match or delete on a version-neighbour overlap
- [x] 6.3 Implement the `IsPrimary` rule: `Core` always primary, `NeighbourTelling` never, `FoldedVersion` primary only when the anime holds no `Core` membership anywhere (larger component, then lower root MAL id, between two folds)
- [x] 6.4 Bump `SeriesGraphBuilder.ClassificationRevisedAt` to this change's ship date
- [x] 6.5 Test that rebuilding any of Clannad's three stored series collapses them into one keeping the largest overlap's identity, and that rebuilding FMA 2003 leaves Brotherhood's stored series intact

## 7. Projection and DTOs

- [x] 7.1 Add `MembershipKind`-derived `OpensOwnSeries` plus `VersionSlotKey` and `BranchHeadAnimeId` to `SeriesEntryDto`
- [x] 7.2 Add a slot descriptor to `SeriesDto` — slot key, its alternatives in order, and its `defaultBranchHeadAnimeId`
- [x] 7.3 Split `BuildStats` so the pick-dependent figures are computed over a supplied member subset, and emit `statsByPick` — one `SeriesStatsDto` per admissible combination of picks, capped at 24 combinations with later slots pinned to their default beyond the cap
- [x] 7.4 Keep `BuildScores` over the whole main line, alternatives included, and confirm the all-member averages still include version-neighbour members
- [x] 7.5 Confirm `SeriesListService`/`SeriesRankingIndex` and the profile's Top series read the default combination, and that `SeriesBulkBuildBackgroundService`'s coverage check treats a `FoldedVersion`-only anime as covered and a `NeighbourTelling`-only anime as not
- [x] 7.6 Verify the series browser's eligibility query ignores version-neighbour memberships

## 8. Frontend

- [x] 8.1 Extend `frontend/src/api/types.ts` with `opensOwnSeries`, `versionSlotKey`, `branchHeadAnimeId`, the slot descriptors and `statsByPick`
- [x] 8.2 Use `entry.opensOwnSeries` for the tile target in `SeriesExtraTile`, replacing the `relationGroup` inference
- [x] 8.3 Render version slots in the main line and the timeline ribbon: one position per slot, a picker over its alternatives, branch entries shown only for the picked alternative, and display numbering computed over the visible entries
- [x] 8.4 Index `statsByPick` by the current pick for every stat, progress bar and time-left figure; leave the score averages on the series-wide values
- [x] 8.5 Add the pick to the series page's restorable view state beside the type filter and per-group state, ignoring a restored pick that names no current alternative
- [x] 8.6 Style the picker in `SeriesPage.css` in the page's existing control language

## 9. Verification

- [x] 9.1 Update or delete the `split-series-by-version` tests that assert anchor partitioning, shared members, boundary members and multi-telling persistence
- [x] 9.2 Run the backend test suite in the `sdk:10.0` container and the frontend build under Node 22
- [x] 9.3 Rebuild against the live database and confirm: Clannad is one series with a two-entry main line; Demon Slayer is one series with one slot; Fate/stay night is one series with four alternatives; Fullmetal Alchemist is still two series; the Kyou-hen tile opens its detail page
- [x] 9.4 Spot-check that the 256 stored series re-derive on read without error and that no series is left with a one-entry main line it should not have
