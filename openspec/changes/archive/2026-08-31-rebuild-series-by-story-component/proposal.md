## Why

`split-series-by-version` shattered franchises instead of separating retellings. Its rule — every main-line-eligible anime that carries an `alternative_version`/`alternative_setting` edge *anchors a series*, and every other member joins the anchor it is nearest to — misfires whenever more than one entry of the same story carries a version edge, which is the common case, not the rare one. Measured against the live database today:

- **Clannad** is three series (`Clannad`, `Clannad: After Story`, `Clannad Movie`), each with a one-entry main line. Clannad and After Story each carry their own `alternative_setting` special, so each anchors itself at distance 0 and neither can hold the other. After Story lands in Clannad's More as a *Sequel*.
- **Demon Slayer** is two near-identical series, 9 members and 8 main-line entries each, differing only in whether Mugen Ressha-hen is the movie or the TV arc.
- **Fate/stay night** is five series, one of them rooted at *UBW Prologue*, a `tv_special`.
- **Fullmetal Alchemist** splits correctly into 2003 and Brotherhood — but Brotherhood is stored as a counted member of the 2003 series, so it lifts that page's all-member average.
- A tile in an *Alternative version/setting* group links to `/series/<animeId>` unconditionally. Kyou-hen's primary series is the page it is already on, so clicking it navigates to itself.

The distinction the old rules were reaching for is real — Fullmetal Alchemist 2003 and Brotherhood *are* two stories — but the signal is not "carries a version edge". It is whether the two tellings are joined by a **story** relation at all. Brotherhood shares no `sequel`/`prequel`/`side_story` path with 2003; Clannad and After Story do; Demon Slayer's movie and TV arc do; every Fate route hangs off Fate/Zero's `sequel` edges.

## What Changes

- **BREAKING** A series is the connected component of the relation graph over **story relations only** — `sequel`, `prequel`, `side_story`, `parent_story`, `summary`, `full_story`, `spin_off`, and the companion-media `other` case. `alternative_version` and `alternative_setting` no longer split a series and no longer merge one; they decide only what appears in the More section, and where a tile points.
- **BREAKING** Nearest-anchor assignment, shared members that tie between anchors, boundary members forced ineligible for the main line, and one-entry tellings are all removed. Story components are disjoint, so an anime has exactly one series again in every ordinary case.
- A **version neighbour** — an anime linked to a member by a version relation but outside the story component — **folds in as an ordinary extra** when it has no story relations of its own (Clannad Movie, Fate/Prototype), and is otherwise a member whose tile opens **its own series page** (Brotherhood, Prisma☆Illya, Fate/Grand Order). Either way it is stored, counted and editable exactly as extras are today.
- **NEW** **Version slots in the main line.** Where main-line entries are alternative versions of each other they collapse into one numbered watch-order position holding a picker. Main-line entries reachable only through one alternative form that alternative's **branch** and are shown only while it is picked. Demon Slayer gets `2. Mugen Ressha-hen ( TV arc | Movie )`; Fate gets `Fate/Zero → Fate/Zero 2nd → ( F/SN '06 | UBW movie | UBW TV | Heaven's Feel )` with each route's sequels behind its own pick.
- The pre-selected alternative is the one the user has the most watch progress in; failing that the better of the alternatives by MAL score, with `PopularityRank` breaking a near-tie; failing that the earliest aired. The choice is remembered per series in the page's restorable view state.
- **BREAKING** Series figures split by kind: MAL and my score averages are computed over **every** main-line entry regardless of the pick, while episode total, runtime total, aired-episode count, progress and time-left are computed over the **trunk plus the picked branch**, so "time left" stops counting the same Grail War four times.
- The More tile's link target is decided by the server, not inferred client-side from the relation group, so a tile can never navigate to the page it is on.
- **BREAKING** A build persists **only the seed's story component**. Traversal runs story-first — the seed's own component is completed before any budget is spent looking outward — then extends one version hop to discover neighbours, and never expands through them. Neighbouring tellings are built on demand when their tile is followed.
- Every stored series rebuilds once on its next read.

## Capabilities

### New Capabilities

_None._ The behaviour introduced here belongs to capabilities `split-series-by-version` already established.

### Modified Capabilities

- `series-versions`: the split rule changes from per-anchor version partitioning to story components; nearest-anchor assignment, shared membership, boundary members and one-entry tellings are removed; version neighbours fold in or link out; version slots and branches inside a main line are added, along with their default pick.
- `series-page`: the main line becomes a sequence of positions rather than a flat chain, with version slots, branch visibility and per-pick numbering; score averages and total/progress figures take different member scopes; the picked alternative joins the restorable view state; a More tile's link target is carried on the wire.
- `series-browser`: one card per story component, with figures over that component's members, replacing one card per telling.
- `series-identity`: title, picture and offered titles resolve per story component, and the identity a stored series carries is inherited by the story component with the largest overlap when the old rules had split it.

## Impact

**Schema / data.** No key change — `SeriesMember`'s `(SeriesId, AnimeId)` key and `IsPrimary` stay, since a folded-in version neighbour can still be shared between two components. New `SeriesMember.VersionSlotKey` and `SeriesMember.BranchHeadAnimeId` (both nullable) carry a main-line entry's slot and branch. `SeriesGraphBuilder.ClassificationRevisedAt` is bumped so every stored series re-derives on its next read.

**Backend.** `SeriesRelations` (traversal set narrows to story relations; version relations become a discovery-only set), `SeriesVersionPartitioner` → replaced by a story-component splitter plus a main-line slot/branch resolver, `SeriesGraphBuilder` (two-phase traversal, single-component classification and persistence, fold-in decision, slot/branch assignment, identity matching), `SeriesService` (branch-scoped stats, link targets, picked-branch resolution), `SeriesDto`/`SeriesEntryDto`, `SeriesMainLineEligibility`, `SeriesListService`/`SeriesRankingIndex`, `SeriesBulkBuildBackgroundService`, `AdjacentAnimeSet`.

**Frontend.** `SeriesPage.tsx` (version slots, branch filtering, per-pick numbering and stats, persisted pick), `SeriesEntryRow`, `SeriesExtraTile` (server-supplied link target), the timeline ribbon, `SeriesPage.css`, and the series API types.

**Sequencing.** This change supersedes the composition rules of `split-series-by-version` and is written as a delta on that change's end state; `split-series-by-version` should be archived before this one is applied. Everything else it delivered — relation groups in More, the media-type filter buttons, related non-member entries, and the relation-discovery build trigger — is kept unchanged.

**Behaviour users will notice.** Franchises that were split into near-duplicate cards collapse back into one; Clannad, Demon Slayer and Fate regain a single page with a complete main line; Fullmetal Alchemist stays two series; series counts, averages and Top series entries change again for every franchise touched by the previous change.
