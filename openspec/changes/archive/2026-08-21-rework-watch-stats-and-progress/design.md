## Context

Three surfaces compute watch-time figures today, and they already share one constant. `ProfileService.AssumedMinutesPerEpisode` (24) is the app's standing fallback for an unknown per-episode runtime; `RecapTimeMath.EpisodeSeconds` is the single place that resolves `AnimeMetadata.AverageEpisodeDurationSeconds ?? AssumedMinutesPerEpisode * 60`, and both the recap's **Time spent** stat and its time-watched rankings read it. The series page reads the same fallback. The profile page's `Days` stat is the odd one out: it never consults a cached duration at all, applying the flat assumption to every episode in the list.

The relevant stored data:

- `UserAnimeEntry` holds `EpisodesWatched`, `RewatchCount`, `Status`, `StartedAt`, `CompletedAt`.
- `AnimeMetadata` holds `TotalEpisodes` (nullable — unknown or still airing), `MediaType`, `AiringStatus`, `AverageEpisodeDurationSeconds` (nullable, populated only by a rich detail fetch).
- `EpisodeAiring` rows hold one AniList-sourced air instant per episode; `IEpisodeAiringRepository.GetMaxAiredEpisodeAsync` reads an aired-so-far count for **one** anime.
- `ActivityLog` holds timestamped `EpisodeIncremented` rows carrying the new episode number in `ChangeDetail` (`"Episode N"`) and the prior value in `PreviousEpisodesWatched`. These rows exist only from the point this app started tracking; nothing reconstructs history before that.

`RecapEntrySelector` is the single home of recap inclusion: **What I watched** takes Completed/Dropped entries attributed by `CompletedAt`; **What aired** takes Completed/Dropped/On-hold/Watching attributed by `AiredFrom`. A season recap always uses the aired rule. `RecapStatsBuilder` already breaks that scope once — **Currently watching** is counted from a separately-selected aired-attributed list under both filters — so a stat computed over a set other than `included` is established precedent rather than a new pattern.

## Goals / Non-Goals

**Goals:**

- One shared definition of "how many episodes did this entry represent", rewatch-aware, used by both the profile `Episodes` stat and the profile `Days` stat.
- Per-anime runtime everywhere watch time is reported, with the 24-minute assumption as the last resort rather than the only rule.
- A whole-list progress bar with a denominator for every entry that can have one, and an honest, visible count of the entries that cannot.
- Recap progress stats that account for anime I am partway through, without inventing history the app never recorded.
- No new stored state and no migration.

**Non-Goals:**

- Reworking which entries a recap's `In this period` count, mean score, top 10, or season/year rankings cover (see decision 3).
- Backfilling watch history from before this app started tracking.
- Fetching `AverageEpisodeDurationSeconds` for anime that lack it — the fallback stays.
- Any change to the series page's runtime stats.

## Decisions

### Decision 1 — Rewatch-inclusive episode count lives in one place

An entry's rewatch-inclusive episode count is `EpisodesWatched + RewatchCount * (TotalEpisodes ?? EpisodesWatched)`.

The rewatch baseline is the anime's published total, because a completed rewatch is a full run through the anime regardless of where the entry's `EpisodesWatched` currently sits — an entry that is one episode into its third viewing reads `1 + 2 * 12 = 25`, not `3`. `EpisodesWatched` is the fallback baseline when no total is published, which is the only figure available for a still-airing or unpublished-length show.

**Alternative considered:** `EpisodesWatched * (1 + RewatchCount)`. Rejected — it reads correctly only when the entry sits at exactly the anime's total, and reports a mid-rewatch entry as a fraction of one viewing multiplied across all of them.

This lives on `RecapTimeMath` alongside `EpisodeSeconds`, which becomes the shared home of both per-entry watch quantities. The type is renamed to `WatchMath` and moved out of `Services/Recap` into `Services/Watching`, since the profile page is now a first-class caller and a profile stat reaching into a `Recap` namespace would misdescribe the dependency.

### Decision 2 — Profile `Days` sums per-entry runtime rather than scaling one episode total

`Days = Σ over every entry (rewatchInclusiveEpisodes(entry) * WatchMath.EpisodeSeconds(entry.Anime)) / 86400`, rounded to one decimal.

Every entry counts, whatever its media type or status — movies, music videos, dropped and in-progress entries included — because the stat answers "how much of my life has gone into this list", and a dropped show still consumed the episodes I watched of it. This is deliberately a *different* population from the `Episodes` stat, which excludes movies and music (decision 4); the two stats answer different questions and the spec states each population explicitly so the divergence is not read as a bug.

