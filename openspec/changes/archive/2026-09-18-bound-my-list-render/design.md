## Context

`MyListPage` renders its entire result set in one pass. `renderBody` (`MyListPage.tsx:578-642`) maps over `derived.groups` or `derived.items` with no slicing, so a few hundred entries means a few hundred `<li>` rows mounted on every visit, each with an `<img>` that has no `loading` attribute (`components/RowPicture.tsx:32-39`) and therefore fires immediately. It is the only long-list page in the app that does this: `SeriesBrowserPage`, `SeasonPage`, `YearPage` and `SearchPage` each hold a `visibleCount` in `useRestorableState`, slice to it, and grow it from an `IntersectionObserver` sentinel.

The second symptom is the same cause seen from the keyboard. `query` (`:158`) updates per keystroke and re-renders the page; `useDebouncedValue(query, 200)` (`:329`) delays only `derived` recomputing, not `renderBody`'s `.map()`, which walks and diffs every row's props each time regardless. `MyListRow` is `memo`-wrapped with stable `useCallback` props, so no row *re-renders* — but the walk itself is proportional to the full list, and an N-character query pays it N times where a filter click pays it once.

The proposal carries the evidence, the reversal of the 2026-09-13 decision, and what stays out of scope. This document settles the shape of the reveal budget, how the reset is wired on a page that deliberately keeps its controls off the URL, and why the debounce goes rather than staying as belt-and-braces.

Constraints this design works under:

- **Four existing implementations set the pattern.** `SeriesBrowserPage.tsx:76,119-130,132` is the closest match and is the one being followed. Deviating from it needs a reason; three of the four are near-identical to each other.
- **This page's controls are not in the URL, by decision.** `MyListPage.tsx:143-149` states that recap scope is "the only use of useSearchParams on this page". Every filter and sort control is an independent `useRestorableState` call, so the automatic reseed the other four pages get for free (`hooks/useRestorableState.ts:16-23`, spelled out at `SeasonPage.tsx:179-183`) does not happen here.
- **The codebase resets derived state during render, not in an effect.** `hooks/usePageData.ts:62-66` and `hooks/useRestorableState.ts:20-23` both compare against a previous value and call a setter synchronously in the render body, with `usePageData`'s comment giving the reason: "so a restore never paints an empty frame first."
- **There is no frontend test runner.** `frontend/package.json` has no test script and no test dependency, and `frontend/src` holds no test file. Verification is `npm run build`, `npm run lint`, and a manual walkthrough — the shape every previous frontend change in `openspec/changes/archive/` used.
- **`RowPicture` is shared by 27 call sites.** Any change to it is global by construction; the alternative would be a prop, which D6 rejects.

## Goals / Non-Goals

**Goals:**

- A first paint that mounts a bounded number of rows and issues a bounded number of image requests, whatever the list's size.
- A keystroke that costs work proportional to what is drawn, not to the whole list.
- The same reveal mechanics the app's other four long-list pages already use, so the page stops being the odd one out.
- A back/forward restore that lands on the same amount of list, like every other control on this page.
- No change to which entries appear, in what order, under what headers, or to any count the page reports.

**Non-Goals:**

- Bounding the DOM *after* the user has scrolled through the list. Reveal grows monotonically; it never unmounts what has been drawn. Virtualization is the answer to that and is out of scope (D7).
- Reducing the cost of `derived` itself. Filtering and sorting a few hundred items is sub-millisecond; the render was the cost.
- Any backend work. N4 and R5 are closed.
- Per-group reveal budgets (D2).
- Moving this page's controls onto the URL (D4).

## Decisions

### D1. One `visibleCount` for the page, consumed in group order

`const [visibleCount, setVisibleCount] = useRestorableState('visibleCount', PAGE_SIZE)` with `const PAGE_SIZE = 24`, matching `SeriesBrowserPage`, `SeasonPage` and `YearPage`. `SearchPage`'s `CHUNK_SIZE = 48` is for a card grid where a row holds several items; a row layout wants 24.

Flat mode slices directly, as `SeriesBrowserPage.tsx:132` does:

```tsx
const visibleItems = derived.items.slice(0, visibleCount)
```

Grouped mode walks the groups with a running budget. Computed once per render, not inside the `.map()`, so the reduction is plainly a fold and not a mutation smuggled into JSX:

