## Context

Fifteen-odd surfaces draw an anime's picture into a fixed box with `object-fit: cover`. That was correct while every picture was MAL's own portrait poster. It stopped being correct when `artwork-selection` let a my-list anime carry any picture MAL publishes for it — including 16:9 key visuals and square art — because the displayed picture is what *every* surface renders.

The app has already answered this question three times, and answered it the same way each time:

- `anime-detail` — "the picture SHALL be rendered at its own proportions… no part of it SHALL be cut off", with the column widening for landscape art.
- `series-page` — the timeline card and More tile keep their **picture area's height** and fit the picture whole inside it, and "the card carrying it MAY be wider than its portrait neighbours so that the fitted picture is shown at a useful size".
- `anime-updates` — the update card's picture is "drawn whole, at its own proportions… bounded in **both** directions, so that a portrait picture cannot make a card towering and a wide picture cannot leave the text beside it unreadably narrow".

All three read orientation off the loaded image via `useLandscapePicture`, because no DTO in the app carries picture dimensions and nothing wants to start fetching them. So this change is not inventing a treatment — it is taking the treatment the app already settled on and finishing the job in the rows, which were skipped.

The navbar half is smaller and unrelated except that both are "the chrome doesn't finish the thought": `navigation-and-search` requires every navbar control that leads to a page to mark itself while you are on that page, and the updates bell — which opens a panel rather than a page — was never covered, so it alone stays flat while its panel hangs open under it.

## Goals / Non-Goals

**Goals:**

- One rule, one implementation, for every fixed-height row and thumbnail slot that draws an anime picture: the slot's height is fixed by its row, the picture is never cropped, and wide artwork takes the width its own proportions give it at that height, bounded so it cannot swallow the row.
- Zero visual change for portrait artwork. An ordinary poster must render pixel-identically to today on every one of these surfaces, because that is what makes a fifteen-surface change safe to land.
- No list gets taller, no row changes height, no overlay whose height is derived from its row height changes size.
- The updates bell marks itself while its panel is on screen, using the navbar's existing active treatment rather than a new one.

**Non-Goals:**

