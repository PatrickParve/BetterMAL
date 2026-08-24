## Context

The season browser already holds everything a year page needs. `SeasonAnimeListing` files each anime under the `(year, season)` MAL classifies it in; `SeasonFetchLog` stamps when each season was last fetched; `SeasonRepository.GetPageAsync` sorts, filters, and pages one season's listing entirely in SQL; `SeasonBrowseService.RefreshAsync` fetches one season from MAL at most once per local day behind a `RefreshGate` single-flight lock; and `SeasonHorizon.Resolve` computes how far forward the user may navigate. `SeasonPage.tsx` reads that data cache-first and refreshes in the background, keeping year/season/sort/type/inMyList in the URL.

A year is exactly four of those season points. Nothing about MAL changes at year grain — there is no year endpoint on MAL and no year-level classification — so the whole change is a matter of reading four cached points together and refreshing four points instead of one.

Two existing facts constrain the design. First, `AnimeTrackerDbContext` is scoped per request and is not thread-safe, so four season refreshes inside one request cannot run concurrently. Second, `SeasonPage`'s correctness rests on a small set of hard-won behaviours — the two-effect split (a read effect keyed on every parameter, a debounced refresh effect keyed on the period alone), the `useLatestRequest` generation guard, the accumulated type-option set, and the four-way terminal state — each of which was a bug at some point. The year page must reproduce them rather than reinvent them.

The name is settled in the proposal: **Year**, route `/year`, page title *Yearly anime*, capability `year-browser`.

## Goals / Non-Goals

**Goals:**

- One combined listing per year, sorted, filtered, and paged across all four seasons at once — so page two of a popularity sort continues from page one across the whole year, not per season.
- Reuse of the season browser's caching, once-per-day, and single-flight rules verbatim, so visiting a year never costs more MAL traffic than visiting its four seasons would have.
- Behavioural parity with the season page's controls, URL state, restoration, infinite scroll, and empty states.
- A yearly recap that leads into the year page the way a season recap leads into the season page.

**Non-Goals:**

- Any new MAL request shape. `IMalClient.GetFullSeasonAsync` is called exactly as it is today, per season.
- Any schema change or migration. No `YearFetchLog`, no year-level listing table — a year is derived, never stored.
- Changing the season page. Its route, URL parameters, and behaviour are untouched.
- A year-level recap ranking or statistic. The recap's yearly mode gains one link, nothing more.
- Deduplicating anime across seasons. `SeasonBrowseService` already files each anime under MAL's own `start_season`, so an anime appears in exactly one season of a year and the union cannot produce duplicates.

## Decisions

### D1 — A year is four season points, resolved at the repository, not merged in memory

`SeasonRepository.GetPageAsync` is generalised from a single `(year, season)` to a set of points: its opening `Where(l => l.Year == year && l.Season == season)` becomes a membership test over the requested points, and everything downstream — the in-my-list filter, the hentai filter, the type filter, the count, the four sort orderings, `Skip`/`Take` — is unchanged. The season endpoint passes one point; the year endpoint passes four.

*Alternative rejected:* calling the existing per-season query four times and merging client- or service-side. It cannot page correctly — to serve offset 100 of a year sorted by popularity you would have to fetch and merge every season in full, defeating the paging the query already does in SQL — and it would need the sort comparators duplicated in C# where they can drift from the SQL ones. Pushing the point set into the one query keeps a single implementation of the ordering rules, including the my-score grouping the `Unwatched` divider depends on.

`HasListingAsync` is generalised the same way: for a year it answers "does any of the four seasons have a cached listing row".

### D2 — The year's four refreshes run sequentially and collapse into one outcome

`RefreshYearAsync` loops the four seasons and awaits each `RefreshAsync` in turn. Sequential is not a performance compromise to apologise for — it is required, since one scoped `DbContext` cannot serve four concurrent EF queries — and in the steady state three or four of the four calls return `Skipped` without touching MAL at all.

The four per-season outcomes fold into one year outcome by this precedence:

1. any `Fetched` → **`fetched`** (something new landed; the client re-reads),
2. else any `Skipped` → **`skipped`** (nothing to do; already current today),
3. else any `NotListed` → **`notListed`** (MAL has opened no season of this year),
4. else (all four `Failed`) → **`failed`**.

The ordering is what makes each outcome mean for a year what it means for a season, which is what lets the client reuse the season page's re-read and terminal-state logic unchanged. `Fetched` outranks everything because any new data warrants a re-read. `Skipped` outranks `NotListed` and `Failed` because a year with even one season already fetched today is a year the client should treat as current rather than as unlisted or broken. `NotListed` outranks `Failed` so a genuinely unopened future year reports the honest "MyAnimeList hasn't listed this year yet" rather than a retry message. Only a year where every season failed reports `failed`.

*Alternative rejected:* returning the four outcomes as an array and folding on the client. It would put a rule that belongs to one concept in two languages, and the client has no use for the per-season detail — the page has one grid and one empty state.

A single-flight key of `$"year:{year}"` is deliberately **not** added: the per-season `RefreshGate` keys already collapse concurrent work at the level where MAL is actually called, and a coarser year lock would serialise a year visit behind an unrelated single-season visit for no benefit.

### D3 — The year ceiling is the season ceiling's year; no new bounds endpoint

