## 1. Storing both ends

- [x] 1.1 Add `NewStartDate`, `NewBroadcastDayOfWeek` (JST) and `NewBroadcastTime` (JST) to `backend/AnimeTracker.Api/Models/AnimeUpdate.cs`, beside their `Previous*` counterparts, and correct the class summary so it says both ends of a move are stored rather than only the moved-from values (design D1).
- [x] 1.2 Add the same three fields to `Services/Updates/ScheduleMoveDetails.cs` and update its summary, which already misdescribes itself as moved-from only although it carries `NewEpisodeDate` (design D2).
- [x] 1.3 Fill them in `Services/Updates/AnimeMetadataChangeDetector.RecordFieldUpdates`, in the two branches that already set the `Previous*` values, reading from the `anime` entity that holds the just-written values.
- [x] 1.4 Write them in `Services/Updates/AnimeUpdateRecorder.RecordAsync` under the same kind-bit guards its existing lines use, so no value is stored for a kind the row does not carry.

## 2. Migration and backfill

- [x] 2.1 Decide the tooling route: either `dotnet tool update --global dotnet-ef --version 10.*` and scaffold, or hand-write the migration, its `.Designer.cs` and the model snapshot edit following the existing files — the installed CLI is 9.0.9 against EF Core 10.0.9 and will refuse to scaffold as-is (design D5).
- [x] 2.2 Create the migration adding the three nullable columns, with no defaults.
- [x] 2.3 Add the backfill statement from design D4 to its `Up`, with the `NOT EXISTS` and `AiredFrom <> PreviousStartDate` guards intact, and the reasoning written into the file the way `RetireUpdatesOutsideMyList.cs` does.
- [x] 2.4 Note in the migration's comment that `Down` drops the recorded destinations, so a future reader does not read that as an oversight.
- [x] 2.5 Run it against the live database and confirm all 6 existing `StartDateChanged` rows now carry a `NewStartDate` equal to their anime's premiere date, and that nothing else was touched.

## 3. Reporting what was recorded

- [x] 3.1 Add the three fields to `Services/Updates/AnimeUpdateDto.cs`, documenting the moved-to values as "as recorded, or the anime's current value where the row predates this being stored", and correct the summary's claim about what is read live (design D3).
- [x] 3.2 In `AnimeUpdateService.ToDto`, report `update.NewStartDate ?? anime.AiredFrom`, and the new slot from the recorded JST pair where present, falling back to the anime's live slot otherwise — converting both ends through the same `ConvertBroadcastSlot` path.
- [x] 3.3 Mirror the three fields on `AnimeUpdateDto` in `frontend/src/api/types.ts`.
- [x] 3.4 In `frontend/src/components/Updates/updateText.ts`, build the premiere-change and slot-change lines from the reported moved-to values rather than from `airedFrom` and the live slot, keeping the `Moved earlier` / `Delayed` wording and the equal-dates guard from `polish-updates-panel`.

## 4. Tests

- [x] 4.1 `AnimeUpdateRecorderTests`: a recorded premiere change stores both dates; a recorded slot change stores both slots; neither stores a value for a kind absent from the row.
- [x] 4.2 `AnimeMetadataChangeDetectorTests`: the detector passes the just-written values as the moved-to ends for both kinds.
- [x] 4.3 `AnimeUpdateServiceTests`: an update reports the pair it was recorded for after the anime's premiere moves again — the `anime-updates` scenario "A later correction does not rewrite an earlier update", which cannot be written today — plus the same for a slot move.
- [x] 4.4 `AnimeUpdateServiceTests`: a row with no recorded moved-to value still reports the anime's current value, so rows predating the backfill keep working.

## 5. Verification

- [x] 5.1 `dotnet build` and `dotnet test` in `backend/` pass.
- [x] 5.2 `npm run lint` and `npm run build` in `frontend/` pass (Node 22 via nvm, not the default v16).
- [x] 5.3 In the running app, confirm the six premiere-change cards read exactly as they did before the change — same dates, same direction word — since the backfill gave them the values they were already showing.
- [x] 5.4 Simulate a second move on one anime (move its `AiredFrom` in the database, let the detector record a new row) and confirm the older card still reports its own pair of dates and its own direction word, while the new card reports the new move.
- [x] 5.5 Run `openspec validate record-both-ends-of-a-schedule-move --strict`.
