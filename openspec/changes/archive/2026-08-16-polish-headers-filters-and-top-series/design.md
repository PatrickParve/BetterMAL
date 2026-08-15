## Context

The frontend is plain CSS per page/component (no Tailwind/CSS-in-JS), with shared tokens in `index.css` and a global `h1` rule sized via `clamp()`. Five of seven top-level pages (My List, Top Anime, Season, Schedule, Search) already wrap their title in a page-specific `__header` container that zeroes the `h1`'s own margin and lets the wrapper's own layout control spacing; Settings and Profile do not, so their `h1` still carries the raw global margin stacked on top of their outer container's flex `gap`.

Search and Season already share one filter pattern via `MyListPage`: a `FilterMultiSelect` popover component plus `MEDIA_TYPE_ORDER`/`mediaTypeLabel` from `utils/anime.ts`. Search loads its full candidate result set (≤90 items) up front and reveals it in client-side chunks; Season paginates 24 items per page server-side via `getSeasonPage`/`/api/season/{year}/{season}` with `includeMyList`/sort already threaded through as query params.

The anime detail page's MAL/AniList/SeriesGraph link buttons and the genres line are both children of the same CSS Grid row in `.anime-detail-page__info-grid` (`AnimeDetailPage.css`). Grid's default `align-items: stretch` makes the links `<div>` stretch to match whatever height the genres `<dd>` needs, and the links' own `flex` container then stretches its `<a>` children to fill that height — a double-stretch that inflates the buttons whenever genres wrap to two lines.

The Top Anime page renders all 500 ranked entries as visually identical `<li class="top-anime-row">` rows; only `:hover`/`:focus-within` changes anything today.

## Goals / Non-Goals

**Goals:**
- Make the detail page's three link buttons a fixed, content-sized shape regardless of genre line count, vertically centered against the genre block.
- Give Search and Season a Type filter using the same interaction pattern users already know from My List.
- Give every one of the seven pages' titles deliberate spacing and a page-appropriate visual treatment, removing the raw double-margin on Settings/Profile.
- Make Top Anime's rank 1–3 rows read as a podium, distinct from the flat list below them.

**Non-Goals:**
- Not reworking the anime detail page's overall layout, info-grid structure, or other fields.
- Not adding a type filter to My List (already has one) or to Top Anime (a single global ranking, not a browse/filter surface).
- Not changing any sort, scoring, or ranking logic.
- Not converting Top Anime's row list into a card grid, or changing its pagination.
- Not introducing a shared header *component* that forces identical markup across pages — divergent per-page treatment is intentional per the proposal.

## Decisions

**1. Detail-page link buttons: fix via `align-self`, not grid restructuring.**
Set `align-self: center` (overriding the grid default `stretch`) on `.anime-detail-page__links`. This removes the grid-driven height stretch so the flex row — and the buttons inside it — takes its own natural, content-sized height, and centers that row within the taller genres row whenever genres wrap to two lines. Considered pulling the genres `dd` onto its own full-width grid row (`grid-column: 1 / -1`) to decouple it from the links entirely; rejected as a larger, riskier restructuring of a 10-cell grid for no behavioral gain over the one-line `align-self` fix, and it would change where the links row sits relative to the other info fields.

**2. Type filter: reuse `FilterMultiSelect` + `MEDIA_TYPE_ORDER`; filter Search client-side, Season server-side.**
Both pages get a `FilterMultiSelect label="Type"` control identical in spirit to My List's, built from `MEDIA_TYPE_ORDER`/`mediaTypeLabel`, with options limited to types actually present in the loaded set (same "never offer a filter that yields zero results" rule My List already follows).
- **Search**: filters client-side over the already-fully-loaded candidate array (`items`/`visibleItems`). No backend change — the whole candidate set (≤90 items) is already in memory, and the existing server-side sort order is preserved by the filter (it only hides rows, doesn't reorder).
- **Season**: filters server-side. Season's server-side pagination (24/page + infinite scroll) means a client-only filter would under-report totals and could hide not-yet-loaded matches, mirroring the same reasoning already applied to the existing `includeMyList` filter. Add an optional `type` query param to `getSeasonPage` and `/api/season/{year}/{season}`, defaulted to "no filter" so omitting it reproduces today's behavior exactly.

**3. Page titles: keep the existing per-page `__header` convention, extend it to all seven pages, allow divergent per-page accents.**
Rather than a single shared title component, standardize the *structural* rule already used by 5 of 7 pages (header wrapper zeroes the `h1`'s own margin; the wrapper's own layout/gap owns spacing) and apply it to Settings and Profile too, giving each a real header block instead of a bare `h1`. On top of that shared structural baseline, each page gets a treatment suited to its header's content density:
- Pages whose header row already carries controls (My List, Top Anime, Season, Schedule, Search): tune the title's own type size/weight/letter-spacing down from the raw global `clamp()` so it reads as a page label sharing a row with controls, rather than a headline crammed against a dropdown or pagination widget.
- Pages with no competing header-row content (Settings, Profile): give the title more presence as a standalone header block (e.g. an icon or short subtitle), since nothing else shares that row.
This keeps one consistent underlying spacing rule (no more raw-margin collisions) while leaving room for the "decide what's best per page" latitude the proposal calls for.

**4. Top 3: rank-specific modifier classes on the existing row markup, not a new card layout.**
Add a rank-based modifier (`top-anime-row--rank-1/2/3`, applied when `item.rank <= 3`) to the existing `<li class="top-anime-row">` markup and style it in `TopAnimePage.css`: a taller row, a larger poster, and a distinct rank badge/accent color per position (gold/silver/bronze family) replacing the plain `#1`/`#2`/`#3` text. This is a new, dedicated color role — not a reuse of the MAL/mine score roles from the `score-presentation` capability, since it marks ranking position, not a score. Keeping the `<li>`/row structure (rather than switching to `AnimeCard`) avoids a second layout system on this page and keeps rows 4+ visually related to rows 1–3 as one continuous list.

## Risks / Trade-offs

- [Risk] If the three link buttons themselves wrap onto two lines at narrow widths, independent of genre wrapping → centering still holds: `align-self: center` centers the links block against whatever height the row ends up needing, regardless of which sibling caused it.
- [Risk] Season's new `type` query param ships a backend change → Mitigation: param is optional and defaults to unfiltered, so it's non-breaking regardless of frontend/backend deploy order.
- [Risk] Taller rank 1–3 rows push rows 4+ further down the first page → acceptable; it's the intended effect of a podium treatment at the top of the ranking.
- [Risk] Diverging title treatments per page could read as inconsistent rather than intentional → Mitigation: keep the structural spacing rule identical everywhere (header wrapper owns spacing, `h1` margin zeroed) so only the accent/emphasis differs, not the underlying rhythm.

## Open Questions

- Exact per-page title accent (color rule, icon vs. subtitle, sizing) is left to implementation-time judgment, consistent with the proposal's "decide what's best" scope.
- Whether Top 3 badges use medal glyphs/icons or color-only coding is an implementation detail to settle while building `TopAnimePage.css`.
