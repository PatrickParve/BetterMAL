## Why

The season/year rankings the profile page and the recap page share have three problems, all in the same small piece of surface. Their three illustrating posters break a tie at one score by **title**, so which anime represents a season is decided alphabetically rather than by the one ordering the app maintains for exactly this question — my ranking. Under a score selection on the profile page the posters do not move at all, so a Favourite years row re-ranked by "how many 8s" is still illustrated by that year's 10s, and the row's picture disagrees with the figure it was ranked on. And the recap page's own season and year rankings offer no score selection at all, even though the data behind it already ships in their DTOs.

While reading that surface: the recap page's two score rankings call `describeSeasonRanking`/`describeYearRanking` through a bare `.map()`, which hands each row's **array index** to the `selectedScore` parameter added by the last change. Every row of the recap's Season ranking and Year ranking is therefore currently mislabelled — the first row reads `undefined × 0 · 6 scored`. The fix falls out of wiring the selection through properly.

Two smaller items ride along: the score-filter buttons size themselves from their own text, so `1` is visibly narrower than `10` and `All` wider than both; and a long title on a recap top 6-10 row runs the full width of its row before it truncates.

## What Changes

- **A ranked season's or year's posters are picked by my ranking.** The three posters on every row of the season ranking, the year ranking, and the profile's Favourite seasons and Favourite years are the group's three best anime **in my ranking order** — highest first, left to right — rather than by score-then-title. Anime the ranking does not cover (scored but Plan-to-watch, or not yet aired) fall after every ranked anime of their score, the nulls-last convention the recap's top 10 and score board already follow. The two time-watched rankings keep picking their posters by episodes watched, unchanged.
- **Posters follow the score selection.** With a score selected, a row's posters are the group's best three anime **at that score**, so the picture agrees with the count the row was ranked on. Under **All** the posters are what they are today: the group's best three overall.
- **The recap page's Season ranking and Year ranking gain the score filter.** The same **All / With most: 10…1** control the profile's favourites rankings carry, with the same tier colours, the same five-row cap, the same "See all" overlay, and the same fall-back-to-All rule. Each ranking holds its own selection, carried in the recap's URL alongside its period, filter, basis, and type — so a filtered ranking is linkable and survives a reload like every other recap control. The two time-watched rankings get no control; they rank on seconds, not scores.
- **The recap's ranking rows stop being mislabelled.** Wiring the selection through replaces the `.map(describeSeasonRanking)` call that was silently passing the row index as the selected score, so an unfiltered row reads `6 scored · 7.64` again rather than `undefined × 0 · 6 scored`.
- **The score-filter buttons are one width.** Every button in the control — **All** and each numeral — occupies the same width, so the strip reads as an even row rather than one that steps in and out with the digit count.
- **A recap top 6-10 row's title is capped.** The title truncates at a fixed cap rather than at the row's own width, so a long title no longer spans the row. The rank, poster, score column, and row height do not move.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `list-recaps`: the season and year rankings pick their posters by my ranking rather than by title, gain the All/with-most-N score filter with its selection carried in the URL, show posters drawn from the selected score, and cap a top 6-10 row's title at a fixed width.
- `profile-stats`: Favourite seasons and Favourite years pick their posters by my ranking; a score selection now moves a row's posters to that score; the filter's buttons share one width.

## Impact

- **Backend DTOs**: `RecapSeasonRankingDto` and `RecapYearRankingDto` replace `TopPosters` with `PostersByScore` — for each score the group holds, that score's best three anime in ranking order, descending by score. The **All** posters are the first three of that list read from the top, so no row ships two poster lists that could disagree. Measured against the current database: 426 poster records for the whole season ranking and 258 for the year ranking, against 345 for today's `TopPosters` across both. `RecapTimeRankingDto` is untouched.
- **Backend services**: `RecapRankingBuilder.BuildSeasonRanking`/`BuildYearRanking` take an `AnimeRankingSnapshot` and order posters by it; `TopPosters` becomes a per-score bucketing. `RecapService` passes the snapshot it already builds. `ProfileService` builds its snapshot once in `GetProfileAsync` and passes it to both `BuildTopAnimeSection` and `BuildFavouriteSeasonsAndYears` rather than ordering the whole list twice.
- **Frontend components**: `RankingSection.tsx` gains the poster-picking helper and takes over `offeredScores`/`rankByScoreCount` (moved out of `ProfilePage`) and the "See all" overlay's title composition, so the two pages share one implementation of the filter rather than a copy each. `RankingSection.css` for the equal-width buttons.
- **Frontend pages**: `RecapPage.tsx` (two new URL parameters, the filter wired into both score rankings, the `.map()` bug), `RecapPage.css` (title cap), `ProfilePage.tsx` (simplified by what moves into `RankingSection`).
- **Backend tests**: `RecapRankingBuilderTests` and `ProfileServiceFavouriteSeasonsAndYearsTests` assert on `TopPosters` and follow it to `PostersByScore`.
- No new endpoint, no schema change, no migration.
