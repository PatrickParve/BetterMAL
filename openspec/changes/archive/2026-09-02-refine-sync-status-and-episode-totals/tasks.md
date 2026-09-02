## 1. Episode-total schema and derivation (design D8, D11)

- [x] 1.1 Add `MalTotalEpisodes` and `AniListTotalEpisodes` (`int?`) to `Models/AnimeMetadata.cs`, and update the entity's doc comment to describe the three-value episode-count shape the way it already describes the three picture values.
- [x] 1.2 Add `ResolveTotalEpisodes()` to `AnimeMetadata` setting `TotalEpisodes = MalTotalEpisodes ?? AniListTotalEpisodes`, documented as the only writer of `TotalEpisodes`.
- [x] 1.3 Change `MalMappingExtensions.ApplyTo` and `ApplyLeanTo` to write `MalTotalEpisodes` (keeping the existing `is null or 0 → null` normalisation) and then call `ResolveTotalEpisodes()`, so no MAL path writes `TotalEpisodes` directly.
- [x] 1.4 Add the EF migration for both columns with a `UPDATE "AnimeMetadata" SET "MalTotalEpisodes" = "TotalEpisodes"` backfill; confirm the generated snapshot carries both columns.

## 2. Rewatching survives every MAL read-back (design D1, D2)

- [x] 2.1 Add `MalStatusResolution.ResolveAgainstLocal(WatchStatus? local, WatchStatus remote)` in `Services/Mal`, returning `Rewatching` when local is Rewatching and remote is Watching, and `remote` otherwise — with a comment naming why (`ToMalStatusString` pushes Rewatching as `watching`).
- [x] 2.2 Call it from `MalListStatus.ApplyTo(target, now)` against `target.Status`, and document that it is inert on the create path because `ToUserAnimeEntry` starts from a default-`Watching` entry. This is what fixes `ResyncService`, which needs no change of its own.
- [x] 2.3 In `ReconciliationService`, resolve against the **local** entry's status when building the diff row so `ToDiffEntry` records Rewatching rather than MAL's `watching`; keep the existing `statusMatches` comparison carve-out.
- [x] 2.4 In `ReconciliationService.AcceptPendingDiffAsync`, resolve again against the live entry's status before assigning `local.Status`, so a diff computed before the entry became a rewatch cannot demote it.
- [x] 2.5 Confirm `EntryActivityRecorder.Diff` therefore logs no status change for a preserved rewatch on either path.

## 3. The shared "everything has aired" predicate (design D3)

- [x] 3.1 Add `EverythingHasAired(AnimeMetadata anime, int? episodesAired)` to `Services/Entries/AiredEpisodeGate.cs` as the union `AiringStatus != "currently_airing" || (TotalEpisodes is { } total && episodesAired is { } aired && aired >= total)`, with a comment naming why it is a union and not a replacement (stale MAL status on one side, incomplete AniList data on the other).
- [x] 3.2 Change `RewatchingEligibility.AnimeHasFinishedAiring` to call it; add the `int? episodesAired` parameter to `IsEligible` and `IneligibilityReason`, and reword the reason to "the anime has not aired in full".
- [x] 3.3 Pass `airedSoFar` at both `RewatchingEligibility` call sites in `UserAnimeEntryEditService` (the Rewatching arm of `ApplyStatus` and the count-drop arm of `ApplyEpisodesWatched`); both already have it in scope.

## 4. Completion is decided by episode counts (design D4)