- Poster grids and strips (season/year/search/browse cards, Top Anime showcase and card row, the Recap podium, the score board, the profile's three poster strips). Their design *is* the uniform tile; `profile-stats` requires the strips to keep a fixed tile size and `list-recaps` sizes score-board tiles against source resolution. Excluded on purpose, and stated as excluded in the spec so a later change has to argue its way in rather than drift in.
- Any backend, DTO, or storage change. Orientation is not fetched, not stored, and not persisted.
- Changing which picture is displayed. `artwork-selection` owns that; this change only draws it.
- The unseen-news dot's rules, which stay exactly as they are.
- `SeriesEntryRow.css`, whose row-picture rule is dead — the component is no longer rendered anywhere, only its label helpers are imported.

## Decisions

### D1 — One shared `RowPicture` component, not fifteen copies of the hook

Every one of these call sites already has the same shape: an `<img className="x__picture">` when there is a picture, a `<div className="x__picture x__picture--placeholder">` when there is not. A single component absorbs that shape along with the orientation detection and the shared class:

```tsx
<RowPicture src={item.pictureUrl} className="my-list-row__picture" />
```

It renders the image with the shared `row-picture` class plus the caller's own, attaches the orientation ref, and adds `row-picture--wide` once the artwork is known to be wide. With no `src` it renders the placeholder as `<caller's class> <caller's class>--placeholder`, which is the convention every call site already follows; the two that don't (`ranking-overlay__poster-placeholder`, and the two dropdowns that render no placeholder at all) are handled by an optional `placeholderClassName` and by callers keeping their existing `{src && …}` guard.

*Alternative rejected:* calling `useLandscapePicture` in each of the fifteen host components. It works, but it puts a `useState` into `MyListRow` — a component deliberately memoised so a filter keystroke doesn't redraw every row — and into `RecapPage`, `ProfilePage` and `SettingsPage`, which render many pictures from one component and would need a hook per picture, which hooks rules forbid. Pushing the state into a leaf component is the only shape that works uniformly, and it also means a picture's load re-renders one `<img>` rather than a row or a page.

### D2 — The slot is height-bound; only its width varies

The shared CSS is driven by two custom properties each call site sets to the box it has today:

```css
.row-picture {
  width: var(--row-picture-w);
  height: var(--row-picture-h);
  object-fit: cover;          /* portrait: exactly today's rendering */
}

.row-picture--wide {
  width: auto;                /* the artwork's own width at that height */
  max-width: calc(var(--row-picture-h) * 16 / 9);
  object-fit: contain;        /* guard for artwork wider than the cap */
}
```

Height never varies, so a row's height, a list's length, and the overlay heights derived from row heights (`RankingOverlay`'s "eight whole rows", the profile's `--activity-row-h`) are all arithmetically untouched. Width is the only free dimension, which is what "the text moves along" means in practice.

The cap is `16/9 × the slot height` rather than a flat pixel figure so it scales with each slot — 128px for a 72px my-list row, 100px for a 56px divergence row, 57px for the 32px settings-picker thumb — and so the single most common wide format, a 16:9 key visual, lands exactly on the cap and fills the slot's height with nothing left over. Artwork wider than 16:9 is reduced whole by `contain` rather than cropped, which is the same pathological-case guard `UpdateCard.css` and `SeriesExtraTile.css` already use.

*Alternative rejected:* dropping `cover` entirely and making every slot height-bound with `width: auto; min-width: <today's width>`. It needs no JavaScript at all and it fixes squares and near-squares for free. It also gives every poster narrower than its slot a small background bar down each side, and those bars differ row to row because posters differ in ratio — replacing a crop nobody notices with raggedness everybody does, on fifteen surfaces, for a case the user did not report. Rejected on blast radius: keeping portrait rendering byte-identical is what makes this change verifiable by looking at it.

*Alternative rejected:* a fixed-width slot with the wide picture letterboxed inside it, which preserves title alignment down the list. A 16:9 image in a 52px slot renders about 52×29 in a 72px row — technically uncropped, visually a strip — and it contradicts `anime-updates`' explicit "SHALL NOT be letterboxed inside a box of a different shape". Rejected, and the alignment cost of the chosen option is accepted below.

### D3 — Geometry moves out of the call-site classes

Each call site's own class stops declaring `width`, `height`, `object-fit`, `background` and `flex`, and declares `--row-picture-w` / `--row-picture-h` instead, keeping only what is genuinely its own (`border-radius`, and the placeholder's border).

This is not tidiness — it is the only way to avoid a specificity coin-flip. `.row-picture--wide` and `.my-list-row__picture` are both single-class selectors, so if both declared `width` the winner would be decided by CSS import order in the module graph, which no one should have to reason about. With the geometry declared in exactly one place, there is nothing to override.

The properties are read off the element itself, so a call site whose height is already a variable passes it straight through: `--row-picture-h: var(--activity-row-h)` for the profile feed, `var(--ranking-poster-h)` for the two ranking clusters, and the widths stay the same expressions they are today (`calc(var(--activity-row-h) * 41 / 56)` and so on).

### D4 — "Wide" means width ≥ height, and only in rows

`useLandscapePicture` today tests `naturalWidth > naturalHeight`. A square picture therefore reads as portrait and gets cropped to 72% of itself in a 52×72 slot — a real crop, and `artwork-selection` already says "a square picture SHALL be drawn square".

So the row rule tests `naturalWidth >= naturalHeight`. It ships as a second export sharing the existing hook's implementation rather than as a change to it, because the four surfaces already using the hook (detail page, series header, timeline cards, More tiles) are governed by specs that say "its intrinsic width greater than its intrinsic height", and quietly moving a square onto the landscape path there would be an unrequested change to three spec'd layouts.

A near-square *portrait* picture (say 9:10) still crops slightly, exactly as today. Fixing that needs the slot's own ratio in JavaScript, which is D2's rejected alternative wearing a different hat.

### D5 — Pre-load default is the portrait box

