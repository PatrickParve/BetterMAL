## Context

**1 — The Season page hangs on a season MAL has not opened.** Probed against the live API today (2026-08-18, current season summer 2026), with `X-MAL-CLIENT-ID` and the same query the app sends:

| `anime/season/…` | status | entries |
|---|---|---|
| `2026/summer` (current) | 200 | 100 + next page |
| `2026/fall` (+1) | 200 | 86 |
| `2027/winter` (+2) | 200 | 35 |
| `2027/spring` (+3) | 404 | — |
| `2027/summer`, `2028/winter`, `2030/winter` | 404 | — |

So MAL publishes the current season plus two, and answers `404` beyond that — the "2 next seasons, then Later" window the user described. Anime announced past that point have no `start_season` at all and are unreachable through this endpoint; MAL's own site files them under a *Later* bucket that API v2 does not expose.

The failure chain today: `MalClient.GetAsync` calls `EnsureSuccessStatusCode`, so the 404 throws `HttpRequestException` → `SeasonBrowseService.RefreshAsync` catches everything (`SeasonBrowseService.cs:52`), logs a warning and returns `Refreshed = false` → no `SeasonFetchLog` row is written → the client's `refreshSeason(...).then` sees `refreshed === false` and skips its re-read (`SeasonPage.tsx:261`) → `neverCached` (`lastFetchedAt === null && items.length === 0`, `SeasonPage.tsx:148`) stays true → the page renders "Loading…" forever, and every revisit spends another MAL request on the same 404. Nothing bounds navigation: `shiftSeason` has no upper limit and `yearOptions` runs to `max(year, currentYear) + 1`, so the next arrow and the year dropdown both walk straight off the end of MAL's data.

The same dead end exists for any first fetch that fails (MAL unreachable on a season never cached before) — the page has no state for "the first fetch is done and produced nothing".

**2 — A More-section tile wraps its meta line.** `.series-page__extras-grid` is `repeat(auto-fill, minmax(140px, 1fr))` (`SeriesPage.css:444`), so a tile's text box is ~120px wide at the narrow end. `SeriesExtraTile` prints `mediaTypeLabel · year · N ep` at 12px with no wrapping guard (`SeriesExtraTile.tsx:38-41`, `SeriesExtraTile.css:78-83`), and "TV special · 2023 · 5 ep" needs more than that — so it takes two lines, pushing that tile's chips and footer down relative to its row neighbours. The media type in that string is also the heading of the group the tile sits under (`SeriesPage.tsx` renders `mediaTypeLabel(group.mediaType)` per group); `openspec/specs/series-page/spec.md:458` reads "with the media type carried by its group heading", which an early draft of decision 6 took as license to drop it from the tile too — reversed below after review, so the base spec's phrasing is corrected by this change rather than followed literally. The main-line timeline card is not affected — `.series-timeline__card-meta-line` is already `nowrap` + ellipsis at 168px (`SeriesTimeline.css:124-134`).

Constraints: `db.Database.Migrate()` runs at startup with no manual migration step, and the local SDK is 9.0 while the project targets .NET 10 (migrations have to be authored through the `sdk:10.0` container) — a schema change is disproportionate for this fix and is avoided below. MAL requests are paced at 1/s globally; the fix must not add per-visit MAL traffic.

## Goals / Non-Goals

**Goals:**
- A season MAL has not opened yet resolves to a stated answer instead of an endless spinner, and costs at most one MAL request per local day.
- Forward navigation stops where MAL's data stops, in both the arrows and the quick-jump dropdowns.
- The horizon rolls forward on its own as MAL opens each new season — no constant to bump, no manual step.
- A season page with nothing to show says which of the three reasons applies (not listed yet / nothing matches the filters / first fetch failed).
- A More tile's meta line is one line at every column width the grid produces.

**Non-Goals:**
- No *Later* bucket for unannounced anime — MAL v2 exposes no endpoint for it, so there is nothing to render. The horizon is where the data stops.
- No schema change and no migration.
- No change to season membership classification, the `start_season` rule, the once-per-day fetch rule, single-flight, or the sort/filter/infinite-scroll paths.
- No change to the main-line timeline card, which already pins its meta lines.
- `SeriesEntryRow` is dead as a component (only its two exported label helpers are imported anywhere); it is left alone rather than swept up here.

## Decisions

### Decision 1: MAL's 404 is an answer about the season, not a fetch failure

`IMalClient.GetFullSeasonAsync` returns `List<MalAnimeListEdge>?`, with `null` meaning "MAL has no listing for this season" — a 404 from `anime/season/{y}/{s}`. `MalClient` grows a 404-tolerant read for that one call, in the shape it already uses for `DeleteMyListStatusAsync` (`MalClient.cs:107`, "already absent on MAL — the goal state already holds"). Every other status keeps going through `EnsureSuccessStatusCode`, so a 500 or a network drop is still a failure and still retries on the next visit.

