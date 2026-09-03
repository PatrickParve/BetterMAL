## Context

Four unrelated pieces of polish, joined only by the fact that each is a change to one shared thing rather than to four pages.

**The filter control.** `frontend/src/components/FilterMultiSelect.tsx` is the whole of it — one checkbox popover used five times (Search page Type, Season page Type, Year page Type, My List Type, My List Airing). Its prop is `selected: string[]`, and every consumer applies the same predicate: `if (selected.length > 0 && !selected.includes(value)) return false`. That single convention — *empty means unfiltered* — is the bug. The panel's **All** writes `options.map(o => o.value)` and **None** writes `[]`; both leave nothing filtered out, so the two shortcuts produce the same view. It also makes the label lie in the other direction: after **All** the trigger reads "Type: 5 selected", because `summary()` reaches its count branch whenever more than one value is held. And because the trigger's text is its own width, the button changes size on every selection change, shifting the sort select and the "In my list" checkbox beside it.

Where that state lives differs by page. Search, Season and Year keep it in the URL (`?type=tv,movie`), read as `typeParam ? typeParam.split(',').filter(Boolean) : []`. My List keeps it in the page snapshot via `useRestorableState<string[]>('typeFilter', [])`, seeded from a `FocusSeed` when the page is opened scoped to a recap period.

**The type-ahead.** `useAnimeSearch` fires on a 250 ms debounce and calls `setOpen(true)` in the promise's `.then`. `SearchBar.submitSearch` calls `setOpen(false)` and navigates. A request already in flight when Enter is pressed resolves *after* that, sets `open` back to true, and drops the dropdown over the results page. The field also keeps focus, so the suggestions sit over the answer the user just asked for. `SettingsPage`'s anime-refresh picker shares the hook and the same `setOpen` surface.

**The score board.** `RecapPage` holds `const [boardOpen, setBoardOpen] = useState(false)` — deliberately page-local, per the note in that file, so the board minted no history entry. The board's posters are `Link`s, so the ordinary use is: open the board, scroll a long way down it, follow a poster, come back — and find the board gone and the scroll lost. The recap page already participates in restoration for everything else (`usePageData` seeds its data synchronously; `useScrollRestoration` returns the page to its position), so the board is the one piece of that page's state that does not come back.

