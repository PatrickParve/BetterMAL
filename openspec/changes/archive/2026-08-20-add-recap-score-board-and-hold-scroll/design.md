## Context

Two independent pieces of work on one page, both sitting on machinery that already exists.

**The rating distribution.** `RecapPage.renderDistribution` renders the shared `<ScoreDistribution>` over ten buckets produced by `scoreBucketsOf(recap.items)` — a local reduce that counts `item.myScore` across every included entry, deliberately *not* narrowed by the top 10's media-type control. Each row is already a `<Link>` into my list scoped to that score. What the block cannot do is name the anime: the counts are the whole of it, and seeing which anime they are means leaving the recap.

**The overlay pattern.** `RankingOverlay` is the precedent: page-local `useState` holds the open overlay, `<Modal>` supplies backdrop/Esc/click-outside close, `modal--wide` widens it to 640px, and a `describe*()` function shared with the inline list keeps the two renderings of one ranking from drifting. `TruncatedTitle` is the precedent for a hover card: a `createPortal(…, document.body)` `position: fixed` tooltip, positioned in a `useLayoutEffect` so its first painted frame is already clamped to the viewport.

**Colour tokens.** `index.css` carries `--medal-gold|silver|bronze` (plus `-bg`/`-border`) with dark-theme values, introduced for the podium and explicitly written to be reusable. The podium's cards read them through a local `--medal` alias set by a rank modifier class, so every coloured surface in a card is written once against `var(--medal)`.

**The scroll reset.** `useScrollRestoration` runs one `useLayoutEffect` keyed on `location.key`: on anything that is not a POP-with-snapshot it calls `window.scrollTo(0, 0)`. `setSearchParams` pushes a history entry, which mints a new `location.key`, so *every* in-place URL update on the recap page reads as a fresh visit and scrolls to the top — including the ranking-basis toggle and media-type select, which sit inside the Top 10 section header well down the page. `TopAnimePage.goToPage` depends on exactly this behaviour and documents it, so the reset cannot simply be dropped for same-path navigations.

`BrowserRouter`, not a data router — React Router's own `preventScrollReset` is not available and would not be read by this hand-rolled hook anyway.

## Goals / Non-Goals

**Goals:**

- A period's scored anime are visible *as anime* — poster art, by score — without leaving the recap.
- The tier a score belongs to (10 / 9 / 8 / 7 / 6–5 / 4–1) is readable from colour alone, and 10 is unmistakably the top of the ladder.
- The board and the distribution beside it can never disagree about how many anime carry a score.
- Comparing the top 10 across ranking bases, or narrowing it by media type, leaves the page where it is — including after navigating away and back.
- Every other page's scroll behaviour, and the back/forward restore path, is untouched.

**Non-Goals:**

- Any backend, DTO, or request change. The board renders `recap.items`, already in hand.
- A MAL-score board. The board is the distribution's companion, and the distribution is my scores.
- Replacing the rating distribution, or changing its rows, its drill-through, or its hover treatment.
- Reworking the recap's data-refetch-on-URL-change behaviour (a URL update makes a new snapshot, which `usePageData` re-loads into — unchanged by this work, and invisible because the previous data stays on screen).
- Making the board addressable in the URL, or a scroll-hold flag available to other pages beyond the one hook change that enables it.

## Decisions

### 1. One grouping pass feeds both the distribution and the board

`scoreBucketsOf(items)` is replaced by `scoreGroupsOf(items): { score: number; items: RecapRowDto[] }[]` — the same ten ascending slots, but carrying the entries rather than only a count. `RecapPage` derives the distribution's buckets from it (`groups.map(g => ({ score: g.score, count: g.items.length }))`) and hands the groups themselves to the board.

This is what makes "the count in a distribution row and the number of posters in the matching slot agree" structurally true rather than a thing to test for: one selection rule, one media-type-narrowing decision (none), one place where `myScore == null` is skipped. It is the same reasoning `describeSeasonRanking` is shared between a ranking's inline five rows and its overlay.

*Alternative considered.* Leaving `scoreBucketsOf` alone and giving the board its own filter over `recap.items` — two reduces that must be kept in step by hand, and that would silently diverge the first time either the distribution's or the board's selection rule was touched.

### 2. Slot order within a score, and slot order down the board

