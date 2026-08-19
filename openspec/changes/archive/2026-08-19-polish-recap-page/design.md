## Context

The recap page (`frontend/src/pages/RecapPage.tsx`, ~550 lines) renders everything itself except the rankings, which it delegates to the shared `RankingSection` / `RankingOverlay` pair also used by `ProfilePage`. Its data comes from one `GET /api/recap` call returning a `RecapDto` with the whole included set; the top-10 slice, the ranking basis, and the media-type narrowing are applied client-side.

Server-side, `RecapService` composes three helpers: `RecapEntrySelector` (which entries the period includes), `RecapStatsBuilder` (the stat block and hot takes), and `RecapRankingBuilder` (all four rankings). `ProfileService` reuses `RecapRankingBuilder` for its Favourite seasons / Favourite years, and both surfaces normalise scores through the shared `ScoreDivergence` helper — but the *gate* that turns a divergence into a named opinion (threshold plus like/dislike boundaries) lives only in `ProfileService` as five `private const` fields, which is exactly why the recap's hot takes drifted from it.

Two constraints shape the work:

- **Posters ride on existing DTO fields.** `RecapSeasonRankingDto.TopPosters` and `RecapYearRankingDto.TopPosters` already exist and are already rendered by `RankingSection`; only the `index == 0 ? … : []` guard in the builder keeps them empty below rank 1. No API shape changes.
- **The recap and the profile page must not disagree.** They share one ranking builder and one divergence normaliser by design (the archived `add-list-recaps` change made that explicit). Any gate added for hot takes has to be sourced from the same definition the profile page uses, not copied.

## Goals / Non-Goals

**Goals:**

- One heading that names the period, with the stat block given its own heading and moved right of the top 10.
- The app's standard accent hover applied to every linking row on the page, plus a hover state on the stat tiles.
- Every season/year ranking row illustrated, on the recap page and the profile page, inline and in the overlay.
- The four rankings regrouped into a season column and a year column.
- Hot takes gated on the profile page's rule, from one shared definition.
- One-step period navigation for season and yearly recaps, and a link from a season recap to its season page.

**Non-Goals:**

- No change to which entries a period includes, to the Bayesian weighting, to the time filter's fallback rules, or to the my-list scoping handoff.
- No change to `RecapDto`'s shape or to `GET /api/recap`'s contract.
- No new hover treatment: this change adopts the existing one rather than designing another.
- No arrows for multi-year recaps, and no re-theming of the profile page's opinion-divergence lists.

## Decisions

### 1. The hot-take gate moves to `ScoreDivergence`, not into a copy

`ProfileService`'s `OpinionDivergenceThresholdSd`, `OpinionDivergenceMyDislikeCeiling`, `OpinionDivergenceMyLikeFloor`, and `OpinionDivergenceMalLikeFloorAndDislikeCeiling` become public members of `ScoreDivergence` (`Services/Profile/ScoreDivergence.cs`), joined by two predicates over `(UserAnimeEntry entry, double divergence)` — one per direction — and a third that is their union. `ProfileService.BuildOpinionDivergence` then filters through the same two predicates instead of restating the conditions inline, and `RecapStatsBuilder.BuildHotTakes` filters through the union.

Alternatives considered: (a) leave the constants in `ProfileService` and have the recap reference them — rejected, it inverts the dependency (a recap helper reaching into a page service) and leaves the predicate logic duplicated; (b) a new `OpinionGate` class — rejected as a third file for four constants that already belong with the normalisation they threshold. `ScoreDivergence` is already the shared home and its doc comment already names both consumers.

