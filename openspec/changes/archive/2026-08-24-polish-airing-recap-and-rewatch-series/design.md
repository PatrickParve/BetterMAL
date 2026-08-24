## Context

Six changes across five pages. Five of them are small and land on machinery that already exists; the sixth adds a section.

**The series status pill.** `SeriesService.ComputeStatus(List<AnimeMetadata> members)` reads every member of the series — main line and extras alike — and returns one of `Ongoing`, `Upcoming`, `Finished`. `Ongoing` is returned both when a member is currently airing and when a member is merely announced. The caller (`SeriesService.cs:156`) computes `mainLineMembers` a few lines above and passes only `allAnime`, so the main line is on hand but not handed over. On the page, `SeriesPage.tsx` maps the value through `SERIES_STATUS_CLASS` for the pill's class and derives `isOngoing` from it to choose between `AiringProgressBar` and `ProgressBar`.

**The recap's season button.** `.recap-page__season-button` hovers to `--accent-bg`/`--accent-border`, the app's generic accent. The Season tab it sits under hovers to `--fam-bg`/`--fam-border` because `MODE_OPTIONS` gives it a `family--season` class, and `index.css` aliases the season family's four tokens into the local `--fam-*` vocabulary. The button renders only in season mode, so it is always beneath an active Season tab.

**The recap's scroll behaviour.** `useScrollRestoration` already honours a `{ state: { keepScroll: true } }` flag on a push navigation: it skips the `scrollTo(0, 0)` and seeds the new history entry's snapshot with the current `scrollY` so a later back navigation returns to the held position. `RecapPage.updateParams` takes an opt-in `options.keepScroll`, and exactly three call sites pass it today — the two ranking-basis buttons and the media-type select. Every period control calls `updateParams` without it.

**The detail page's picture.** `.anime-detail-page__picture` is `width: 260px; height: 368px; object-fit: cover`. A landscape picture escapes that box through a `--landscape` modifier the `useLandscapePicture` hook applies once the image reports `naturalWidth > naturalHeight`. Portrait artwork has no escape: anything taller than 260:368 is centre-cropped.

**Search.** `AnimeSearchService.SearchMalAsync` catches every exception from the live MAL search and returns an empty list. For the type-ahead (`SearchAsync`) that is harmless, because it merges MAL's candidates with the whole local title index and ranks them together — a MAL failure silently degrades to local-only results. `SearchPageAsync` builds its candidates from MAL's edges alone, so the same failure produces a genuinely empty page, and nothing in `SearchPageDto` distinguishes "no matches" from "the search never ran". The local index it would need (`GetSearchIndexAsync` → `AnimeTitleProjection`) carries id, titles, picture, and popularity rank, but not the media type, episode count, or MAL score a results card shows.

**Most rewatched.** The section is anime-scoped: `BuildRewatchedSection` filters `RewatchCount > 0`, orders by count, and returns one tile per entry. Beside it on the same page, **Top series** already demonstrates the franchise-scoped shape this needs — `SeriesRankingLookup` loads a `SeriesMembers ⋈ Series ⋈ AnimeMetadata ⋈ UserEntry` projection, `SeriesRankingIndex` aggregates it in memory, and the tiles link to `/series/{rootAnimeId}`. That projection carries scores and airing status but not rewatch counts or runtimes. `WatchMath.RewatchInclusiveEpisodes` already encodes the per-entry rewatch arithmetic (`EpisodesWatched + RewatchCount * (TotalEpisodes ?? EpisodesWatched)`), and `WatchMath.EpisodeSeconds` the duration fallback (24 minutes).

## Goals / Non-Goals

**Goals:**

- The series pill answers "is anything on right now?" from the main line, and every other value it can take keeps meaning exactly what it means today.
- The recap's period controls join the scroll-holding side of a distinction that already exists, rather than a second mechanism being invented for them.
- Every anime's picture is shown whole on the detail page, portrait and landscape alike, without the layout around it moving or a load-time collapse being introduced.
- The search page's fallback lives entirely in the backend, so the page's own logic is one extra notice and nothing more.
- Rewatch time is derived from the same per-entry arithmetic the profile's own stats already use, so a franchise total and the "Days" stat can never disagree about what a rewatch is worth.

**Non-Goals:**

- Changing the completion badge beside the status pill, or the progress bar the pill's value selects. `Airing` inherits `Ongoing`'s progress-bar choice exactly.
- Adding a fallback to the navbar type-ahead. It already merges the local index on every keystroke.
- Enriching or merging local results into a *successful* MAL search. When MAL answers, the page is byte-identical to today.
- Changing Top series' eligibility, ranking, or controls. The new Series scope is a separate reading of the same join.
- Capping how tall a detail-page picture may grow, or applying the same treatment to the series page's own header picture.
- Changing the anime-scoped "Most rewatched" scopes. All / TV / Movie / OVA / ONA / Specials keep counting rewatches, not time.

