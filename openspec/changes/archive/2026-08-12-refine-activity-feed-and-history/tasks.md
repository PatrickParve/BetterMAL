## 1. Backend: deterministic ordering and the composer

- [x] 1.1 In `Data/Repositories/ActivityLogRepository.cs`, order both `GetRecentAsync` and `GetAllAsync` by `Timestamp` descending then `Id` descending, so rows written in the same request have a defined order (newest-written first).
- [x] 1.2 Add `Services/Profile/ActivityFeedComposer.cs` (static, no dependencies) with a `Summarize` entry point that turns a log row — plus an optional merged score and an optional collapsed episode range — into a single display phrase, following the phrase table in `design.md`.
- [x] 1.3 In the composer, parse `ChangeDetail` per change type (`"Added as {status}"`, `"Episode N"`, `"{from} -> {to}"`) and fall back to the raw `ChangeDetail`, or a humanized change-type name when it is null, whenever the stored text doesn't match the expected shape — this is what keeps rows written by older versions rendering.
- [x] 1.4 In the composer, humanize `WatchStatus` names for display (`OnHold` → "On hold", `PlanToWatch` → "Plan to watch"), mirroring the frontend's `STATUS_LABELS`.
- [x] 1.5 In the composer, add the field-group mapping used by the feed's collapsing: `Progress` (`EpisodeIncremented`, `Completed`), `Score`, `RewatchCount`, `Membership` (`Added`, `Removed`).
- [x] 1.6 In the composer, add the completion+score merge rule: a `ScoreChanged` row merges into a `Completed` row for the same anime when the two are adjacent in that anime's event stream and the score's timestamp is at or after the completion's and within 5 minutes of it; the merged row keeps the completion's timestamp, `Id`, and change type.

## 2. Backend: feed and history builders

- [x] 2.1 Add `Summary` to `ActivityFeedItemDto` in `Services/Profile/ProfileDto.cs`, keeping `ChangeType` and `ChangeDetail`, and fill it wherever feed items are constructed in `ProfileService`.
- [x] 2.2 In `ProfileService.BuildActivityFeed`, add `RewatchCountChanged` to the kept change types.
- [x] 2.3 In `ProfileService.BuildActivityFeed`, apply the completion+score merge before any other collapsing, so a completion carries its score and the standalone score row is dropped.
- [x] 2.4 Replace the feed's "consecutive same-anime progress event" check with per-anime, per-field-group collapsing: walking most-recent-first, keep a row only when its `(AnimeId, FieldGroup)` pair has not been seen. Keep the `IsGenuineIncrease` filter so episode decreases still never reach the feed.
- [x] 2.5 Raise `RecentActivityFetchWindow` from 200 to 400, since collapsing is subtractive and can otherwise leave the feed short of its 20 items after a long session on one anime.
- [x] 2.6 In `ProfileService.CollapseEpisodeRuns`, apply the completion+score merge while keeping every episode row: a completed run renders as the `Episodes a-b` row followed by the merged `Completed — Score N` row, and repeated same-field edits are all kept.
- [x] 2.7 Re-read `ProfileService` for comment accuracy — the existing comments on `BuildActivityFeed` and `CollapseEpisodeRuns` describe the old collapsing rules and must describe the new ones.

## 3. Frontend: single-phrase rows

- [x] 3.1 Add `summary: string` to `ActivityFeedItemDto` in `frontend/src/api/types.ts`.
- [x] 3.2 In `pages/ProfilePage.tsx`, render `item.summary` as the row's meta text in place of the `CHANGE_TYPE_LABELS[...] — changeDetail` pair.
- [x] 3.3 In `components/EditHistoryOverlay.tsx`, render `item.summary` as the row's detail text in place of the same pair.
- [x] 3.4 Delete `CHANGE_TYPE_LABELS` from `frontend/src/utils/anime.ts` and drop its now-unused imports.

## 4. Frontend: history search and date range

- [x] 4.1 In `components/EditHistoryOverlay.tsx`, add local `search`, `fromDate`, and `toDate` state with a title search input and two `<input type="date">` controls in a filter row between the overlay title and the list.
- [x] 4.2 Filter the fetched rows in a `useMemo`: case-insensitive substring match against both `animeTitle` and `animeEnglishTitle`, and inclusive whole-day local-time date bounds (reduce each timestamp to `YYYY-MM-DD` via the `en-CA` locale and compare against the inputs' values), with each filter optional and all of them combining.
- [x] 4.3 Add a Clear control that resets all three filters, shown only while at least one filter is active.
- [x] 4.4 Add a "no history matches these filters" empty state, distinct from the existing "No activity yet." message for a genuinely empty history.
- [x] 4.5 Style the filter row in `components/EditHistoryOverlay.css` so it sits inside the existing `modal--wide` layout without the list losing its `scroll-y` treatment or the overlay growing past the viewport.

## 5. Verify

- [x] 5.1 Compile the backend: `rsync -a --exclude 'bin/' --exclude 'obj/' backend/ /private/tmp/bm-build/` then `docker run --rm -v /private/tmp/bm-build:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 bash -c "dotnet build AnimeTracker.Api/AnimeTracker.Api.csproj -c Release"` (local SDK is 9.0; `~/Documents` cannot be bind-mounted).
- [x] 5.2 Build the frontend: `cd frontend && PATH="$HOME/.nvm/versions/node/v22.23.1/bin:$PATH" npm run build` (default node is v16 and fails under Vite).
- [x] 5.3 Run the stack and check "Latest updates" against the spec: no phrase is repeated in a row, an addition names its status, a rewatch-count change appears, finishing an anime shows one `Completed — Score N` row (both when scored in the editor and via the completion prompt), and score-then-rescore or episode-bounce edits leave one row per field.
- [x] 5.4 Check the full history: rows read as one phrase, a completed run shows the episode-range row above the merged completion row, repeated edits are all still listed, and status/date changes appear with their own phrasing.
- [x] 5.5 Check the history filters: title search matches English and default titles, each date bound works alone and together, filters combine with the search, Clear restores the full list, and a no-match state shows its message.
- [x] 5.6 Update `CODE_GUIDE.md` where it describes the profile activity feed and `EditHistoryOverlay` (sections covering `ActivityLogRepository`, `ProfileService`, and the overlays list) to match the new composer, collapsing rules, and history filters.