```tsx
// One budget for the page, spent group by group (design D1/D2): each group
// draws what the remainder allows and passes the rest on, so the cap is on
// rows actually mounted rather than on rows per group. Headers are excluded
// from it — a header states its group's real count whether or not its rows
// are drawn yet.
const visibleGroups = useMemo(() => {
  if (derived.mode !== 'grouped') return []
  let remaining = visibleCount
  return derived.groups.map((group) => {
    const shown = Math.min(group.items.length, remaining)
    remaining -= shown
    return { ...group, visibleItems: group.items.slice(0, shown) }
  })
}, [derived, visibleCount])
```

Every group stays in the output even when it draws no rows, because its header still carries its count. `group.items.length` continues to feed the header, untouched by the cap.

`shown` is `Math.min(length, remaining)` rather than a bare `slice(0, remaining)` only to keep `remaining` honest — `slice` would clamp for us, but `remaining` would then go negative and the next `Math.min` would return a negative `shown`. Explicit clamping keeps the fold readable.

Rank numbers in flat mode are unaffected: `showRanks` numbers from the `.map()` index, and slicing from 0 preserves it.

### D2. Shared budget, not one per group — considered and rejected

Per-group budgets were the first design: give each of the six groups its own 24. Rejected because a viewport shows a handful of rows regardless of how many groups hold content, so that mounts up to 144 rows, nearly all of them off-screen, for no visible benefit over 24. The intuition behind it — "otherwise the user can't reach Completed" — does not hold: reaching a later group means scrolling past every earlier group's *real* rows either way, and the reveal strategy does not change that vertical distance. It only changes how many of those rows are already mounted when the user has not scrolled there yet.

The shared budget does mean a later group can show a header with a real count above an empty list for a moment. That is the same thing the other four pages do when scrolled above their not-yet-revealed rows, and the header's count stays truthful throughout.

### D3. The reset is a render-time identity comparison, not a `useEffect`

`visibleCount` must fall back to `PAGE_SIZE` whenever the result set or its order changes. Without it, removing the debounce (D5) puts back exactly the cost being removed: a user who has scrolled to `visibleCount === 240` and then types pays 240 rows per keystroke.

Following `usePageData.ts:62-66`:

```tsx
// Every input `derived` keys off except `scopedItems` — the list's contents
// changing under an unchanged view (an edit, a settle) must NOT reset the
// reveal, only a change to *which* entries or *what order* should.
const viewIdentity = JSON.stringify([
  query, statusFilters, typeFilter, airingFilter, scoreFilter,
  sort, sortDirection, sortThen, airingStatusFirst, groupByStatus,
])
const [renderedViewIdentity, setRenderedViewIdentity] = useState(viewIdentity)
if (viewIdentity !== renderedViewIdentity) {
  setRenderedViewIdentity(viewIdentity)
  setVisibleCount(PAGE_SIZE)
}
```

Why a string rather than a `useRef` of a tuple: three of the ten values are arrays or nullable arrays (`statusFilters`, `typeFilter`, `airingFilter`) whose identity changes on every set even when the contents match, so a reference comparison would reset spuriously and an element-wise comparison would be a hand-written deep-equal. `JSON.stringify` over ten small scalars-and-short-arrays runs per render on a page that already re-renders per keystroke; it is nothing next to the row walk it exists to bound.

Why not `useEffect`: the effect would run *after* a paint that drew the old `visibleCount` worth of rows against the new filter, then reset and paint again — one wasted full-size render per change, which is the bug. The codebase's two existing instances of this idiom both cite the same reason.

Why `scopedItems` is excluded: it changes when the underlying list data changes (an entry edited, a recap scope resolved). Resetting the reveal there would yank a scrolled-down user back to the top after editing a row.

Calling `setVisibleCount` during render is the supported React pattern for this ("adjusting state when props change"): it re-renders the component immediately, before the browser paints, and the setter here belongs to `useRestorableState`, which writes through to the history snapshot exactly as a click would.

### D4. Not moving the controls onto the URL

The other four pages get this reset for free because every control goes through `setSearchParams`, minting a history entry whose key `useRestorableState` reseeds under. Putting my list's ten controls on the URL would get the same effect with no explicit reset — but it contradicts a decision this page states in a comment (`:143-149`), changes what back/forward means on the page (each filter click becoming a history entry), and is a much larger change than the eight lines in D3. Not taken.

### D5. Remove the debounce rather than keep it

