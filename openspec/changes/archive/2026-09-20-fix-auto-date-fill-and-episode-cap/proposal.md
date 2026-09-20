## Why

Two editing rules misfire against real entries. The automatic started-date fill invents today's date on the first episode of a rewatch, which then collides with the finish date from the original watch and rejects the edit with "Finish date cannot be earlier than start date" — a date the user never touched. 286 of 620 entries in the live list (a finish date, no start date) are one rewatch away from this. The same invented date also blocks backfilling an anime watched years ago whenever the episode count is typed in the same save.

Separately, episodes watched is capped at the aired-so-far count without regard for the total, so an anime whose AniList entry carries more episode rows than MyAnimeList publishes can be set past its own total — Mushoku Tensei II saves as `13/12`, which then never completes, since completion requires reaching the total exactly. Five anime in the live list are affected.

## What Changes

- The automatic started-date fill SHALL consider the finish date **as it will stand once the save finishes**, not only the value already stored. A finish date that is stored, or that is arriving in the same request, both suppress the fill.
- An explicit start date in the same request SHALL also suppress the fill, rather than being written and then immediately overwritten.
- The finish-date fill is unchanged — "fill only when empty, only on completion" is already the rule and already the implementation.
- Episodes watched SHALL be capped at the **lower** of the aired-so-far count and the total episode count whenever both are known, on every surface that offers the cap: the shared progress row, the entry editor, and the server.
- The entry editor's episodes field, which today caps at the total alone and ignores the aired-so-far count, SHALL use the same shared ceiling as everything else — removing edits the editor accepts and the server then rejects.
- No data migration: no entry currently sits above its total, and no stored date is rewritten.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `list-editing`: three requirements change.
  - **Started-date on first progress** — the fill gains its "finish date empty after this save, and no start date supplied" condition.
  - **Manually set dates are never overwritten** — its principle extends from "a date already stored is authoritative" to "a date the user supplies is authoritative, whether stored already or arriving in the same save".
  - **Inline editable episode count** — the ceiling becomes the lower of aired-so-far and total when both are known, resolving what the requirement leaves open when stored airing data reports more episodes than the published total, and extends to the entry editor's own episodes field.

## Impact

- `backend/AnimeTracker.Api/Services/Entries/UserAnimeEntryEditService.cs` — the started-date condition in `ApplyEpisodesWatched`, and the `maxEpisodes` ceiling in the same method.
- `frontend/src/utils/anime.ts` — a shared ceiling helper alongside `hasAiredEpisodes`.
- `frontend/src/pages/AnimeDetailPage.tsx`, `frontend/src/components/MyListRow.tsx`, `frontend/src/components/CurrentlyWatchingCarousel.tsx` — the `max` passed to `ProgressBar`.
- `frontend/src/components/EntryEditorOverlay.tsx` — the episodes field's `max`.
- `backend/AnimeTracker.Api.Tests/Services/Entries/` — new coverage for the rewatch trap, the backfill trap, and the ceiling.
- No API contract change, no schema change, no migration. MyAnimeList sync is unaffected: the values pushed are the same ones the user sees.
