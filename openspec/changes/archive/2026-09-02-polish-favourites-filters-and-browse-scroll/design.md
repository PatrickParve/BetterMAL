## Context

Six independent items, four of them presentation-only. The one that carries real architecture is the Season/Year scroll regression, and it is worth stating precisely because the shape of the bug decides the shape of the fix.

`usePageData` (frontend/src/hooks/usePageData.ts) seeds a restored page from its history-entry snapshot with `loading: false`, then silently re-runs the page's loader in the background and replaces the data with whatever comes back. Season and Year are the only two pages whose loader fetches a *server-side page* of a longer list — `offset: 0, limit: 24` — while their infinite scroll appends further 24-item pages onto the same state as it is scrolled. So a restore of a grid holding, say, 96 cards renders all 96, `useScrollRestoration` reaches the recorded position on its first attempt (the page is tall enough), and ~200 ms later the background refresh replaces those 96 with the loader's first 24 — the page shortens and the browser clamps the scroll position back up. Search and the Series browser load their whole set in one read and reveal it through a restorable `visibleCount`, so neither is affected; this is why the report names exactly Season and Year.

There is a second, quieter instance of the same shrink, and it needs no navigation at all. `GET /api/season/{year}/{season}` and `GET /api/year/{year}` clamp `limit` to `Math.Clamp(limit, 1, 100)`. Both pages' post-MAL-refresh re-read already asks for `Math.max(itemsLength, 24)`, so a grid scrolled past 100 cards is cut back to 100 the moment its visit-triggered refresh lands, under the user's pointer. The season-browser spec already forbids this ("preserving my scroll position and the number of pages I had already loaded"); it is simply not honoured past 100.

Neither page needs server-side paging to begin with. The listings are small and local: measured against the current database, the largest single season holds 347 anime (fall 2023) and the largest year 1,230 (2023), across 11,760 cached listing rows in total. `AnimeBrowseItemDto` is ten scalar fields, so a whole year is a few hundred kilobytes of JSON off local Postgres — one modest read, not a stream. Paging these two pages buys nothing and is the sole source of both failure modes above.

The remaining five items: a Safari-only repaint artefact in the home carousel; two new controls' worth of behaviour on the profile page's favourites rankings; a type-size change; and two layout nits on the recap page. The favourites score filter is the only one needing data the API does not already return.

## Goals / Non-Goals

**Goals:**
- Nothing caps how many of a season's or a year's anime the browser will show: everything MAL lists for the selected period is reachable by scrolling.
- Sorting, the type filter, and the in-my-list filter apply instantly, with no read and no loading state, and produce exactly the results and ordering they produce today.
- A restored Season/Year grid keeps what it had revealed, so its restored scroll position survives the background refresh — by construction, because there is no longer a paged read that can return less than the page is showing.
- A refresh never shortens a loaded grid, at any depth, restore or not.
- The home carousel's hover treatment clears completely in Safari.
- Favourite years and Favourite seasons can be re-ranked by how many anime of a chosen score each group holds, in the score's own tier colour.
- The profile's episode-progress figure reads as a headline figure.
- The recap page's control row groups its period-scoped controls; its top 6-10 scores sit clear of the row border.

**Non-Goals:**
- No change to how the Bayesian ranking itself is computed, or to its tie-break sequence. The score filter re-orders an already-ranked list; it does not re-rank from scratch.
- No score filter on the recap page's own season/year rankings. The DTO gains the data both pages read, but only the profile page offers the control.
- No new endpoint, and no change to how the season and year listings are ordered, filtered, or counted — only to how much of the result one read returns.
- No move of the *ordering rules* to the client. Sort, type, and in-my-list all become instant (D3), but the four orderings stay where they are: the client re-sorts by a precomputed key, never by reimplementing the my-score banding or the alphabetical collation.
- No move of the hide-NSFW filter off the server. It reads MAL's `rating`, which the browse DTO deliberately does not carry.
- No general audit of Safari repaint artefacts elsewhere in the app — only the reported carousel case is in scope, though the same treatment is the answer wherever else it turns up.
- No change to the favourites rankings' rows, posters, five-row cap, or overlay under the default **All** selection.

## Decisions

### D1 — Season and Year stop paging on the server

`offset` and `limit` leave `GET /api/season/{year}/{season}` and `GET /api/year/{year}`, along with the `Math.Clamp(limit, 1, 100)` that caps them and the `Skip`/`Take` at the end of `SeasonRepository.GetPageAsync`. Each endpoint returns the season's or year's whole listing. `SeasonPageDto`/`YearPageDto` drop their now-meaningless `offset`/`limit` fields; `totalCount` stays as the count of that listing, with the page deriving its own count for whatever its filters leave.

