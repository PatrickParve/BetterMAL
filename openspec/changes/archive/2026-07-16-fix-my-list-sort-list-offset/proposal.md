## Why

On the My list page, every sort other than Alphabetical pushes the list rows about 20px further down the page than the grouped (alphabetical) view does, so switching sort makes the list visibly jump. It happens under every status filter — All, Completed, Watching, and the rest — because the ranked rendering path is shared by all of them. The header line itself stays put, so the gap between a status title and its first row is inconsistent between the two view modes, which reads as a rendering glitch rather than a deliberate layout.

## What Changes

- Ranked view (sort = MAL score, my score, or airing status) renders its header and list inside the same single-element group wrapper the grouped view already uses, so the two modes share one spacing rule.
- The vertical distance from a status header to its first row becomes identical in both modes; toggling sort no longer shifts the list down.
- No change to sorting, grouping, ranking, filtering, rank numbers, or the quick-filter control's placement or contents.

## Capabilities

### New Capabilities

None. This is a layout defect fix within an existing capability.

### Modified Capabilities

- `library-views`: Adds a requirement that the My list ranked view and grouped view place the list at the same vertical offset below its status header, so changing sort does not shift the list. Existing requirements for grouping, rank numbers, filter tabs, and quick-filter placement are unchanged.

## Impact

- `frontend/src/pages/MyListPage.tsx` — the ranked branch's React fragment becomes a `<section class="my-list-page__group">` wrapper, matching the grouped branch.
- `frontend/src/pages/MyListPage.css` — possible explicit rule for `.my-list-page__group` to pin the contract rather than leaning on it being an unstyled block.
- Frontend-only, presentational. No backend, API, data, or dependency impact. No breaking changes.