**The scrollbars.** `.scroll-y` in `index.css` does the opposite of what these lists now want: `scrollbar-gutter: stable` plus a `::-webkit-scrollbar { width: 8px }` rule exists specifically to force WebKit out of overlay mode so the gutter is filled by a visible bar. Three of the four lists in scope use it (`.activity-feed`, `.divergence-list`, `.edit-history__list`); the fourth, `.ranking-overlay__list`, rolls its own `overflow-y: auto`. The app already has the opposite pattern in four places (`.carousel__track`, `.series-timeline__scroll`, `.updates-menu__list`, `.updates-history__list`, the profile's poster strips): `scrollbar-width: none` plus `::-webkit-scrollbar { display: none }`, each written out locally, and `UpdatesMenu.css` records why — opting out of `.scroll-y` rather than fighting its specificity.

## Goals / Non-Goals

**Goals:**
- **All** and **None** name two different views, and the trigger says which one is in force.
- The trigger button's width is a property of its options, not of the current selection, so a filter cluster never reflows while it is used.
- One implementation of all of that, in `FilterMultiSelect`, with the five call sites differing only in where they keep the value.
- A submitted search leaves no dropdown behind it and no focus in the field, whatever a request in flight does afterwards.
- The score board comes back — open, and scrolled where it was — when the recap page is returned to, without becoming an overlay that can hang over a page it does not belong to.
- The lists named in the proposal scroll exactly as they do now and draw no bar, through one shared utility rather than a fifth and sixth copy of the two-property idiom.

**Non-Goals:**
- No change to which options a filter offers, how they are labelled, or how the predicate treats a *selected* value. Only the meaning of "nothing selected" and the trigger's own presentation change.
- No change to the score board's layout, its slots, its tier colours, its hover card, or its dismissals. It is restored, not redesigned.
- No new history entry for the board, and no `?board=` parameter. Opening it still does not push.
- No change to the `.scroll-y` utility's behaviour for its remaining users, and no sweep of every scrollable box in the app — only the ones named.
- No change to the season/year/search pages' empty-state *machinery*; the new "you have selected nothing" case reuses the filtered-out state each already has.
- No backend, API or DTO change of any kind.

## Decisions

### D1 — `null` is All, `[]` is None

`FilterMultiSelect`'s `selected` prop becomes `string[] | null`. `null` means no restriction; an array means exactly those values pass, and the empty array therefore means nothing passes. Every consumer's predicate becomes `if (selected !== null && !selected.includes(value)) return false` — one character of structure more than today, and it no longer conflates two states.

This is the smallest change that makes the two shortcuts distinguishable, because the thing that has to become expressible is "the user chose to select nothing", which an empty list of selections cannot say on its own. The alternative shapes were worse:

- *A sentinel value in the array* (`['__none__']`) — every consumer's `includes` check keeps working by accident, but the sentinel leaks into the URL, into the snapshot, and into any future option list that might legitimately contain that value.
- *Inverting to a list of excluded values* — All becomes `[]` and None becomes "every option", which fixes the semantics but makes the URL depend on the option set, so a link stops meaning the same thing once the underlying data changes which types exist.
- *A separate `mode: 'all' | 'some' | 'none'` prop* — three states expressed in two variables, with four consumers each responsible for keeping them consistent.

**In the URL** (Search, Season, Year) the distinction is already available and currently thrown away: `searchParams.get('type')` returns `null` when the parameter is absent and `''` when it is present and empty. So the parse becomes `typeParam === null ? null : typeParam.split(',').filter(Boolean)`, giving three URL states — absent for All, `?type=` for None, `?type=tv,movie` for a selection. No sentinel, no new parameter, and every link that exists today still means what it meant, since it either omits `type` or names values.

**In My List** the state becomes `useRestorableState<string[] | null>('typeFilter', null)` and the same for `airingFilter`. `FocusSeed`'s `typeFilter: []` becomes `null` (its "no narrowing" seed), while the movie-scoped seed keeps `['movie']`. The two derived flags that ask "is anything narrowing the list" — the filter-active test behind the count line and `airingBadgeActive` — change from `.length > 0` to `!== null`, which is what they always meant.

### D2 — The control normalises "every option is ticked" to All

`onChange` emits `null` whenever the resulting selection would cover every option currently offered, so All has one representation regardless of how it was reached: the shortcut, a fresh visit, or ticking the last unticked box by hand. Ticking a box while All is in force starts from the full option list and removes that one, which is what unticking a box under "everything is showing" visibly means.

This is what makes the label honest without a special case in `summary()`: the count branch can only be reached by a selection that is genuinely partial. It also keeps the URL short — the common case writes no parameter rather than every value — and keeps two states that render identically from being separately bookmarkable.

The option set is derived per page from the loaded data (`presentTypes` and friends), so "every option" is evaluated against what the control is offering at that moment. This is sound here because every one of these pages loads its whole candidate set before filtering it — the season and year browsers read the whole listing, the search page loads its whole candidate array, My List holds the whole list — so the option set does not grow underneath a selection.

### D3 — The trigger reserves the width of its widest possible label

The trigger renders the live summary plus every other summary it could show for its own options, all in one CSS grid cell (`grid-template-areas: 'summary'`, every child in it), with the ones that are not current marked `visibility: hidden` and `aria-hidden`. The cell is as wide as its widest child, so the button's width is fixed by the option set and changes only when the option set changes.

The candidate set is small and enumerable: `All`, `None`, each option's own label, and `N selected` for each N from 2 to one below the option count. A five-option Type filter reserves eleven strings, of which one is visible.

*Rejected:* a `min-width` in pixels. The app's root font size is fluid (`page-header-design` "Height holds as the font scales" already turns on this), and these labels are data — "Currently airing" and "TV special" are longer than any round number a stylesheet would pick, and a wrong guess either clips the label or leaves the control permanently too wide. Reserving the real strings is self-maintaining: a new media type widens the control by exactly what it needs.

*Rejected:* `ch`-based sizing computed from the longest label's character count. Proportional fonts make `ch` a poor proxy, and it re-derives in JS what the layout engine already measures for free.

### D4 — The shortcuts become buttons of the app's small-control family

`.filter-multi-select__shortcut` picks up the border, radius, background and hover treatment the app's other small buttons carry (`.recap-page__board-button` is the closest existing example: 1px `--border`, rounded, `--bg`, hovering to `--accent-bg`/`--accent-border`), replacing `border: none; background: none` plus a hover underline. Both shortcuts sit on one row and share a width, so the pair reads as a segmented pair rather than two links of different lengths; the divider under them stays.

The **All** button is the one that shows the current state most of the time, so neither carries a persistent "selected" treatment — the trigger's own label is where the state is read, and giving the shortcuts a second, competing indication of the same fact is what made the count-vs-All mismatch confusing in the first place.

### D5 — Dismissal that a resolved request cannot undo

`useAnimeSearch` stops exposing `setOpen` and exposes `dismiss()` and `reopen()` instead. `dismiss()` records the query it dismissed in a ref; the fetch's `.then` opens the dropdown only when the query it resolved for is not the dismissed one. Typing anything changes the debounced query, so the next result set opens normally — dismissal is scoped to the exact query that was dismissed, not sticky.

That one rule covers all three dismissals a caller has (submit, Escape, click-outside) and closes the race for each of them, rather than only for submit. `reopen()` keeps the focus behaviour (`onFocus` reopens when results are already in hand) available to both callers. `SettingsPage`'s picker moves to the same two functions; its behaviour is unchanged, and it gets the same race fixed for free.

*Rejected:* leaving `setOpen` and having `SearchBar` guard the reopen itself. The race lives in the hook — the `.then` that opens is the hook's — and two callers would each need their own copy of the guard.

Blur is separate and belongs to `SearchBar`: `submitSearch` calls `blur()` on the input it owns via a ref, after `dismiss()`. Blurring does not clear the field, so "Search submission keeps the query text" is untouched.

### D6 — The board's open state is restorable page state; its scroll offset uses the snapshot's scroll map

`boardOpen` becomes `useRestorableState<boolean>('scoreBoard', false)`, so it is written into the history entry's `view` map like every other view control on the page and seeded back on a restore. That is the entire mechanism for "the board is open again": no URL parameter, no history entry, and no change to how the board is opened or closed.

For the offset, the snapshot already carries a per-entry map of scroll positions for in-page scroll containers — `PageSnapshot.strips`, written by `putStripScroll` and read by `ProfilePage`'s `useStripScroll`. It is a `Map<string, number>` of "this named scroller was at this offset"; nothing about it is horizontal except its name and its one current caller. It is renamed to `scrollers` / `putScrollerOffset`, and the record-and-restore half of `useStripScroll` — the ref callback that attaches a `scroll` listener and re-applies the target under a `ResizeObserver` until it is reachable or the user takes over — is extracted to `useRestorableScroll(restoreKey, axis)`.

`useStripScroll` then composes that hook with its drag handlers (its own ref callback and the hook's both run on attach), keeping its behaviour identical; the score board calls the same hook with the vertical axis. The alternative — a second, vertical copy of that ref-callback-plus-`ResizeObserver` logic in `ScoreBoardOverlay` — duplicates the subtlest piece of restoration code in the app, whose comments explain twice why it cannot be an effect.

**The element to attach it to** is `Modal`'s own box, which is the score board's scroll container (`.modal` is `overflow-y: auto`). `Modal` gains an optional `contentRef` forwarded to that box. *Rejected:* making the board its own scroll container the way `.modal--rank` does — that is the better long-term shape, but it moves the board's header out of the scroll flow and changes what the sticky slot headers scroll under, which is a visible redesign of a board this change is only meant to bring back.

### D7 — Restoring an overlay is the page being rebuilt, not an overlay outliving its page

`overlay-behaviour`'s "An overlay closes when the page it was opened over is left" is unaffected in mechanism: `Modal` still calls `onClose` on a pathname change, and leaving `/recap` still tears the board down. What is new is that the recap page *records* that the board was open, so the history entry, when it is returned to, renders a page that has one. The board on screen after a restore is a new overlay over the page it belongs to — the invariant the requirement protects (an overlay can never hang above a page it has nothing to do with) is untouched, and a restore of any *other* page shows no board.

Two orderings make this safe, and both already hold:

- A dismissal caused by leaving the page must not overwrite the departing entry's record. It cannot: `/recap` is a fixed path, so a route change unmounts `RecapPage` and the `Modal` with it, and an unmounting component's pathname effect never fires. (The effect exists for overlays on parameterised routes such as `/anime/:id`.)
- The page's scroll restore must not be defeated by the lock the reopened board takes. It is not: `useScrollRestoration` restores in a **layout** effect, and `useScrollLock` takes the lock in a **passive** effect, so the page is already at its recorded position before `overflow: hidden` reaches the body — and on a restore the page is fully laid out on that first pass, because `usePageData` seeds restored data synchronously (the same property `useStripScroll` relies on). A later retry attempt under the lock is inert rather than harmful: `overflow: hidden` leaves `window.scrollY` alone, so a clamped `scrollTo` moves nothing.

### D8 — One `.scroll-hidden` utility beside `.scroll-y`

`index.css` gains `.scroll-hidden` — `overflow-y: auto`, `scrollbar-width: none`, `&::-webkit-scrollbar { display: none }` — as the counterpart to `.scroll-y`. `.activity-feed`, both `.divergence-list`s and `.edit-history__list` swap `.scroll-y` for it; `.ranking-overlay__list` keeps its own `max-height` (which is load-bearing for its eight-whole-rows rule) and takes the class for its overflow.

Named for what it does rather than as a `.scroll-y` modifier, because it is not a variant of the gutter-reserving utility — it is its opposite, and `UpdatesMenu.css` has already recorded what happens when the two are layered. The four existing local copies of the idiom are left alone: they are horizontal scrollers whose rules sit beside other axis-specific properties, and rewriting them is not what was asked for.

Losing `scrollbar-gutter: stable` gives each list back the gutter's width, so rows grow slightly wider. Nothing depends on that width: `profile-stats`'s "Profile lists show whole rows only" constrains row *heights* and counts, and the ranking overlay's height comes from its row height.

## Risks / Trade-offs

- **A bookmarked `?type=` behaves differently from a bookmarked bare URL, and the difference is one invisible character.** → Only the app writes that form, and it writes it only when the user pressed **None**. A hand-edited URL landing on it gets the empty view the page then explains ("No anime match the current filters"), which is recoverable in one click.
- **None is a state a user can leave a page in and not understand.** → Each page already has, and will use, its filtered-to-nothing message rather than its "nothing exists" one; My List's offers to clear the filters. The search page gains that message, which it lacks today.
- **The reserved-width trick puts hidden text in the accessible tree.** → The hidden candidates carry `aria-hidden="true"`; the button's accessible name comes from the one visible summary, unchanged.
- **A very long option label makes the trigger permanently wide.** → It is already as wide as that label whenever that option is selected; the change is that it stays that way. The alternative is the reflow being removed.
- **Extracting `useRestorableScroll` touches the profile page's strips, which are subtle and already correct.** → The extraction is a move, not a rewrite: same ref-callback shape, same `ResizeObserver`, same cancel-on-user-input. Tasks verify the profile strips restore before the board is wired to the hook.
- **Renaming `strips` to `scrollers` touches the shared snapshot type.** → Three call sites, all in this repo, all compiled by TypeScript; a missed one does not build.