This is the whole fix for both failure modes. There is no paged read left that can return less than the page is showing, so a restore's background refresh cannot shrink the grid, and a deep scroll cannot outrun a read cap. It also answers the ask directly: nothing caps what the two pages will show.

The query itself is untouched but for its last two lines and its filter arguments — the four `OrderBy` chains, the `hideHentai` predicate, and the projection all stay exactly as they are. What each read now returns is a listing rather than a page of one; D3 covers what happens to the sort and the two page filters, which no longer belong on the read at all.

*Alternative rejected:* keeping the paging and teaching the client to re-read its loaded depth (a loader argument carrying the restored data, plus a chunked read to work around the 100 cap). It fixes the reported bug, but it adds a mechanism — parallel chunked reads, a restore-versus-in-page distinction inside `usePageData` — to preserve paging that a 347-row season never needed. Deleting the paging is less code than repairing it.

### D2 — The client reveals the loaded set progressively, exactly as Search and the Series browser do

Loading everything is not rendering everything: 1,230 cards, each with a poster, is not a first paint worth having. Both pages keep their `IntersectionObserver` sentinel and their 24-at-a-time growth, but it now advances a client-side `visibleCount` over the already-loaded array instead of firing a request — the pattern `SearchPage` and `SeriesBrowserPage` already use (`useRestorableState('visibleCount', PAGE_SIZE)`).

Restoration then falls out of behaviour the app already has and the `page-state-restoration` capability already requires ("A page that renders only part of a loaded list, revealing more as it is scrolled, SHALL restore how much of that list had been revealed as well as the list itself — otherwise its recorded scroll position is unreachable on restore and is clamped back to the top"). `visibleCount` is a view control, so `useRestorableState` restores it on a back/forward and reseeds it to its default on a fresh visit — including the fresh visit a sort or filter change mints, where resetting to the first screen is what should happen and is what happens today.

No change to `usePageData` or `useScrollRestoration` is needed, which is the point: the two pages were the only ones not built to the model the rest of the app already follows.

*Note:* `Math.max(itemsLength, PAGE_SIZE)` disappears from both pages' post-refresh re-read — the re-read is simply the same whole-listing call as any other.

### D3 — Sort, type, and in-my-list act on the loaded listing; ordering stays server-side as precomputed keys

With the whole listing in hand, the three page controls have no reason to reach the server. `type` and `inMyList` become client-side predicates over fields the DTO already carries (`mediaType`, `inMyList`), so both toggle instantly. `hideHentai` stays server-side and keeps causing a read: it filters on MAL's `rating`, which the DTO deliberately does not carry, and it is a Settings toggle rather than a page control, so a read on change is the right cost.

Sorting is the interesting one. The four sorts are **not** four one-line comparators — the my-score sort splits scored from unscored, then orders the scored half by score, band (`anime-ranking`'s hand-ordered / short-form / dropped / uncovered banding), stored ranking position within the hand-ordered band, and a popularity fallback for the rest; the alphabetical sort orders by Postgres collation, which a JavaScript comparator would not reproduce for non-ASCII titles. That rule already exists in three forms in the backend (`AnimeRankingKey.Compare`, its `OrderByRanking` SQL twin, and `SeasonRepository`'s browse-specific inlining of it, which adds the unscored split and the band-3 popularity fallback), and the codebase carries a parity test and explicit "these must move together" comments because of it. A fourth copy in TypeScript is not a trade worth making.

So the ordering never reaches the client as a *rule* — it reaches it as a *number*. Each listing read runs its four existing `OrderBy` chains as id-only projections, and every item carries `sortOrder: { popularity, malScore, alphabetical, myScore }` — its index under each. The client sorts by `a.sortOrder[sort] - b.sortOrder[sort]`, an integer compare that cannot disagree with the server. Filtering does not disturb it: these are indices into a total order over the listing, so removing items preserves their relative order.

`AnimeBrowseItemDto` is shared with search, so `SortOrder` is an optional trailing field, populated only by the season and year listing reads and left null everywhere else.

*Alternatives rejected:* re-expressing the four orderings in C# over the materialised listing (one query instead of five, but it moves the alphabetical sort off Postgres collation, silently reordering non-ASCII titles, and rewrites a block of heavily-reasoned SQL for no user-visible gain); and shipping the raw band inputs (`status`, `airingStatus`, `rankPosition`) for the client to sort on (cheaper on the wire, but it is exactly the fourth copy of the ranking rule this decision exists to avoid).

