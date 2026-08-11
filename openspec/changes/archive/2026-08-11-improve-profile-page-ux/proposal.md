## Why

The profile page has accumulated layout and readability problems that make it feel unfinished: poster tiles shrink instead of scrolling, the stats box stretches so wide that a number no longer visually belongs to its label, distribution bars barely leave their track, the activity feed hides two of the edits it should report, and overlay scrollbars sit on top of content. Separately — and across every page, not just this one — navigating away and coming back with the browser's back gesture tears the page down and rebuilds it from scratch, so the app flashes empty, loses the filter that was selected, and jumps back to the top of the page. Both classes of problem are cheap to fix and both are felt on every visit.

## What Changes

**Cross-page navigation (whole app)**

- Returning to a page via browser back/forward SHALL restore it as it was left: the data already fetched renders immediately (no empty flash), the page's view controls keep their selections, and the scroll position is restored.
- Restored data is refreshed in the background so a restored page is never permanently stale.
- Restoration is scoped to back/forward navigation. Arriving at a page by clicking a nav link is still a fresh visit with default filters and top-of-page scroll — the existing "filter resets on revisit" behaviour is preserved for that case.

**Profile page — layout**

- "My top anime" poster tiles keep a fixed size instead of shrinking as the list grows; the strip scrolls horizontally, matching "Most rewatched".
- Switching either strip's media-type filter no longer clears the box while the new list loads — the previous list stays on screen until the new one is ready.
- Both poster strips lose their incidental vertical scroll — a trackpad can currently nudge them up and down a few pixels.
- The "Anime stats" box is constrained to a narrow, resolution-independent width so each number stays visually paired with its label.
- The "Latest updates" list fills its box down to the bottom edge instead of stopping at a fixed height.
- Scrollbars in the "Latest updates" box and both overlays are laid out beside the content rather than floating over the cards' right edge.

**Profile page — rating distribution**

- Bar length is scaled against the most-common score, so the largest bar fills the track and the rest are drawn in proportion to it.
- Each row's count is followed by that score's share of all rated anime as a percentage.

**Profile page — activity**

- "Latest updates" additionally reports completions and score changes. A completion supersedes the episode increment that caused it, so finishing a show reads as "Completed", not as a final episode bump.
- Long titles are truncated with an ellipsis; hovering a truncated title shows the full title in a small box that follows the cursor.
- The full-history overlay shows the English title when one exists (it currently shows the Japanese/romaji title), shows each anime's poster, truncates titles to two lines with the same hover tooltip, compacts a run of consecutive episode-watched entries into a single "Episodes low-high" row, and keeps its scrollbar beside the rows.

## Capabilities

### New Capabilities

- `page-state-restoration`: how the web client preserves and restores a page across back/forward navigation — cached page data rendered immediately with a background refresh, restored view-control selections, and restored scroll position — and how a fresh navigation differs from a restored one.

### Modified Capabilities

- `profile-stats`: top-anime strip sizes tiles fixed and scrolls rather than shrinking; poster strips scroll horizontally only; rating-distribution bars scale to the largest bucket and each row shows a percentage; the latest-updates feed includes completions (superseding their episode increment) and score changes; latest-updates and full-history rows truncate titles with a cursor-following tooltip; the full-history overlay shows English titles and posters.
- `navigation-and-search`: adds the requirement that a truncated title anywhere is recoverable by hovering it, since the English-title and hover-state rules for shared list rows already live here.

## Impact

- **Frontend — new shared code**: a page-state store plus hooks for cached page data, restorable view state, and scroll restoration; a hover-tooltip component for truncated titles.
- **Frontend — every page**: `HomePage`, `SeasonPage`, `TopAnimePage`, `AiringPage`, `MyListPage`, `ProfilePage`, `SearchPage`, `AnimeDetailPage` adopt the cached-load hook; pages with view controls (`MyListPage`, `ProfilePage`, `SearchPage`, `SeasonPage`, `AiringPage`) adopt restorable view state.
- **Frontend — profile**: `ProfilePage.tsx` / `.css`, `EditHistoryOverlay.tsx` / `.css`.
- **Backend**: `ProfileService.BuildActivityFeed` widens the activity feed's change-type filter and collapses a completion with its triggering episode increment. No schema change, no new endpoint — `ActivityChangeType.Completed` and `ScoreChanged` are already written to `ActivityLog`.
- **No API contract change**: `ActivityFeedItemDto` already carries `changeType`, `animeEnglishTitle`, and `pictureUrl`; the history overlay simply stops ignoring the last two.