`SeasonBrowseService.FetchAndCacheAsync` treats `null` as "no anime for this season": it adds no listing rows, and — the part that fixes the hang — still stamps `SeasonFetchLog.LastFetchedAt`. That single write puts the season under the existing once-per-day rule, so the second visit costs nothing, and gives the client a non-null `lastFetchedAt` so it leaves the never-cached loading state.

**Alternatives considered.** *Catch `HttpRequestException` with `StatusCode == NotFound` inside `SeasonBrowseService`* — puts MAL wire semantics outside `Services/Mal/`, against the guide's "nothing else talks to MAL" rule. *Map 404 to an empty list inside `MalClient`* — tempting, and the downstream handling would be identical, but it erases the distinction the refresh outcome reports (decision 5) and makes an unopened season indistinguishable from a MAL bug that returns an empty page for a real season.

### Decision 2: "MAL has no listing" is derived from the tables we already have, not a new column

A season that has been fetched and has no `SeasonAnimeListing` rows *is* a season MAL has no listing for — the 404 path above and a hypothetical `200` with zero entries produce the same rows and mean the same thing to both the user and the horizon. So the fact is read, not stored:

```
hasListing(y,s)   := EXISTS (SELECT 1 FROM SeasonAnimeListings WHERE Year=y AND Season=s)
fetchedAt(y,s)    := SeasonFetchLogs.LastFetchedAt   (already read by GetLastFetchedAsync)
notListed(y,s)    := fetchedAt is not null AND NOT hasListing(y,s)
```

