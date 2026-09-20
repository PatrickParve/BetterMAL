## Why

Three pieces of everyday friction around how scores are shown and how the Top anime page loads.

Revealing a hidden MAL score or rank on the anime detail page makes the box holding it grow a couple of pixels taller, so the page nudges downward under the pointer at the exact moment the user is reading the number they just uncovered. The shared `ScoreChip` already solved this for the chip form; the detail page's labelled-line form never got the same treatment.

The Top anime page blocks on a live MAL fetch on the first visit of a new day, even though a full cached ranking is sitting in Postgres — the page has something to show and shows nothing while it waits.

And the app has no settled answer to which side MAL's score sits on. Six surfaces put MAL first, four put it second, so scanning two of them side by side means re-reading the labels.

## What Changes

- **Anime detail page** — the MAL box (MAL score / Rank / Popularity) is drawn at its full height from the start. Revealing the score, revealing the rank, or both no longer changes the box's height, so the boxes below it never move. The fix is made at the shared score slot rather than on this page, so the same reveal-grows-the-row effect is gone everywhere the labelled-line form is used.

- **Top anime page** — the page serves its cached ranking immediately and refreshes from MAL in the background, instead of making the first visit of a new day wait on the fetch. When the refresh lands, the rows update in place. The daily cadence, the per-list clocks, the single-flight behaviour, and the never-fetch-an-unvisited-list rule are all unchanged — only *when* the request answers changes.
  - The one case that still waits is a category selected for the very first time ever, which genuinely has nothing cached. It keeps today's behaviour: the previously shown list stays on screen, muted, until the new one arrives.

- **MAL is always on the left of a score pair** — wherever an anime's MAL score and my score are shown together, MAL leads. Six surfaces already do this and are untouched (series browser cards, the series page's average chips, series timeline cards and extra tiles, series-page entry rows, the profile page's Top series tiles, and the detail page's own two boxes). Four are flipped:
  - **My list rows** — the MAL score column moves ahead of the my-score control.
  - **Top anime** — all three tiers: the rank 1–3 showcase chips, the rank 4–10 cards, and the flat rows from rank 11 down.
  - **Profile page divergence lists** — "They liked it, I didn't" and "I liked it, they didn't" rows read `MAL <score> · Me <score>` instead of `Me … · MAL …`.
  - **Recap page hot takes** — the same flip, so the recap's twin of the divergence lists agrees with it.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `score-presentation`: a new requirement fixing the order of a MAL/mine score pair — MAL leads — so every surface showing both agrees, and the existing paired-chip requirement gains the ordering it never stated.
- `score-visibility`: the requirement that a hidden score occupies the same slot as a shown one is extended from width to **height**, so revealing never grows the line or the box around it; its scenarios that describe the old mine-then-MAL order are reworded to the new one.
- `anime-detail`: the two score boxes' shared size is stated to be independent of the hide/reveal state, so the pair holds one height whatever is revealed.
- `library-views`: the Top anime page serves its cache first and refreshes in the background rather than answering only after the fetch; and the my-list row and all three Top anime tiers put MAL's score ahead of mine.

## Impact

- **Backend**
  - `ITopAnimeService` / `TopAnimeService`: `GetRankingAsync` no longer awaits the refresh. A separate refresh entry point is added, following the `SeasonBrowseService.RefreshAsync` shape already in the codebase, and reports whether it fetched, skipped, or failed.
  - `TopAnimeController`: `GET /api/top-anime` becomes a cache-only read; a new `POST /api/top-anime/refresh?type=` triggers the refresh. Both reject an unrecognised `type` exactly as today.
  - Tests: `TopAnimeControllerTests` and the top-anime service tests change shape — what was one call is now two.
  - No migration. `TopAnimeRankingEntry` and `TopAnimeFetchLog` are unchanged; all seven lists already cache independently.
- **Frontend**
  - `TopAnimePage`: the module-scope per-type cache gains a background refresh-and-re-read after a list's first load in a session, and swaps the refreshed rows into the cache (and into any optimistic Add/Edit state) when they arrive.
  - `api/client.ts` / `api/types.ts`: the new refresh call and its outcome type.
  - `ScoreValue.css` (and the detail page's rank slot): a height floor on the reserved score slot.
  - `MyListRow` + `MyListPage.css`, `TopAnimePage` + `TopAnimePage.css`, `ProfilePage`, `RecapPage` + `RecapPage.css`: score-pair order.
- **Not affected**: the season, year, and search browse cards show a MAL score alone, so no ordering question arises there; `AnimeCard` is untouched.