*What this deletes from both pages:* the `reload()`-on-filter-change effect and its `isFirstFilterRun` guard, the `seenTypesRef` accumulation workaround (type options now come straight off the whole listing, so a selected type can no longer hide the others), the `start()` invalidation effect, and `useLatestRequest` if the refresh path turns out to be its only remaining user.

### D4 — Promote the carousel's scroll container, not every card

The hover treatment is `.anime-card::before`, a plate at `inset: -6px` with `z-index: -1` — deliberately outside the card's own border box so it never depends on the poster's colours. Safari computes the repaint rect for the state change from the card's border box, which excludes that 6px overhang, so on `mouseleave` the overhang keeps its last paint; the bottom edge of the plate is exactly the "purple line" left under the card, and it clears whenever something else repaints that region (scrolling the row, hovering another card) — matching the report's "it goes away at some point".

The fix promotes `.carousel__track` to its own compositing layer (`transform: translateZ(0)`), so the scrollport is invalidated as a unit and the overhang repaints with the card. It goes on the track and **not** on `.anime-card::before` deliberately: the same card renders in the Season and Year grids, where promoting the pseudo would mint a compositing layer per card — hundreds of them on a scrolled grid. One layer per carousel is free; the reported artefact only appears in the carousel, which is also the only horizontally scrolling container the card is used in.

*Fallback, if Safari still shows it:* `backface-visibility: hidden` on `.anime-card::before`, scoped to `.carousel__card`, so the per-card promotion stays out of the grids. This is a browser-behaviour fix and cannot be verified from the test suite — verification is a manual pass in Safari (tasks 3.2-3.3).

### D5 — The ranking DTOs carry the histogram the builder already computes

`RecapRankingBuilder` builds a per-score histogram for every ranked season and year today, uses it as its third tie-break step, and throws it away. Both ranking DTOs gain `ScoreCounts` — ten counts, ascending, score 1 through score 10 — populated from that same array (its index 0 is unused and is dropped on the way out). No new computation, no new query, and by construction the counts a row reports are the counts its own ranking was ordered by.

Ten integers per row is small: even a whole-list favourites read is a few dozen rows. Both DTOs are shared with the recap page, which will carry the field and ignore it; giving the profile page its own parallel DTO to avoid that would fork the shape the two pages are otherwise required to agree on.

*Serialization shape:* a flat `int[10]` ascending rather than `{score, count}` objects or a map keyed by score. It matches `ScoreDistributionDto`'s existing ascending-buckets convention, and the frontend reads it through one helper (`scoreCountAt(row, score)`) rather than open-coding the offset.

### D6 — The score filter re-orders a ranked list with a stable sort

The backend returns rows already in full rank order — weighted score at full precision, then scored count, then the histogram from 10 down, then newest first. A **stable** sort by "count of the selected score, descending" over that array therefore resolves every tie in exactly the backend's own order, for free. The spec's tie-break clause is satisfied without a line of comparison logic on the client, and there is no second copy of `CompareGroups` to drift from the original.

Groups holding none of the selected score are filtered out before the sort, so no offered button can produce an empty ranking. The buttons offered are derived the same way — a score is offered when any row in that ranking holds at least one of it.

### D7 — The control lives in `RankingSection`, its state in `ProfilePage`

`RankingSection` renders both favourites rankings and both of the recap page's, so the control row goes there behind one optional prop (`scoreFilter: { scores, selected, onSelect }`); passing nothing leaves the recap page's four rankings exactly as they are. The two profile rankings then get an identical control without ProfilePage writing the markup twice.

The selection itself is `ProfilePage` state, held in `useRestorableState` under two keys (`favouriteYearsScore`, `favouriteSeasonsScore`), which is how every other view control on that page is restored and gives the spec's restore-on-back and default-on-fresh-visit behaviour for free. ProfilePage owns the filtering and ordering (D6) and hands `RankingSection` the rows it should show — the same arrangement `onSeeAll` already uses for the overlay.

`describeYearRanking`/`describeSeasonRanking` take an optional selected score and, when given one, render the row's meta as `{count} × {score} · {scoredCount} scored` instead of `{scoredCount} scored · {weighted}`: under a selection the count is what the row was ranked on, so it is what the row should state. Both callers are the same two functions the overlay reads, so an overlay row and its inline row cannot disagree.

### D8 — Tier colour through the existing `--fam-*` aliasing, not a new palette

