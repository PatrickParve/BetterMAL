## Context

Recaps landed in `add-list-recaps` (archived 2026-08-18) as a single page reachable only from a "Recap a period" button in My list, which opens `RecapPickerOverlay` and navigates to `/recap?...`. The recap page (`frontend/src/pages/RecapPage.tsx`) renders stats → hot takes → top 10 → season ranking → year ranking, top to bottom. My list already accepts a recap scope from the URL (`recapMode`/`recapFrom`/`recapTo`/`recapYear`/`recapSeason`/`recapFilter`/`recapType`, `MyListPage.tsx:114-196`) — that machinery exists and works; today it is only ever populated by the recap page's "See all N in my list" link.

The backend serves one payload per period+filter from `GET /api/recap` (`RecapService.GetRecapAsync`) containing the whole included set plus stats and the two rankings; the client does the top-10 slice, the ranking-basis switch, and the media-type narrowing locally. Season/year rankings are Bayesian-weighted on my scores, gated to `filter == aired && mode != season`, with the year ranking additionally gated to multi-year.

That gives this change a favourable shape: three of its five parts are pure frontend re-wiring of code that already exists, and the two backend parts (a dropped count, a time-watched ranking) fit the existing builder split without touching the request contract.

## Goals / Non-Goals

**Goals:**
- Make Recap a first-class nav destination with a sensible zero-input default.
- Turn My list's period control into an in-place scope, reusing the scope machinery that already exists there instead of adding a parallel one.
- Reorder the recap page so the top anime leads and the stats sit beside it.
- Pair the season and year rankings, cap both at five, give both a "See all" overlay.
- Add a time-watched ranking of the period's seasons/years.
- Raise hot takes to five; relabel the entry-count stat and add a dropped count.

**Non-Goals:**
- No change to the `/api/recap` request contract, the period modes, the time-filter semantics, or the entry-selection rules.
- No change to the Bayesian weighting, its thresholds, or the poster rule.
- No caching, persistence, or schema work — the recap is still computed per request.
- No redesign of `RecapPickerOverlay`'s internals beyond what its new caller needs.

## Decisions

### D1. The nav entry is a plain link to a fixed default, not a new route

`Navbar.tsx`'s `NAV_LINKS` gets `{ to: '/recap?mode=yearly&year=<currentYear>&filter=aired', label: 'Recap' }`, built at render from `new Date().getFullYear()`. The recap page already parses every one of those params and already falls back off an empty filter (`RecapPage.tsx:145-152`), so the default needs no new server or page behaviour.

The one wrinkle: `NavLink`'s active matching ignores the query string but `NAV_LINKS` is a static array today. Building the entry inside the component (rather than in the module-level constant) keeps the year correct across a midnight boundary in a long-lived tab, and `NavLink`'s `to` with a search string still matches `/recap` for the active class. *Alternative considered:* a `/recap` route with no params that redirects — rejected, it adds a history entry and the page's existing defaults (`mode=yearly`, current year, `filter=watched`) would have to change to match, which would alter behaviour for existing links.

Note the page's own default filter is `watched` (`RecapPage.tsx:114`); the nav link passes `filter=aired` explicitly rather than flipping that default, so the recap page's existing URLs keep behaving as they do.

### D2. My list's picker confirm writes scope params instead of navigating

`RecapPickerOverlay`'s `onConfirm(search)` contract stays exactly as it is — it hands back a query string and the caller decides. Only `MyListPage`'s handler changes: instead of `navigate('/recap?' + search)` it translates the picker's vocabulary (`mode`/`from`/`to`/`year`/`season`/`filter`) into the scope vocabulary (`recapMode`/`recapFrom`/`recapTo`/`recapYear`/`recapSeason`/`recapFilter`) and calls `setSearchParams`. The scope then flows through the code path that already exists at `MyListPage.tsx:148-172`: one `getRecap` call keyed by period, intersected against the list by anime id.

Doing the translation in `MyListPage` rather than teaching the overlay a second output shape keeps the overlay a pure period picker with one job, and keeps the two param vocabularies deliberately distinct — the `recap`-prefixed names exist precisely so they cannot collide with My list's own filter params (the original design decision 9). *Alternative considered:* having the overlay emit scope params directly — rejected, it couples the overlay to My list and breaks its reuse from anywhere else.

The picker's confirm button label changes from "Show recap" to something scope-shaped ("Apply to list"), and its title from "Recap a period"; both become props with the current strings as defaults, so the overlay stays reusable if another caller wants the navigate behaviour later.

### D3. The scope chip carries the recap link; period → `/recap` handoff is the mirror of the existing one

`recapScopeLabel()` already assembles the period/filter/type string. The chip gains a `Link` built from the same values, reusing the inverse of `RecapPage`'s `myListScopeSearch` — a small `recapSearchFromScope()` helper in `MyListPage`. Two small mapping functions in two files, each next to its own vocabulary, beats one shared bidirectional module that both pages would have to import.

### D4. The period control stays in the status-tab row

It is already inside `my-list-page__tabs` (`MyListPage.tsx:569`), but reads as a separate feature because `.my-list-page__recap-button` is styled unlike its neighbours. The work here is CSS: match the tab chrome, and separate it from the status tabs with a divider or `margin-left: auto` rather than a different shape. No structural change.

### D5. Recap page layout is a two-column grid, stats left of the top anime

A CSS grid on the recap's lead section: `grid-template-columns: minmax(14rem, 20rem) 1fr` with the stat block first in DOM order and the top-anime section second. Below a breakpoint it collapses to one column, which puts stats above the top anime — exactly the fallback the spec asks for, with no separate mobile markup. DOM order matching the narrow-display order also keeps the tab sequence sensible.

