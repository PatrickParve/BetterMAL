## Context

Three independent adjustments land in one change because they share a review surface, not because they share code:

1. **Dropped scores.** The always-show setting is implemented as a boolean `completed` prop threaded into `ScoreValue` from ~12 call sites, each of which independently writes `entry?.status === 'Completed'`. The server mirrors the same rule in three places — `RecapService.ToRow`/hot takes, `ProfileService.ToDivergenceItem`, and `SeriesAverages.MainLineCompletedByMe` (consumed by `SeriesRankingIndex` for the profile's Top series tiles, and re-implemented client-side in `SeriesPage.malGroupRevealed`). Widening the rule means widening one predicate in each of those places.
2. **Navbar.** `Navbar.tsx` renders three flex children: a left link group (`flex: 1`), a centred search (`flex: 2`), and a right control group (`flex: 1`, `justify-content: flex-end`). The change removes the middle group.
3. **Profile.** `ProfileService.GetProfileAsync` already loads the whole entry list and builds every section from it. The two new sections — an all-list episode progress figure and the favourite seasons/years rankings — are both derivable from that same list, and the ranking math already exists in `RecapRankingBuilder` and must not be duplicated (the spec requires the profile and a recap to report the same weighted score for the same season).

## Goals / Non-Goals

**Goals:**

- One rule, stated once per side of the wire, for "this entry is settled, so its MAL score may be revealed".
- The profile's favourite seasons/years produce byte-identical weighted scores to a recap covering the same group, by construction rather than by parallel implementations kept in step.
- No second copy of the five-rows-plus-"See all"-overlay ranking UI.
- The always-show setting's stored preference survives the widening — a user who had it on keeps it on.

**Non-Goals:**

- The series page's **highest-MAL-score** box (`isCompletedAndScored`) is deliberately left on completed-only. It withholds the entry's *title and link*, not merely a number, and it does so regardless of the hide toggle — it protects against learning *which* entry is the series' best, which is a spoiler about entries still ahead of the user rather than a score they have already settled. Widening it is a separate judgement call, not implied by this change.
- The series page's `Completed` / `Caught up` header badges keep their completed-only definitions. A dropped season is not a completed one for the purpose of claiming the user finished a franchise.
- No change to the hide/reveal mechanics themselves: the placeholder, the per-score reveal control, the reserved-slot width rule, and the non-persistence of an individual reveal are untouched.
- My own scores are untouched — the hide toggle never governed them.

## Decisions

### 1. A single `settled` predicate per side, replacing the inline `status === 'Completed'` checks

Frontend: add `isScoreRevealableStatus(status)` (returns true for `'Completed'` and `'Dropped'`) to `utils/anime.ts`, and route every `ScoreValue`'s `completed` prop through it. Backend: add the equivalent to the `WatchStatus` side (an extension or small static helper) and use it in `RecapService.ToRow`, the hot-take projection, and `ProfileService.ToDivergenceItem`.

*Why:* the rule is currently written out longhand at every call site, so widening it means editing every one of them anyway — and leaving them longhand guarantees the next widening misses one. One named predicate makes the rule greppable and gives the tests a single unit to pin.

*Alternative considered:* keep the wire boolean but compute it centrally on the server and drop the client-side checks entirely. Rejected: several client call sites (my list rows, series entry rows, the detail page) render from DTOs that carry the raw entry status and no reveal flag; adding a flag to each of those DTOs is more churn than a shared client predicate, and the DTOs that *do* carry a server-computed flag (`malRevealed`) already exist for the cases where the rule is a group-level computation the client shouldn't redo.

*Naming note:* the `ScoreValue` prop stays named `completed` rather than being renamed to `settled`. Renaming it touches every call site a second time for no behavioural gain, and the prop's contract is documented in one place. The spec's requirement header likewise keeps its historical name; the user-facing label is what changes.

### 2. The stored preference key does not change

`bettermal.alwaysShowCompletedScores` in `localStorage` keeps its name and its `'true'`/`'false'` values. Only the Settings page's visible label changes, to "Always show MAL scores for completed and dropped shows" (and its hint text alongside it).

*Why:* renaming the key would silently reset the setting to off for anyone who had it on — a regression disguised as a rename. The key is an implementation detail nobody reads.

### 3. The series group rule widens with the per-entry rule

`SeriesAverages.MainLineCompletedByMe` becomes `MainLineSettledByMe`: a group qualifies when it has at least one finished-airing member and **every** finished-airing member is Completed **or** Dropped. `SeriesRankingIndex` (which feeds the profile's Top series tiles) and `SeriesPage.malGroupRevealed`/`isGroupCompleted` follow.

A finished-airing entry that is **not in my list at all** continues to fail the rule — `EntryStatus` is null there, and null is neither Completed nor Dropped. That is the existing behaviour and the spec now states it explicitly, because "not completed" and "not in my list" are easy to conflate once the predicate has two accepted values.

*Why:* this was the user's explicit call. It also keeps the surfaces consistent: it would be strange for a dropped entry's own row to show a MAL score while the average that entry participates in stays blurred.

### 4. Navbar: the search field joins the right group rather than staying a third flex child

`.navbar__search` stops being a sibling of the two link groups and becomes the first child of `.navbar__links--right`, with its own flex basis (it must not be squeezed to button width, and must not eat the whole row). The left group keeps `flex: 1`; the right group takes the remaining width with `justify-content: flex-end`. `SearchBar` itself is unmodified — only where it is mounted and what constrains its width.

Left group order becomes `Home, My List, Recap, Top, Season, Airing`. Recap is still built at render time (the current-year link), so moving it in `NAV_LINKS`-adjacent JSX must preserve that — it cannot simply be folded into the static `NAV_LINKS` array, whose entries are hoisted to module scope and would freeze the year for a tab left open across New Year.

The `max-width: 900px` wrap rule is kept, retargeted so the search field wraps onto its own row while the two groups keep their internal orderings.

*Alternative considered:* keeping three flex children and reordering with CSS `order`. Rejected: DOM order would then disagree with visual order, which breaks tab order — the search field would be tabbed to after Settings.

### 5. Episode progress is computed server-side and shipped in `ProfileDto`

A new `EpisodeProgressDto(int EpisodesWatched, int EpisodesTotal, int EntriesCounted, int TotalEntries)` is built alongside `AnimeStatsDto` in `BuildStats`'s neighbourhood, over the entries `GetProfileAsync` already has in hand. Entries whose `Anime.TotalEpisodes` is null are excluded from all four figures except `TotalEntries`; each counted entry contributes `Min(EpisodesWatched, TotalEpisodes)` to the watched side.

*Why the clamp:* `EpisodesWatched` is not structurally bounded by `TotalEpisodes` (MAL data can disagree with itself, and a total can shrink when metadata is refreshed). Without the clamp the bar can render past 100%, which reads as a bug rather than as data.

*Why server-side:* the client would otherwise need every entry's `TotalEpisodes`, which `ProfileDto` does not carry today — sending four integers beats sending the list.

*Placement:* its own full-width section beneath the top row of boxes, not inside the "Anime stats" box. That box is pinned narrow by `profile-stats`' own requirement (`clamp(200px, 18%, 260px)`), which is too narrow for a progress track to read as one.

*Rendering:* reuse `components/ProgressBar` with neither `onIncrement` nor `onSetWatched` passed — it already degrades to a plain read-only track and `watched/total` label in that case, so no new component and no changes to `ProgressBar` are needed.

### 6. Favourite seasons/years reuse `RecapRankingBuilder` verbatim, via a synthetic whole-list period

`ProfileService` builds a `RecapPeriod.MultiYear(minYear, maxYear)` from the earliest and latest air year among my entries that carry an `AiredFrom`, then calls the existing `RecapRankingBuilder.BuildSeasonRanking` / `BuildYearRanking` with that period, the air-dated entry set, and the same global mean (`ScoredMean` over the whole list). Both builders already drop groups with no scored anime, already apply the 5/20 trust thresholds, and already attach top-three posters to rank 1 only.

The DTOs are reused unchanged too: `ProfileDto` carries `List<RecapSeasonRankingDto> FavouriteSeasons` and `List<RecapYearRankingDto> FavouriteYears`.

*Why:* the spec requires the profile and a recap to report the same weighted score for the same season. Reusing the builder makes that true by construction. A second "all-time" builder would have to be kept in step by hand forever.

*The `C` term is already whole-list:* `RecapService` passes `ScoredMean(wholeList)`, not the period's mean — which is exactly what the profile wants, so no parameter changes.

*`ScoredMean` is currently a private static in `RecapService`.* It moves to a shared location (alongside `RecapRankingBuilder`) so `ProfileService` calls the same function rather than a copy.

*Cost:* `SeasonPoints` for a period spanning the full catalogue is `(maxYear - minYear + 1) * 4` — a few hundred lookups against an in-memory `ILookup`. Negligible next to the queries the call already makes.

*Empty case:* when no entry carries an air date, or when `ScoredMean` is null (nothing scored at all), both lists come back empty and the client renders the section's empty state.

### 7. Both new sections ride on `GET /api/profile` rather than getting their own endpoints

*Why:* unlike `top-series` — which was split out precisely because it runs its own `SeriesMembers ⋈ AnimeMetadata` join and enqueues background builds — these two are pure projections over the entry list `GetProfileAsync` already loads. A second round trip would cost more than the computation.

*Payload:* the rankings are sent in full (not truncated to five) because the "See all" overlay needs the tail. The row count is bounded by the number of distinct air seasons/years in one user's list — low hundreds at the extreme, each row a handful of scalars plus up to three posters on rank 1 only.

### 8. The ranking UI is extracted from `RecapPage` into a shared component

`describeSeasonRanking` / `describeYearRanking`, the `VISIBLE_RANK_COUNT = 5` slice, the inline `<ol>` row renderer, and the "See all N …" control move out of `RecapPage.tsx` into a shared module (e.g. `components/RankingSection.tsx`, sitting beside the existing `RankingOverlay`). `RecapPage` and `ProfilePage` both consume it; the overlay component itself is already shared and unchanged.

*Why:* the spec asks for "the same treatment the recap page's rankings use", in the literal sense — five rows, the same row anatomy, the same overlay. Copying ~40 lines into `ProfilePage` would guarantee the two drift.

*Scope discipline:* the extraction is a move, not a redesign. `RecapPage`'s rendered output must be unchanged by it — including the time-watched rankings, which share the same renderer.

*Side-by-side layout:* `ProfilePage` reuses the `recap-page__ranking-pair` grid pattern (two equal columns collapsing to one at the same breakpoint) under its own class name, rather than importing the recap page's stylesheet.

## Risks / Trade-offs

- **A `ScoreValue` call site is missed when widening the predicate** → the compiler cannot catch it, since the prop keeps its type. Mitigation: `tasks.md` enumerates all of them from a grep of `completed=`, and the work is verified by re-running that grep and confirming every hit routes through the shared predicate.
- **Widening `MainLineCompletedByMe` accidentally changes the series header badge** → the `Completed`/`Caught up` badge reads its own logic path, but the rename touches a file that badge code sits near. Mitigation: rename the function rather than only changing its body, so every call site must be revisited deliberately; assert the badge's completed-only behaviour in a test.
- **The navbar's right group overflows at mid widths** once the search field shares it with three controls → mitigation: the search field gets a flexible basis with a floor, and the existing wrap breakpoint is retested rather than assumed.
- **Extracting the ranking renderer regresses the recap page** → mitigation: the extraction lands as its own task with `RecapPage`'s output verified before the profile page consumes it.
- **The episode-progress exclusion looks like a bug to a user with many airing shows** ("why is my total lower than I expect?") → mitigated by the spec's requirement that the section state how many entries it counted out of the total, which makes the exclusion legible instead of mysterious.
- **Profile payload grows** by two ranking arrays on every profile load → bounded and small (see decision 7), and paid once per visit rather than per interaction.

## Migration Plan

No data migration. No API version break — `ProfileDto` gains fields, and `AnimeStatsDto` is unchanged. The `localStorage` key is deliberately preserved (decision 2), so no client-side migration either. Rollback is a straight revert; nothing persists state in a new shape.

## Open Questions

None outstanding. The two decisions that were genuinely the user's — the episode-progress denominator (unknown totals excluded from both sides) and whether the dropped extension reaches series-level averages (yes) — were resolved before this document was written and are recorded in decisions 5 and 3.
