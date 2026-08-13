## Why

The series page redesign (`add-series-page`, `redesign-series-page`) shipped recently, and living with it surfaced six rough edges: stats go blank more often than the data actually requires, a stat the app already tracks (rewatch count) isn't shown anywhere on the page, the timeline ribbon and the main-line list repeat the same chronology as two disconnected, visually noisy sections, the highest-MAL-score tile can name an entry the user hasn't watched, the Rebuild control sits in the most prominent spot on the page for an action used rarely, and the More-section collapse controls apply even when collapsing serves no purpose.

## What Changes

- **Episode/runtime totals use aired count as a fallback, not zero.** `EpisodesAndRuntime` currently contributes nothing for a main-line entry with an unknown `TotalEpisodes`, so a series whose latest season is airing with no announced total shows "Unknown" instead of a real (tighter) lower bound. It SHALL fall back to that entry's known `AiredEpisodes` count instead, keeping `HasUnknownEpisodeCounts` set so the total is still marked as a lower bound rather than exact.
- **Rewatch count surfaces on the page.** `UserAnimeEntry.RewatchCount` is already synced from MAL and used for the profile page's "Most rewatched" section but is invisible on the series page. Add a per-entry rewatch indicator on main-line rows/extra tiles (only when > 0) and a series-wide "Most rewatched" stat naming the entry(ies) tied for the highest rewatch count in the series — shown only when at least one member has been rewatched at all.
- **Timeline and Main series merge into one section.** The current timeline ribbon (thin flex-grow blocks with vertical score tick-bars, connected by unlabeled gap segments) and the numbered Main series list below it show the same main-line entries twice. They combine into a single chronological, numbered main-line list laid out on a time axis: each row keeps its picture, title, episode/status info, and MAL/my scores (colour-keyed blue/purMAL-purple-mine as already established for score chips), positioned and sized along the axis to make gaps between seasons visually legible. An entry with no air date is still shown, in a clearly marked "no date" state, rather than folded into an ambiguous flex block.
- **Highest MAL score hides the title, not just the score, when unwatched. BREAKING (behavioral):** the "Highest MAL score" stat currently always names and links the entry holding it, blurring only the score itself when the hide-scores toggle is on. It SHALL also withhold the entry's title/link — not just its score — unless the viewer has completed and scored that entry, matching the existing "shown in full only when completed and scored" gate that already governs the score.
- **Rebuild control moves out of the header banner.** It currently renders as its own full-width row directly under the hero header, ahead of every stat. It moves to a less prominent position (alongside the series-stats section, near the partial/truncated notice it already sits beside) so the header's first impression is the series itself, not an admin action.
- **More-section collapse/expand becomes conditional.** The collapse/expand affordance (per-group toggle and the "Expand/Collapse all" control) SHALL NOT be offered when every extra in the series is already marked Completed in my list, since there is nothing left worth hiding. The all-groups control SHALL read "Expand"/"Collapse" (no "all") when the series has only one extras group, and "Expand all"/"Collapse all" only when it has more than one.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `series-page`: Series stats (episode/runtime lower-bound fallback to aired count, new rewatched stat, highest-MAL-score title gating), Series timeline ribbon (merged into the main-line list, redesigned per-entry presentation), Main series and More sections (merged timeline presentation, rewatch indicator, conditional/relabeled collapse controls)

## Impact

- Backend: `AnimeTracker.Api/Services/Series/SeriesService.cs` (`EpisodesAndRuntime`, `BuildStats`), `SeriesDto.cs` (new stats fields for rewatch), possibly `SeriesEntryDto` to carry `RewatchCount` through (it's already on `UserAnimeEntryDto` reachable via `Entry`, so likely no DTO change needed there).
- Frontend: `pages/SeriesPage.tsx` (stats grid, rebuild control placement, More-section header/control logic), `components/SeriesTimeline.tsx` (merged into or replacing the main-line list rendering — likely absorbs `SeriesEntryRow`'s per-row info), `components/SeriesEntryRow.tsx`, `components/SeriesExtraTile.tsx` (rewatch indicator), associated `.css` files.
- No API surface removed; `SeriesStatsDto` gains fields (additive). No database schema changes — `RewatchCount` already persisted.
