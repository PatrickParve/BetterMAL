## Why

Two small gaps in how MAL scores and list rows read today. First, users who hide MAL scores globally (to avoid anchoring their own ratings) still want to see the crowd score for shows they've already finished — the score can't bias a rating they've already made, so hiding it there is pure noise. Second, the "Plan to watch" group in My list shows only the media type (TV/movie), giving no signal about whether a show is already out, still airing, or hasn't started — exactly the thing you want to know when deciding what to queue next.

## What Changes

- Add a **Score display** setting on the Settings page: a persisted toggle, "Always show MAL scores for completed shows", that when on reveals the MAL score of any anime the user has **completed** even while the global hide-scores toggle is active. It defaults to off, preserving today's behavior, and does not touch the global toggle.
- Make the per-score component honor this setting: a MAL score belonging to a completed entry is shown in full (no blur, no reveal button) whenever the setting is on, regardless of the global hide state. All other scores keep their existing blur/reveal behavior.
- Add an **airing-status indicator** next to the media type on **Plan to watch** rows in My list — "Not aired", "Airing", or "Aired" (mapped from MAL's `not_yet_aired` / `currently_airing` / `finished_airing`) — so the type reads e.g. `TV · Airing`. Other status groups are unchanged.
- Flow the anime's airing status through the My list API so the row has the value to render (the field is already stored on the cached anime record; it is simply not yet included in the my-list payload).

## Capabilities

### New Capabilities
<!-- None: both features extend existing capabilities. -->

### Modified Capabilities
- `score-visibility`: Add a requirement for an opt-in setting that always reveals MAL scores for completed anime while the global hide toggle is on; clarify that this override is scoped to completed entries and persists like the global toggle.
- `library-views`: Extend the "My list grouped and ordered by status" requirement so Plan-to-watch rows also show an airing-status indicator (Not aired / Airing / Aired) alongside the type.

## Impact

- **Frontend**
  - `frontend/src/context/ScoreVisibilityContext.tsx` — add a second persisted boolean (e.g. `alwaysShowCompletedScores`) with its own `localStorage` key and setter.
  - `frontend/src/components/ScoreValue.tsx` — accept a `completed` prop; when set and the new setting is on, render the value unblurred.
  - `frontend/src/pages/SettingsPage.tsx` (+ `.css`) — new "Score display" section hosting the toggle.
  - `frontend/src/pages/MyListPage.tsx` (+ `.css`) — pass `completed` to `ScoreValue`; render the airing badge for Plan-to-watch rows.
  - `frontend/src/pages/AnimeDetailPage.tsx` / `TopAnimePage.tsx` — pass `completed` where a MAL score sits next to a known entry status (consistency).
  - `frontend/src/api/types.ts` — add `airingStatus` to `MyListItemDto`.
  - Shared airing-status label mapping (`frontend/src/utils/anime.ts`) reused by My list and the detail page.
- **Backend**
  - `backend/AnimeTracker.Api/Services/Library/MyListItemDto.cs` and `MyListService.cs` — add `AiringStatus` to the record and populate it from `Anime.AiringStatus`. No schema/migration change (the column already exists on `AnimeMetadata`).
- No database migration, no MAL API changes.