Slots render 10 → 1, matching the distribution's own high-first order directly beside it. Within a slot, entries sort by `title.localeCompare` — the same tie-break the top 10 already uses — so the board is stable across reloads and across a refetch of the same period. No secondary sort by MAL score: a my-score board that quietly ranks by MAL's opinion inside each slot would be answering a question nobody asked.

All ten slots always render, including ones nothing scored. An empty 3 row is information about the period (nobody rated anything a 3), the same information an empty track carries in the distribution, and keeping the ten fixed means the board's shape is comparable between two periods.

### 3. Six colour tiers, three new tokens, three aliased

The mapping is fixed: 10 → apex, 9 → red, 8 → blue, 7 → gold, 6 and 5 → silver, 4 through 1 → bronze. A single `scoreTier(score)` in the component returns the tier slug; CSS sets local `--tier`, `--tier-bg`, `--tier-border` aliases per `--apex|--red|--blue|--gold|--silver|--bronze` modifier, and every coloured surface in the slot is written once against `var(--tier)` — the podium card's `--medal` pattern exactly.

`index.css` gains `--tier-apex-*`, `--tier-red-*`, `--tier-blue-*` (colour / `rgba(…, 0.12)` tint / `rgba(…, 0.45)` border, plus dark-theme values, following the `--status-*` shape). Gold, silver, and bronze alias `--medal-gold|silver|bronze` rather than duplicating them — the podium already solved the "silver disappears into `--border` in the light theme" problem, and a second silver would have to solve it again.

**Apex is a gradient, not a colour.** "Purple and white mixed" cannot be one token value, so the apex tier carries an additional `--tier-apex-sheen` (near-white in the light theme, a bright lilac-white in the dark one) and its slot's rail and label are drawn as `linear-gradient(135deg, var(--tier-apex), var(--tier-apex-sheen) 50%, var(--tier-apex))` with a slow sheen sweep, reusing the podium's `#1`-card motion vocabulary. That gradient, not the hue alone, is what makes the 10 slot belong to no other tier.

*Alternatives considered.* Hard-coding the six tier colours in the board's CSS — rejected for the same reason the medal colours are tokens: a colour with a meaning is a token in `index.css` in this codebase. Generating the tier from `score` in CSS with `:nth-child` — rejected because the mapping is not monotonic in a way `nth-child` expresses (6 and 5 share, 4–1 share) and a modifier class states the mapping where TypeScript can be read.

### 4. The blue tier is a different blue from the MAL blue

