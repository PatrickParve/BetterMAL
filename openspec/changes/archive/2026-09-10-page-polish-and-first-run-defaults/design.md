## Context

This change bundles seven small, independent adjustments across the detail page, home dashboard, recap, profile, my list, and client-side preferences. Most are presentation-only. Three touch shared computation:

- **Next-episode countdown.** It is computed server-side in two places. `MainDashboardService.ToEta` and `AnimeDetailService.ToEta` are identical copies that return `TimeSpan.Days` / `TimeSpan.Hours`. Both truncate, so anything under an hour becomes `0d 0h`. Frontend formatting differs by surface: the detail page shows `next in 2d 7h`, and the carousel shows `Next ep: in 2 days, 7 h`. Both render the pair the server sends.
- **Profile stats.** `ProfileService.BuildStats` builds `Episodes` and `Days` from `WatchMath.RewatchInclusiveEpisodes`, which is episodes watched plus `rewatchCount × run`. `RewatchCount` only goes up when a rewatch finishes, and entering Rewatching resets episodes watched to 0. So for an entry currently being rewatched, the rewatch-inclusive figure leaves out the completed first viewing.
- **Preference defaults.** `ContentFilterContext` (`bettermal.hideNsfw`) and `ScoreVisibilityContext` (`bettermal.scoresHidden`, `bettermal.alwaysShowCompletedScores`) read `localStorage.getItem(key) === 'true'`. A missing key therefore means `false`. Each provider also writes its current value back on mount, so any browser that has ever opened the app already has explicit values stored.

## Goals / Non-Goals

**Goals:**

- The countdown never reads zero hours while an episode is still ahead, and both surfaces agree because they share one computation.
- The profile's Episodes / Rewatched episodes / Days figures are consistent: Days is the sum of the two episode populations (with no movie/music exclusion) multiplied by runtime.
- A first run starts with scores hidden, completed/dropped scores always shown, and NSFW hidden. Stored choices are never overridden.

**Non-Goals:**

- Recap stats (`RecapStatsBuilder`) keep using `RewatchInclusiveEpisodes`. Only the profile's stat list is redefined here.
- No change to countdown *formatting*, only to its rounding.
- No server-side preference storage. Preferences stay in `localStorage`.
- The profile's Favourite seasons / Favourite years row, and the yearly recap's layout, are not reordered.

## Decisions

### D1. One shared round-up countdown helper

Replace both `ToEta` copies with one static helper (for example `NextEpisodeEta.From(DateTimeOffset? next, DateTimeOffset now)` next to `NextEpisodeEtaDto`). It computes `totalHours = (int)Math.Ceiling(remaining.TotalHours)`, then `Days = totalHours / 24` and `Hours = totalHours % 24`.

- An exact whole-hour remainder is unchanged (2d 7h 0m → `2d 7h`).
- 59 minutes → `0d 1h`.
- 23h 30m → `1d 0h`.
- 2d 6h 20m → `2d 7h`.

**Alternatives considered:**

- **Round to nearest.** It still shows `0h` under 30 minutes, which is the complaint.
- **Show minutes under an hour.** It changes both surfaces' formats and wasn't asked for.
- **Fix it on the frontend.** The pair is already truncated by then, so the frontend can't recover the minutes.

### D2. The synopsis box renders only the sections with text

`AnimeDetailPage` treats `synopsis` / `background` as present only when non-null and non-whitespace. It renders a Synopsis section only when there is a synopsis, a Background section only when there is a background, and no `detail-box` at all when there is neither. The "No synopsis available." placeholder goes away.

The "No info" rule stays scoped to the info box's fields.

### D3. The multi-year recap leads with the year column

In `RecapPage.renderRankings`, the multi-year branch gives the year column grid column 1 and the season column grid column 2 when both are present. A lone column still takes column 1.

The year sections are also emitted first in DOM order, so the ≤1024px single-column collapse stacks year rankings above season rankings. It relies on DOM order, as the CSS comment describes, rather than an `order` property. Row alignment is unchanged: both score rankings sit on row 1 and both time-watched rankings on row 2. Update the CSS comment in `RecapPage.css` to match.