The stat block itself goes from a 6-tile horizontal strip to a vertical stack in a narrow column; `.recap-page__stats` changes from a row-flow flex to a grid that is 1–2 columns wide. Tile order becomes: In this period, Completed, Dropped, Mean score, Episodes watched, Movies watched, Time spent.

*Alternative considered:* stats in a sticky sidebar — rejected as scope creep; the section scrolls away with the rest.

### D6. One generic ranking overlay replaces `YearRankingOverlay`

Three rankings now need the same "See all" overlay. Rather than three near-identical components, generalise the existing one into `RankingOverlay` taking `title`, plus a normalised row shape `{ key, label, meta, to, posters }`. Each caller maps its DTO into that shape — the same mapping the inline five-row list already does, lifted into a small per-ranking `describe()` function used by both the inline list and the overlay, so the two can never drift.

`YearRankingOverlay.tsx`/`.css` are renamed rather than left in place; there is exactly one caller today, so there is no compatibility surface to preserve.

### D7. The time-watched ranking is a new builder pair on the server, not a client-side derivation

The client cannot compute it: `RecapRowDto` carries no episodes-watched or duration fields, and adding them to every row to support one ranking would grow the payload for the common case. `RecapRankingBuilder` gains `BuildSeasonTimeRanking` and `BuildYearTimeRanking`, returning `RecapTimeRankingDto(Year, Season?, long TimeSpentSeconds, int EpisodesWatched, List<RecapRankingPosterDto> TopPosters)` — one DTO for both levels, with `Season` null at the year level, since the two differ only in grouping key.

Per-episode seconds must match the **Time spent** stat exactly (the spec requires the same basis), so `RecapStatsBuilder.EpisodeSeconds` moves to an internal shared helper both builders call rather than being duplicated. This is the one place where a copy-paste would produce a visible inconsistency: a per-group total that does not sum to the headline stat.

Grouping mirrors the score rankings — `SeasonCalendar.GetSeasonFor(e.Anime.AiredFrom!.Value)` and `AiredFrom.Value.Year` over the same `airedIncluded` set — so the `AiredFrom` non-null invariant the existing builders rely on holds unchanged. Filtering is `TimeSpentSeconds > 0` rather than the score rankings' `scored.Count > 0`; ordering is time desc, then episodes desc, then chronological.

**Poster rule:** the leading row shows posters, same as the score rankings, but picked by episodes watched (then title) rather than by score — a time ranking illustrated by anime I rated highly but barely watched would misrepresent the row.

### D8. Eligibility reuses the score rankings' gate verbatim

`RecapService.BuildRankings` already computes `rankingEligible = effectiveFilter == Aired && mode != Season`; the time rankings hang off the same boolean, with the year-level one additionally gated to `MultiYear`, matching the score year ranking. The one difference: the time rankings do **not** need `ScoredMean(wholeList)`, so they must be built outside the current `if (!rankingEligible || ScoredMean(...) is not { } globalMean) return ([], [])` early return — otherwise a user who has scored nothing at all loses their time rankings too. Restructure so the global-mean guard covers only the score rankings.

### D9. Hot takes: three → five is a constant, but the layout is not

`RecapStatsBuilder.HotTakeCount` 3 → 5. The hot-take list is currently styled for three rows; five in the same treatment is fine vertically, and the section now sits last on the page where extra height costs nothing.

### D10. Stat relabelling is display-only plus one new server field

"Anime" → "In this period" is a `RecapPage` label change. **Dropped** is new and needs `RecapStatsDto.Dropped` — `included.Count(e => e.Status == WatchStatus.Dropped)`, computed exactly like the existing `Completed`. Note that under **What I watched** the included set is completed+dropped by definition, so the three tiles will satisfy `InThisPeriod == Completed + Dropped` there; that identity is a feature (it explains the total), not a redundancy to hide.

## Risks / Trade-offs

- **Removing My list's navigate-to-recap breaks a habit** → The scope chip's recap link (D3) keeps the destination one click away, and the nav entry (D1) makes it reachable from anywhere. Net reachability improves.
- **`RecapTimeRankingDto` with a nullable `Season` is a mild union type** → Confined to one DTO with one meaning per level, and the alternative (two near-identical DTOs and two overlay shapes) costs more than it buys. The field is documented as null-at-year-level.
- **Time-watched and score rankings can disagree loudly** (the season I watched most is not the one I scored highest) → That divergence is the point of the feature; the two blocks are separately titled so the basis of each is never ambiguous.
- **Three rankings side by side plus a paired layout is a lot of horizontal structure** → Season and year pair in two columns; the time ranking is full-width below them rather than forced into a third column, which would squeeze all three below usable width on a laptop.
- **A group with watched episodes but no `AiredFrom` silently vanishes from the time ranking** → Already true of the score rankings and of the aired selection itself (an anime with no start date cannot be placed on the calendar); consistent with the existing spec rather than a new gap.
- **Recomputing five rankings per request** → All are single passes over an in-memory list already loaded for the recap; the whole-list fetch dominates, unchanged.

## Migration Plan

No data migration. `RecapDto` grows `Stats.Dropped`, `SeasonTimeRanking`, and `YearTimeRanking`; all three are additive, so a stale client ignores them and a new client against an old server would see `undefined` — not a state that occurs here, since both ship together. Rollback is a straight revert of the change.

## Open Questions

- Should the time-watched ranking's rows link anywhere? The score rankings link each row to that season's/year's own recap. Doing the same is the consistent choice and is what the tasks assume, but a row representing "where my hours went" may read as a stat rather than a destination.
