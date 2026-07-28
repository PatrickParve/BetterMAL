## Context

Everything lives in one component: `frontend/src/components/TopAnimeSelectionOverlay.tsx` (140 lines). It copies `section.tiers` into local `EditableTier[]` state (`{ score, members, includedCount }`), mutates it with `moveMember(tierIndex, fromIndex, toIndex)`, and on Save `PUT`s `{ score, animeIds }` per tier. Reordering today has two affordances: native HTML5 drag-and-drop (`draggable` row, `dragRef` holds `{tierIndex,index}`, drop on a row calls `moveMember`) and per-row ↑/↓ buttons that step one place.

Two structural facts drive the design:

- `ProfileService.BuildTopAnimeSection` gives the score-10 tier `includedCount = members.Count` (uncapped) and every other tier `min(members.Count, slotsRemaining)` against `TopAnimeMinimumSize = 10`. So the whole list is either a single uncapped tier that can be arbitrarily long, or exactly 10 items across several tiers with only the last one truncated. That means "the first 10 slots of a tier" is exactly `min(tier.includedCount, 10)` — no cross-tier position arithmetic is needed, and for a truncated tier the boundary lands precisely on the existing cut line.
- The scroll container is `.top-anime-selection__tiers` (`max-height: 420px; overflow-y: auto`), nested inside `.modal` which is itself `max-height: 85vh; overflow-y: auto`. Auto-scroll only ever needs to drive the inner one; the outer rarely overflows because the inner is capped.

No backend work: both features are pure local-state edits before the existing Save.

## Goals / Non-Goals

**Goals:**
- One click gets any row from anywhere in a long tier to the last slot of that tier's top 10.
- One uninterrupted drag can carry a row across a tier longer than the visible scroll box.
- A styled card follows the cursor for the whole drag, and the list shows where the row will land before release.
- Keep the existing tier-scoped semantics: order changes never cross tiers, the cut line still means membership, Save still sends one `animeIds` array per tier.

**Non-Goals:**
- Cross-tier drag, multi-select, or an "undo" in the editor.
- Changing the top-anime strip on the profile page itself, or the `PUT /api/top-anime/order` contract.
- Keyboard-driven drag (the arrow/promote buttons already cover keyboard reordering).
- A generic reusable drag-list component or a drag library dependency — this is the only reorderable list in the app.

## Decisions

### 1. Promote boundary is `Math.min(tier.includedCount, 10)`, computed per tier

A single derived number per tier decides both which control a row gets and where promote lands it:

```
promoteBoundary = Math.min(tier.includedCount, TOP_LIST_SIZE)   // TOP_LIST_SIZE = 10
index <  boundary → ↑ / ↓ (disabled at index 0 / last)
index >= boundary → promote, moveMember(tierIndex, index, boundary - 1)
```

`moveMember` already splices out and re-inserts, which shifts everything from `boundary - 1` onward down by one — exactly the "previous 10th goes 1 down" behavior, no new state helper needed.

