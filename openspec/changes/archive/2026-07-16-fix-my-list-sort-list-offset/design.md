## Context

`MyListPage` renders two different view modes out of one component:

- **Grouped view** (`sort === 'alphabetical'`) maps over `GROUP_ORDER` and emits one `<section class="my-list-page__group">` per status. The group header and its `<ul class="my-list-page__list">` are children of that section.
- **Ranked view** (`sort` is MAL score, my score, or airing status) emits a React fragment `<>` containing the same header `<div>` and `<ul>`, with no wrapping element.

The page container is a flex column:

```css
.my-list-page {
  display: flex;
  flex-direction: column;
  gap: 20px;
}
```

A React fragment produces no DOM node, so in ranked view the header and the `<ul>` become **two separate flex children** of `.my-list-page` and the 20px column gap lands between them, on top of the header's own `margin: 0 0 8px`. Header-to-first-row spacing is therefore 28px in ranked view versus 8px in grouped view — the list sits 20px lower. The header itself does not move (it is 20px below the tabs in both modes), which is why the defect reads as "the list drops" rather than "the page shifts."

This affects every status filter, because `showRanked` is computed from `sort` alone and all filters share the ranked branch. The inconsistency dates to `e8e13c6`, which introduced the ranked branch alongside the inline sort controls.

## Goals / Non-Goals

**Goals:**

- Header-to-first-row spacing is identical in ranked and grouped view, under every status filter.
- One spacing rule governs both modes, so they cannot drift apart again.
- Fix stays presentational: no change to sorting, grouping, ranking, or filtering logic.

**Non-Goals:**

- Re-tuning the page's overall spacing scale (the 20px section gap, the 8px header margin).
- Changing which sorts are offered, where the sort control sits, or how rank numbers render.
- Auditing other pages for the same fragment-vs-wrapper pattern.

## Decisions

**Wrap the ranked branch in `<section class="my-list-page__group">`, matching the grouped branch.**

Both branches then emit the same DOM shape — one section per header+list unit — and the section is the only direct flex child of `.my-list-page`, so the 20px gap separates sections from each other and never a header from its own list. Spacing collapses to the header's 8px `margin-bottom` in both modes, from a single rule.

Alternatives considered:

- *Negative margin or a ranked-only `margin-top: -20px` on the list* — cancels the gap numerically rather than structurally. It re-breaks the moment the gap value changes, and encodes "20px" in two places.
- *Set `.my-list-page` gap to 0 and give every child explicit margins* — fixes it, but touches header/tabs/section spacing across the whole page for a defect confined to one branch.
- *A ranked-only wrapper class (e.g. `my-list-page__ranked`)* — a second class that must be kept in sync with `my-list-page__group` forever, which is the drift the bug came from.

**No new CSS rule.** `.my-list-page__group` is currently unstyled, and that is sufficient: a plain block section shields its children from the parent's flex gap, and `.my-list-page__group-header`'s existing `margin: 0 0 8px` supplies the spacing. Adding an empty or default-valued rule (`display: block`) would document nothing the cascade does not already do.

**Reuse the `group` name in ranked view even though ranked view is not grouped by status.** The name is slightly loose — ranked view is one flat list under an active-filter header, not a status group. But the component already reuses `my-list-page__group-header` for exactly that header, so reusing the matching section keeps one naming convention instead of introducing a parallel one for a single element.

## Risks / Trade-offs

- **The 20px drop might be load-bearing to someone's eye — i.e. intended breathing room in ranked view.** → The proposal and the reported defect both treat it as a glitch, and the grouped view is the reference the ranked view should match. If more space is wanted later, it belongs on `.my-list-page__group-header`'s margin so both modes move together.
- **Fix is verified visually, and the repo has no test for this page's layout.** → Verify by loading My list, toggling sort across All and at least one filtered tab, and confirming the first row does not move. Header-to-row spacing should measure 8px in both modes.
- **`.my-list-page__group` carries the contract implicitly, through the absence of styling.** → Low risk: any future rule that made the section a flex/grid container with a gap would reintroduce the drop, but it would then do so in both modes at once, which is visible immediately rather than only when sorting.
