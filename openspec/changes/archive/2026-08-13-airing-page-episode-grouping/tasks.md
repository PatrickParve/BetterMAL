## 1. Backend grouping

- [x] 1.1 Add nullable `EpisodeNumberEnd` to `AiringSlotDto` (`backend/AnimeTracker.Api/Services/Airing/AiringScheduleDto.cs`)
- [x] 1.2 Implement the per-anime interval run-formation + day-level merge as an isolated, pure function per `design.md` (phases 1-3): group a day's rows into runs, excluding unknown-episode rows from merging, splitting on any other-anime instant strictly between two of an anime's rows, and ordering merged/singleton runs with the "single-instant sorts before multi-instant at an equal key" tie rule
- [x] 1.3 Switch the day sort/grouping key in `AiringScheduleService.GetWeekAsync` from the formatted `LocalTime` string to the underlying `AirsAtUtc` instant
- [x] 1.4 Wire the grouping function into `GetWeekAsync`, replacing the current one-row-to-one-slot mapping, emitting `EpisodeNumber`/`EpisodeNumberEnd`/time/title/picture per run
- [x] 1.5 Add unit tests for the grouping function: simultaneous episodes merge; non-simultaneous same-day episodes merge; an interruption splits a run; a tie at the earliest episode sorts the other anime before without splitting; a tie at the latest episode sorts the other anime after without splitting; an all-same-instant batch merges as one range; an unknown-episode row never merges and doesn't block the other episodes of its anime from merging; different anime never merge; episodes on different local days never merge

## 2. Frontend rendering

- [x] 2.1 Add `episodeNumberEnd` to `AiringSlotDto` in `frontend/src/api/types.ts`, matching the backend field
- [x] 2.2 Update the slot rendering in `AiringPage.tsx` (around lines 140-169) to show `Ep {episodeNumber}-{episodeNumberEnd}` when `episodeNumberEnd` is present and greater than `episodeNumber`, otherwise the existing `Ep {episodeNumber}` / `—` placeholder behavior
- [x] 2.3 Check `AiringPage.css` for the episode-number row against a range-width label (e.g. `Ep 1-24`) and adjust truncation/spacing only if the wider text overflows the existing reserved row

## 3. Verification

- [x] 3.1 Search the backend and frontend for other consumers of `AiringSlotDto`/`episodeNumber` (besides `AiringPage.tsx`) that might assume it always names exactly one episode, and update them if any assume otherwise
- [x] 3.2 Manually verify the weekly view against seeded/real data covering: a same-instant batch release, a same-day-different-time contiguous set with no interruption, and a same-day set interrupted by another anime's episode
- [x] 3.3 Run the backend test suite and the frontend build/typecheck