`GET /api/season/bounds` already returns the furthest navigable season, computed by `SeasonHorizon.Resolve` from MAL's forward window, the latest cached season, and today's observed 404s. The furthest navigable *year* is that result's `latestYear` — nothing more to compute, since a year is navigable exactly when any season in it is. The year page calls the same endpoint the season page calls and reads `latestYear` off it, seeded meanwhile with the year of the same client-side default the season page seeds — the current season shifted forward two, whose year is the current year in the first half of the year and the next one in the second half — so nothing is disabled while the request is in flight. Seeding a flat `currentYear + 1` would be wrong for half the calendar: winter + 2 is summer of the *same* year.

*Alternative rejected:* a `GET /api/year/bounds` endpoint. It would return one field derivable from an endpoint the app already calls, and add a second place for the horizon rules to be re-derived.

As on the season page, a year addressed directly in the URL beyond the ceiling still renders as asked — it is never silently rewritten — and the dropdown keeps that year as an option so the `<select>` always has a valid value. Forward stepping from there stays blocked.

### D4 — `YearPage.tsx` is a sibling of `SeasonPage.tsx`, not a generalisation of it

The two pages share their shape but differ in their period type (a `(year, season)` point versus a year), their navigation controls (two dropdowns versus one), their ceiling comparison, their page-state key, and their empty-state wording. Factoring a shared `PeriodBrowsePage` around those differences would mean threading five or six props of period-specific behaviour through a component whose entire body is period-specific — the abstraction would be larger than either page.

What *is* shared is shared already and stays so: `AnimeCard`/`AnimeCardMeta`, `FilterMultiSelect`, `usePageData`, `useLatestRequest`, `useDebouncedValue`, `useContentFilter`, and the `MEDIA_TYPE_ORDER`/`mediaTypeLabel` helpers. `YearPage.css` reuses the season page's three-zone header grid and its fixed six-column results grid so the two pages are visually identical below the header.

The year page reproduces, deliberately and with the reasoning intact in comments:

- **the two-effect split** — a read effect that re-runs on year, sort, type, in-my-list, and hide-hentai, and a *separate* refresh effect keyed on the debounced year alone, so changing a sort or a filter never triggers a MAL fetch;
- **the debounce** (the season page's 400 ms) so arrow-stepping through years only refreshes the year landed on — worth four times as much here, since each skipped year is four season refreshes not started;
- **the `useLatestRequest` generation guard**, bumped by every read parameter, so a slow background re-read or load-more cannot apply to a year the user has since left; and the unconditional `setLoadingMore(false)` in `finally` that keeps a superseded load-more from wedging infinite scroll;
- **the accumulated type-option set**, reset when the year changes — since the type filter runs server-side, re-deriving the options from the current `items` would make every unselected type vanish the moment one is chosen;
- **the four terminal states** — grid, filters-empty, not-listed, load-failed — resolved in the same order, with `refreshOutcome` reset the instant the year changes so one year's outcome can never decide another's render.

### D5 — Page-state key is the year alone

`usePageData` is keyed `year:${year}`, mirroring the season page's `season:${year}/${season}`. A different year is a different history snapshot, restored with whatever data it held regardless of which sort or filter was active when it was left; sort and filter changes within one year go through `reload`, which carries its own generation guard, rather than opening a second racing fetch under a second key.

### D6 — The recap's year control is the season control with a different family

`renderSeasonPageLink` gains a year-mode sibling rendering the same markup — same `<Link>`-as-button, same height, same calendar icon, same `&rsaquo;` — with `family--year` in place of `family--season` and the label *Browse the year*, pointing at `/year?year={startYear}`. The `family--*` classes already set `--fam-bg`, `--fam-border`, and `--fam-from`, which the existing `.recap-page__season-button` hover and focus rules read, so the year family's achromatic treatment comes for free with the class swap.

The two controls are mutually exclusive by mode — season mode renders the season one, yearly mode the year one, multi-year mode neither — which keeps the recap's control row the same shape it has today.

### D7 — Hide NSFW follows the data, not the page

The hide-hentai exclusion is a `Where` clause on the same listing query D1 generalises, so it applies to the year page automatically. The `season-browser` spec's claim that the exclusion applies "to the season browser only" is therefore updated to name both browsers rather than being worked around — the setting excludes hentai from the cached season listings wherever they are read, and the pages it does *not* touch (search, my list, top anime, airing, home, detail) are unchanged.

## Risks / Trade-offs

**A cold year's first visit costs four full MAL season fetches, each itself paginated.** → This is inherent to the request ("just get the four seasons") and is bounded: it happens once per year per local day, the four run sequentially rather than bursting, and any season already fetched today by a season-page visit is skipped outright. A user who browsed the four season pages first pays nothing extra on the year page.

**Four sequential refreshes make the year page's "Updating…" indicator sit visible for longer than the season page's.** → Accepted, and it is the honest signal: the page renders from cache immediately and the indicator is passive. It reports exactly what is happening rather than pretending the work is shorter.

**The generalised repository query is on the season page's hot path.** → The change is to the `Where` predicate only — a membership test over one point for the season page, producing an equivalent plan to today's equality test — with the projection, filters, count, and orderings untouched. Existing season repository tests cover the single-point case and must keep passing unchanged; that is the regression signal.

**A partly-failed year refresh shows a complete-looking grid.** → Accepted, and consistent with how the season page treats a failed refresh: the cached listing renders and the failure is not surfaced as a page error. The precedence in D2 means a year is only ever *reported* as failed when all four seasons failed, and the next visit retries, since a failed fetch never counts as the day's fetch.

**Two pages now reproduce the same effect structure, so a future fix to one can miss the other.** → Accepted deliberately (D4) over an abstraction that would be larger than either page. The mitigation is that the shared pieces genuinely at risk of divergence — the request-generation guard, the debounce, the data hook — are already extracted into hooks used by both.
