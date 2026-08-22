## 1. Backend: the shared predicate and its rejections

- [x] 1.1 Add an internal helper in `Services/Entries/` that answers "has this anime aired an episode?" from an `AnimeMetadata` plus a nullable aired-so-far count, per design D1: known count → `>= 1`; unknown count → `AiringStatus != "not_yet_aired"`. Comment it as the counterpart of the frontend helper in task 4.1.
- [x] 1.2 Add exception types in `Services/Entries/` for the new rejections — episodes watched on an unaired anime, a status not permitted on an unaired anime, a score on an unaired anime, a rewatch count on an unaired anime, and Completed with no known fill target on a currently-airing anime — each carrying a message naming the reason.
- [x] 1.3 Map the new exceptions to `400 Bad Request` in `Controllers/EntriesController.cs`, alongside the existing `CannotCompleteUnknownEpisodeCountException` arm.

## 2. Backend: `UserAnimeEntryEditService` rules

- [x] 2.1 Resolve the aired-so-far count and the predicate once at the top of `UpdateEntryAsync`, before the `Apply*` steps, so every step reads the same values (the service already calls `EpisodesAiredAsOfAsync` inside `ApplyEpisodesWatchedAsync` — hoist that call rather than adding a second one).
- [x] 2.2 `ApplyEpisodesWatchedAsync`: when the anime has aired no episode, the ceiling is 0 — reject any value above 0 instead of falling back to `anime.TotalEpisodes`. Setting 0 stays accepted.
- [x] 2.3 `ApplyStatus`: reject Completed, Dropped, and On hold when the anime has aired no episode. Watching and Plan to watch stay accepted.
- [x] 2.4 `ApplyStatus`: rework the Completed arm per design D4 — for `currently_airing`, require a known aired-so-far count and fill `EpisodesWatched` to it; for every other status, keep the existing known-total requirement and fill to the total. Leave the `CompletedAt ??=` never-overwrite behaviour untouched.
- [x] 2.5 `ApplyScore`: reject a score of 1–10 when the anime has aired no episode; keep accepting 0 ("no score") so an existing score can always be cleared.
- [x] 2.6 `ApplyRewatchCount`: reject a value above 0 when the anime has aired no episode; keep accepting 0.
- [x] 2.7 Confirm the auto-complete branch in `ApplyEpisodesWatchedAsync` is unchanged — it must still fire only at a known `anime.TotalEpisodes`, so reaching the latest aired episode of a part-broadcast run leaves the entry Watching.
- [x] 2.8 Completing a `currently_airing` anime SHALL NOT set a finish date (found during post-implementation review, not in the original design): guard both `CompletedAt ??= today` call sites — the explicit Completed edit in `ApplyStatus` and the auto-complete arm in `ApplyEpisodesWatchedAsync` — on `anime.AiringStatus != "currently_airing"`, so a caught-up-but-still-airing entry doesn't get a stale finish date before D6 re-opens it.
- [x] 2.9 The `ApplyEpisodesWatchedAsync` auto-complete/count-drop arm SHALL use the aired-so-far count, not the total, as its completion target for a `currently_airing` anime (found during post-implementation review): reaching the latest aired episode via the "+" control or the in-place count field now completes the entry the same way an explicit Completed edit already does (2.4), and a later count drop below that target returns it to Watching (never Rewatching, per the existing eligibility rule). A `currently_airing` anime with an unknown aired-so-far count silently does not auto-complete, mirroring the existing unknown-total behavior. A finished anime's auto-completion target is unchanged (the total).
- [x] 2.10 Fixed `UserAnimeEntryEditServiceRewatchingTests.LoweringTheCountOnAStillAiringAnimeYieldsWatchingInsteadOfRewatching`, which relied on the pre-2.9 behavior of using the total (not the aired-so-far count, which its fake hardcoded to null) as the currently-airing completion target — updated its fake to accept a real aired-so-far value and adjusted the scenario to a coherent caught-up state.

## 3. Backend: re-opening a completed entry

