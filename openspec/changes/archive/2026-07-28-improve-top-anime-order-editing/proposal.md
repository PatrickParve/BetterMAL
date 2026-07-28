## Why

The "Edit top anime order" overlay only supports single-step arrows and a drag that stops at the edge of a 420px scroll box. With a large score tier (all my 10s are uncapped, so that tier can be dozens of rows long) getting an anime from the bottom into the visible part of the list means either clicking ↑ dozens of times, or a grab → drop → scroll → grab → drop → scroll cycle. Both make ordering a long tier tedious enough that I avoid doing it.

## What Changes

- **Jump-into-the-top-10 button.** In the order editor, a row that sits outside the first 10 slots of its tier loses the ↑/↓ pair and instead gets a single promote button. Clicking it moves that row directly into the last top-10 slot of its tier, pushing the row that held that slot (and everything after it) down by one. Rows already inside the top 10 keep the existing ↑/↓ single-step arrows.
  - For a truncated tier the "last top-10 slot" is the cut line, so promoting an excluded member both pulls it into the top list and drops the displaced member out — the same semantics the cut line already has.
- **Edge auto-scroll while dragging.** Holding a dragged row near the top or bottom edge of the editor's scroll area scrolls that way continuously until the row leaves the edge zone or the drag ends, so one grab can carry a row across the whole tier.
- **Floating drag preview.** A styled card of the dragged row follows the cursor for the whole drag, the row's old place is dimmed, and an insertion line marks where it will land before I release. Getting a preview that can be styled and updated mid-drag means dropping native HTML5 drag-and-drop for pointer events — which also brings touch support and makes the auto-scroll above reliable.
- **Roomier editor.** The tier scroll area gets a viewport-relative max height so more rows are reachable without scrolling at all.

No backend, DTO, or persistence change: the promote button is just another local reorder before Save, and `PUT` of the tier order is unchanged.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `profile-stats`: the "My top anime with minimum-of-ten fill and manual selection" requirement gains the editor's promote-into-top-10 action and drag auto-scroll behavior.

## Impact

- `frontend/src/components/TopAnimeSelectionOverlay.tsx` — promote action and per-tier promote threshold; native HTML5 drag handlers replaced by a pointer-event drag (active-drag state, hit-tested drop target, rAF auto-scroll loop, floating preview); Escape is claimed while a drag is active so it cancels the drag instead of closing the editor.
- `frontend/src/components/TopAnimeSelectionOverlay.css` — promote button, floating preview card, insertion line, dimmed source row, taller tier scroll area.
- `frontend/src/components/Modal.tsx` — unchanged; the overlay intercepts Escape in the capture phase rather than the modal changing.
- Backend, API contract (`PUT /api/top-anime/order`), and `TopAnimeSectionDto`/`TopAnimeTierDto`: unchanged.
- `CODE_GUIDE.md` — overlay description refresh.
