## 1. Started-date fill (backend)

- [x] 1.1 In `ApplyEpisodesWatched` (`UserAnimeEntryEditService.cs`), resolve the finish date the entry will hold after the edit — `request.CompletedAt` where `request.HasCompletedAt`, the stored `entry.CompletedAt` otherwise — and use it, not the stored value, in the started-date condition (design D1)
- [x] 1.2 Extend the same condition so a start date supplied by the request suppresses the fill (design D2)
- [x] 1.3 Replace the comment on the fill so it states the whole rule — fills only where the user has said nothing about either date — and names the out-of-order check it used to trip

## 2. Episode ceiling (backend)

- [x] 2.1 Change `maxEpisodes` in `ApplyEpisodesWatched` to the lower of the aired-so-far count and `anime.TotalEpisodes` where both are known, keeping the existing behaviour where only one is known and the existing 0 ceiling for an anime that has aired no episode (design D3/D4)
- [x] 2.2 Keep the rejection message naming the ceiling actually applied, so a cap that comes from the total reads correctly
- [x] 2.3 Add a comment naming the frontend counterpart, matching the `AiredEpisodeGate` / `hasAiredEpisodes` precedent

## 3. Episode ceiling (frontend)

- [x] 3.1 Add a ceiling helper beside `hasAiredEpisodes` in `frontend/src/utils/anime.ts`, with a comment naming the backend counterpart
- [x] 3.2 Use it for the `max` passed to `ProgressBar` in `AnimeDetailPage.tsx`, `MyListRow.tsx` and `CurrentlyWatchingCarousel.tsx`, replacing `episodesAired ?? totalEpisodes`
- [x] 3.3 Use it for the episodes-watched field's `max` in `EntryEditorOverlay.tsx`, replacing the total-only cap so the editor stops accepting counts the server rejects

## 4. Backend tests

- [x] 4.1 Rewatch trap: an entry with a finish date and no start date at 0 episodes saves its first episode, leaves both dates untouched, and is not rejected
- [x] 4.2 Backfill trap: an entry with no dates saves Completed, a typed episode count and a past finish date in one request, ending with no start date
- [x] 4.3 Backfill without touching episodes still saves (guards the path that works today)
- [x] 4.4 A start date supplied alongside a 0 → >0 episode change is stored as supplied, not replaced by today
- [x] 4.5 The unchanged cases still hold: a fresh entry with no dates gets today as its start date, and an existing start date is never overwritten
- [x] 4.6 Ceiling: with 13 aired rows against a total of 12, an edit to 13 is rejected and an edit to 12 completes the entry
- [x] 4.7 Ceiling: the still-airing case (3 aired of 12) and the single-figure cases are unchanged

## 5. Verification

- [x] 5.1 Run the backend test suite and confirm no existing date, rewatching or airing-gate test regressed
- [x] 5.2 Build the frontend (node v22 via nvm) and confirm it compiles clean
- [x] 5.3 Check Mushoku Tensei II in the running app: the count caps at 12, the plus control disables there, and reaching 12 completes the entry
- [x] 5.4 Check a rewatch on an entry with a finish date and no start date: the first episode saves and neither date moves
- [x] 5.5 Run `openspec validate fix-auto-date-fill-and-episode-cap` and confirm the change is still valid
