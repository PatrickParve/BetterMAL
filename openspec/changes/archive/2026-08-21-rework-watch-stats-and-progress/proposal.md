## Why

The profile page's watch-time figures are built on assumptions that no longer hold. `Days` multiplies a flat 24-minute assumption by a raw episode sum even though most anime now carry a cached average episode duration, `Episodes` counts a film and a music video as one episode each alongside TV episodes, and a rewatch contributes nothing at all — a 12-episode series watched twice still reads as 12 episodes and half a day. The all-list progress bar has the opposite problem: it silently drops every entry whose anime has no published total, then apologises for it with an "x of y entries counted" note, even though the app already stores AniList aired-episode rows that could supply the missing denominator.

The recap page has a matching gap. Under **What I watched**, an anime I am partway through contributes nothing to **Episodes watched** or **Time spent**, so the page can report "3 currently watching" beside an episode count that pretends those three do not exist. And its episode count treats a rewatch identically under both time filters, when the two filters are asking different questions — what I watched in a stretch of time, versus what aired in it.

## What Changes

**Profile page — Anime stats**

- `Episodes` counts rewatches: an entry contributes its episodes watched plus one further full run per recorded rewatch, so a 12-episode series watched twice reads 24.
- `Episodes` excludes `movie` and `music` entries, which are not episodic and were inflating the count by one apiece.
- A new `Movies` stat sits directly below `Episodes`, counting movie entries with at least one episode watched.
- Dropped entries keep counting toward `Episodes` — stated explicitly so it survives future edits.
- `Days` is computed from each anime's own cached average episode duration (falling back to the existing 24-minute assumption only when none is stored), multiplied by that entry's rewatch-inclusive episode count, over **every** entry — TV, movie, music, dropped, watching alike.

**Profile page — All-list episode progress**

- Every entry counts, whether or not its anime publishes a total episode count. When the total is unknown, the stored AniList aired-so-far count supplies the denominator.
- An entry with neither a published total nor a known aired count, whose anime genuinely has not started airing, has nothing to progress against yet and is excluded outright, the same as a dropped entry — it is not a data gap, so it does not belong in the unresolved note either.
- An entry with neither a published total nor a known aired count whose anime *has* started airing (or finished) is a genuine data gap: it is reported in a short note under the bar rather than dropped in silence, and that note offers a control to open a list naming exactly those entries. The bar's total is never suffixed with a `+`.
- Dropped entries and not-yet-aired-with-no-total entries are the only exclusions — everything else counts, including plan-to-watch, movies and music.
- **BREAKING** (surface): the "x of y entries counted" note is removed.

**Recap page — stat block**

- **What I watched** stops meaning "what I finished here" and starts meaning what its name says. An entry qualifies for a period when its completion date falls inside it **or** when the activity log records episode progress for it inside it. A start date is never required — `StartedAt` is blank on most imported entries, so requiring one would have emptied the filter.
- A show I am partway through therefore appears in the period I watched it in, and a logged rewatch places an anime in that later period too. An anime is counted **once** per period — one place in `In this period`, the mean score, the top 10 and the rankings — however many viewings the period holds.
- **Episodes watched** and **Time spent** count every viewing: under **What I watched** an entry's episodes are the sum of its logged increases inside the period. Because a rewatch resets the counter and climbs again, a full watch plus a rewatch inside one period sums to both, with no rewatch bookkeeping and no risk of counting the original twice.
- The stored rewatch count is read in exactly one place — an entry included on its completion date that the activity log does not reach, where the finish year is the only period that can hold the rewatch. Under **What aired** there is no log lookup and no rewatch multiplier: a rewatch is not something that aired in the period.
- Nothing is estimated where the log is silent. The log begins 2026-07-08, so recaps from 2026 on gain true per-period viewing while earlier ones keep behaving as they do today.
- **Movies watched** is unchanged: still a count of films in the included set, still followable. Counting viewings there would break the existing rule that the tile's number equals the list it opens — see `design.md` Open Questions.

## Capabilities

### New Capabilities

None. Both surfaces already have owning specs.

### Modified Capabilities

- `profile-stats`: the `Days` and `Episodes` stat definitions change, a `Movies` stat is added, and the "All-list episode progress" requirement is rewritten around the aired-count fallback, the dropped-entry exclusion, and the removal of the entries-counted note.
- `list-recaps`: **What I watched** gains a logged-progress arm ("Dynamic time filter"); single-period attribution is scoped explicitly to **What aired** ("Period attribution by start date"); and the stat block's **Episodes watched** and **Time spent** move to a per-period episode count with the rewatch rule confined to pre-tracking entries.
- `episode-airing-data`: a batched aired-episode read is added to the stored-rows capability so the profile page can resolve a whole list's aired counts in one query rather than one per entry.

## Impact

**Backend**

- `Services/Profile/ProfileService.cs` — `BuildStats` and `BuildEpisodeProgress` rewritten; `GetProfileAsync` gains an aired-count lookup. `BuildEpisodeProgress` excludes a not-yet-aired, no-total entry outright rather than counting it unresolved, since that's a normal state, not a data gap.
- `Services/Profile/ProfileDto.cs` — `AnimeStatsDto` gains `Movies`; `EpisodeProgressDto` swaps `EntriesCounted` for an unresolved-entry count plus a new `UnresolvedAnime` list (new `UnresolvedEpisodeEntryDto` record) so the frontend can name exactly which entries the count covers.
- `Services/Recap/RecapEntrySelector.cs` — the second **What I watched** arm, which the recap, the my-list scope links, and `RecapAvailabilityService` all inherit from one place.
- `Services/Recap/RecapStatsBuilder.cs`, `RecapService.cs`, new `RecapWatchLog.cs` — per-period episode counts; `RecapService` gains an activity-log dependency.
- `Services/Recap/RecapTimeMath.cs` — becomes the shared home of the rewatch-inclusive episode count as well as per-episode runtime.
- `Data/Repositories/IEpisodeAiringRepository.cs` + `EpisodeAiringRepository.cs` — new batched max-aired-episode read.
- `Data/Repositories/IActivityLogRepository.cs` + `ActivityLogRepository.cs` — new episode-progress-in-range read.
- Tests: `ProfileServiceEpisodeProgressTests`, `RecapEntrySelectorTests`, `RecapStatsBuilderTests`, `RecapServiceTests`, and any test asserting `AssumedMinutesPerEpisode`-derived days.

**Frontend**

- `src/pages/ProfilePage.tsx` — `Movies` stat row, progress-bar note replaced, plus a "View" control that opens the new unresolved-entries overlay when the note is showing.
- `src/components/UnresolvedEpisodesOverlay.tsx` (new) + `.css` — the unresolved-entries list, following the `EditHistoryOverlay`/`Modal` pattern.
- `src/api/types.ts` — `AnimeStatsDto` and `EpisodeProgressDto` shapes, plus the new `UnresolvedEpisodeEntryDto` type.
- `src/pages/RecapPage.tsx` — no structural change; tile values follow the API.

**No** database migration: every new figure is derived from rows that already exist.
