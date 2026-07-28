## 1. Promote into the top 10

- [x] 1.1 In `TopAnimeSelectionOverlay.tsx`, add a `TOP_LIST_SIZE = 10` constant and a `promoteBoundary(tier)` helper returning `Math.min(tier.includedCount, TOP_LIST_SIZE)`
- [x] 1.2 Render the ↑/↓ pair only for rows at `index < promoteBoundary`, keeping the existing disabled-at-the-ends behavior
- [x] 1.3 Render a single promote button for rows at `index >= promoteBoundary` — glyph `⤒`, `aria-label="Move into top 10"`, `onClick` calls `moveMember(tierIndex, index, boundary - 1)`
- [x] 1.4 Update the overlay hint text to mention the promote button alongside dragging and the arrows

## 2. Pointer-event drag (replaces HTML5 drag-and-drop)

- [x] 2.1 Remove `draggable`, `onDragStart`, `onDragOver`, `onDrop` and the `dragRef` from the rows
- [x] 2.2 Add `data-tier-index` and `data-row-index` attributes to each row, plus a ref on the `.top-anime-selection__tiers` scroll container
- [x] 2.3 Add active-drag state `{ tierIndex, fromIndex, targetIndex, x, y, offsetX, offsetY, width } | null` and a mirror ref for handlers that need it synchronously
- [x] 2.4 On row `onPointerDown`: capture the row rect and pointer offset, `setPointerCapture`, and arm (not start) a drag; start it only once the pointer has moved ~4px so plain clicks still behave like clicks
- [x] 2.5 Stop `pointerdown` propagation on the `__row-buttons` container so the arrow/promote buttons never arm a drag
- [x] 2.6 On `pointermove` while dragging: update `x`/`y`, then resolve the target via `document.elementFromPoint(x, y).closest('[data-row-index]')` — accept it only if its `data-tier-index` matches the dragged row's tier, otherwise keep the previous target
- [x] 2.7 On `pointerup`: call `moveMember(tierIndex, fromIndex, targetIndex)` when the target differs from the source, then clear the drag state; on `pointercancel`, clear without reordering
- [x] 2.8 Add `touch-action: none` on rows and `user-select: none` on the tier list while a drag is active

## 3. Floating preview and landing indicator

- [x] 3.1 Render the floating preview as the last child of `.top-anime-selection` when a drag is active — picture and title only, no buttons — positioned `position: fixed; left: x - offsetX; top: y - offsetY` at the captured row width
- [x] 3.2 Give the preview `pointer-events: none` (load-bearing for `elementFromPoint` hit-testing — comment it) and confirm the drop target still updates when the pointer sits over the preview card
- [x] 3.3 Dim the source row in place while its drag is active
- [x] 3.4 Render the insertion line below the target row when `targetIndex > fromIndex` and above it when `targetIndex < fromIndex`, matching `moveMember`'s splice-out-then-insert semantics

## 4. Drag auto-scroll

- [x] 4.1 Add `EDGE_ZONE` and min/max scroll-speed constants near the top of the file, plus refs for the latest pointer Y and the rAF handle
- [x] 4.2 Implement the rAF loop: each frame compare the stored Y to the container rect, scroll by a speed ramped with edge proximity when inside `EDGE_ZONE`, re-resolve the target index so the indicator tracks rows scrolling past, re-schedule itself, and bail out if the container ref is gone
- [x] 4.3 Start the loop when a drag activates; cancel it on `pointerup`, `pointercancel`, Escape, and in a `useEffect` cleanup on unmount

## 5. Escape ownership

- [x] 5.1 Register a `document` keydown listener with `{ capture: true }` in the overlay; when a drag is active, cancel the drag and `stopPropagation()` so `Modal`'s bubble-phase listener never fires
- [x] 5.2 Confirm Escape with no drag active still closes the overlay unchanged (no edit to `Modal.tsx`)

## 6. Styling

- [x] 6.1 Style the promote button in `TopAnimeSelectionOverlay.css` so it is visually distinct from the single-step arrows but sits in the same `__row-buttons` slot width
- [x] 6.2 Style the floating preview: background, border, `var(--shadow)`, slight scale, high `z-index` within the backdrop's stacking context
- [x] 6.3 Style the insertion line (2px `var(--accent)`) and the dimmed source row
- [x] 6.4 Raise `.top-anime-selection__tiers` `max-height` from `420px` to `min(60vh, 640px)`; keep `cursor: grab` on rows and add `cursor: grabbing` while dragging

## 7. Verify

- [x] 7.1 `cd frontend && npm run build` and `npm run lint` clean (use nvm's node v22, not the default v16)
- [x] 7.2 Manually check a long uncapped tier (30+ rows): rows 11+ show only the promote button, promoting row ~37 lands it at row 10 and pushes the old row 10 to 11, and everything after row 37 keeps its position
- [x] 7.3 Manually check a truncated tier: promoting a row below the cut line lands it in the last included slot, the displaced member falls below the cut line, and Save persists both
- [x] 7.4 Manually check the drag: preview follows the cursor, source row dims, insertion line matches where the row actually lands on release, and rows do not shuffle mid-drag
- [x] 7.5 Manually check auto-scroll: holding a dragged row at the top and at the bottom edge scrolls continuously and stops on leaving the edge zone, on release, and on Escape
- [x] 7.6 Manually check Escape during a drag cancels only the drag and leaves earlier pending reorders intact, and Escape outside a drag still closes the overlay
- [x] 7.7 Confirm clicking the arrow and promote buttons never starts a drag, and dragging a row onto a different tier still changes nothing
- [x] 7.8 Spot-check touch behavior with Chrome DevTools device emulation: drag works and the page does not scroll instead of the row
- [x] 7.9 Update the `TopAnimeSelectionOverlay` description in `CODE_GUIDE.md`

## 8. Fixes from manual testing

- [x] 8.1 Stop dragging from starting native text selection on rows the pointer passes over: `preventDefault()` on row `pointerdown`, and `user-select: none` unconditionally on `.top-anime-selection__tiers` (not only while a drag is active)
- [x] 8.2 Center the move-up/move-down/promote glyphs within their button box (`display: flex; align-items: center; justify-content: center` plus `padding: 0; box-sizing: border-box`, removing dependence on default button padding/text-align)
- [x] 8.3 Add a native `title` attribute to the truncated row title so hovering shows the full untruncated title (also applied to `AnimeCard` and the other page-level truncated title rows app-wide, since the same truncation problem exists there)
- [x] 8.4 Show the English title (falling back to the default title) for each row in the editor and its drag preview, via the same `pickDisplayTitle` helper the rest of the app uses