**Alternative considered:** keeping `Days` derived from the `Episodes` stat for internal consistency. Rejected — it would either drag movies and music back into `Episodes` or drop their runtime from `Days`, and a two-hour film contributing nothing to time spent is the worse error.

### Decision 3 — **What I watched** gains a second attribution arm, inside the shared selector

`RecapEntrySelector.AttributionDate` currently answers one question per filter and returns a single date. For **What I watched** it becomes two-armed: an entry qualifies for a period when its `CompletedAt` falls inside it **or** when the activity log holds episode progress for it inside it. Results are deduplicated by anime id, so an anime satisfying both arms is one entry.

A start date is never consulted. Requiring one would have dropped every entry whose `StartedAt` is blank — and `StartedAt` is only auto-filled when this app itself observes a 0→1 episode transition (`UserAnimeEntryEditService.cs:161`, from 2026-07-08 onward), so most imported back-catalogue entries have none.

This replaces the "watch-progress set" an earlier draft of this design proposed — a second, wider set used only by the volume stats. It is no longer needed: an anime I watched episodes of in a period now belongs to the period outright, so every stat reads one set again. That is both simpler and more honest, since a show I watched twelve episodes of in 2026 genuinely is part of my 2026.

Because the selector is also what the my-list scope links resolve through (its own doc comment names `RecapAvailabilityService` as the other reader), putting the second arm *inside* it is what keeps the followable tiles' counts equal to the lists they open, and what keeps the filter's availability check honest. Implementing the arm anywhere else would silently desynchronise those three surfaces.

**Consequence, accepted:** under **What I watched** an anime may now appear in several periods of the same mode — its first watch in one, a logged rewatch in another. The existing single-period guarantee is scoped to **What aired** in the spec, so this is a widening rather than a contradiction, but the requirement text is amended to say so out loud.