`ISeasonRepository` gains `HasListingAsync(year, season, ct)` (an unfiltered `AnyAsync`; `GetPageAsync`'s `TotalCount` cannot serve here because it is computed *after* the type/hentai/in-my-list filters), and `SeasonPageDto` gains `HasListing`. No column, no migration, and nothing to backfill for the seasons already cached.

**Alternatives considered.** *Add `SeasonFetchLog.MalHasListing`* — the honest modelling, and the first draft of this design; rejected because it buys nothing the two existing tables do not already say, at the cost of a migration authored through the .NET 10 container. If MAL ever starts returning `200` with an empty page for a season it really does list, that is the moment to add the column.

### Decision 3: The navigable ceiling is computed, self-correcting, and pure

`SeasonHorizon.Resolve` is a pure function over a season-ordered point (`year * 4 + seasonIndex`, extending `SeasonCalendar` with a `Shift` and an ordering helper), so it is unit-testable with no DB:

```
candidate := current + 2                       // MAL's published forward window
candidate := max(candidate, latestCachedSeason) // a wider window, once seen, stays reachable
while candidate > current and notListedToday(candidate):
    candidate := candidate - 1                  // today's observed horizon
ceiling := candidate
```

Each clause earns its place: **+2** is what MAL publishes and rolls forward with the calendar by itself, so nothing needs bumping when a new season opens; **raise to the latest cached season** means that if MAL ever widens the window and a season past +2 gets cached, it stays reachable instead of being walled off; **retreat past a season MAL 404'd today** is what actually blocks movement past the last season it was possible to get, for the case where MAL's window is narrower than +2 (likely early in a season). The retreat is scoped to `LastFetchedAt` falling on the *current local date* — an older 404 never constrains the ceiling, so each new day the ceiling springs back to +2, the boundary season is reachable again, and one visit re-probes it. That is what keeps the horizon from deadlocking: a season blocked forever could never be re-fetched to discover MAL had opened it.

Cost: at most one wasted MAL request per boundary season per local day, and only when the user actually walks to the edge.

`SeasonBrowseService.GetBoundsAsync` feeds the resolver from one query over the ≤4 candidate seasons — `(Year, Season, LastFetchedAt, HasListings)` — plus the max cached `(Year, Season)`. No MAL call; `GET /api/season/bounds` stays a pure cache read like `GetPageAsync`.

**Alternatives considered.** *A hardcoded `+2` with no DB input* — correct today and much smaller, but silently wrong the moment MAL's window differs, in the direction that leaves the user staring at an empty season. *Server-side probing of the season past the ceiling* — self-correcting without the user ever hitting a wall, but it spends a MAL request per day on a season nobody asked for, and needs its own daily-gate. *Client-side `current + 2` only, with no endpoint* — no request at all, but it cannot know about a cached season past the window nor about today's 404, i.e. neither correction survives.

### Decision 4: The client starts from the same default the server does

`SeasonPage` seeds its ceiling state with the client-computed `current + 2` and replaces it when `GET /api/season/bounds` resolves. Nothing is blocked or disabled while that request is in flight, and the common case (server agrees) shows no transition at all. The dropdowns follow the ceiling: the year list ends at `max(ceilingYear, viewedYear)` — the viewed year is included so a URL-addressed season past the horizon still shows its own year rather than a `<select>` with a value it does not offer — and the season list is cut to the ceiling's season when the selected year *is* the ceiling year. Changing the year to the ceiling year while a later season is selected clamps the season down to the ceiling, since the year `<select>` changes one half of the target at a time.

A season addressed directly in the URL is rendered as asked, never rewritten: the page shows the "not listed" state and leaves the next arrow disabled. Silently redirecting a bookmark is worse than answering it honestly.

The previous arrow gets the same disabled treatment at the opposite end, floored at the year dropdown's own `EARLIEST_YEAR` (1989, winter) — otherwise stepping backward past it would desync the arrow from the dropdown (a viewed year the `<select>` has no matching `<option>` for). Both arrows render visibly greyed out while disabled — `opacity` plus `cursor: not-allowed` — and the existing hover rule is scoped with `:not(:disabled)` so a disabled arrow never lights up on hover either.

### Decision 5: A four-valued refresh outcome, and a total empty-state matrix

`SeasonRefreshResultDto` becomes `(SeasonRefreshOutcome Outcome)`, serialized as `fetched` | `notListed` | `skipped` | `failed`, replacing the bare `Refreshed` bool that conflated "already fetched today" with "the fetch blew up" — the exact conflation that strands the page. The client re-reads on `fetched`/`notListed`, on `skipped` only when it has nothing cached (a cross-tab race where its own read landed before another tab's fetch), and never on `failed`.

The page's terminal states are then decided in order, so no combination falls through to a blank page:

| condition | rendering |
|---|---|
| `items.length > 0` | the grid |
| `lastFetchedAt != null && hasListing` | "No anime match the current filters." |
| `lastFetchedAt != null && !hasListing` | "MyAnimeList hasn't listed this season yet." |
| `lastFetchedAt == null`, refresh not settled | "Loading…" |
| `lastFetchedAt == null`, refresh settled | "This season couldn't be loaded — it'll be retried next time you open it." |

The last row is the only new *error* surface, and it is confined to a season with nothing cached; a failed refresh over a cached season still says nothing, exactly as the spec requires today. The refresh-settled flag resets on every season change, so stepping seasons cannot carry one season's outcome onto another's render.

### Decision 6: The tile's lines are pinned to one row each; the media type stays

`.series-extra-tile__meta` and `.series-extra-tile__aired` take the `white-space: nowrap; overflow: hidden; text-overflow: ellipsis` treatment `.series-timeline__card-meta-line` already uses, so "TV special · 2023 · 5 ep" truncates on one line instead of wrapping to a second, and an unforeseen value (a five-digit episode count, a future extra field) truncates the same way rather than reflowing the tile.

The media type itself is kept on the tile rather than dropped in favour of the group heading above it. An earlier draft of this decision removed `mediaTypeLabel(entry.mediaType)` from the line entirely, reasoning that the heading already names it once per group — but that reads worse in practice: a tile is legible on its own (hover states, screenshots, scrolled views where the heading has scrolled out of view), and the ellipsis truncation above already solves the actual layout bug (the second row, and the chip/footer misalignment it caused) without needing to remove any content. Kept per direct product feedback after seeing the type-less tile live.

**Alternatives considered.** *Drop the media type, keep only year/episode count* — the first cut of this decision (see above); rejected after review because it made a tile depend on its group heading for a fact it should carry on its own. *Shorten the label to "TV sp."* — invents a second vocabulary for media types that the group headings, the my-list filter and the season page would not share. *Widen the grid's `minmax`* — trades every franchise's tile density against one string's width, and still breaks at the next narrower breakpoint.

## Risks / Trade-offs

- **MAL's window is not exactly +2 at every point in the calendar** → The ceiling is only a default; the retreat clause (decision 3) corrects it downward the same day it is observed, and the cached-season clause corrects it upward. Worst case the user reaches one season that says "not listed yet" — an honest answer, not a hang.
- **The retreat costs one MAL request per boundary season per day** → Bounded by the daily fetch log, only spent when the user walks to the edge, and it is the price of a horizon that can un-block itself when MAL opens a season.
- **Deriving "not listed" from "no listing rows" (decision 2) would misread a season MAL lists but returns nothing for** → No such season exists in the probe above, and the two states are indistinguishable to the user anyway ("nothing to show for this season"); if MAL ever changes, decision 2 names the column to add.
- **`SeasonRefreshResultDto` changes shape** → Internal API, single caller, shipped together; the frontend is the only consumer of `/api/season/*`.
- **The tile's meta line can still truncate at the narrowest column width** (e.g. "TV special · 2023 · 5…") → An honest trade of completeness for one-line alignment; the full facts are always available via the tile's own link to the detail page, and the truncated case is narrower than before (the ellipsis now only has to absorb the episode/year tail, not fight a second row).

## Migration Plan

No schema change, no data migration, no config. Ship backend and frontend together (the refresh response shape changes). Rollback is a plain revert — cached listings and fetch logs written under this change are the same rows the current code writes, and a fetch log stamped for a 404'd season simply means the old code skips that season's MAL call for the rest of that day.

## Open Questions

None. The MAL horizon behaviour was measured rather than assumed (table above), and the "2 next seasons then Later" shape the request described is confirmed as MAL's published window.