Alternatives considered: computing each row's *global* position across tiers (`1 + sum of previous tiers' includedCount`) and promoting to global slot 10. Rejected — given the backend's fill rule the two are provably identical, and the global version adds cross-tier bookkeeping plus an unreachable-tier edge case that can't actually occur.

Why the boundary is `includedCount`-based rather than a flat 10: in a truncated tier `includedCount < 10`, and promoting to slot 10 there would be *below* the cut line, i.e. a promote that doesn't promote. Clamping to `includedCount` makes the action mean "take the last slot that's actually in the list" in both shapes.

### 2. Replace, not supplement, the arrows outside the boundary

Requested explicitly, and it's also what makes the control legible: below the boundary the only ordering question that matters is "does this get into the list", so one button answering it beats three buttons. Fine-grained shuffling below the boundary stays available via drag. The button gets a distinct glyph (`⤒`) and `aria-label="Move into top 10"` so it doesn't read as another single-step arrow.

### 3. Drop native HTML5 drag-and-drop for pointer events

A floating preview is the forcing decision here. With HTML5 DnD the only supported preview is `dataTransfer.setDragImage`, which takes a **static bitmap snapshot at dragstart**: it can't restyle mid-drag, can't show a count or a landing hint, needs the source element rendered off-screen to snapshot cleanly, and renders differently per browser. The half-measure — hide the native image with a 1×1 transparent GIF and position our own element from `dragover` coordinates — leaves the preview trailing the OS drag cursor, since `dragover` is movement-driven and coarser than `pointermove`.

So the drag lifecycle moves to pointer events on the row: `pointerdown` → `setPointerCapture` → `pointermove` → `pointerup`/`pointercancel`. Everything the change needs falls out of one coordinate stream: preview position, target index, auto-scroll velocity. Touch support comes along for free (`touch-action: none` on rows).

What has to be rebuilt, and how:

- **Drag activation.** `pointerdown` records `{ tierIndex, index, startX, startY, offsetX, offsetY, width }` from the row's `getBoundingClientRect()`; the drag only becomes active once the pointer has moved ~4px, so a plain click still behaves like a click. The `__row-buttons` container stops `pointerdown` propagation so pressing ↑/↓/promote never arms a drag.
- **Target resolution.** On each `pointermove`, `document.elementFromPoint(x, y)` → `closest('[data-row-index]')`. Each row carries `data-tier-index` / `data-row-index`. If the hit row is in the dragged row's tier, that becomes the target index; if the pointer is over a gap, a tier header, or another tier, the last valid target is kept. This is why the preview must be `pointer-events: none` — otherwise it hit-tests itself.
- **Uniform row heights are not assumed.** Hit-testing beats arithmetic here because the cut-line separator makes one gap in one tier taller than the rest.
- **Cancel.** `pointercancel` and Escape both abort with no reorder (see decision 6).

Alternative considered: adding a drag library (`dnd-kit`, `react-beautiful-dnd`). Rejected — the frontend has zero UI dependencies today (React, React DOM, React Router, nothing else), and this is the app's only reorderable list. ~150 lines in one component is a better trade than a dependency plus its API.

### 4. Reorder on release, with a floating preview and an insertion line

The array is **not** mutated during the drag. `moveMember` runs once, on `pointerup`. During the drag the component holds:

```
drag = { tierIndex, fromIndex, targetIndex, x, y, offsetX, offsetY, width } | null
```

and renders two things:

- **The floating preview** — a copy of the row (picture + title, no buttons) at `position: fixed; left: x - offsetX; top: y - offsetY; width: <captured row width>; pointer-events: none`, with a shadow and slight scale so it reads as lifted. It is the last child of `.top-anime-selection`, which sits inside `.modal-backdrop` (itself `position: fixed`, no transform/filter anywhere in the chain) — so the fixed preview is viewport-positioned and escapes `.modal`'s and `.top-anime-selection__tiers`' `overflow-y: auto` clipping without needing a portal.
- **The insertion line** — a 2px accent rule marking where the row lands, plus the source row dimmed in place. Because `moveMember` splices out before inserting, the line goes **below** the target row when `targetIndex > fromIndex` and **above** it when `targetIndex < fromIndex`; that is exactly where the row ends up under those semantics.

Not mutating during the drag is the important half. A live-reordering list moves rows under a stationary cursor, which re-triggers hit-testing and makes the drop target oscillate — especially while auto-scroll is running. Deferring the mutation makes the drag a pure preview and the drop a single, obviously-correct `moveMember` call, and it keeps the existing `moveMember` as the only mutation path.

### 5. Auto-scroll: rAF loop fed by the pointer position

The loop is independent of event cadence, because `pointermove` stops firing when the pointer is held still at the edge — which is exactly the case that has to keep scrolling.

`pointermove` writes the latest `clientY` to a ref. A `requestAnimationFrame` loop, started when the drag activates, each frame compares that Y to the tiers container's `getBoundingClientRect()`; if it is within `EDGE_ZONE` (48px) of either edge it applies `el.scrollTop += direction * speed`, where speed ramps from ~4px to ~18px per frame with proximity to the edge, then re-resolves the target index so the highlight tracks the rows scrolling past. The loop is cancelled on `pointerup`, `pointercancel`, Escape, and in a `useEffect` cleanup on unmount, and self-cancels if the container ref is gone.

`EDGE_ZONE` and the speed bounds are named constants at the top of the file — they are feel values and will want one re-tune after trying it.

### 6. The drag owns Escape while it is active

`Modal` closes on Escape via a `document` keydown listener in the bubble phase. Losing a half-finished reorder — and every other pending edit in the overlay — to an Escape meant for the drag would be worse than the problem this change fixes. The overlay registers its own `document` keydown listener with `{ capture: true }`; while a drag is active it cancels the drag and calls `stopPropagation()`, which prevents the event from ever reaching `Modal`'s bubble-phase listener on the same node. With no drag active it does nothing and Escape closes the overlay as before.

### 7. Taller scroll area

`.top-anime-selection__tiers` goes from `max-height: 420px` to `max-height: min(60vh, 640px)`. The modal's own `max-height: 85vh` plus the ~120px of title/hint/buttons keeps this from overflowing the viewport. Directly reduces how often scrolling is needed at all, which is half the complaint.

## Risks / Trade-offs

- **Hand-rolled drag is more code to get wrong than the native one it replaces** → the surface is deliberately small: one active-drag state object, one mutation call on release, no live list mutation. The ↑/↓/promote buttons remain a complete non-drag path to every reorder, so a drag bug can't make the editor unusable.
- **`elementFromPoint` hit-testing depends on the preview not intercepting** → `pointer-events: none` on the preview is load-bearing; note it in a comment, and verify by checking that the drop target still updates when the pointer is directly over the preview card.
- **Losing HTML5 DnD loses the browser's built-in drag cursor and Esc-to-cancel** → both are replaced explicitly (`cursor: grabbing` on the body while dragging, decision 6 for Escape).
- **Pointer capture on a row that unmounts mid-drag** → rows are keyed by `animeId` and the array is not mutated during the drag, so the captured node is stable for the whole gesture.
- **Auto-scroll speed is a feel constant tuned blind** → named constants, verified manually against a tier of 30+ rows.
- **Losing ↓ below the promote boundary is a real capability loss** → drag now covers it well, and those rows are outside the top 10 where relative order is close to invisible.
- **Promote boundary depends on `includedCount` semantics staying as they are** → if the backend fill rule ever changed (e.g. capping the 10s tier), the boundary formula would need revisiting; it is one named helper, and the spec states the rule explicitly.