Orientation is unknown until the image decodes, so the slot renders at its portrait dimensions until then and adopts the wide treatment on load. This is the behaviour `anime-detail` and `series-page` already specify word for word ("the page SHALL render the existing portrait box until then"), and it means a list of portrait posters — the overwhelming common case — never shifts at all.

The existing hook already handles the case that actually bites here: a callback ref rather than an `onLoad` prop, because a cached image can finish decoding before React attaches a handler, which would leave a revisited page silently portrait. Reusing it means that fix is inherited rather than re-derived.

React bails out of a re-render when `setState` is passed the value the state already holds, so a portrait image's load — writing `false` over `false` — costs nothing. Only genuinely wide pictures re-render, and each re-renders one `<img>`.

### D6 — The bell is marked by its panel, not by a route

`navigation-and-search` marks a control while its *page* is being viewed; the bell has no page. It is marked while `open` — its dropdown — or while `historyOpen` — the full-history overlay reached from that dropdown — is true, so following "History" from the dropdown keeps the bell marked rather than dropping the mark at the moment the thing it opened becomes fullscreen.

The styling reuses `.navbar__link--active`'s declarations rather than inventing a variant: `var(--accent-bg)`, `border-color: var(--accent)`, `color: var(--text-h)`. It also needs the `--open:hover { border-color: var(--accent) }` companion rule for the same reason `Navbar.css` already documents twice: `.updates-menu__button:hover` is a two-class selector and `.updates-menu__button--open` is a one-class selector, so hover would otherwise win back the soft border and un-mark a hovered open bell.

The unseen dot and the mark are independent by construction — one is `unseen`, the other is `open || historyOpen` — so opening the panel clears the dot (as it already does) while the mark appears, and neither state is inferable from the other.

## Risks / Trade-offs

**Titles no longer align down a list that mixes orientations** → Accepted, explicitly, as the chosen trade. A wide row's picture is wider, so its title starts further right. The same trade was already made on the series page, where a landscape timeline card is wider than its portrait neighbours. It only appears at all for anime whose picture was deliberately changed to wide artwork, which is a small and self-selected set — and the 16:9 cap bounds how far any title can be pushed.

**Fifteen surfaces changed at once, most of them not directly reported** → Mitigated by D2's "portrait renders identically" property: every surface's default path keeps the exact box it has today, so the regression surface is the wide path alone, which does not exist today anywhere in a row. Each surface is one mechanical edit — swap the markup for `RowPicture`, move five declarations to two custom properties — and the tasks keep them individually checkable rather than as one sweep.

**A wide picture squeezing a narrow row's text** → The `max-width` cap is the mitigation, and it is deliberately expressed as `calc()` over a px-valued custom property, never a percentage: `UpdateCard.css` documents at length that a percentage `max-width` on an `<img>` used directly as a flex item is resolved against the image's intrinsic size in WebKit's flex-basis pass, which lets the picture render at native size and push the card wider than its container. A `calc()` of pixels has nothing to resolve against.

**An overlay sized from its rows changing height** → Cannot happen by construction: `--row-picture-h` is what those heights are derived from and it is exactly the value each call site holds today. Worth checking by eye on the two that derive geometry — `RankingOverlay` ("eight whole rows") and the profile's `--activity-row-h` feed ("whole rows only") — because both have spec'd height rules.

**`RowPicture`'s derived `--placeholder` class going wrong at a call site that doesn't follow the convention** → Two call sites don't (`ranking-overlay__poster-placeholder`; the search and settings dropdowns render no placeholder). Both are handled explicitly — an optional prop and an unchanged `{src && …}` guard — rather than by hoping the derivation matches.

## Migration Plan

Pure frontend, no data or API involvement, so there is nothing to migrate and nothing to roll forward. Reverting is reverting the commit.

Land order is shared-first so each surface's edit is trivial: the hook variant and `RowPicture` first, then the updates-menu mark (independent of everything else), then the call sites in the order the tasks list them, starting with the three the user actually reported so they can be looked at early.

## Open Questions

None. The two decisions that could have gone either way — widening the picture versus letterboxing it in place, and how far beyond the three reported surfaces to go — were put to the user and answered: widen, and cover every row and thumbnail while leaving the poster grids alone.