The app already has one mechanism for "draw this control in a colour other than the accent": a class that re-points `--fam-from/-to/-bg/-border` at a family's tokens (`.family--year`, `.family--mal`, …), which every segmented control then reads. Each score button gets a tier class doing exactly that from the tier tokens the recap distribution uses — `--tier-apex`, `--tier-red`, `--tier-blue`, `--medal-gold`, `--medal-silver`, `--medal-bronze` — mapped by the same `scoreTier()` the distribution calls, so "the colour the rating distribution gives that score" is guaranteed rather than eyeballed. Scores 6 and 5 therefore share silver and 4 through 1 share bronze, exactly as the distribution's own bars do.

**All** carries no tier class, so it inherits the `--fam-*` its section already sets — the year family in Favourite years, the season family in Favourite seasons.

The button treatment mirrors `.profile-media-tabs__tab` on the same page: neutral at rest, family-tinted on hover, family-tinted with a coloured label when selected, `outline` from `--fam-from` on focus, and no size change between states. The 10 button uses the apex hue flat rather than the sheen gradient the distribution's own 10 row wears — a gradient reads as noise at button size.

### D9 — Episode-progress size is a page-level override, not a `ProgressBar` prop

`ProgressBar` is used on the dashboard, my list, the detail page, and here; the profile's whole-list bar is its only headline instance. Rather than add a size prop to the shared component for one caller, the profile page wraps its bar in `.profile-page__episode-progress` and raises `.progress-bar__label`'s font size within it. Nothing else about the bar changes, and no other bar in the app can be affected.

### D10 — The recap control row grows a trailing group

`.recap-page__controls` is `justify-content: space-between` over its children. With three children (yearly: period stepper, time filter, year link) that puts the filter in the middle; with two (season, multi-year) it already reads correctly. Wrapping the time filter and the period-browsing link in one `.recap-page__controls-trailing` flex group makes the row two children in every mode, so yearly gains the intended grouping and the other two modes are pixel-identical. The group keeps `flex-wrap` so a narrow window still wraps rather than overflows.

### D11 — The top 6-10 score is inset by its own margin

`padding-inline-end` on `.recap-top-ten-row__score` rather than more padding on the row: the row's padding also positions its rank and poster, and the requirement is that only the score moves.

## Risks / Trade-offs

- **The Safari fix cannot be verified here** → It is a browser repaint behaviour, invisible to the test suite and to non-WebKit browsers. Tasks carry an explicit manual Safari check, and D4 names the fallback to try if the first treatment does not take.
- **Compositing the carousel track could change how its text rasterises in Safari** → One layer on a small strip, and the manual check covers the strip's appearance as well as the artefact. If it regresses, the fallback in D4 moves the promotion off the track entirely.
- **Every season/year read now returns the whole listing** → The first read of a year grows from 24 rows to at most ~1,250 (2023, the largest in the database today), a few hundred kilobytes of JSON from local Postgres. In exchange it is the *only* read the page makes: scrolling, sorting, the type filter, and the in-my-list filter now cost nothing, where a deep scroll through a year previously cost ~50 round trips and every sort or filter change cost one more. If a future year were to grow far past this, the answer is a virtualised grid, not a return to paging.
- **A listing read now runs five queries instead of one** → The items, plus four id-only ordered projections for the sort keys. Each is the same shape Postgres already plans today over at most ~1,250 local rows. Worth re-measuring on the largest year (task 12.3); if it ever mattered, the four could collapse into one query with window functions.
- **The four order keys add ~4 integers per item** → Perhaps 15 KB gzipped on the largest year, against a payload that already carries titles and poster URLs — and it buys the removal of an entire class of ordering drift.
- **A large year renders more DOM than before if the reveal step is ever removed** → The `visibleCount` reveal is what keeps the first paint to a screenful; it is load-bearing, not decorative, and the code should say so where it is defined.
- **`totalCount` now always equals `items.length`** → It stays in the DTO because it is still the honest count of what matched, and removing it would churn callers for nothing; the client should stop deriving "is there more to load" from it and read `visibleCount < items.length` instead.
- **Ten more integers per ranking row on every recap read** → Measured against a payload that already carries three poster records per row; the recap page ignores the field. The alternative was forking a DTO whose whole purpose is that the two pages agree.
- **Eleven buttons under a title in a half-width column** → The row wraps rather than overflowing, and only scores actually present are offered, so a modest list shows fewer than eleven.
- **A stable sort is load-bearing in D6** → `Array.prototype.sort` has been required to be stable since ES2019 and every target browser complies. There is no frontend test suite to pin it, so the reliance is stated in a comment at the sort itself — naming what it depends on and what breaks if it is ever replaced by a hand-rolled comparator.