## Decisions

### D1: `Airing` is a fourth value of the one pill, decided by the main line

`ComputeStatus` gains a first arm and the caller passes the main line it already has:

```
mainLine.Any(currently_airing)                 -> "Airing"     (new)
members.Any(currently_airing)                  -> "Ongoing"    (an extra is on air)
!members.Any(finished) && members.Any(unaired) -> "Upcoming"
members.Any(unaired)                           -> "Ongoing"
otherwise                                      -> "Finished"
```

The first arm is the only addition; arms two through five are today's function unchanged, so every series whose main line is not currently airing keeps the pill it has now. In particular a series whose main line has finished but whose OVA is on air still reads `Ongoing` — that was the deliberate reading chosen over tightening the whole pill to the main line, which would have flipped such a series to `Finished` and lost the "something is still happening here" signal the pill exists to give.

`Airing` therefore *narrows* `Ongoing` rather than replacing it: after this change `Ongoing` means "nothing of the main line is on air, but something is still to come".

*Alternative considered:* deriving the value client-side from `series.mainLine[].airingStatus`, the way `completionBadge` is derived, and leaving the DTO alone. Rejected because unlike the completion badge — which depends on `episodesWatched` and therefore changes under an in-place row edit — the airing status of a member changes only when metadata is refetched, which reloads the series anyway. There is no live-edit reason to move it to the client, and a second definition of "is this series airing" would be free to drift from the server's.

### D2: Four pill colours, taken from tokens the page already aliases

`SeriesPage.css` already declares `--airing: var(--status-watching)` at `.series-page` scope, and the timeline's "on air now" tag renders in it. The new pill takes that same token, so the header pill and the timeline tag agree on what green means on this page.

That leaves `Ongoing` needing its own colour, since it no longer means "on air". It takes the on-hold amber (`--status-onhold*`) — "paused between entries, more to come" — giving the pill four distinguishable values:

| value | colour | means |
|---|---|---|
| `Airing` | green (`--airing`) | a main-line entry is broadcasting now |
| `Ongoing` | amber (`--status-onhold*`) | nothing on air, something announced |
| `Upcoming` | purple (`--status-plantowatch*`) | nothing has aired yet |
| `Finished` | blue (`--status-completed*`) | nothing left to come |

The pill already borrows watch-status tokens for airing states (`ongoing`→watching green, `upcoming`→plan-to-watch purple, `finished`→completed blue), so it treats them as a palette rather than as status semantics; amber joins that palette on the same terms.

### D3: `isOngoing` becomes "is the series still running", covering both values

`SeriesPage.tsx` uses `isOngoing` for two things: choosing `AiringProgressBar` over `ProgressBar`, and passing `showAired` to the progress readout. Both should stay true for `Airing` — that is precisely the case the broadcast bar exists for — and stay true for `Ongoing`, which is what they do today. The derivation becomes `series.status === 'Airing' || series.status === 'Ongoing'`, renamed to `isRunning` so it does not read as a check for one pill value.

### D4: The season button borrows the Season tab's hover declarations verbatim

`renderSeasonPageLink` adds `family--season` to the link's class, which resolves the `--fam-*` vocabulary to the season family, and the button's hover and focus rules become the same three declarations `.recap-page__tab:hover` and `.recap-page__tab:focus-visible` already use:

```css
.recap-page__season-button:hover  { background: var(--fam-bg); color: var(--text-h); border-color: var(--fam-border); }
.recap-page__season-button:focus-visible { outline: 2px solid var(--fam-from); }
```

Its resting state stays neutral, matching the period controls beside it.

*Alternative considered:* giving it the active Season tab's gradient fill on hover. Rejected — the tab is drawn that way because it is *selected*, and a hover that fills a link like a selected tab would read as a second selection in the same control group rather than as a link being pointed at. The tab's own hover state is the season family's "you are pointing at this" colour, which is what was asked for.

### D5: The period controls opt into `keepScroll`, and so does the filter's auto-fallback

Every `updateParams` call in `renderPeriodControls` — the multi-year from/to selects, the yearly select and its two arrows, the season select, the season-mode year select, and the season stepper's two arrows — passes `{ keepScroll: true }`. The recap-type tabs (`switchMode`) and the manual time-filter buttons do not: they change the shape of the page, not its period.

The dynamic time filter's automatic fallback — the effect that flips `filter` when the selected one turns out empty for the new period — also passes it. That effect fires only as a correction to a period or mode change the user just made, never in response to touching the filter itself (the filter buttons are `disabled` when their count is zero, so a manual choice can never be the empty one). Without the flag, stepping to a year whose `watched` set is empty would hold the scroll on the step and then lose it a moment later to the correction. After a *mode* change the flag is harmless: the mode change has already scrolled to the top, so the flag seeds a snapshot of 0 and nothing moves.