**Alternative considered:** requiring both a start and a finish date inside the period. Rejected — blank start dates would empty most recaps, and an anime watched across a new year would land in no period at all. The logged-progress arm achieves the intent (only count a period's real viewing) without either failure.

### Decision 4 — Profile `Episodes` excludes movies and music; `Movies` is its own stat

`Episodes` sums the rewatch-inclusive count over entries whose `MediaType` is neither `movie` nor `music`. OVA, ONA, special and unknown media types keep counting — only the two non-episodic forms are removed.

`Movies` counts entries with `MediaType == "movie"` and at least one episode watched, matching the recap's **Movies watched** definition exactly so the two pages can never disagree about what a watched movie is. Music entries get no companion stat: they were removed from `Episodes` because a music video is not an episode, not because they deserve their own tile.

Both media-type strings are compared case-insensitively against MAL's raw vocabulary, consistent with how `RecapStatsBuilder` already tests `MovieMediaType`.

### Decision 5 — Episodes come from the log's in-period increases, summed

Under **What I watched**, an entry's episode figure is **the sum of its logged increases inside the period** — for each `EpisodeIncremented` row, `newEpisodes - PreviousEpisodesWatched`, keeping only positive results. Decreases contribute nothing and never subtract.

Summing increases rather than taking `max - start` is what makes multiple viewings fall out for free. A rewatch resets the counter to zero and climbs again, so a period holding a full watch and a rewatch of a 12-episode series logs +12 then a reset then +12, and the sum is 24 — while `max - start` would read 0. This is exactly the "count both viewings for volume" behaviour without any rewatch bookkeeping, and it cannot double-count the original watch the way adding a stored `RewatchCount` on top of logged progress would.

The stored `RewatchCount` is therefore read in exactly **one** place: an entry included on its completion date for which the log holds *no* in-period rows — a viewing predating tracking. There it contributes `WatchMath.RewatchInclusiveEpisodes` (decision 1), because the rewatch has no date of its own and the finish year is the only period that can hold it. An entry with logged progress never has the multiplier applied.

Nothing is estimated where the log is silent and no completion date lands in the period. The log begins 2026-07-08 (first commit) and today is 2026-08-21, so in practice: 2026 recaps get true per-period viewing, earlier recaps behave as they do today plus the rewatch fallback. That is the accepted consequence of not guessing.

Under **What aired**, and on every season recap, no log lookup happens at all and each entry contributes its stored `EpisodesWatched`.

**Alternative considered:** `max - start` per period. Rejected — it silently loses a rewatch that completes inside the period, which is the case this change exists to capture.

### Decision 6 — The rewatch multiplier belongs to the pre-tracking fallback, not to the filter

Under **What aired**, and on every season recap, an entry contributes its plain `EpisodesWatched` — a rewatch is not something that aired in the period.

Under **What I watched**, rewatches count, but they reach the figure through the log (decision 5) rather than through a multiplier. The multiplier survives only on the pre-tracking fallback branch. An earlier draft applied it per-filter to every entry; that would have double-counted any rewatch the log already recorded.

**Time spent** multiplies the same per-entry episode figure **Episodes watched** reports, whichever branch produced it, so dividing one by the other always lands on a plausible runtime.

**Movies watched** stays a count of films rather than viewings, and stays scoped to the included set. `list-recaps` "Drilling into a recap stat" makes it followable and requires that "the number the tile shows and the number of anime my list then holds SHALL agree"; the list it opens holds one row per anime, so a tile counting viewings could read 3 and open a list of 1. The extra viewings' runtime still reaches **Time spent**, which is where watched time is reported. Flipping this would mean moving **Movies watched** out of the followable stats and into the aggregate group beside **Episodes watched** and **Time spent** — a deliberate loss of a navigation affordance, not a free change.

The season/year *time-watched rankings* are gated to **What aired** only (`RecapService.BuildRankings`), so they never see a log-derived or multiplied figure and cannot drift from the **Time spent** total the `list-recaps` spec requires them to sum to.

### Decision 7 — Progress-bar denominator: published total, then aired count, then unresolved (revised)

Per entry, in order:

1. `Anime.TotalEpisodes` when published.
2. Otherwise the stored AniList aired-so-far count, when known and greater than zero — the count is capped at nothing, since with no published total there is nothing to cap against.
3. Otherwise, if the anime has not started airing (`AiringStatus == "not_yet_aired"`), the entry is **excluded outright** — the same treatment as a dropped entry, on both sides of the bar and out of the unresolved count too.
4. Otherwise the entry is **unresolved**: it contributes to neither side of the bar and is counted in `UnresolvedEntries`.

**Revision, post-implementation:** the original version of this decision folded steps 3 and 4 together — any entry reaching neither a published total nor a non-zero aired count was called unresolved, on the reasoning that "nothing has aired yet" shouldn't be silently read as a zero-length denominator. In production this meant *every* not-yet-aired entry (which is most of the unresolved-so-far real data — announced sequels, plan-to-watch entries for shows that haven't premiered) landed in the unresolved bucket, even though there's nothing wrong with the data: a show that hasn't aired a single episode has nothing to measure progress against, full stop. That's a normal state, not a gap. The unresolved bucket is now reserved for what it was actually meant to catch — a show that is airing or has finished airing yet still has neither a published total nor a single stored aired row, which is a genuine hole in the app's data. `AiringStatus` (already the established field for this check elsewhere in the codebase — `SeriesRankingIndex`, `EpisodeScheduleRefreshService`, `MainDashboardService`) is the signal, since the airing-rows table can't distinguish "hasn't aired yet" from "no rows fetched" on its own.

A published total still resolves an entry regardless of its airing status — an announced show with a confirmed episode count counts normally even before it airs; the exclusion only applies once both the total and the aired count have failed.

Watched is clamped to the entry's own denominator so a stored over-count cannot push the bar past 100%, and rewatches do **not** multiply here — the bar measures coverage of the list, and a second viewing covers nothing new. Dropped entries are excluded outright, on both sides: a show I abandoned is not outstanding work.

The frontend renders the resolved total plainly. It must not append `+` to signal the aired-count fallback — the fallback *is* the total episode count as far as the app knows it, and a `+` would read as an unresolved estimate.

**Alternative considered:** counting an unresolved entry's watched episodes on the numerator while contributing nothing to the denominator. Rejected — it can drive the bar past 100%, which is exactly the failure the existing clamp guards against.

### Decision 8 — One batched aired-count query, over the shortfall only

`IEpisodeAiringRepository` gains `GetMaxAiredEpisodesAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset asOfUtc)`, returning `Dictionary<int, int>` from a single grouped query. `ProfileService` calls it once per profile read, passing only the anime ids of entries whose `TotalEpisodes` is null — typically a small tail of the list.

`MyListService` and `SeriesService` loop `EpisodesAiredAsOfAsync` per entry today, so a per-entry loop would have precedent, but the profile page is a whole-list read on the app's landing surface and one query is strictly better. Those two call sites are left alone: converting them is unrelated work and would widen this change's blast radius.

`IEpisodeScheduleService` gains a matching batched method so `ProfileService` depends on the service layer rather than reaching past it to the repository, consistent with every other consumer of airing data.

### Decision 9 — Wire-shape changes are additive-plus-one-rename

