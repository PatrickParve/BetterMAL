## Why

Three faults, all verified in the code (2026-09-12):

1. **A revealed MAL score follows you to the next anime.** `score-visibility` already
   requires that an individually revealed score re-hides "when navigating away", and
   `ScoreValue` relies on the page unmounting to do it. Anime detail is one route
   (`/anime/:id`), so walking from Fate/Zero to another anime and back keeps the same
   `AnimeDetailPage` — and the same `revealed` state — mounted the whole time. The
   reveal therefore leaks onto every later anime on that route. `usePageData` already
   handles this exact "the component stays mounted across a parameter change" problem
   for page data; the reveal control never learned about it.

2. **Pressing a media type on the series page turns the "in my list" filter on.** The
   More section's filter actually starts **on** today, and its control deliberately
   misreports it — `filterActive` is `mineOnly && no exemptions && at least one group
   expanded`, so a freshly opened page (all groups collapsed) reads "off" and the first
   press does something visible. Pressing **Music** opens the groups holding music,
   which satisfies that third clause, so the control lights up on its own and the music
   shown is only the music in my list. The user no longer wants the filter on by
   default, which removes the reason the control was lying in the first place.

3. **Two whole-list reads issue one database query per entry.** `/api/my-list` (~613
   entries, 162 ms) and `/api/dashboard` (149 ms for 8 KB) both resolve per-entry
   airing facts in a loop, while the batch read they need already exists and is already
   used correctly by `TopAnimeService`, `ProfileService` and `SeriesListService`.
   `MainDashboardService` is the starkest case: it builds `airedSoFarByAnimeId` with the
   batch call on line 25, then ignores that dictionary on line 106 and re-queries per
   item. Round trips, not payload size, are the cost — `/api/profile` serves 143 KB in
   18 ms. A fourth, smaller loop sits alongside them, and `AiringScheduleService`
   already documents the right shape: "one range query across every my-list anime,
   rather than resolving each show against each of the week's seven dates".

## What Changes

**A revealed score re-hides when you leave the page**
- An individually revealed MAL score re-hides when the pathname changes, whether or not
  React kept the page component mounted across that navigation — so anime → anime →
  back starts hidden every time, as the capability already requires.
- A revealed score also re-hides when the global hide toggle is switched back on, rather
  than staying visible because it was revealed before the toggle went off and on.

**The series page's "in my list" filter starts off and reports itself honestly**
- The filter is **off** when a series page opens: every group collapsed, no type
  selected, and neither section control reading as on. Opening a group — from its
  heading, from expand-all, or by selecting a media type — shows every extra it holds.
- The control reports the filter's real state: on while the filter is in force with no
  group exempt, off otherwise. It no longer depends on whether any group is expanded,
  which is what let a type button turn it on.
- Selecting or deselecting a media type never changes the filter or how the control
  reads.
- The control becomes a plain two-way toggle: pressing it while on turns the filter off,
  leaving every group's collapsed state alone, so expanded groups widen to all their
  extras. This **replaces** today's "press again to collapse everything with the filter
  still on", which would otherwise leave the control reading on with nothing on screen.
- While the filter is on, a media type that none of my extras carries is offered
  **disabled**, and turning the filter on drops any selected type of that kind. Select
  Music, then press "in my list" with no music of mine in that franchise, and the
  section shows my extras across every type instead of a column of empty groups.

**My list and the dashboard resolve per-entry airing facts in bulk**
- `MyListService` resolves every entry's aired-so-far count through the existing bulk
  read instead of one query per entry (~613 → 1).
- `MainDashboardService` reads the `airedSoFarByAnimeId` dictionary it already built for
  its current-season section instead of re-querying per item, and resolves "airing
  today" for the whole list in one range query instead of one query per entry (~613 →
  1) — the shape `AiringScheduleService` already uses for its week.
- Its next-episode countdowns resolve in one read for all currently-watching entries
  rather than one apiece.
- Every value is unchanged: each bulk read answers exactly what the per-anime read
  answers for the same anime and instant, and "nothing stored" stays distinguishable
  from "nothing yet" as it is today.

**A page does not refetch a read the URL change did not touch**
- `usePageData` treats a new history entry as a reason to reload even when the read's own
  key — which by contract carries every parameter the data depends on — has not changed.
  Any `setSearchParams` call therefore re-downloads a page's whole payload: a series
  browser filter re-reads 119 KB, and dismissing a my-list recap scope re-reads 305 KB
  and its ~613 queries. Such a read is no longer re-issued; the data already in hand is
  carried into the new history entry's snapshot so a later back/forward restore still
  has it. Restores keep their documented silent background refresh.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `score-visibility`: "Reveal is not persisted" is tightened — leaving the page re-hides
  a revealed score however the client happens to reuse page components across that
  navigation, and the global toggle returning to hidden re-hides it too.
- `series-page`: the More section's "in my list" filter starts off rather than on; its
  control reports the filter's actual state instead of also requiring an expanded group;
  pressing it while on turns the filter off instead of collapsing every group; a media
  type no extra of mine carries is unavailable while the filter is on; selecting a type
  never changes the filter.
- `episode-airing-data`: two further bulk reads are exposed — the episode airing on a
  given local date, and the next airing instant, each for many anime in a single
  database read — alongside the aired-count bulk read already specified.
- `main-dashboard`: the dashboard read resolves every per-entry airing fact through
  those bulk reads, so its database round trips do not grow with the size of my list.
- `library-views`: the my-list read does the same for its aired-so-far column.
- `frontend-data-loading`: a read is not re-issued when the URL changes in a way its own
  key does not depend on.

## Impact

**Frontend**
- `src/components/ScoreValue.tsx` — reveal state resets on pathname change and on the
  global toggle returning to hidden. The only holder of per-score reveal state, so this
  is the single place the fix is needed for all 13 call sites.
- `src/pages/SeriesPage.tsx` — `mineOnly` default, `filterActive`, `toggleMineOnly`,
  `toggleMediaType`, and the type buttons' disabled state.
- `src/hooks/usePageData.ts` — skip the redundant reload; carry existing data into the
  new snapshot.

**Backend**
- `Services/Library/MyListService.cs:30` — the aired-count loop.
- `Services/Dashboard/MainDashboardService.cs:65,76,106` — the next-instant loop, the
  airing-today loop, and the re-query of an already-built dictionary.
- `Services/Airing/IEpisodeScheduleService.cs`, `EpisodeScheduleService.cs` — two bulk
  reads added, as default interface members delegating to the per-anime read so the 28
  existing test doubles keep compiling *and* keep answering consistently with
  themselves; `EpisodeScheduleService` overrides both with a single query each.
- `Data/Repositories/IEpisodeAiringRepository.cs`, `EpisodeAiringRepository.cs` — a bulk
  next-airing-instant read beside the existing bulk aired-count read.

**Not touched**
- `SeriesService.AiredEpisodesByAnimeIdAsync` loops per member too, but over one
  franchise's currently-airing members — a handful, not a whole list. Left alone.
- No API shape, DTO, or database schema changes; no MAL or AniList call-pattern changes.