With D1 in place the render is bounded at `PAGE_SIZE` rows, and with D3 a keystroke resets to exactly that. What remains per keystroke is `derived`'s filter and sort over `scopedItems` — a few hundred items, sub-millisecond — plus ≤24 rows of prop diffing. The debounce's only remaining job would be to shave that, at the cost of the field feeling 200 ms behind the list.

Keeping it was considered as cheap insurance. Rejected: it would leave `derived` recomputing on a timer while the render resets immediately, so between a keystroke and the timer the page draws the *old* filtered set capped to 24 — a visible flicker of the wrong rows that does not exist today, because today's reset and recompute are both late. Removing it makes both immediate and keeps them in step.

`useDebouncedValue` itself stays; `hooks/useAnimeSearch.ts:36-37`, `SeasonPage.tsx:291` and `YearPage.tsx:246` still use it. Only MyListPage's import and its one call go.

### D6. `loading="lazy"` on `RowPicture` globally, not behind a prop

`RowPicture` is the one place a row or thumbnail picture is drawn (its own doc comment, `:11-19`), reached from 27 call sites. Adding the attribute there covers my-list rows without threading a prop through `MyListRow`.

All 27 sites were checked for a path needing an eagerly loaded image, rather than assuming my list is the only one that matters. There is none: `grep` for `html2canvas`, `toPng`, `domtoimage` and `screenshot` across `frontend/src` returns nothing, so no export or capture path depends on pictures being present. Every site is an ordinary list, row, dropdown or conditionally-mounted overlay, and a conditionally-mounted overlay's images intersect as soon as it opens.

Compatibility with `useWidePicture` was checked in code, not assumed. `hooks/useLandscapePicture.ts:29-42` attaches a callback ref that takes `node.complete` when true and otherwise adds a `load` listener. A deferred picture is simply not `complete` at attach time, so it takes the listener branch — which is already the cold-cache path on every first visit today. The `artwork-presentation` spec's "Until a picture's proportions are known, its slot SHALL render at the unchanged portrait size" describes exactly that state.

### D7. Reveal, not virtualization

`react-window` or equivalent would keep the DOM bounded even after scrolling the whole list, which reveal does not. Not chosen: a new dependency, a larger change, fixed row heights or a measurement cache, and a page that then solves this problem differently from the four pages beside it. The problem being fixed is first paint and per-keystroke cost, both of which reveal bounds. Worth revisiting only if a list grows large enough that scrolling through all of it becomes the complaint.

## Risks / Trade-offs

- **A group header above no rows looks like a bug.** → It is the shared budget working (D2), and the header's count stays truthful. The same thing is already visible on the other four pages when scrolled above unrevealed rows. Worth a look during the walkthrough to confirm it reads as "more below", not "empty group".
- **Per-keystroke filtering is now unthrottled.** → Bounded by `scopedItems.length` (a personal list) and a ≤24-row render. A list an order of magnitude larger would make `derived` itself the cost the debounce used to hide; D7 is the answer then, not the debounce.
- **`JSON.stringify` on every render.** → Ten small values on a page already re-rendering per keystroke. If it ever showed up, the fallback is a hand-written comparison, not a `useEffect`.
- **`loading="lazy"` reaches 26 call sites this change is not about.** → Checked one by one (D6); none needs eager loading, and `useWidePicture` is unaffected. The risk is a surface nobody scrolls in the walkthrough regressing silently — mitigated by walking the overlays and the search dropdown explicitly in tasks §5.
- **No automated test can pin any of this.** → Stated plainly rather than papered over: there is no frontend test runner, so the nine cases from the request are a manual walkthrough (tasks §5), item for item. Adding vitest + React Testing Library is a separate change, offered but not assumed.
- **The reveal reset interacts with restoration.** → `visibleCount` restores from the history snapshot *and* resets when `viewIdentity` changes. On a restore both the controls and `visibleCount` reseed from the same snapshot in the same render, so `viewIdentity` matches what was stored and no reset fires. Worth confirming in the walkthrough (task 5.8) since it is the one place the two mechanisms meet.

## Migration Plan

None. Frontend-only, no data, no API, no migration, no flag. `visibleCount` joins the page's existing `useRestorableState` keys; a history snapshot written before this change simply lacks that key and falls back to `PAGE_SIZE`, which is the fresh-visit value anyway. Rollback is reverting the commit.

## Open Questions

None blocking. One offer standing: whether to add a frontend test harness (vitest + React Testing Library + jsdom) as a follow-up change, which would let the nine cases in tasks §5 become real tests rather than a walkthrough.