`AnimeStatsDto` gains `Movies`. `EpisodeProgressDto` replaces `EntriesCounted` with `UnresolvedEntries` — a rename rather than an addition, since the old field's only consumer is the note being removed and leaving it in place would invite a future reader to resurrect it. `TotalEntries` stays: it still contextualises the unresolved count. Both frontend types change in the same commit as the backend records.

### Decision 10 — The unresolved note names its entries, not just their count (post-implementation addition)

Once the unresolved bucket was narrowed to genuine data gaps (decision 7's revision), a bare count stopped being enough to act on: "3 entries whose episode count isn't known yet" gives no way to find *which* three. `EpisodeProgressDto` gains `UnresolvedAnime: List<UnresolvedEpisodeEntryDto>` — the same population `UnresolvedEntries` counts, in full, alphabetical by title, each carrying just enough (anime id, title, English title, picture, episodes watched) to render a row and link to the anime's detail page. `ProfileService.BuildEpisodeProgress` builds this list alongside the count rather than a second pass over `entries`.

The frontend note gains a "View" control, shown exactly when `UnresolvedEntries > 0` (the same gate the note itself already uses), opening a new `UnresolvedEpisodesOverlay` — a `Modal` listing each entry with its poster and episodes-watched, following the same row layout `EditHistoryOverlay` already established for "picture + title + link to detail page" lists. No new fetch: the list rides on the profile response already loaded for the page.

**Alternative considered:** a dedicated endpoint fetched lazily when the overlay opens. Rejected — the list is at most as long as the whole list's not-fully-synced tail, cheap to include in the existing payload, and avoiding a second round trip keeps the overlay instant.

## Risks / Trade-offs

- **Recaps before 2026-07-08 gain almost nothing from the logged-progress arm** → Accepted, and deliberate per decision 5. The log starts at the first commit, so pre-tracking periods keep behaving as they do today (completion-date attribution plus the rewatch fallback), and only 2026 onward gets true per-period viewing. The `list-recaps` spec states this out loud so an empty older recap reads as designed rather than broken.
- **An anime appearing in several `What I watched` periods could look like double-counting** → It is counted once *per period*, and each period counts only the episodes watched in it, so no total is inflated. The spec makes the once-per-period rule explicit and gives it its own scenario.
- **`RecapAvailabilityService` and the my-list scope links must learn the second arm too** → Mitigated by putting the arm inside `RecapEntrySelector`, which is already documented as the single home all three read from. Task 5.3 covers the availability path explicitly rather than assuming it follows.
- **The log query runs on every recap read** → One indexed range query over `ActivityLog` per read, returning only `EpisodeIncremented` rows in the period. Comparable to the entry read already on that path.
- **`Days` jumps noticeably on first load after deploy** → Expected: films and long-runtime entries stop being counted at 24 minutes an episode, and rewatches start counting at all. This is the correction the change exists to make; no communication mechanism is needed for a single-user local app.
- **`Episodes` drops for a movie-heavy list while `Movies` appears beside it** → The new stat absorbs the visible delta in the same render, so the drop is self-explaining rather than a mystery.
- **The progress bar's denominator moves as AniList airing rows refresh** → Inherent to using a live aired count; the bar already redraws on every profile read and the movement is small relative to a whole list.
- **`EpisodesWatched` as a rewatch baseline over-counts a partially-watched-then-rewatched entry** (decision 1's fallback branch) → Bounded: it only applies when the anime publishes no total at all, and it never exceeds the episodes the user actually recorded times the rewatch count.
- **Renaming `RecapTimeMath` touches recap files unrelated to this change's behaviour** → Mechanical rename with a compiler-verified call graph; the four call sites are all in `Services/Recap`.
- **A stale or missing `AiringStatus` mis-sorts an entry between "excluded" and "unresolved"** (decision 7's revision) → Bounded: `AiringStatus` is only ever `"not_yet_aired"` for shows MAL/AniList themselves report as unaired, so a null or stale value defaults to the safer side — treated as *not* not-yet-aired, i.e. left in the unresolved bucket rather than silently dropped. Worst case is a genuine gap staying visible one render longer than ideal, never the reverse.

## Open Questions

- **Movies watched: films or viewings?** Decision 6 keeps it a count of films so it stays followable and its count keeps matching the list it opens. The request asked for extra viewings to add to "the time and episode movie watched". If viewings are wanted on that tile, it must move out of `list-recaps` "Drilling into a recap stat"'s followable list into the aggregate group — a separate, deliberate edit to that requirement. Everything else in this change is unaffected either way; the extra viewings already reach **Time spent**.
