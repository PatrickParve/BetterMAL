## 1. Backend: flow airing status into the my-list payload

- [x] 1.1 Add `string? AiringStatus` to the `MyListItemDto` record in `backend/AnimeTracker.Api/Services/Library/MyListItemDto.cs`
- [x] 1.2 Populate it from `e.Anime.AiringStatus` in the projection in `backend/AnimeTracker.Api/Services/Library/MyListService.cs`
- [x] 1.3 Build the backend (sdk:10.0 image per project convention) and confirm the my-list endpoint returns `airingStatus` for entries

## 2. Frontend: score-visibility setting plumbing

- [x] 2.1 In `frontend/src/context/ScoreVisibilityContext.tsx`, add `alwaysShowCompletedScores: boolean` plus a setter/toggle to the context value, persisted to `localStorage` under a new key `bettermal.alwaysShowCompletedScores` (mirroring the existing `hidden` persistence), defaulting to off
- [x] 2.2 In `frontend/src/components/ScoreValue.tsx`, add a `completed?: boolean` prop (default `false`); read `alwaysShowCompletedScores` from context and render the plain value with no reveal control when `completed && alwaysShowCompletedScores`, otherwise keep the existing hidden/reveal logic
- [x] 2.3 Verify the no-leak guarantee still holds: a non-completed hidden score (or completed with the setting off) still keeps the numeric value out of the DOM

## 3. Frontend: Settings page toggle

- [x] 3.1 Add a "Score display" `settings-box` section to `frontend/src/pages/SettingsPage.tsx` with a labeled toggle for "Always show MAL scores for completed shows" wired to the context flag
- [x] 3.2 Add supporting styles in `frontend/src/pages/SettingsPage.css` consistent with the existing settings boxes/buttons
- [x] 3.3 Confirm the toggle persists across reload and does not alter the global hide toggle

## 4. Frontend: pass `completed` where a score sits next to a known status

- [x] 4.1 `frontend/src/pages/MyListPage.tsx` — pass `completed={item.entry.status === 'Completed'}` to the row's `ScoreValue`
- [x] 4.2 `frontend/src/pages/AnimeDetailPage.tsx` — pass `completed={detail.entry?.status === 'Completed'}` to its `ScoreValue`
- [x] 4.3 `frontend/src/pages/TopAnimePage.tsx` — pass `completed={entry?.status === 'Completed'}` to its `ScoreValue`

## 5. Frontend: airing badge on Plan-to-watch rows

- [x] 5.1 Add `airingStatus: string | null` to `MyListItemDto` in `frontend/src/api/types.ts`
- [x] 5.2 Add a shared short-label mapping in `frontend/src/utils/anime.ts` (`not_yet_aired → "Not aired"`, `currently_airing → "Airing"`, `finished_airing → "Aired"`) returning `null` for unknown/missing values
- [x] 5.3 In `MyListPage.renderRow`, for `PlanToWatch` entries with a mapped label, render an airing badge next to `my-list-row__type` (e.g. `TV · Airing`); render nothing extra for other statuses or unknown status
- [x] 5.4 Add badge styling in `frontend/src/pages/MyListPage.css`

## 6. Verification

- [x] 6.1 Build the frontend with the nvm v22 toolchain (per project convention) and confirm no type errors
- [x] 6.2 Manually verify: with global hide on + setting on, completed scores show fully while others stay blurred; with setting off they blur like the rest
- [x] 6.3 Manually verify: Plan-to-watch rows show Not aired / Airing / Aired next to the type; other groups are unchanged
- [x] 6.4 Run `openspec validate completed-scores-setting-and-airing-badges` and resolve any issues
