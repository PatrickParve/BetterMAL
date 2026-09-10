## Why

Changes 03 and 04 will copy the ranking and the activity log between my two devices. Neither table can yet hold what that copy needs.

The ranking has no usable time for deciding which device's copy is newer. `TopAnimeSelection.SelectedAt` exists on every row, but every write of the order rewrites every row with one shared value, and nothing reads it.

An activity-log row has no identity that means the same thing on both devices. `ActivityLog.Id` is a per-database identity column, and the two devices have already issued the same numbers for different events.

**Verified against the code and the live database** (2026-09-10):

- **`SelectedAt`:**
  - Declared at `Models/TopAnimeSelection.cs:15`.
  - Written only at `Data/Repositories/TopAnimeSelectionRepository.cs:54`, where `ReplaceOrderAsync` deletes every row and re-adds them all with one `now`.
  - Read nowhere in the application. The only other references are seeds in `SeasonRepositoryTests`, `TopAnimeSelectionRepositoryTests` and `AnimeRankingKeyOrderingParityTests`.
  - `SeasonRepository.cs:166` joins the table but reads only `Position`.
- **The ranking in the live database:** 455 rows (the brief said 453; two anime have been placed since). All of them hold one `SelectedAt`: 2026-09-09 07:59:51 UTC.
- **`ReplaceOrderAsync` is a slot-preserving merge** (`ITopAnimeSelectionRepository.cs:13-24`): given a stored `[A, B, C]` and an argument `[C, A]`, it produces `[C, B, A]`.
  - Its three callers are `AnimeRankingService.cs:105`, `:132` and `:153`.
  - The third runs on every score save (`UserAnimeEntryEditService.cs:110-111`).
  - Nothing replaces the stored order outright.
- **`ActivityLog.Id`** is a `long` identity column. The live table has 913 rows with ids 1–919.
- **The `Id` tiebreak carries real weight.** 94 distinct timestamps are shared by more than one row. The feed breaks those ties by `Id` descending (`ActivityLogRepository.cs:11-12`, `:19-20`), and that order is what lets `ActivityFeedComposer.FindCompletionScoreMerges` fold a completion and a score saved together into one row.
- **Correction to the brief:** `EntryActivityRecorder` is not the only writer of log rows. Seven sites in four files build them:
  - `UserAnimeEntryEditService`: 3
  - `AiringWatchStatusService`: 2
  - `HeldChangeService`: 1, inline
  - the recorder's own `Added` and `Diff`

  The archived change `log-only-my-own-edits` made unifying them a non-goal. The identifier is therefore assigned on the entity itself, which covers every writer at once (design D5).

## What Changes

**The ranking carries one last-modified time**

- `TopAnimeSelection.SelectedAt` and its column are dropped.
- A new singleton `RankingState` row holds `ModifiedAt`, in the same shape as `AiringRefreshState`. `ModifiedAt` is the time the stored order was last written. Null, or no row at all, means the ranking has never been arranged on this device.
- `ModifiedAt` is written in the same save as every write of the stored order. That covers:
  - a reorder in the ranking editor
  - a move on a series page
  - the automatic placement when a score is saved

  There is no comparison with the previous order, and no distinction between a hand reorder and an automatic placement.
- `Position` stays exactly as it is.
- A new `ReplaceAllAsync(animeIds, modifiedAt)` replaces the stored order with exactly the list it is given:
  - ids not in the list are removed
  - ids in the list take the list's positions
  - the stored time becomes `modifiedAt`

  It stores the time it is given, not the time it runs. Adopting another device's ranking is not arranging one, and stamping "now" on adoption would let a stale ranking outrank a newer edit on the other device. This follows the rule a series rebuild already uses when it moves a choice: the choice keeps its own time.
- `ReplaceOrderAsync` keeps its behaviour and stamps the current time.

**Every activity-log row carries a GUID**

- `ActivityLog` gains `EventId`, a `uuid` column with a unique index. A new row gets a fresh value when the row object is created, so every writer, now and later, produces one without doing anything.
- `EventId` is the only identity that is meaningful on another device. A row written with an `EventId` it already carries keeps that value; this is the property 04's import will rely on.
- `Id` stays the primary key and the ordering tiebreak. It is never exported. `ActivityFeedComposer`, the repository's ordering and `ProfileService` are unchanged.

**One migration does both**

- **Ranking:** creates `RankingStates` and seeds its single row from `max(SelectedAt)`, but only when the ranking has rows. It then drops `SelectedAt`, so my current ranking arrives carrying the real time it was last written rather than null.
- **Log:** adds `EventId`, backfills every existing row with `gen_random_uuid()`, makes the column non-null and adds the unique index.
- No row count, position, timestamp, local id or log content changes.

**Not in scope**

- Exporting or importing anything. No endpoint exposes the ranking time or `EventId`.
- A reader of the ranking time, and a caller of `ReplaceAllAsync`. Both arrive with 03 and 04.
- Moving the primary key to the GUID. `ActivityFeedComposer` keys its merge map by `long` `Id`, and moving the key would change code with no change in behaviour.
- Routing every log writer through `EntryActivityRecorder`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `data-persistence`:
  - **ActivityLog entity** gains the portable identifier. It also states that the local number stays local and orders records sharing a timestamp.
  - **Persisted manual top-anime selections** is removed. It describes a top-ten tie-break choice the app no longer has.
  - **Replacement requirement:** the stored ranking order carries one last-modified time, which every write of the order sets, and can be replaced exactly with a given list and time.
  - **New requirement:** covers the migration's backfills.

## Impact

**Backend**

- `Models/TopAnimeSelection.cs`: `SelectedAt` removed.
- `Models/RankingState.cs` (new) and `Data/AnimeTrackerDbContext.cs`: a `RankingStates` set, plus the unique index on `ActivityLog.EventId`.
- `Data/Repositories/ITopAnimeSelectionRepository.cs` and `TopAnimeSelectionRepository.cs`: `ReplaceAllAsync` added. Both write methods go through one private write that sets `RankingState.ModifiedAt` in the same save as the order.
- `Models/ActivityLog.cs`: `EventId` with a `Guid.NewGuid()` initializer. The comments on `Id` and `EventId` say which one travels.
- One EF migration and the updated model snapshot.

**Unchanged**

- `AnimeRankingService`, `UserAnimeEntryEditService`, `AiringWatchStatusService`, `HeldChangeService`, `EntryActivityRecorder`, `ActivityLogRepository`, `ActivityFeedComposer`, `ProfileService`.
- Every DTO and the frontend.

**Tests**

- 12 test doubles implement `ITopAnimeSelectionRepository` and each gains a throwing `ReplaceAllAsync` stub.
- Three test seeds drop `SelectedAt =`.
- `TopAnimeSelectionRepositoryTests` gains:
  - coverage of the ranking time on both write methods
  - exact replacement
  - the merge example from the brief
- A test pins that a new `ActivityLog` has a non-empty `EventId`, and that one set explicitly survives a save.

**Operations**

- The migration applies when the rebuilt backend starts, on each device separately.
- Nothing it does is lossy: the dropped per-row times all hold one value, and that value is kept as the ranking time. A dump is still taken first, as for every migration.
