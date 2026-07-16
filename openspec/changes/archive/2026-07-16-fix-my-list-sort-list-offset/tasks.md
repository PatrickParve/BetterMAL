## 1. Implementation

- [x] 1.1 In `frontend/src/pages/MyListPage.tsx`, replace the ranked branch's React fragment (`<>…</>` around the `my-list-page__group-header` div and the `my-list-page__list` ul) with `<section className="my-list-page__group">`, matching the grouped branch's wrapper.
- [x] 1.2 Confirm no CSS change is needed: `.my-list-page__group` stays unstyled, and header-to-list spacing comes from the existing `.my-list-page__group-header { margin: 0 0 8px }`. Do not add a negative margin or a ranked-only class.
- [x] 1.3 Confirm nothing else in the ranked branch changed — sorting, rank numbers, the active-filter header text, and `renderSortControls()` stay as they are.

## 2. Verification

- [x] 2.1 Run `npm run lint` and `npm run build` in `frontend/` using node v22 (`nvm use 22`; the default node is v16 and Vite will fail on it).
- [x] 2.2 Start the dev server and open My list with the All filter. Switch sort between Alphabetical, MAL score, and My score, and confirm the first entry row does not move vertically as the sort changes.
- [x] 2.3 Repeat 2.2 on at least one filtered tab (e.g. Completed) and on Plan to watch including the Airing status sort, confirming the list stays level in every case.
- [x] 2.4 Confirm grouped view is unregressed: in Alphabetical + All, each status group's header still sits directly above its list, and consecutive groups are still separated by the page's 20px section spacing.
