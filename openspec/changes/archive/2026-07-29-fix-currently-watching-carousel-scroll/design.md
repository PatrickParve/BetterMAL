## Context

`CurrentlyWatchingCarousel.tsx` currently implements infinite looping by rendering `LOOP_COPIES = 3` back-to-back copies of `items` and running an effect that silently re-centers `scrollLeft` onto the middle copy whenever the position drifts out of it. That effect is keyed on `[looping, items]`.

`HomePage.handleEpisodesWatchedChange` rebuilds `dashboard.currentlyWatching` with `.map()` on every increment, so the `items` prop gets a fresh array identity even though only one entry changed. That identity change re-runs the looping effect, whose cleanup/setup path ends in `center()` → `node.scrollLeft = oneCopyWidth()`. The visible result is the row snapping back to the first card after every plus click — but only when the row overflows, since `looping === overflowing`.

The user wants the looping behaviour gone regardless, which makes the fix and the feature removal the same edit: delete the looping machinery and nothing is left that writes `scrollLeft` on an `items` change.

## Goals / Non-Goals

**Goals:**
- The row scrolls as a plain bounded list — stops at the first and last card, no wrap-around.
- Each currently-watching anime renders exactly once.
- Incrementing an episode leaves the scroll position untouched at any entry count.
- Keep everything else identical: 5-card cap, 5-card-wide bound, arrows only when overflowing, one-card arrow steps, click-to-navigate, progress bar + plus control.

**Non-Goals:**
- Disabling or dimming the arrows when the row is already at its first/last card. Native scroll clamping already makes a further click a no-op; adding bound-aware arrow state is a separate UI concern.
- Changing the increment/sync pipeline, the DTO, or `HomePage`'s cross-section patching.
- Touching any other carousel or list on the site.

## Decisions

**Remove looping rather than making the effect increment-tolerant.** The alternative was to keep looping and stabilize it — e.g. depend on `items.length` instead of `items`, or save/restore `scrollLeft` around re-centers. That is strictly more code to defend a behaviour the user explicitly asked to drop, and the `items`-identity dependency is genuinely needed by the copy-index arithmetic (`node.children[items.length]`). Deleting it removes both the bug and the feature in one step.

**What gets deleted:** the `LOOP_COPIES` constant, the `looping` derived value, the `copies` array with its `flatMap` wrapper, and the entire re-centering effect (`oneCopyWidth`, `reposition`, `center`, `handleScroll`, and its `ResizeObserver`). Cards are rendered with a direct `items.map(...)`.

**Keep the overflow effect, simplified.** The `overflowing` state still drives arrow visibility, so its `ResizeObserver` stays. With a single copy the copy-count arithmetic (`Math.round(node.children.length / items.length)`) is dead weight; the check reduces to `node.scrollWidth > node.clientWidth + 1`. This effect only calls `setState` and never writes `scrollLeft`, so it is safe to keep depending on `items`.

**Keys become `item.animeId`.** With copies gone the `${copy}:` prefix is meaningless, and a stable per-anime key is what lets React patch the changed card in place — which is what actually preserves the scroll position across an increment.

**No CSS change.** `.carousel__track` already has `max-width: calc(5 * 160px + 4 * 16px)` and `overflow-x: auto`, which is exactly a bounded 5-card scroller. `justify-content: safe center` keeps a short row centered and, with `safe`, falls back to start-alignment when the row overflows, so nothing needs adjusting.

**`scroll()` is unchanged.** It already rounds to the nearest card boundary and steps by one card width; with no copies, `scrollTo` past either end is clamped by the browser.

## Risks / Trade-offs

- **Users who liked the endless spin lose it** → This is the explicit request; the behaviour is removed deliberately, and the spec is updated so the removal is the recorded intent rather than a regression.
- **A stale reference to the removed looping behaviour could linger** → The component's leading block comment describes the three-copy looping in detail and must be rewritten alongside the code, as must the `main-dashboard` spec's looping scenarios (covered by the delta spec).
- **Scroll-position preservation depends on React reusing the card DOM nodes** → Guaranteed by stable `animeId` keys and the fact that only `episodesWatched` changes; if a future change reorders or replaces `currentlyWatching` wholesale on increment, the position would move again. Worth verifying by hand with more than 5 entries.
