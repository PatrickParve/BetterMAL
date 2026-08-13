## Why

The weekly Schedule page renders one slot per stored episode row, so an anime that drops several episodes on the same local day (a batch release, or just a day with more than one airing gap closed at once) shows up as several near-identical cards stacked in a row — same title, same picture, differing only by episode number. This clutters the day column and hides the fact that it's really one release event.

## What Changes

- When an anime has more than one episode airing on the same local day, the schedule collapses them into a single card labeled with an episode range (e.g. `Ep 1-8`) instead of one card per episode.
- Collapsing is not limited to episodes sharing the exact same air time: episodes of the same anime on the same day merge into one card as long as no other anime's episode aired strictly between the earliest and latest of them. If another anime's episode falls between two of that anime's episodes that day, the run splits at that point instead of merging across it.
- A merged card's displayed time is the earliest episode's air time, which is also where the card sorts in the day's list.
- Tie-break rule for same-instant collisions at a group's boundary: if another anime's episode shares the exact air time of a would-be group's first (earliest) episode, that other anime's card sorts before the group; if it shares the exact air time of the group's last (latest) episode, that other anime's card sorts after the group. This keeps an exact-time coincidence at the boundary from being treated as "between" the group's episodes and splitting it.
- Episodes with an unknown episode number never merge with neighboring episodes (a range can't be formed from an unknown number), but such a slot does not itself count as an "other anime interrupting" and therefore does not block a same-anime run from merging around it.
- Scope: this applies to the weekly Schedule page (`/airing`, `AiringPage.tsx` and its backing `GET /api/airing` response). The dashboard's "Airing today" list is a separate, simpler component and is out of scope for this change.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `airing-schedule`: a day's slots are no longer strictly one-per-stored-episode; contiguous same-anime episodes on the same local day merge into one ranged card per the grouping and tie-break rules above, and the day-column slot box displays an episode range when a card represents more than one episode.

## Impact

- Backend: `AiringScheduleService.GetWeekAsync` (`backend/AnimeTracker.Api/Services/Airing/AiringScheduleService.cs`) currently emits one `AiringSlotDto` per stored `EpisodeAiring` row per day; it gains the grouping/tie-break logic described above and emits merged slots instead. `AiringSlotDto` (`backend/AnimeTracker.Api/Services/Airing/AiringScheduleDto.cs`) gains an episode-range end field.
- Frontend: `AiringSlotDto` in `frontend/src/api/types.ts` gains the matching field; `AiringPage.tsx`'s slot rendering (around lines 140-169) formats `Ep {start}` or `Ep {start}-{end}` instead of always `Ep {episodeNumber}`.
- No change to the `episode-airing-data` capability or to stored `EpisodeAiring` rows — grouping is a presentation-layer aggregation of existing rows, not a change to what's persisted.
