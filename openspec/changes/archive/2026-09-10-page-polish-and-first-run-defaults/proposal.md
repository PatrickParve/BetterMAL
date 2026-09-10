## Why

A round of everyday-use friction across six pages. The detail page shows an empty-feeling synopsis box for anime with no text. The next-episode countdown reads `0d 0h` for most of the last hour, and "0 days, 0 h" on home-page cards. The multi-year recap puts the finer-grained season rankings first when the year story should lead. Profile stats blend rewatches into Episodes and repeat the mean score already shown under the rating distribution. My list can't be narrowed by popularity. And a fresh install starts with every MAL score visible and NSFW titles shown, which is the opposite of what I want on first run.

## What Changes

- **Anime detail page**
  - Omit the synopsis/background box entirely when the anime has neither a synopsis nor a background. Show only the section that has text, and drop the "No synopsis available." placeholder.
  - Round the next-episode countdown **up** to the next whole hour, so anything under an hour reads `0d 1h` instead of `0d 0h`.
- **Home page**: the currently-watching card countdown uses the same round-up computation (one shared server-side helper), so 59 minutes reads "in 0 days, 1 h".
- **Recap page**: in a multi-year recap showing both, the **year** column (year ranking + years-by-time-watched) leads and the season column follows. On narrow displays the year rankings also stack first. Yearly recaps are unchanged.
- **Profile page**
  - Remove **Mean score** from the anime stats list. It stays under the rating distribution.
  - **Episodes** now counts first-viewing episodes only, with rewatches no longer folded in. It still leaves out movies and music.
  - Add **Rewatched episodes**: every episode rewatched across every media type (films count one per rewatch), including progress in a rewatch under way. It sits directly below Rewatched.
  - Days is the sum of both figures, which also fixes Days under-counting the completed first viewing of an entry currently being rewatched.
  - Add a **Scored: N** line directly above the mean score under Rating distribution.
- **My list page**: add **Popularity** to the sort options, alongside Alphabetical, My score, MAL score, Progress and the rest, for both the primary sort and the tiebreaker. It orders by the anime's MAL popularity rank, most popular first, and the direction control reverses it. Entries with no known rank go last.
- **First-run defaults** (fresh browser with no stored choice): **Hide NSFW** starts on, **Always show MAL scores for completed and dropped shows** starts on, and the navbar **hide-scores** switch starts on (scores hidden). Any choice already stored keeps working exactly as today.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `anime-detail`: the synopsis/background box only appears when there's text for it, and the Status-field countdown rounds up to the next whole hour.
- `main-dashboard`: the currently-watching countdown rounds up to the next whole hour.
- `list-recaps`: the multi-year ranking layout leads with the year column, and narrow displays stack year first.
- `profile-stats`: Mean Score is removed from the stats, Episodes no longer counts rewatches, the new Rewatched episodes stat is added, Days is redefined as the sum of both, and the distribution gains a scored-anime count.
- `library-views`: Popularity is added as a my-list sort key, most popular first and reversible.
- `score-visibility`: the global hide toggle defaults to on, and "Always show completed/dropped scores" defaults to on, both only when nothing is stored.
- `season-browser`: the "Hide NSFW" setting defaults to checked when nothing is stored.

## Impact

- **Backend**
  - `MainDashboardService` / `AnimeDetailService`: their duplicated `ToEta` is replaced by one shared round-up helper.
  - `WatchMath`: new first-viewing helper.
  - `ProfileService.BuildStats` and `AnimeStatsDto`: drop `MeanScore`, add `RewatchedEpisodes`, redefine `Episodes` and `Days`.
  - `MyListItemDto` / `MyListService`: add `PopularityRank`.
  - Tests: `ProfileServiceStatsTests` expectations change, plus new ETA and stats tests.
- **Frontend**
  - `AnimeDetailPage` (synopsis box).
  - `RecapPage` / `RecapPage.css` (ranking column order).
  - `ProfilePage` (stat labels, scored count) and `ScoreDistribution` (optional scored-count line).
  - `MyListPage` and `utils/anime.ts` (popularity sort key), and `api/types.ts`.
  - `ScoreVisibilityContext` / `ContentFilterContext` (first-run defaults).
- **No migration.** The localStorage keys don't change. Any browser that has already opened the app has explicit values stored, so it keeps them. The new defaults only appear on a fresh browser, or after clearing those keys.
