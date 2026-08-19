## Context

The recap page already knows how to hand a period to my list: `myListScopeSearch` in `RecapPage.tsx` writes a `recap`-prefixed vocabulary (`recapMode`, `recapFrom`/`recapTo`/`recapYear`, `recapSeason`, `recapFilter`, `recapType`) that `MyListPage` reads back, re-fetches the same recap for, and intersects by `animeId` against its own rows. That machinery carries the *period*; it has never carried a narrowing *within* a period.

The narrowings the stat tiles and distribution rows describe are all expressible from data `MyListPage` already holds per row — `entry.status`, `entry.myScore`, `entry.episodesWatched`, `mediaType` — so nothing here needs the API or `RecapStatsBuilder` to change. Two of them, though, have no control on the page today: no way to ask for a *single* score value (the score filter is Any/Rated/Unrated), and no way to ask for "I have watched at least one episode of this", which is what **Movies watched** counts.

`MyListPage`'s controls are `useRestorableState`, which seeds from `initial` on a fresh visit and from the history snapshot on a back/forward restore. A navigation from the recap page is a fresh visit, so a URL-derived `initial` is honoured without an effect and without stripping the parameter afterwards — and a later back-navigation still restores whatever the user changed the control to.

The two layout faults are pure CSS/markup: `.recap-page__rankings` stacks each column's two ranking sections in an independent flex column, so a missing "See all" in one column shortens it and lifts everything below; and hot-take rows size their score and direction cells by content.

## Goals / Non-Goals

**Goals:**

- A recap number and the my-list page it opens agree exactly: the row count on my list equals the number that was clicked.
- The narrowing is visible and adjustable on my list's own controls, per the chosen approach — not a second hidden scope.
- The two ranking columns' time-watched rankings begin on the same line regardless of overflow controls, without inventing a redundant "See all".
- Hot-take rows read as three aligned columns.
- Only tiles that lead somewhere read as clickable.

**Non-Goals:**

- No backend, DTO, or endpoint change; `RecapStatsBuilder` and every recap DTO are untouched.
- No drill-down for Mean score, Episodes watched, or Time spent — they describe an aggregate, not a set of anime.
- No change to the profile page's distribution or favourites, and none to the existing "See all N in my list" control.
- No general-purpose progress/percentage filter on my list; the Started filter is a boolean.

## Decisions

### 1. One `focus` parameter, alongside the existing scope vocabulary

The link a stat tile builds is the existing scope search string plus one extra parameter: `focus=<token>`, with tokens `completed`, `dropped`, `watching`, `movies`, and `score-1` … `score-10`. **In this period** adds no token, since the plain scope already is that set.

Alternatives considered: one parameter per control (`focusStatus`, `focusType`, `focusScore`, `focusStarted`), which pushes the mapping into the URL and makes an inconsistent combination expressible; or reusing the `recap`-prefixed names, which would imply the narrowing is part of the scope rather than a seed for the page's controls. A single opaque token keeps the URL short, keeps the mapping in one place on each side, and makes an unknown value trivially ignorable.

`focus` is a *seed*, not a scope: `MyListPage` translates it into control values once and never re-reads it, and the page never writes it. `dismissRecapScope` drops it along with the `recap` keys, so dismissing a scope cannot leave a stale focus behind to reapply.

### 2. The focus seeds `useRestorableState` initials rather than running an effect

Each affected control's `initial` argument becomes a function of the parsed focus (`statusFilter`, `typeFilter`, `scoreFilter`, and the new `startedFilter`); every other control keeps its literal default. This needs no effect, no `replace: true` URL rewrite, and no "have I applied this yet" flag, and it composes correctly with restore: on back/forward the snapshot wins, so a control the user changed after arriving stays changed.

Alternative considered: an arrival effect that sets the controls and strips the parameter. Rejected — it fights the restore path (the strip is itself a navigation) and introduces a render where the list is briefly unfocused.

