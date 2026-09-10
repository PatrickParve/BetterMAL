## 1. Stop the sync paths recording

- [x] 1.1 `Services/Import/InitialImportService.cs`: remove `isBaselineRun`, its comment, the `isBaselineRun` parameter of `ImportOneAsync`, and both guarded `EntryActivityRecorder.Added` calls; drop the `Services.Entries` using if nothing else needs it (design D7)
- [x] 1.2 `Services/Sync/ReconciliationService.cs` `AcceptPendingDiffAsync`: remove both recording calls and the `EntrySnapshot? before` bookkeeping that only fed them, leaving the apply logic (including the `PendingSync` skip and new-entry creation) unchanged (D1)
- [x] 1.3 `Services/Sync/ResyncService.cs`: remove both recording calls and the entry `before` snapshot with its comment; keep the metadata `changeDetector.Snapshot` path untouched (D1)
- [x] 1.4 Check the three services' class comments and any remaining comments for claims that they record activity, and correct them

## 2. Remove the origin from the backend

- [x] 2.1 `Models/ActivityLog.cs`: delete the `ActivityChangeSource` enum and the `Source` property with its doc comment
- [x] 2.2 `Services/Entries/EntryActivityRecorder.cs`: drop the `source` parameter from `Added` and `Diff` and every `Source =` line; rewrite the class comment to describe it as building the rows for a declined held change (its only caller) (D8)
- [x] 2.3 `Services/Sync/HeldChangeService.cs`: drop `Source` from the inline removal row and the two recorder calls in the decline paths; delete the `Source` clause in `GetUnsentChangesAsync` (D6)
- [x] 2.4 `Data/Repositories/ActivityLogRepository.cs`: delete the `Source` clause in `GetEpisodeProgressInRangeAsync`; rewrite the method's doc in `IActivityLogRepository.cs` to say it returns every episode row in range because the log holds only changes made in the app (D5)
- [x] 2.5 `Services/Profile/ProfileDto.cs`: remove `Source` from `ActivityFeedItemDto`; `Services/Profile/ProfileService.cs`: stop passing `log.Source` in `ToActivityFeedItem`
- [x] 2.6 `Data/AnimeTrackerDbContext.cs`: remove the `Source` property configuration

## 3. Migration

- [x] 3.1 Generate `RemoveActivityLogSource` with `dotnet ef migrations add` in the `mcr.microsoft.com/dotnet/sdk:10.0` image: rsync `backend/` (excluding `bin/`, `obj/`) to `/private/tmp`, install `dotnet-ef` 10.x in the container, run it there, then copy the migration, its Designer file and the updated `AnimeTrackerDbContextModelSnapshot.cs` back (D4)
- [x] 3.2 In `Up`, before `DropColumn`, add `migrationBuilder.Sql` deleting rows `WHERE "Source" IN ('MalStartupImport', 'MalReconciliation', 'MalResync')`, with a comment explaining why it is by value rather than `<> 'BetterMal'` (held-decline rows are kept) and not by id (the second device's rows differ)
- [x] 3.3 Make `Down` re-add the column with default `'BetterMal'`, with a comment that deleted rows come back only from the dump

## 4. Frontend

- [x] 4.1 Delete `frontend/src/components/MalOriginTag.tsx` and `MalOriginTag.css`
- [x] 4.2 `frontend/src/api/types.ts`: remove the `ActivityChangeSource` type with its comment, and `source` from `ActivityFeedItemDto`
- [x] 4.3 `components/EditHistoryOverlay.tsx` and `.css`: remove the import and the tag; collapse `.edit-history__detail` + `.edit-history__detail-text` into one span carrying the current text styles, including the ellipsis truncation (D9)
- [x] 4.4 `pages/ProfilePage.tsx` and `.css`: same for `.profile-list-row__meta` + `.profile-list-row__meta-text`
- [x] 4.5 `pages/SettingsPage.tsx`: rewrite the held-changes, reconciliation-diff and re-sync sentences that say "marked as coming from MyAnimeList", per the `settings-page` delta and D10
- [x] 4.6 Confirm `pages/AnimeDetailPage.tsx` (`formatSource(detail.source)`) is untouched

## 5. Tests

- [x] 5.1 `Services/Import/InitialImportServiceActivityTests.cs`: replace the baseline tests with "an import records nothing", covering a first import, an import once activity exists, and the backfilled-entry branch; keep "an anime already having an entry records nothing"
- [x] 5.2 `Services/Sync/ReconciliationServiceActivityTests.cs`: assert that accepting a diff with changed fields and with a new entry applies the values and writes no rows; keep "declining records nothing"; fold tests that differed only in what they recorded
- [x] 5.3 `Services/Sync/ResyncServiceActivityTests.cs`: the changed-entry and new-entry tests assert no rows; leave the anime-updates tests as they are
- [x] 5.4 `Services/Sync/HeldChangeServiceActivityTests.cs`: drop the `Source` asserts and the "UnderTheHeldDeclineOrigin" name; add "declining a held removal MyAnimeList still lists records one addition"
- [x] 5.5 `Services/Entries/EntryActivityRecorderTests.cs`: drop the source argument and asserts
- [x] 5.6 `Data/Repositories/ActivityLogRepositoryTests.cs`: replace the origin tests with one test that `GetEpisodeProgressInRangeAsync` returns every episode row in range and nothing outside it or of another type
- [x] 5.7 Delete `Services/Recap/RecapMalOriginTests.cs`; add a recap test that a declined held change raising episodes 5 → 7 inside a period adds 2 to it and places the anime there
- [x] 5.8 `Services/Profile/ProfileServiceMalOriginTests.cs`: check each test's non-origin assertion against the existing Profile tests, move any uncovered case there without `Source`, then delete the file
- [x] 5.9 `grep -rn 'ActivityChangeSource\|MalOriginTag\|mal-origin\|isBaselineRun'` over `backend` and `frontend/src` finds nothing outside `Migrations/`
- [x] 5.10 When syncing specs, rewrite the Purpose paragraph of `openspec/specs/activity-recording/spec.md` — it still lists the sync paths as recording and mentions "the origin every record carries" and the baseline import, which delta specs don't update

## 6. Verify and deploy

- [x] 6.1 Build the backend and run the full test suite in the `sdk:10.0` container
- [x] 6.2 Run `npm run build` and `npm run lint` in `frontend/` with nvm's Node 22
- [x] 6.3 Before rebuilding the running stack, take a dump: `docker compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > <outside the repo>/bettermal-before-log-only-my-own-edits.dump`, and confirm `docker compose exec -T postgres pg_restore --list < <that file>` lists `ActivityLogs`
- [x] 6.4 `docker compose up -d --build backend frontend`; confirm `ActivityLogs` has no `Source` column, holds 913 rows, the profile feed and edit history show no badges, and the Settings copy reads as specified
- [x] 6.5 On the second device: record its `BetterMal` + `MalHeldDecline` row count, repeat 6.3–6.4, and confirm the count matches, before any export/import change lands
