## Why

I use this app on two devices and am about to make the activity log travel between them. Every change I make on one device reaches the other through MyAnimeList, and the receiving device's sync paths then record it *again* as rows of its own — later than the real edit, and coarser, because a reconciliation sees only the net change (three evenings of episodes 5 → 6 → 7 → 8 arrive as one 5 → 8 row). A union of the two logs would hold every change twice. Stripping those rows out at import time would be guesswork; not writing them at all is exact. Each device records only what was done on it, and the union of the two is the real history.

## What Changes

- The three paths that apply MyAnimeList's values — the startup import, accepting a reconciliation diff, and the corrective re-sync — stop writing to the activity log. What they apply to my list is unchanged.
- Declining a change held for review **is still recorded**. It undoes an unsent edit of mine, which only this device knows happened, and when MyAnimeList has no entry for that anime it removes the anime from my list. That must not happen without a trace.
- My own edits and removals, and the automatic completion and reopening of entries, keep recording exactly as they do today.
- The first-import guard (`isBaselineRun`) is removed. It existed only to stop a first import writing one addition per anime, and no import records anything any more.
- **BREAKING (data):** the `Source` column on `ActivityLogs` and the `ActivityChangeSource` enum are removed. With MyAnimeList never writing, every row is my own action and there's nothing left for the column to tell apart. The same migration deletes the 4 existing rows that aren't `BetterMal` (1 addition from a reconciliation, 3 status changes from a re-sync). They can't outlive the column that identifies them, and keeping them would bring back the duplication above the first time an export is imported. **Not reversible — take a database dump first.**
- **BREAKING (API):** `source` is removed from the activity feed and edit-history items, and the "via MAL" badge (`MalOriginTag`) is removed from Latest updates and the full edit history.
- Two queries lose their `Source` filter, because it would now match every row: the recap's episode-progress query and the held-change review's "unsent changes" query.
- The three Settings explanations that currently promise changes "marked as coming from MyAnimeList" are rewritten so they describe what actually happens now.
- Accepted consequence: a change made on MyAnimeList's own website appears in my history on no device.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `activity-recording`: *"Every path that changes my list records what it changed"* narrows to changes made in the app (my edits and removals, the automatic completion and reopening of an entry, and a declined held change) and is renamed to match. *"The import that establishes the baseline records nothing"* becomes the rule that nothing arriving from MyAnimeList is recorded — import, accepted reconciliation diff, or corrective re-sync. *"A record names where the change came from"* is removed.
- `profile-stats`: *"Changes that came from MyAnimeList appear alongside my own"* is removed. No such rows exist any more, and neither does their marker.
- `list-recaps`: *"A recap counts only progress recorded in the app"* now holds because of what gets recorded, not because of a filter. The recap reads every logged episode row, which includes the progress a declined held change applies.
- `settings-page`: in *"Every sync control states what it will do"*, the rule that each explanation says its changes are recorded "marked as coming from MyAnimeList" is replaced. Accepting a reconciliation diff and the corrective re-sync say that nothing they apply is recorded; the held-change review says that what declining applies is.

## Impact

- **Backend:** `Models/ActivityLog.cs`, `Data/AnimeTrackerDbContext.cs`, a new EF migration (plus the model snapshot), `Services/Entries/EntryActivityRecorder.cs`, `Services/Import/InitialImportService.cs`, `Services/Sync/ReconciliationService.cs`, `Services/Sync/ResyncService.cs`, `Services/Sync/HeldChangeService.cs`, `Data/Repositories/ActivityLogRepository.cs` and `IActivityLogRepository.cs`, `Services/Profile/ProfileDto.cs`, `Services/Profile/ProfileService.cs`.
- **Frontend:** `components/MalOriginTag.tsx` and `.css` (deleted), `components/EditHistoryOverlay.tsx` and `.css`, `pages/ProfilePage.tsx` and `.css`, `api/types.ts`, `pages/SettingsPage.tsx`.
- **Tests:** the activity tests for import, reconciliation, re-sync and held changes; `EntryActivityRecorderTests`; `ActivityLogRepositoryTests`; `RecapMalOriginTests`; `ProfileServiceMalOriginTests`.
- **Data:** 4 of 917 rows are deleted. The backend applies migrations on startup (`Database.Migrate()` in `Program.cs`), so the dump has to be taken before the rebuilt backend container starts.
- **Deliberately untouched:** the anime's *source material* (`AnimeMetadata.Source`, `AnimeDetailDto.Source`, `formatSource(detail.source)` on `AnimeDetailPage`), which has nothing to do with this.