- [x] 3.1 Add a single re-open service exposing one method that takes entries plus their already-resolved aired counts and, for each Completed entry whose anime is `currently_airing` and whose aired count exceeds its episodes watched, sets `Status = Watching`, writes an `ActivityLog` row, and sets `PendingSync = true`. Leave `EpisodesWatched` and `CompletedAt` alone. Both callers below use this one method.
- [x] 3.2 Schedule each re-opened entry's sync through `IEntrySyncScheduler`, the same path an interactive edit uses — not an inline MAL call, so the render path stays free of MAL requests.
- [x] 3.3 Make the write path tolerant of concurrent readers: catch `DbUpdateConcurrencyException`, reload, and continue with the winner's value rather than surfacing an error on a page render. The operation is idempotent, so a losing race needs no retry.
- [x] 3.4 Call it from the read paths that display an entry, so an entry reads as Watching the first time it is looked at after the episode aired:
  - `MyListService` — free: it already resolves an aired count for every entry before building any row.
  - `AnimeDetailService` — free: it already resolves the aired count for the one anime it renders.
  - `MainDashboardService` — **must run on the full entry list before the `Where(e => e.Status == Watching)` filter**, otherwise a still-Completed entry is discarded before it can be re-opened and would never appear in Currently watching. This one is not free: the dashboard filters to Watching before resolving any aired counts, so add a single **bulk** `EpisodesAiredAsOfAsync` lookup over the Completed-and-`currently_airing` entries — one query, not one per entry.
- [x] 3.5 Call it as a backstop sweep at the end of `EpisodeScheduleRefreshBackgroundService.RunTickAsync`, after the refresh and recheck passes, selecting Completed entries whose anime is `currently_airing` and resolving their aired counts with the **bulk** `EpisodesAiredAsOfAsync` overload in one query.

## 4. Backend: DTOs the frontend gate needs

- [x] 4.1 Add `AiringStatus` to `CurrentlyWatchingItemDto` (`Services/Dashboard/MainDashboardDto.cs`) and populate it in `MainDashboardService`. Keep the existing `CurrentlyAiring` flag — the blue aired fill still keys off it.
- [x] 4.2 Add `AiringStatus` and `EpisodesAired` to `TopAnimeItemDto` and populate them in `TopAnimeService.GetRankingAsync`, resolving the aired counts for the whole ranking through the bulk `EpisodesAiredAsOfAsync` overload rather than one call per row.

## 5. Backend tests

- [x] 5.1 Add `Services/Entries/UserAnimeEntryEditServiceAiringGateTests.cs` covering the rejections: episodes watched above 0, Completed, Dropped, On hold, a score of 1–10, and a rewatch count above 0 — each on an anime that has aired no episode, each leaving the entry unchanged.
- [x] 5.2 Cover the clear-always-allowed cases: score → "no score", rewatch count → 0, and episodes watched → 0 on an unaired anime all save.
- [x] 5.3 Cover the predicate's branches: a stored past airing row beats a `not_yet_aired` status; `finished_airing` with no stored rows counts as aired; an unrecorded airing status counts as aired.
- [x] 5.4 Cover the Completed rules: a finished anime fills to its total; a currently-airing anime fills to its aired count and not to the total; a currently-airing anime with an unknown aired count is refused; a finished anime with an unknown total is still refused.
- [x] 5.5 Add tests for the re-open rule: an airing anime whose aired count passed the watched count is set back to Watching with its finish date and episodes watched intact and an activity row written; a caught-up entry is untouched; a `finished_airing` entry whose aired count exceeds its cached total stays Completed; applying it twice writes only one activity row.
- [x] 5.6 Add a test that a read path re-opens the entry — a service read taken with the clock past a stored future episode's air instant returns the entry as Watching, without any refresh having run.
- [x] 5.7 Add a dashboard-specific test for the ordering trap: a Completed, currently-airing entry whose next episode has just aired appears in the currently-watching section on that same read. This fails if the re-open runs after the Watching filter, which is the mistake the test exists to catch.
- [x] 5.8 Extend `Services/Library/TopAnimeServiceTests.cs` for the two new DTO fields, and add coverage that the dashboard's currently-watching items carry `AiringStatus`.