- [x] 4.1 In `UserAnimeEntryEditService.ApplyEpisodesWatched`, change `completionTarget` to `anime.TotalEpisodes` unconditionally and delete the aired-so-far branch, updating the comment to explain that the episodes-watched cap already guarantees an entry can only reach its total once every episode has aired.
- [x] 4.2 Add a private `CanComplete(anime, airedSoFar, hasAired)` = `hasAired && anime.TotalEpisodes is not null && AiredEpisodeGate.EverythingHasAired(anime, airedSoFar)`, and gate both the automatic completion arm and `ApplyStatus`'s explicit Completed arm on it.
- [x] 4.3 In `ApplyStatus`, delete the `isCurrentlyAiring` branch: the aired-so-far fill, the finish-date suppression (`entry.CompletedAt ??= today` becomes unconditional), and `CannotCompleteUnknownAiredCountException` with its handler mapping. Add `CannotCompleteBeforeFullyAiredException` to `Services/Entries/EntryEditExceptions.cs` with a reason the `action-failure-notices` path can show, and keep `CannotCompleteUnknownEpisodeCountException` for the unknown-total case.
- [x] 4.4 Confirm the automatic path stays silent when `CanComplete` is false (matching today's unknown-total behaviour) while the explicit path throws, and that the Rewatching completion arm still raises the rewatch count when a rewatch reaches the total.

## 5. One airing watch-status service, both directions (design D5, D6)

- [x] 5.1 Rename `ICompletedEntryReopenService`/`CompletedEntryReopenService` to `IAiringWatchStatusService`/`AiringWatchStatusService` with a single `SettleAsync(IReadOnlyCollection<UserAnimeEntry> entries, IReadOnlyDictionary<int, int> airedSoFarByAnimeId, CancellationToken ct)`; update the DI registration in `Program.cs`.
- [x] 5.2 Re-open direction: `Completed && AiringStatus == "currently_airing" && !(TotalEpisodes is { } t && EpisodesWatched >= t)` → `Watching`. Comment both halves — why the `currently_airing` guard stays (imported partial completions on finished shows must not churn) and why it reads the total rather than the aired count (a stale status must not un-complete a finished run).
- [x] 5.3 Completion direction: `Watching && TotalEpisodes is { } t && EpisodesWatched >= t && EverythingHasAired(...)` → `Completed` with `CompletedAt ??= today`, excluding `Rewatching` explicitly; note that its real job is a total that arrived after the viewing.
- [x] 5.4 Keep the fresh tracked re-read, activity row, `PendingSync`, sync scheduling and `DbUpdateConcurrencyException` handling on both directions.
- [x] 5.5 Update the four call sites — `MainDashboardService`, `MyListService`, `AnimeDetailService`, `EpisodeScheduleRefreshBackgroundService` — to pass every loaded entry rather than a pre-filtered subset, and to share the aired-count dictionary each already builds.
- [x] 5.6 Keep the ordering constraint in `MainDashboardService`: settle before the carousel selects entries, so a re-opened show reaches Currently watching on the same visit.

## 6. Caught-up entries leave the carousel (design D7)

- [x] 6.1 In `MainDashboardService.GetDashboardAsync`, resolve aired-so-far once with the bulk `EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata>, ...)` overload instead of per card inside the DTO loop, and feed the same dictionary to `SettleAsync`.
- [x] 6.2 Filter the Watching/Rewatching set before ordering: drop an entry where `aired is { } a && a > 0 && EpisodesWatched >= a`. Comment that no airing-status condition belongs here, and why `>=` and `a > 0`.
- [x] 6.3 Build the currently-watching DTOs from the bulk dictionary, leaving `NextAiringInstantAsync` as the only remaining per-card await.
- [x] 6.4 Confirm the Current season and Airing today sections still include a caught-up airing show.

## 7. AniList supplies an unknown total (design D9, D10)

- [x] 7.1 Add `episodes` to `LookupQuery`'s `Media` selection and to `ScheduleQuery`'s `Media(id:)` selection in `AniListClient`, and carry it on `AniListMediaLookup` and `AniListScheduleResult` as `int? Episodes`, normalising `0` to null.
- [x] 7.2 In `EpisodeScheduleRefreshService.RefreshOneCoreAsync`, switch the anime read from `AsNoTracking()` to a tracked read, and take the total as `schedule.Episodes ?? lookup.Episodes` (schedule first, matching how `status`/`nextAiringAt` are already merged).
- [x] 7.3 Capture `TotalEpisodes` before the write, set `AniListTotalEpisodes`, call `ResolveTotalEpisodes()`, and where the effective total moved from null to non-null record `AnimeUpdateKinds.EpisodeCountReleased` via the already-injected `IAnimeUpdateRecorder` — not via `IAnimeMetadataChangeDetector`.
- [x] 7.4 Confirm the AniList path writes no other `AnimeMetadata` field, and that a lookup returning no media leaves both episode-count columns untouched.
- [x] 7.5 Confirm `AnimeUpdateService` renders the new update from the anime's live `TotalEpisodes`, so an AniList-sourced count needs no rendering change.

## 8. Frontend

- [x] 8.1 In `EntryEditorOverlay.tsx`, add an `everythingHasAired` helper mirroring `AiredEpisodeGate.EverythingHasAired` (`airingStatus !== 'currently_airing' || (totalEpisodes !== null && episodesAired !== null && episodesAired >= totalEpisodes)`), with the usual "mirrors the backend" comment naming its counterpart.
- [x] 8.2 Change `completionTarget` to `totalEpisodes` unconditionally and `canComplete` to `totalEpisodes !== null && everythingHasAired`, updating the comment that says it mirrors `ApplyEpisodesWatched`.
- [x] 8.3 Change `canEnterRewatching` to test `everythingHasAired` in place of `airingStatus === null || airingStatus === 'finished_airing'`, so a stale airing status no longer hides the option; it needs `totalEpisodes` and `episodesAired` passed in.
- [x] 8.4 Verify the Completed and Rewatching options are disabled through the existing `option.value === ... && (!canComplete || !hasAired)` gate, and that a rejection reason still surfaces if a stale form submits one.
- [x] 8.5 In `CompletionPromptContext.tsx`, drop the now-redundant `saved.completedAt !== null` condition and rewrite the comment: every completion reachable from the progress row concerns a fully-aired anime, so the finish-date proxy no longer distinguishes anything. Keep the `previousStatus !== 'Rewatching'` carve-out.
- [x] 8.6 Check no other surface predicts completion from an aired count (`MyListRow`, `SeriesEntryRow`, `AnimeDetailPage`); progress rows and bars are unaffected.

## 9. Tests

- [x] 9.1 `Services/Sync`: reconciliation records Rewatching in the diff when another field differs; accepting keeps Rewatching; a diff computed pre-rewatch does not demote; a remote `dropped` still applies.
- [x] 9.2 `Services/Sync`: a corrective re-sync leaves a Rewatching entry Rewatching while applying its other fields, and creates a new entry as Watching from a remote `watching`.
- [x] 9.3 `Services/Entries`: `EverythingHasAired` is true for a finished status with no airing data, true for a stale `currently_airing` whose aired count has reached the total, false while episodes are still to come, and false for a currently-airing anime with an unknown total.
- [x] 9.4 `Services/Entries`: reaching the total of a stale-`currently_airing` anime completes with a finish date; reaching the aired-so-far count of a run still to come leaves the entry Watching with no finish date; an explicit Completed before the run is out is rejected; an unknown total still refuses and never auto-completes.
- [x] 9.5 `Services/Entries`: Rewatching is accepted for a fully-aired run whose MAL status is stale, and still refused while episodes are to come.
- [x] 9.6 `Services/Entries`: the settle service completes a Watching entry once its total becomes known, fills a missing finish date, preserves an existing one, leaves Rewatching and unknown-total entries alone, re-opens a partial-count Completed entry on an airing anime, does **not** re-open one that has watched the total under a stale status, does not touch a `completed`-at-0 entry on a finished anime, and is idempotent in both directions.
- [x] 9.7 `Services/Dashboard`: a caught-up entry is absent from Currently watching regardless of airing status and present in Current season; it returns once the aired count grows; an unknown aired count is never filtered out; a part-way rewatch is kept; nothing is written by the filter.
- [x] 9.8 `Services/Airing`: an AniList total fills an unknown MAL total and records one `EpisodeCountReleased`; a known MAL total wins; a later MAL total supersedes; a MAL refresh reporting nothing does not blank an AniList total; a second refresh records no second update; no other update kind is recorded. (The MAL-side clauses — a later MAL total superseding, a MAL refresh reporting nothing not blanking an AniList total — are covered under 9.9, where `MalMappingExtensions.ApplyTo`/`ApplyLeanTo` live.)
- [x] 9.9 `Services/Mal`: `ApplyTo` writes `MalTotalEpisodes` and re-derives the effective total; `ApplyLeanTo` does the same and still leaves rich fields alone.
- [x] 9.10 Update the existing tests that assert the removed behaviour — the aired-so-far completion target, `CannotCompleteUnknownAiredCountException`, the reopen service's old signature and condition, and `RewatchingEligibility`'s airing-status-only test.

## 10. Verify

- [x] 10.1 Build and run the backend test suite in the `sdk:10.0` Docker image (the local SDK is 9.0); confirm the migration applies against a fresh database.
- [x] 10.2 Build the frontend with Node 22 via nvm (the default `node` is v16) and typecheck.
- [X] 10.3 Run the app end to end against the real list: check that a rewatch survives a corrective re-sync, that a caught-up show is absent from Currently watching but still Watching in My List, that a run whose final episode has aired completes and prompts for a score even while MAL still calls it airing, and that *Tensei shitara Ken deshita II* reads `watched/12` with an episode-count update on the home page.
- [x] 10.4 Run `openspec validate refine-sync-status-and-episode-totals --strict`, then `/opsx:verify` before archiving.
