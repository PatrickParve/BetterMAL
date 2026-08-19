## Why

A recap's stat block and rating distribution report numbers and stop there. "31 completed", "7 movies watched", "12 anime scored 8" each name a set of anime the app already knows exactly, yet the only way to see that set is to open my list and rebuild the narrowing by hand. Every tile also highlights on hover today, promising a click none of them answer.

Two layout faults sit alongside that. On a five-year multi-year recap the year rankings hold exactly five rows, so no "See all" control appears under them while the season rankings beside them do carry one — and the missing control pulls "Years by time watched" up out of line with "Seasons by time watched", breaking the two-column pairing the layout exists to create. In the hot-take list my score, MAL's score, and the direction label are laid out by content width, so each row places them differently and none of the three reads as a column.

And the navbar search field highlights on focus but not on hover, unlike every other control in the app.

## What Changes

- **Recap stats open my list.** The **In this period**, **Completed**, **Dropped**, **Movies watched**, and **Currently watching** tiles become clickable. Following one opens my list scoped to the recap's period, time filter, and media type, narrowed further to exactly the anime that tile counts — the 2020 movies I have watched, the 2020 anime I dropped, and so on. The narrowing arrives set on my list's *own* controls (status tab, type filter, score filter), so it is visible and adjustable rather than hidden behind the scope indicator. A tile whose own count is 0 keeps the same highlight but isn't a link — there's nothing for it to lead to.
- **Only clickable tiles read as clickable.** Mean score, Episodes watched, and Time spent describe no set of anime, so they stay inert; the accent hover treatment and the pointer cursor now mark the tiles that actually lead somewhere, and the inert tiles get a quieter hover instead of the same one.
- **Distribution rows open my list too.** Each row of the recap's rating distribution — its score, bar, count, and share together as one target — opens my list holding that period's anime that I gave that score. The row takes the same accent-bordered, elevated hover treatment the project's cards and rows already use, without shifting the bars' alignment. A row with nothing in it keeps that same highlight but, like a zero-count stat tile, isn't a link.
- **My list gains the two controls those drill-downs need.** Its score filter, today Any/Rated/Unrated, gains an option per score value (10 down to 1). A new **Started** filter narrows to entries I have watched at least one episode of, which is what "Movies watched" counts. Both are ordinary filter-bar controls, usable in their own right and cleared by "Clear filters".
- **The rankings line up across their two columns.** A recap's four rankings are laid out so the season-level and year-level time-watched rankings always begin on the same line, whether or not the ranking above either of them carries a "See all" control. A ranking that already shows every row still offers no redundant "See all".
- **Hot-take rows read as columns.** My score, MAL's score, and the "MAL liked it more"/"I liked it more" label each occupy a fixed column across the list, so scores line up under scores and the direction labels under each other.
- **The search field highlights on hover.** Hovering the navbar search field gives the same highlight focusing it does, matching how the rest of the app's controls behave.

## Capabilities

### New Capabilities

None — every change refines behaviour already covered by an existing spec.

### Modified Capabilities

- `list-recaps`: the stat block's set-describing tiles become links into my list and only those tiles read as clickable; the rating distribution's rows become links into my list for a single score; the two-column ranking layout gains a cross-column row alignment rule; hot-take rows gain fixed columns for the two scores and the direction label.
- `library-views`: the my-list score filter gains a per-score-value option; a new Started filter joins the filter bar and the "Clear filters" default set; arriving from a recap stat sets my list's own controls to the narrowing that stat describes.
- `navigation-and-search`: the navbar search field shows its highlight on hover as well as on focus.

## Impact

- **Frontend only** — no API shape change and no backend work. Every drill-down is expressible from data the client already holds: the recap scope's included set (`RecapDto.items`, already fetched by `MyListPage` for the scope) intersected with my list's own entries, which carry status, media type, score, and episodes watched.
- `RecapPage.tsx`/`.css` — stat tiles become links built from the existing `myListScopeSearch` helper plus a focus parameter; the tile list grows a clickable/inert distinction; `renderRankings` moves from two flex columns to a row-aligned grid; hot-take score and direction columns get fixed widths.
- `ScoreDistribution.tsx`/`.css` — gains an optional per-score link builder, used by the recap and left unset by the profile page, whose block is unchanged.
- `MyListPage.tsx`/`.css` — reads the focus parameter and seeds its status, type, score, and Started controls from it; the score filter and the new Started filter join `clearFilters` and the off-default check.
- **Currently watching is the one tile whose link changes the scope's time filter** — the stat is counted on air date under both filters (an unfinished entry has no completion date), so its link carries the aired-attributed scope even from a **What I watched** recap, and the scope indicator names what it actually applies.
- **Unaffected** — the recap API, `RecapStatsBuilder`, the profile page's own distribution and favourites, and the existing "See all N in my list" control, which keeps its current unfocused scope link.