The yearly branch is untouched, since it has no year column.

### D4. Splitting profile episodes into first viewing and rewatched

Add `WatchMath.FirstViewingEpisodes(entry)`:

- For any entry not marked Rewatching, it returns `EpisodesWatched`.
- For an entry marked Rewatching, it returns one full run: `TotalEpisodes ?? EpisodesWatched`. Rewatching requires a finished-airing anime, so a total is almost always published.

Rewatched episodes reuses the existing `WatchMath.RewatchEpisodesIncludingCurrentRun`: completed runs plus current-rewatch progress.

In `BuildStats`:

- `Episodes = Σ FirstViewingEpisodes` over entries that are neither movie nor music.
- `RewatchedEpisodes = Σ RewatchEpisodesIncludingCurrentRun` over **all** entries, films and music included.
- `Days = Σ (FirstViewingEpisodes + RewatchEpisodesIncludingCurrentRun) × EpisodeSeconds` over all entries.

For entries not being rewatched, Days is unchanged. For entries being rewatched, it now includes the completed first viewing that was silently dropped before. That is a deliberate correction, needed so Days stays equal to the sum of what it's built from.

`AnimeStatsDto` drops `MeanScore`, since the score distribution DTO carries its own mean, and gains `RewatchedEpisodes`.

**Alternative considered:** keep Days on `RewatchInclusiveEpisodes`. That leaves Days disagreeing with Episodes + Rewatched episodes for every entry being rewatched, which is harder to explain than the correction.

### D5. The scored count is derived on the client from the distribution buckets

The profile's distribution buckets count exactly the scored entries (the share-percentage denominator already uses their sum). `ScoreDistribution` gains an optional `scoredCount` prop, rendered as `Scored: N` on its own line directly above the `Mean score:` line.

`ProfilePage` passes the bucket sum. The recap's use of `ScoreDistribution` doesn't pass it, so the recap is unchanged. No API change is needed.

### D6. Popularity is a plain sort key

Popularity joins the existing `SortKey` union and `SORT_OPTIONS`, placed directly after MAL score. It is therefore offered by both the primary and the tiebreaker selectors with no new control.

Its comparator is the same `nullsLast` pattern the other keys use, keyed on `popularityRank`. Its natural direction is ascending by rank (rank 1 = most popular first), so the existing direction control flips it to least popular first. Entries with no known rank stay last in either direction, as for every other key.

The data comes from a new `MyListItemDto.PopularityRank`, populated in `MyListService` from `entry.Anime.PopularityRank`. That value is already fetched via the `popularity` field and cached on `AnimeMetadata`.

Choosing it is stored and restored exactly like any other sort key, and "Clear filters" already resets the sort to alphabetical, so neither needs any change.

### D7. First-run defaults apply only when a key is absent

Each read function becomes: `stored === null ? DEFAULT : stored === 'true'`, with `DEFAULT = true` for all three keys. The `typeof window === 'undefined'` branch also returns the default. The keys don't change, so any stored `true`/`false` is honoured.

**Alternative considered:** new key names, which would push the new defaults onto existing browsers. Rejected, because it would silently override deliberate choices.

## Risks / Trade-offs

- **Existing browsers won't see the new defaults**, because every provider writes its value on mount, so the keys already exist. → This is intended, since the request is about first run. To check the defaults locally, use a private window or clear the three `bettermal.*` keys.
- **Days rises for entries currently being rewatched**, by one run each. → This is a documented correction (D4), and the spec states Days = first viewing + rewatched.
- **Popularity rank can be stale or missing** for anime whose metadata hasn't been refreshed. → Entries with no rank sort last in either direction, like any missing sort value.
- **The countdown now slightly over-states** (for example, 6h 1m reads 7h). → Accepted. The user asked for this so the countdown never under-states.
- **Scores hidden by default can surprise a first-time user.** → The navbar switch is visible on every page, and each score has its own reveal control.

## Open Questions

- **Episodes still excludes movies and music**, because Movies sits directly below it. "Rewatched episodes" includes films, one per rewatch, as requested. If Episodes should also count films as one episode each, say so before implementation.