The doc comment on the constants must survive the move: the "why 5 / 8 / 7.5" reasoning (MAL's own score labels) is the only record of where those boundaries came from.

### 2. Hot takes keep their cap of five and their ordering, and lose only the unqualified entries

`BuildHotTakes` gains one `.Where(...)` between the both-scored filter and the `OrderByDescending(Math.Abs(divergence))` it already has. Ordering by absolute divergence largest-first, tie-broken by title, is already correct — the user's "ordered by the highest difference" is satisfied by what is there today; the change is purely which entries are eligible. Five stays the cap.

Consequence, called out because it is a visible behaviour change: most periods will show fewer than five hot takes, and many will show none. That is the point — the gate exists so that "hot take" means a real disagreement — and the empty case keeps its existing note rather than dropping the section, so the page does not silently lose a heading between one period and the next.

### 3. Posters on every row: drop the index guard, keep the time rankings as they are

`RecapRankingBuilder.BuildSeasonRanking` and `BuildYearRanking` drop `index == 0 ? TopPosters(c.Scored) : []` in favour of `TopPosters(c.Scored)`. `BuildSeasonTimeRanking` / `BuildYearTimeRanking` keep their `index == 0` guard. `ProfileService` inherits the change for free because it calls the same two builders, and both `RankingSection` and `RankingOverlay` already render whatever posters a row carries — so the entire frontend delivery of this bullet is CSS.

Cost: three extra poster URLs per ranked row, on rankings capped at five inline rows but *uncapped* in the overlay payload (a multi-year recap over a long range can rank dozens of years and hundreds of seasons). `RecapRankingPosterDto` is three small fields, so a 200-season ranking grows the response by roughly 600 poster records rather than 3 — measurable but far below the `Items` list the same response already carries for the whole included set. Accepted; no pagination introduced.

The stale doc comments must be corrected in the same edit: `RecapDto.cs`'s "`TopPosters` is empty for every row but the leader" on both score-ranking DTOs, the shared summary on `RecapRankingPosterDto`, and `frontend/src/api/types.ts`'s equivalent comment. The time-ranking DTO's version of that sentence stays true and stays put.

**Follow-up: `BuildSeasonTimeRanking` drops its leader posters too.** Once decision 4 (below) puts the season ranking and seasons-by-time-watched side by side in yearly mode, the time ranking's leader posters made that narrower row visually cramped next to its score-ranking neighbour. `BuildSeasonTimeRanking` now always passes `[]` for `TopPosters` rather than `index == 0 ? TopPostersByTime(c.Group) : []`; `BuildYearTimeRanking` is unchanged and keeps its leader-only posters, since years-by-time-watched still sits stacked under the year ranking (full column width) rather than beside anything. `RecapTimeRankingDto`'s doc comment and its `frontend/src/api/types.ts` equivalent now describe the two levels separately rather than with one shared sentence.

### 4. Ranking layout: one grid of two columns, replacing two grids of two cells

`.recap-page__ranking-pair` is used twice today — once for the score rankings, once for the time rankings. It is replaced by a single `.recap-page__rankings` grid holding two `.recap-page__ranking-column` flex columns: the season column (season ranking, then seasons-by-time-watched) and the year column (year ranking, then years-by-time-watched). A column renders only when at least one of its two rankings has rows, and the existing `:has(> :only-child)` trick collapses the grid to one column when only one side survives — which is what a yearly recap (season-level rankings only) gets.

Alternative considered: keep the two pair grids and reorder with `grid-auto-flow: column` — rejected, because the two rankings in a column must sit in *one* grid cell to stack cleanly, and a four-cell grid would tie the season ranking's height to the year ranking's rather than letting each column flow.

The 1024px breakpoint and the season-first stacking order carry over unchanged, and the DOM order (season column first) already produces the required narrow-display order with no `order` property needed.

**Follow-up: a yearly recap splits its one column into two.** A yearly recap never has year-level rankings, so under the rule above it got a single, un-split column stacking the season ranking directly above seasons-by-time-watched — full page width, one under the other. Feedback after using it: the two are more useful side by side, so a period's best-scored season and its most-watched season can be read across rather than down. `RecapPage.tsx`'s rankings block now special-cases `mode === 'yearly'`: it renders the season ranking and seasons-by-time-watched as two direct children of `.recap-page__rankings` (no `.recap-page__ranking-column` wrapper needed, since each column now holds exactly one section), reusing the same grid and the same `:has(> :only-child)` collapse for when only one of the two has rows. The multi-year season-column/year-column grouping is untouched. This is why the previous bullet's poster removal exists — the same visual crowding that motivated the side-by-side layout made the time ranking's leader posters unwelcome in its new, narrower column.

### 5. Stats move right by grid placement, and the DOM order flips

`.recap-page__lead` becomes `grid-template-columns: 1fr minmax(14rem, 20rem)` with the top-10 section written first in the JSX and the stat block second. Flipping the DOM rather than using `order: 1` keeps the visual and reading orders identical, which matters because the collapsed single-column layout is driven by DOM order.

This changes the narrow-display stacking from stats-above-top-10 to top-10-above-stats — a deliberate departure from the current `list-recaps` spec scenario. The requirement's own stated intent is "leading with the period's top anime rather than with its statistics"; keeping stats first on narrow displays contradicted that, and moving stats to the right makes the intent unambiguous in both layouts. Called out here because it is a behaviour change nobody asked for directly.

### 6. Two hover treatments, both reusing what exists

- **Rows** (top-10, hot takes, ranking rows inline and in the overlay) take `background: var(--accent-bg); border-color: var(--accent-border); box-shadow: var(--shadow)` on `:hover, :focus-within`, with `transition: background-color .12s ease, border-color .12s ease, box-shadow .12s ease` — copied verbatim from `.top-anime-card` and `.anime-card::before`. The row elements already carry `border: 1px solid var(--border)`, so no pseudo-element plate is needed (that trick exists on `AnimeCard` only because a bare poster has no border to tint). `RankingSection.css`'s existing `.recap-ranking-row__link:hover { border-color: var(--accent-border) }` is the partial version of this and gets completed rather than replaced.
- **Stat tiles** take the same three properties. They are not links, so `:focus-within` is meaningless on them and only `:hover` is applied.

Alternative considered: a shared `.row-hover` utility class in `index.css` — attractive but out of scope; it would touch four other pages' CSS to be worth having, and this change would be the only consumer. The duplication is three declarations in two files.

### 7. Period stepping reuses SeasonPage's arithmetic, bounded by the recap's own year list

`shiftSeason(year, season, delta)` and `seasonPointIndex(year, season)` already exist in `SeasonPage.tsx` as private functions. Rather than importing across pages or copying, both move into `frontend/src/utils/anime.ts` alongside the existing `seasonLabel` (which `RecapPage` already imports from there), and `SeasonPage` imports them back. That keeps one definition of "what is the season after fall" in the app.

Bounds come from the recap's *own* year options — `[min(EARLIEST_YEAR, selected), max(currentYear, selected)]`, the same widening range `yearOptions()` already computes — so an arrow is disabled exactly when the select it sits beside has nothing further to offer. In season mode that is winter of the low year through fall of the high year. Note the recap's `EARLIEST_YEAR` is 1960 and deliberately differs from the season browser's 1989: a recap looks at watch history, not at MAL's catalogue.

Markup follows `.season-page__nav`'s: two `<button>`s carrying `&lsaquo;` / `&rsaquo;` with `aria-label="Previous season"` / `"Next season"` (or `"…year"` in yearly mode) and `disabled` at the bounds, wrapped around the existing period selects.

### 8. The season-page link, and widening the season browser's year list to accept it

The link is a plain `<Link to={/season?year=${startYear}&season=${season}}>` rendered only in season mode.

That exposes a latent bug: `SeasonPage`'s `yearOptions` widens *upward* to include a URL-addressed year past the ceiling but does not widen *downward*, so `/season?year=1975` renders a `<select>` whose value is not among its options. The season-browser spec already requires that "a season addressed directly in the URL SHALL still be rendered as asked", so the fix is in-spec: floor the list at `Math.min(EARLIEST_YEAR, year)`, mirroring the existing `Math.max(ceiling.year, year)` above it. Two words of change, and it means the recap link needs no range gate of its own.

Alternative considered: only offer the link for years ≥ 1989 — rejected as papering over the bug with a second hard-coded 1989.

### 9. The title composes from the period label that already exists

`periodLabel` is already computed for the empty-state message and the removed `<h2>`. The `<h1>` becomes `` `${periodLabel} recap` `` and the standalone `<h2 className="recap-page__period-label">` is deleted along with its CSS rule. The mode tabs stay in the header row beside the title, so the header keeps its existing `justify-content: space-between` shape and the `page-header-design` vocabulary (explicit size, weight, zeroed margin) is untouched.

The stat block's new `<h2>Stats</h2>` reuses `.recap-page__section h2` by wrapping the stats in a `<section className="recap-page__section">`, so it matches "Top 10", "Season ranking", and "Hot takes" without a new rule.

## Risks / Trade-offs

- **Most periods will show no hot takes** → Expected and intended; the empty note stays so the section never vanishes mid-browse. Worth watching in use: if a typical year genuinely yields zero, the shared threshold may be tuned later — but it must be tuned for both surfaces at once, which decision 1 makes possible.
- **Existing backend tests will fail by design** → `RecapStatsBuilderTests`'s five-hot-take assertions and `RecapRankingBuilderTests`'s leader-only-poster assertions encode the old rules. They are rewritten as part of the change, not deleted; the poster tests invert to assert every row carries posters, and the hot-take tests gain fixtures that sit either side of each boundary.
- **Promoting `ProfileService`'s constants risks changing profile behaviour** → The move is mechanical (constants and two predicates, same values, same comparisons). `ProfileServiceOpinionDivergenceTests` is the guard: it must pass unchanged. If it needs edits, the move was not mechanical.
- **Larger recap payloads on long multi-year ranges** → Bounded by three posters per ranked group; a decade ranks 40 seasons and 10 years. The pathological case is a 60-year range in the overlay, still an order of magnitude below the `Items` array beside it. Accepted rather than paginated.
- **`shiftSeason` moving out of `SeasonPage`** → It is exercised today only through `SeasonPage`'s arrows and there are no frontend tests to catch a regression. Mitigation: move it verbatim, change no arithmetic, and verify both pages' arrows by hand including the year-boundary and bound-disable cases.
- **Stats-below on narrow displays** → Someone on a phone now scrolls a ten-row list before reaching the summary. Judged the lesser evil against contradicting the section-order requirement's own rationale; if it reads badly in practice the collapse point, not the order, is the thing to revisit.

## Migration Plan

No data migration, no API contract change, no feature flag. Backend and frontend land together — the poster change is backward-compatible in both directions (an old client renders extra posters fine; a new client renders a leader-only payload fine), so ordering between the two is not load-bearing.

## Open Questions

None blocking. One deferred: whether the row-hover declarations eventually deserve a shared utility class alongside the other four pages that hand-roll them (decision 6) — worth doing when a fifth consumer appears, not now.