`--mal` is `--status-completed` (#2563eb), and the app's colour language is "blue is MAL, purple is mine". An 8 slot in a board of *my* scores drawn in that same blue would be reading against the grain. `--tier-blue` is a distinctly cooler, cyan-leaning blue instead, so the two do not sit in the same place in the eye. The board carries no MAL scores anywhere (decision 6), so the two blues never appear side by side in one view.

The purple/white apex has the mirror-image tension with `--accent`: purple already means "mine" and also colours every selected control on the page. The white-mixed gradient and the sheen are what separate the apex slot from a flat accent surface — a flat `--accent` rail there would read as "selected", not as "the best".

### 5. The board is page-local state, opened from a section header

`RecapPage` grows a `boardOpen` boolean beside its existing `overlay` state, and `renderDistribution` grows the same `.recap-page__section-header` wrapper the Top 10 uses, with the "Score board" button on the right of the heading. No URL parameter and no history entry — matching `RankingOverlay`, and keeping the "back leaves the recap" contract intact rather than making back mean "close the board".

When the period has no scored entries the control renders **disabled with an explanatory title**, rather than disappearing: the recap's time filter already establishes that idiom for "this exists but has nothing behind it right now", and a control that vanishes and reappears as the filter changes makes the section header jump.

The board needs more width than `modal--wide`'s 640px to lay a slot's posters out, so `Modal.css` gains one more size class (`modal--board`, `max-width: min(1080px, 100%)`). `Modal` already supplies Esc, backdrop-click, `role="dialog"`, and `aria-modal`; the board supplies `labelledBy` for its own heading, exactly as `RankingOverlay` does.

### 6. The hover card is portalled and anchored to the tile, and carries no MAL score

The card follows `TruncatedTitle`'s pattern — `createPortal` to `document.body`, `position: fixed`, positioned in a `useLayoutEffect` so its first visible frame is already clamped to the viewport. Portalling is not optional here: `.modal` is `overflow-y: auto`, which makes it a scroll container in *both* axes, so a card positioned inside it would be clipped at the slot's edges.

It is anchored to the tile's `getBoundingClientRect()` rather than to the pointer, because the same card shows on `:focus-visible` — a keyboard focus has no pointer coordinates. Preferred placement is above the tile, flipping below when there is no room, then clamped to the viewport on both axes.

Contents: the display title (`pickDisplayTitle`), my score for the slot, and the media type via `mediaTypeLabel`. **No MAL score**: `ScoreValue`'s hidden state renders an interactive reveal button, and a hover card is `pointer-events: none` by construction (a card that intercepted the pointer would fight the tile it is describing). Rather than special-case score visibility inside a tooltip, the board simply does not show a score the visibility rules govern.

The card is decorative for assistive technology — each tile is a `<Link>` whose `aria-label` already carries the title, since the poster `<img>` is `alt=""` like every other poster in the app.

### 7. Scroll hold is an opt-in flag in navigation state, not a rule about URLs

`updateParams` grows an options argument; the ranking-basis toggle and the media-type select pass `{ keepScroll: true }`, which becomes `setSearchParams(next, { state: { keepScroll: true } })`. `useScrollRestoration` reads `location.state?.keepScroll` and skips its `scrollTo(0, 0)` on a fresh navigation carrying it.

Opt-in rather than inferred, because the obvious inference — "same pathname, only the query changed, so hold the scroll" — is exactly what `TopAnimePage.goToPage` and `selectType` must *not* do; they push same-path query changes precisely to get the reset, and say so in a comment. Only the two mid-page controls whose subject stays the same opt in. The recap-type tabs, the period stepper and selects, and the time filter all change what the page is *about* and keep today's reset (and all sit at the top of the page anyway, where there is nothing to hold).

Pushing (rather than replacing) stays as it is, so back still undoes a toggle, and the POP path then restores the position through the existing snapshot machinery.

### 8. A held scroll must be written into the new entry's snapshot

The subtle part. A push mints a new `location.key` with a fresh, empty `PageSnapshot` whose `scrollY` is `0`. Today the reset-to-top makes that accurate. Skip the reset and it becomes a lie: the page sits at 800px while its snapshot says 0, and the scroll listener only corrects that if the user happens to scroll afterwards. Navigate to an anime page and come back, and the restore path faithfully restores 0 — the same bug the fix was meant to remove, just deferred behind a navigation.

So the skip branch seeds the entry: `pageStateStore.putScroll(key, window.scrollY)` in the same layout effect, before paint. The hook already holds `key` in a ref that is assigned during render, so it is pointing at the arriving entry when the effect runs.

The flag is also, harmlessly, still on the entry when it is POPped back to later. A POP with a snapshot takes the restore branch and never consults the flag; a POP *without* one (a reload, then back) skips the reset, and the browser has already put that fresh document at scroll 0, so there is nothing to reset.

## Risks / Trade-offs

- **A large period makes a very tall board.** A multi-year recap can hold hundreds of scored anime, and ten wrapping slots of 52px posters get long. → The modal's existing `max-height: 85vh; overflow-y: auto` contains it, the slot header (numeral + count) is sticky within the scroll container so you always know which slot you are in, and the poster grid uses `auto-fill` so it packs at whatever width is available.
- **Two purples and two blues on one page.** The apex tier next to `--accent`, `--tier-blue` next to `--mal`. → Both are addressed in decisions 3 and 4 (gradient + sheen for apex, a cyan-leaning blue for the 8 tier, no MAL scores anywhere on the board), but this needs checking by eye in both themes rather than being taken on trust.
- **The board renders every poster in the period at once.** No virtualisation, and every poster is a network image. → The images are the same MAL CDN URLs the page has already loaded for the top 10 and hot takes, `loading="lazy"` keeps off-screen slots from fetching until scrolled to, and the board is opened deliberately rather than rendered on page load.
- **`keepScroll` is a hook-level contract that only two call sites use.** A later page could set the flag and get the scroll hold without the snapshot seeding being obvious to whoever adds it. → The seeding lives inside the hook, not at the call site, so it applies to every future user of the flag automatically; the hook comment states the contract.
- **The tier mapping is a design decision with no data behind it.** 6–5 sharing silver and 4–1 sharing bronze means a 1 and a 4 look alike. → That is the intent (the bottom of the ladder is one tier), and every slot still carries its own numeral and count, so the exact score is never ambiguous.