### 3. Mapping from token to controls

| Token | Status tab | Type filter | Score filter | Started |
|---|---|---|---|---|
| *(none)* | All | — | Any | off |
| `completed` | Completed | — | Any | off |
| `dropped` | Dropped | — | Any | off |
| `watching` | Watching | — | Any | off |
| `movies` | All | `movie` | Any | **on** |
| `score-N` | All | — | `N` | off |

`movies` needs the Started flag because the stat counts movies with at least one episode watched, status-agnostic: under **What aired** the included set holds plan-to-watch films with no progress, and Type=Movie alone would over-count them.

`score-N` deliberately leaves the status tab at All: the distribution counts every included entry carrying that score, whatever its status.

### 4. Currently watching links to the aired-attributed scope

**Currently watching** is the one stat computed outside the selected filter — always on the anime's air-start date, under both filters (an in-progress entry has no completion date). Its link therefore sets `recapFilter=aired` even when the recap is showing **What I watched**, so the scope `MyListPage` re-fetches is the set the tile was counted over. The scope indicator then reads "What aired", which is honest about the set being shown; nothing else on the page has to know about the exception.

Alternative considered: keeping the recap's own filter in the link and accepting a mismatch. Rejected — the tile's number and the list's row count would disagree, which is the one thing this change exists to prevent.

### 5. Two new control values on my list, both first-class

The score filter's type widens from `'any' | 'rated' | 'unrated'` to that union plus `` `score-${number}` ``-style values (stored as the plain number string), and the select grows ten options between Rated and Unrated. The Started filter is a toggle button in the filter cluster, in the same `my-list-page__tab` style as "Group by status"/"Reverse".

Both join `clearFilters` and `isOffDefault`, so "Clear filters" appears when either is set and restores them. They are ordinary controls, independent of whether a recap scope or a focus is present.

### 6. The distribution row becomes a link via an optional prop

`ScoreDistribution` gains an optional `hrefForScore?: (score: number) => string`. When present, each row renders as a `<Link>` wrapping the existing four cells; when absent (the profile page), the markup is exactly what it is today. This keeps one component and avoids a recap-only copy of the block.

The row's resting state gains the padding and a transparent 1px border that its hover state needs, so hover adds a colour rather than a box, and no bar track changes width between states. "Pop" is delivered by the shared `var(--shadow)` elevation plus `var(--accent-bg)`/`var(--accent-border)` — the same treatment ranking rows, top-10 rows, and hot-take rows already use — not by a transform, which would visibly shift a row of a block whose whole point is that its columns line up.