### D6: The poster box becomes width-fixed and height-natural, with `aspect-ratio: auto 260 / 368` reserving space before load

```css
.anime-detail-page__picture {
  width: 260px;
  height: auto;
  aspect-ratio: auto 260 / 368;   /* natural ratio once known; portrait box until then */
}
```

`object-fit: cover` is dropped: the box now matches the image, so there is nothing to crop to. The `auto` keyword in `aspect-ratio` is what makes this work in one rule — for a replaced element that has loaded, the *natural* ratio wins and the specified `260 / 368` is used only while the element has no natural dimensions. So the column reserves exactly the portrait box's height before the image arrives, then settles to the artwork's own height, and no separate JavaScript-measured state is needed for the portrait case.

The placeholder keeps an explicit `height: 368px`, since it has no image to take a ratio from. The landscape modifier is unchanged: it exists to make the *column* wider for landscape art, which is a layout decision the natural ratio cannot make on its own, so `useLandscapePicture` stays exactly as it is.

*Alternative considered:* `object-fit: contain` inside the existing 368px box. Rejected — it shows the whole image but letterboxes it, so a tall poster would be rendered *smaller* than a normal one to fit a box it never needed.

### D7: The search fallback is a backend decision, surfaced as one flag

`SearchMalAsync` stops conflating the two zero-result cases and returns `(List<MalAnimeListEdge> Edges, bool Failed)`. `SearchAsync` (the type-ahead) ignores `Failed` and is otherwise untouched.

`SearchPageAsync` branches on it. When MAL answered, everything downstream is today's code. When it failed, candidates come from a new local projection instead — the same `AnimeMetadata` rows the type-ahead index reads, widened with `MediaType`, `TotalEpisodes`, and `MalScore` so a results card has everything it renders — filtered by the same `SearchTextMatch` predicates the type-ahead uses (exact equality for a quoted query, substring on title or English title otherwise).

Local candidates have no MAL relevance index, so `RelevanceIndex` is assigned by a local ranking that mirrors `SearchAsync`'s: exact title matches first, then prefix matches, then the remaining substring matches, each band ordered by `SearchTextMatch.PopularityKey` and then title. Every other sort (`popularity`, `malScore`, `alphabetical`, `myScore`) is defined over fields the local rows carry, so those branches need no fallback of their own — they sort the local candidate list with the same comparators.

The local set is truncated to the same `MaxResults` (100) cap before sorting, so a one-letter query cannot try to rank the entire cache.

The response carries `MalSearchFailed: bool`. Series results are drawn from the local `SeriesSearchIndex` either way and are unaffected, so a fallback page still leads with matched series under the relevance sort.

*Alternative considered:* a `Source: "mal" | "local"` string. A bool naming the condition the notice reports is more direct, and there is no third source to leave room for.

### D8: Rewatch-only time is factored out of the arithmetic the profile already trusts

`WatchMath` gains:

```csharp
RewatchOnlyEpisodes(entry) = entry.RewatchCount * (entry.Anime.TotalEpisodes ?? entry.EpisodesWatched)
RewatchInclusiveEpisodes(entry) = entry.EpisodesWatched + RewatchOnlyEpisodes(entry)
```

The second is today's expression re-expressed in terms of the first, so the franchise totals and the profile's rewatch-inclusive "Days"/"Episodes" stats are guaranteed to agree on what a rewatch is worth — including the established fallback that an anime with no published total measures a rewatch by the entry's own `EpisodesWatched`. A member's contribution is `RewatchOnlyEpisodes(entry) * WatchMath.EpisodeSeconds(anime)`, which keeps the 24-minute assumption for an anime with no published duration.

First watches are excluded by construction: `EpisodesWatched` appears in `RewatchInclusiveEpisodes` and nowhere in `RewatchOnlyEpisodes`.

### D9: A franchise's total is summed over every member, and eligibility is just "above zero"

Every member of the series contributes — main line and extras. The question the section answers is how much time a franchise has taken back, and an OVA or a side film rewatched three times is time spent on that franchise.

A series is listed when its total is above zero, with no coverage rule. Top series requires two aired main-line entries in my list because it ranks by an *average*, which a single entry would misrepresent; a sum has no such problem — a franchise where I have only ever rewatched season one has a total that is exactly right. Ordering is total descending, ties broken by title case-insensitively, uncapped, like the strips beside it.

A rewatched anime that belongs to no *stored* series is absent from the Series scope. It is still in every media-type scope, and the profile page already enqueues background series builds for my-list anime with no series (via the Top series read on the same page), so such an anime appears once its franchise is built. This is a known gap rather than a rule (see Risks).

### D10: Series is a seventh tab on the existing control, with its own resource key