## 6. Frontend: the shared predicate and types

- [x] 6.1 Add `hasAiredEpisodes(airingStatus, episodesAired)` to `utils/anime.ts`, mirroring design D1, with a comment naming the backend rule it mirrors (as `SERIES_TRAVERSAL_RELATIONS` already does).
- [x] 6.2 Add `airingStatus` to `CurrentlyWatchingItemDto` and `airingStatus` / `episodesAired` to `TopAnimeItemDto` in `api/types.ts`, matching task 4.
- [x] 6.3 Add `airingStatus: string | null` and `episodesAired: number | null` to `EntryEditorTarget` in `api/types.ts`.

## 7. Frontend: hiding the progress row

- [x] 7.1 `components/MyListRow.tsx`: render the progress cell and the inline score dropdown only when `hasAiredEpisodes(item.airingStatus, item.episodesAired)`; leave both cells empty otherwise, keeping the row's column positions and height.
- [x] 7.2 `components/CurrentlyWatchingCarousel.tsx`: render the `ProgressBar` only when the card's anime has aired an episode; keep the next-episode countdown in the card footer either way.
- [x] 7.3 `pages/AnimeDetailPage.tsx`: render the `ProgressBar` only when the anime has aired an episode; keep the status text beside it and the action buttons beneath it unconditionally.
- [x] 7.4 Check the CSS for each of the three surfaces (`MyListRow.css`, `CurrentlyWatchingCarousel.css`/`AnimeCard.css`, `AnimeDetailPage.css`) so an empty progress cell or missing row collapses cleanly rather than leaving a gap or shifting a column.

## 8. Frontend: the entry editor

- [x] 8.1 `components/EntryEditorOverlay.tsx`: derive the anime's aired state from the new target fields and disable the Completed, Dropped, On hold, and Rewatching options when nothing has aired. Keep the existing `Completed` disable when there is no known fill target, and the existing `Rewatching` disable from `add-rewatching-status` (its own D0 eligibility check) — this new aired-state disable composes with that one rather than replacing it, so an option can be unavailable for either reason.
- [x] 8.2 Restrict the score select to "No score" when nothing has aired, so an existing score stays visible and clearable but no value can be chosen.
- [x] 8.3 Pin the rewatch-count input's max to 0 when nothing has aired.
- [x] 8.4 Show a short line in the editor explaining why those controls are unavailable, so the restriction reads as deliberate rather than broken.
- [x] 8.5 Pass `airingStatus` and `episodesAired` into `openEditor(...)` from all five call sites: `pages/AnimeDetailPage.tsx`, `pages/MyListPage.tsx`, `pages/TopAnimePage.tsx`, `pages/SeriesPage.tsx` (mapping `SeriesEntryDto.airedEpisodes`), and any other site added since.
- [x] 8.6 `context/CompletionPromptContext.tsx`: require `saved.completedAt !== null` alongside the existing `justCompleted` checks (found during post-implementation review, paired with 2.9) — reaching the aired-so-far count on a currently-airing anime now auto-completes the entry, but shouldn't pop the score prompt for a series that hasn't actually finished; the prompt fires only once a real finish date is set.

## 9. Verification

- [x] 9.1 Build the frontend (`nvm use 22 && npm run build` in `frontend/`) and run `npm run lint`.
- [x] 9.2 Build and test the backend via the `sdk:10.0` Docker image (see the project's build note — the local SDK is 9.0).
- [x] 9.3 Walk the change through the running app: an unaired anime in my list shows no progress cell or score control; its editor offers only Watching and Plan to watch, "No score", and rewatch 0; a `PATCH` sent by hand with `episodesWatched: 1` is refused with a 400 naming the reason.
- [x] 9.4 Confirm the read-only aggregates are untouched: the profile page's episode-progress bar, the series page's progress bar, and the dashboard's "Followed shows airing" bars all render as before.
- [x] 9.5 Run `openspec validate gate-editing-on-aired-episodes --strict`.