An empty score (a bucket with count 0) has nothing to lead to, so it is not a link — following it could only ever land on an empty list, which reads as broken rather than useful. It still renders with the same accent-highlight box as the rows around it (the row's *shape* — "this is one of the block's rows" — doesn't depend on whether it happens to be empty this period), just as a plain `<div>` rather than a `<Link>`: no `cursor: pointer`, no navigation, no keyboard stop. *(Reversed from this change's original decision, which made an empty row followable to an empty-result list — revised after review: a highlighted-but-inert row communicates "nothing here" more directly than a click that goes nowhere useful.)*

### 7. Clickable, highlighted-but-inert, and aggregate tiles are different renderings

A tile takes one of three renderings, sharing one CSS class (`recap-page__stat--link`) between the first two so the *box* — the accent hover, padding, border, size — is identical:

- **Followable with results** — a stat that describes a set of anime and whose count is non-zero. Renders as `<Link>`: accent hover/focus highlight, `cursor: pointer`, keyboard-reachable, navigates into my list.
- **Followable but empty** — a stat that describes a set of anime whose count is currently 0 (e.g. no dropped anime this period). Renders as a plain `<div>` carrying the *same* `recap-page__stat--link` class, so it takes the same accent highlight on hover — but no `cursor: pointer`, no navigation, not a keyboard stop, since there is nowhere to go. The highlight says "this is a followable *kind* of tile," not "this specific tile has a target right now."
- **Aggregate** — Mean score, Episodes watched, Time spent — never describes a set of anime regardless of value. Stays the plain `<div>` with the quieter background-only hover, unrelated to whether its own value is 0.

The tile's box (padding, border, size) is identical across all three, so the grid does not reflow as the pointer crosses between them.

### 8. Rankings become a row-aligned grid instead of two flex columns

`.recap-page__rankings` becomes a two-column, two-row grid whose four ranking sections are direct children with explicit `grid-column`/`grid-row` placement: season score at (1,1), seasons-by-time at (1,2), year score at (2,1), years-by-time at (2,2). Grid rows size to their tallest item, so the two time-watched rankings share a start line whether or not either score ranking above them carries a "See all" control.

The intermediate `.recap-page__ranking-column` wrappers are removed for the multi-year layout, since a wrapper would reintroduce the independent stacking this fixes. Where one level's rankings are entirely absent, the surviving sections are placed in column 1 and the grid collapses to a single column, preserving today's behaviour. The yearly mode's side-by-side pair is already a single row and is unchanged.

Alternative considered: `grid-template-rows: subgrid` on the existing column wrappers — equivalent result, but it keeps a wrapper whose only job is to be transparent to the grid, and it depends on subgrid support for a layout that plain placement expresses directly. Also considered and rejected per the answered question: always rendering a "See all" control so both columns end alike — redundant for a ranking already showing every row.

### 9. Hot-take rows get fixed columns

The two score cells and the direction label take fixed widths (`ch`-based, so they scale with the fluid root font) with tabular numerals on the scores, and the direction pill takes a fixed min-width sized for the longer of its two labels, centred. The title cell keeps `flex: 1 1 auto`, absorbing the slack, so the three trailing columns line up down the list regardless of title length or whether a MAL score is hidden.

The two score cells sit inside their own nested flex row (`.recap-hot-take__scores`), separate from the direction pill beside them. That row needs its own `align-items: center`: MAL's score renders through `ScoreValue`, which is itself an `inline-flex` that self-centers its content, while my score is a plain text node with nothing centering it. Left at the flex default (`stretch`), the two cells stretched to the row's full height but only one of them recentered itself inside that extra height — my score sat visibly higher than MAL's and the direction pill beside it. Centring the row's own children fixes that without touching the individual cells' fixed widths.

### 10. Search-field hover reuses the focus rule

`.search-bar:hover` gets the same accent border and inset ring `.search-bar:focus-within` already draws, as one shared rule. The ring is inset, so hovering cannot reach the magnifier or grow the field, and a focused-and-hovered field looks the same as a focused one rather than compounding.

## Risks / Trade-offs

- **A drill-down's count could still disagree with its tile** if a rule is mistranslated (for example forgetting Started on `movies`, or forgetting the aired basis on `watching`) → each mapping is pinned by a spec scenario naming the exact set, and the mapping lives in one table in one place on each side.
- **My list's controls arriving pre-set may look like the page "remembered" something** → the recap scope indicator is present in the same arrival, naming the period; the controls it sets are the visible ones the user can immediately clear, which is the behaviour chosen over a hidden second scope.
- **A focus parameter left in the URL after the user changes a control** is inert by construction (read only at mount / history-entry change), but a *new* history entry on the same URL — pushed by dismissing the scope — re-seeds from it → `dismissRecapScope` deletes `focus` along with the `recap` keys, so that path resets to plain defaults.
- **Making distribution rows links adds up to ten focusable stops** to the recap's tab order (fewer when some score buckets are empty, since those render as unlinked `<div>`s) → they are ordered top to bottom within a labelled section, matching the ranking lists that already contribute the same way.
- **Widening the ranking layout to a flat grid could regress the single-column collapse** → the collapse conditions are unchanged in kind (only one level has rankings; narrow viewport) and are covered by existing spec scenarios that still apply.