The Series scope joins `MEDIA_TYPE_TABS` as a seventh option rather than arriving as a separate control, because it answers the same question — *which slice of my rewatching?* — and the section already has exactly one control for that.

It follows the pattern the media-type tabs already use: its own `usePageData` key (`rewatched-series`), so a restored page shows the scope it was left on, and the same "hold the last section on screen while the next loads" ref that keeps the strip from collapsing between tabs. It is a view control, not persisted state: a fresh visit opens on All.

Selecting it swaps the strip's contents, not its form — same tile size, same hover, same drag-scroll, same uncapped horizontal scroll. Two things differ: the badge carries a formatted duration rather than an integer count, and a tile links to `/series/{rootAnimeId}` rather than `/anime/{animeId}`.

The endpoint is `GET /api/profile/rewatched-series`, alongside `/api/profile/top-series` and for the same reason: it runs a `SeriesMembers` join that should not land on every profile visit for a scope that may never be selected. Unlike the top-series read it does **not** enqueue background series builds — the same page's Top series read already does, and queueing the same ids twice only contends on one queue.

`SeriesRankingMemberProjection` is widened with `RewatchCount`, `EpisodesWatched`, `TotalEpisodes`, and `AverageEpisodeDurationSeconds`, and `SeriesRankingIndex` gains a `RewatchedSeries()` reading beside its existing `EligibleSeries()`. One lookup, two readings.

### D11: The rewatch-time format, and why it is not `formatRuntime`

`formatRuntime` renders `3d 7h 40min` and is used for episode runtimes across the app. The rewatch badge needs something shorter — it sits over a poster — and was asked for in days and decimal hours. It is a new `formatRewatchTime` in `utils/anime.ts` rather than an option on the existing one, so no existing caller can be affected.

```
total < 1h      ->  hours to 2 decimals            0.38h   0.03h   0.4h
otherwise       ->  round to a tenth of an hour first, then split:
                    days > 0  ->  "{d}d {h.h}h"    3d 7.7h   2d 0h
                    days == 0 ->  "{h.h}h"         7.7h   1.5h   2h
```

Trailing zeros and a trailing `.` are trimmed, so `0.40h` renders `0.4h` and `2.0h` renders `2h`.

Rounding to a tenth of an hour **before** splitting off days is what stops `47.96h` from rendering `1d 24h`: rounded first it is `48.0h`, which splits cleanly to `2d 0h`. The sub-hour branch cannot collide with the day split because it is only reached when the total is under an hour.

A total that is above zero but would render as `0h` renders `<0.01h` instead, so a badge never claims no time was spent on a series that is in the list precisely because time was.

## Risks / Trade-offs

- **A very tall poster makes the detail page's left column tall.** At 260px wide, a 2:5 poster is 650px tall, pushing the progress controls and action buttons below it further down. → Accepted deliberately: showing the artwork whole is the point of the change, and the right-hand column is independent, so nothing beside it moves. No cap is imposed because any cap reintroduces cropping for exactly the images this change exists to fix.
- **The pill's `Ongoing` changes colour for series that are not otherwise affected.** A series with an announced sequel and nothing on air keeps its `Ongoing` label but goes from green to amber. → Intended: green now means "on air", and leaving the two sharing it would defeat the change. The label itself is unchanged, so nothing a user reads becomes wrong.
- **A local fallback ranks by popularity, not by MAL's relevance, so results can be ordered differently from a normal search.** → The notice states that these are local results, so the difference is attributed rather than mysterious. The ranking mirrors the type-ahead's, which users already see for the same query.
- **The fallback only knows anime the app has cached.** A query for something never fetched returns nothing even though MAL would have found it. → Unavoidable, and stated by the notice: it says the MAL search could not be reached and these are the locally stored matches, so an empty fallback reads as "not stored here" rather than "does not exist".
- **A rewatched anime with no stored series is missing from the Series scope.** → The profile page already schedules background series builds for my-list anime with no series, so the gap closes on its own across visits; the anime is present in every other scope meanwhile.
- **Widening `SeriesRankingMemberProjection` makes the Top series read carry four more columns it does not use.** → Four scalars per member on a query that already selects eleven, run once per section read; not worth a second lookup and a second in-memory index to avoid.
- **Holding scroll across a period change can leave the reader at an offset the new period's page cannot reach**, if the new period is much shorter. → The browser clamps the scroll to the new document height, which is the same behaviour the top 10's controls already have when a media-type narrowing shortens the page.

## Migration Plan

No data migration. `SearchPageDto` and `SeriesDto.Status` are read-only response shapes: the new `malSearchFailed` field is additive, and `Airing` is a new value of an existing string field whose only consumer is `SeriesPage`. `/api/profile/rewatched-series` is a new endpoint; nothing calls it until the Series tab is selected. Backend and frontend can ship in either order — an older frontend ignores the new field and never sends `mediaType=series`.

## Open Questions

None.
