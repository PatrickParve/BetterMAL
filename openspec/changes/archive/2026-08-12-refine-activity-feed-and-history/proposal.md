## Why

The activity rows on the profile page read badly and carry noise. Every row prints a change-type label and then a change detail that repeats it ("Removed from list — Removed from list", "Added — Added as Watching", "Completed — Completed"). Finishing an anime produces up to three separate rows (episode, completion, score) where one would say it better, and re-editing a field right after editing it floods "Latest updates" with intermediate values that nobody wants to see there. Rewatch-count changes — a field the editor exposes — never reach the feed at all. And the full history has grown long enough that finding a specific anime or a specific day in it means scrolling.

## What Changes

- **One phrase per activity row.** The label/detail pair is replaced by a single composed sentence for each change type, used by both "Latest updates" and the full history: "Added to list as Watching", "Removed from list", "Episode 5", "Episodes 4-8", "Score 8", "Score cleared", "Rewatch count 2", "Watching → On hold", "Completed — Score 8".
- **Rewatch-count changes appear in "Latest updates"**, alongside additions, progress, completions, score changes, and removals.
- **A finished anime reads as one row.** A score set at (or right after) a completion merges into the completion row as "Completed — Score 8", in both the feed and the full history.
- **"Latest updates" collapses churn.** Within the feed, each anime keeps only its newest change per field group, so editing a score 8 → 9 → 8, or bouncing an episode count back and forth, leaves one row rather than a stack of them. The full history still records every step.
- **The full history keeps its episode rows.** A completed run shows the episode-range row followed by the merged "Completed — Score 8" row, so no episode entry is lost.
- **The full history gains search and a date range.** A title search box (matching either the default or English title) and from/to date inputs filter the list, each optional and combinable, with a way to clear them.

## Capabilities

### New Capabilities

None — this refines existing behavior.

### Modified Capabilities

- `profile-stats`: rewrites the "Latest updates activity feed" requirement (single-phrase rows, rewatch-count changes included, per-field churn collapsing, completion+score merging) and the "Full edit-history overlay" requirement (single-phrase rows, completion+score merging, title search and date-range filtering).

## Impact

- **Backend** — `Services/Profile/ProfileService.cs` (feed and history builders: phrase composition, merging, churn collapsing), `Services/Profile/ProfileDto.cs` (`ActivityFeedItemDto` gains the composed summary), `Services/Entries/UserAnimeEntryEditService.cs` (activity details written in a form the composer can parse for status and added-status text).
- **Frontend** — `components/EditHistoryOverlay.tsx` + `.css` (row rendering, search field, date range), `pages/ProfilePage.tsx` (row rendering), `api/types.ts` (DTO field), `utils/anime.ts` (`CHANGE_TYPE_LABELS` no longer drives row text).
- **Data** — no schema change and no migration: existing `ActivityLog` rows are re-read through the new composer, and rows written before this change still render sensibly.
- **API** — `/api/profile` and `/api/profile/activity` response shape gains a field; no endpoint or query-parameter changes (search and date filtering are applied client-side over the already-complete history payload).
