## Why

Six unrelated rough edges have accumulated across the browse, home, profile, and recap pages. The worst of them is a real regression in back/forward navigation: the Season and Year pages, alone among the browse pages, fetch their results from the server in 24-item pages as you scroll, and the background refresh that follows a restore re-reads only the *first* of those pages — so a grid you had scrolled deep into silently collapses back to 24 cards and the scroll position the app just restored is clamped away. The server also caps any one read at 100 items, so the same collapse happens without navigating at all once a grid passes 100 cards and its background refresh lands.

The fix is to stop paging these two pages from the server. A season holds at most ~350 anime and a year at most ~1,250, so the whole listing is one modest read; the page then reveals it as it is scrolled, the way Search and the Series browser already do. That removes the regression by construction rather than patching the paging, and it removes any ceiling on what the two pages will show. The rest of the change is smaller: a Safari repaint artefact on the home carousel, two presentation nits on the recap page, and the profile's favourites rankings offering only one way to read them.

## What Changes

- **Season and Year load their whole listing in one read and reveal it as you scroll.** Server-side paging (`offset`/`limit`) leaves both endpoints: each returns the whole listing for the selected season or year, and the page renders it a screen at a time behind a scroll sentinel, with how much has been revealed restored on back-navigation like every other view control. Nothing caps how many anime either page will show, a restored grid can no longer shrink under its own refresh, and the 100-item read cap stops truncating a deeply-scrolled grid.
- **Sorting, the type filter, and the in-my-list filter become instant.** With the whole listing loaded, all three act on it in place — no request, no loading state, no scroll reset beyond the fresh-view reset they already cause. Type and in-my-list become client-side predicates on fields the listing already carries; sorting re-orders by a per-item sort key the server computes, so the four ordering rules — including the my-score banding and the alphabetical collation — stay in the one place they live today rather than gaining a copy in the client. The type filter also stops narrowing its own options once a type is chosen, since the options now come off the whole listing.
- **Home carousel leaves no hover residue.** The card hover plate is drawn by a pseudo-element that overflows the card's own box; Safari's repaint rect excludes that overhang, leaving a purple line under a card the pointer has left until something else forces a repaint. The plate is given its own compositing layer so the whole treatment is invalidated with the card.
- **Favourite years and Favourite seasons gain score filters.** Under each ranking's title sits a row of controls: **All** (the default, exactly today's ranking) followed by **With most:** and one button per score present in that ranking, 10 down to 1. Choosing a score re-ranks that ranking by how many anime of that score each year/season holds, most first, and omits groups holding none. Each score button wears its score's tier colour — the same colours the recap page's rating distribution uses — on hover, on keyboard focus, and while selected. The selection is restored on back-navigation like the profile's other view controls.
- **Profile episode-progress numbers are larger.** The `watched / total` figure in the profile's Episode progress section is set at a size that matches its standing as a headline figure, rather than the 12px used on cards and list rows.
- **Recap top 6-10 rows breathe.** The score at each row's trailing edge is inset from the row's border rather than sitting hard against its padding edge.
- **Yearly recap groups its controls.** The **What I watched** / **What aired** filter moves out of the middle of the control row to sit beside the **Browse the year** button, so the period stepper leads the row and the two period-scoped controls travel together.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `season-browser`: the season's whole listing is loaded in one read and revealed progressively, with nothing capping how far it can be scrolled and the revealed count restored on back-navigation; sorting and the type and in-my-list filters act on that loaded listing instantly rather than through a read.
- `year-browser`: the same, stated at the year level.
- `main-dashboard`: a currently-watching card's hover treatment must clear completely when the pointer leaves, leaving no residue outside the card's own box.
- `profile-stats`: Favourite seasons and Favourite years gain an All/with-most-N score filter, tier-coloured and restored on back-navigation; the episode-progress figure gets its own larger size.
- `list-recaps`: the top 6-10 rows' trailing score is inset from the row border; on a yearly recap the time filter sits beside the year-page control.

## Impact

- **Frontend pages**: `SeasonPage`, `YearPage` (whole-listing read, client-side reveal, sort and filters in place), `ProfilePage` (+ CSS: score filters, episode-progress size), `RecapPage` (+ CSS: control grouping, row inset). Both browse pages shed machinery that only paging needed: the reload-on-filter-change effect and its first-run guard, the accumulated type-option workaround, the request-invalidation effect, and the load-more bookkeeping.
- **Frontend components**: `CurrentlyWatchingCarousel.css` (hover-plate repaint), `RankingSection.tsx`/`.css` (filter row), `RankingOverlay` (filtered rows and title).
- **Frontend API client**: `getSeasonPage`/`getYearPage` lose their `offset`/`limit`, `sort`, `includeMyList`, and `type` parameters — only `hideHentai` remains. `usePageData` and `useScrollRestoration` are untouched.
- **Backend**: `SeasonController`/`YearController` drop `offset`, `limit` (and the `Math.Clamp(limit, 1, 100)` cap), `sort`, `includeMyList`, and `type`; `ISeasonRepository`/`ISeasonBrowseService` drop the same arguments and the `Skip`/`Take`; `SeasonPageDto`/`YearPageDto` drop their `offset`/`limit` fields. The four `OrderBy` chains stay exactly as they are and are run as id-only projections to produce a per-item sort key. `AnimeBrowseItemDto` gains an optional `SortOrder`, populated only by these two reads and left null for search.
- **Backend**: `RecapSeasonRankingDto` and `RecapYearRankingDto` gain a per-score histogram — a figure `RecapRankingBuilder` already computes for its own tie-break and currently discards. `RecapService` and `ProfileService` are unchanged beyond passing it through.
- No new endpoint, no schema change, no migration.
