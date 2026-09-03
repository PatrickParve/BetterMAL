## 1. Shared foundation

- [x] 1.1 In `frontend/src/hooks/useLandscapePicture.ts`, factor the existing hook's body onto a shared internal implementation taking the comparison to apply, and export a second hook — `useWidePicture(src)` — that tests `naturalWidth >= naturalHeight`. `useLandscapePicture`'s own behaviour, signature, and `>` comparison stay exactly as they are; the four surfaces using it (detail page, series header, timeline cards, More tiles) must be untouched (design D4).
- [x] 1.2 Add `frontend/src/components/RowPicture.tsx`: props `src: string | null | undefined`, `className: string`, optional `title` and `placeholderClassName`. With a `src` it renders `<img ref={wideRef} src alt="" title className={"row-picture" + (isWide ? " row-picture--wide" : "") + " " + className}>`; without one it renders `<div aria-hidden="true" className={"row-picture row-picture--placeholder " + className + " " + (placeholderClassName ?? className + "--placeholder")}>`. Comment why the state lives in this leaf rather than in the host components (design D1).
- [x] 1.3 Add `frontend/src/components/RowPicture.css` with the shared geometry from design D2 — `.row-picture` (`width: var(--row-picture-w)`, `height: var(--row-picture-h)`, `object-fit: cover`, `background: var(--code-bg)`, `flex: 0 0 auto`, `display: block`), `.row-picture--wide` (`width: auto`, `max-width: calc(var(--row-picture-h) * 16 / 9)`, `object-fit: contain`), and `.row-picture--placeholder`. Comment the cap's derivation-from-height and why it is a `calc()` of pixels rather than a percentage (the WebKit flex-basis trap `UpdateCard.css` documents).

## 2. The updates control's mark

- [x] 2.1 In `frontend/src/components/Updates/UpdatesMenu.tsx`, add `updates-menu__button--open` to the button's class while `open || historyOpen` — so following **History** keeps it marked (design D6). Leave `aria-expanded`, the accessible name, and the unseen dot exactly as they are.
- [x] 2.2 In `UpdatesMenu.css`, style `--open` with `.navbar__link--active`'s declarations (`color: var(--text-h)`, `background: var(--accent-bg)`, `border-color: var(--accent)`), and add the `.updates-menu__button--open:hover { border-color: var(--accent) }` companion rule, with the same specificity note `Navbar.css` already carries twice.
- [x] 2.3 Check by eye: opening the dropdown marks the bell; hovering it keeps the mark; following History keeps it; closing either clears it; the dot still appears and clears on its own rules.

## 3. The three reported surfaces

- [x] 3.1 `MyListRow.tsx` — render the picture through `RowPicture` (`className="my-list-row__picture"`). In `MyListPage.css`, replace `.my-list-row__picture`'s `width`/`height`/`object-fit`/`background`/`flex` with `--row-picture-w: 52px; --row-picture-h: 72px`, keeping the placeholder's border rule. Replace the stale "fixed, not aspect-ratio" comment with one pointing at the shared rule.
- [x] 3.2 `ProfilePage.tsx` — both the divergence rows and the Latest updates rows go through `RowPicture` (`className="profile-list-row__picture"`). In `ProfilePage.css`, `.profile-list-row__picture` sets `--row-picture-w: 41px; --row-picture-h: 56px`, and the `.activity-feed .profile-list-row__picture` override sets `--row-picture-h: var(--activity-row-h); --row-picture-w: calc(var(--activity-row-h) * 41 / 56)` instead of `height`/`width`.
- [x] 3.3 `EditHistoryOverlay.tsx` / `.css` — same treatment, `--row-picture-w: 57px; --row-picture-h: 78px`.
- [x] 3.4 Check by eye, with an anime whose chosen picture is landscape: it is whole and landscape on My List, in Latest updates, and in Full history; row heights are unchanged; the Latest updates feed still shows five whole rows and the history overlay's rows are unchanged in height.

## 4. Remaining rows and overlays

- [x] 4.1 `TopAnimePage.tsx` / `.css` — the rank 11+ rows only (`.top-anime-row__picture`, `--row-picture-w: 52px; --row-picture-h: 72px`). Leave the showcase and card grid alone; they are out of scope by design.
- [x] 4.2 `UnresolvedEpisodesOverlay.tsx` / `.css` — `57px` / `78px`.
- [x] 4.3 `AnimeRankOverlay.tsx` / `.css` — `32px` / `44px`. Verify the drag-reorder hit-testing still behaves: the dragged row's clone is `pointer-events: none` and its picture's width is now variable.
- [x] 4.4 `RecapPage.tsx` / `.css` — hot-take rows and top-ten rows, both `40px` / `56px`.
- [x] 4.5 `AiringPage.tsx` / `.css` — `.airing-slot__thumb`, `52px` / `72px`.
- [x] 4.6 `AiringTodayList.tsx` / `.css` — `.airing-today__thumb` currently sizes itself with `width: 72px; aspect-ratio: 2 / 3`; it becomes `--row-picture-w: 72px; --row-picture-h: 108px` with the `aspect-ratio` dropped, which is the same box.
- [x] 4.7 `RelatedAnimeOverlay.tsx` / `.css` — `40px` / `56px`.
- [x] 4.8 `CompletionScoreOverlay.tsx` / `.css` — `96px` / `136px`, keeping its own `border-radius: 6px`.

## 5. Ranking poster clusters

- [x] 5.1 `RankingOverlay.tsx` — give the poster `<img>` a real class (`ranking-overlay__poster`) and render it through `RowPicture`, passing `title={p.title}` and `placeholderClassName="ranking-overlay__poster-placeholder"` since that placeholder does not follow the `--placeholder` convention. In `.css`, replace the `.ranking-overlay__posters img` rule with `.ranking-overlay__poster { --row-picture-w: 28px; --row-picture-h: var(--ranking-poster-h) }`.
- [x] 5.2 `RankingSection.tsx` / `.css` — same for `.recap-ranking-row__poster`: `--row-picture-w: 30px; --row-picture-h: var(--ranking-poster-h)`.
- [x] 5.3 Confirm no row or overlay height moved: `--ranking-row-outer-h` is still derived from `--ranking-poster-h`, the "See all" overlay still opens on exactly eight whole rows, and a row with fewer posters still stands the same height as one with three.

## 6. Dropdown thumbnails

- [x] 6.1 `SearchBar.tsx` / `.css` — both result thumbnails (local and live) through `RowPicture`, keeping the existing `{result.pictureUrl && …}` guard so no placeholder path is introduced. `--row-picture-w: 28px; --row-picture-h: 40px`.
- [x] 6.2 `SettingsPage.tsx` / `.css` — the difference rows (`.settings-diff-row__picture`, `32px` / `44px`), and the refresh-picker results, which need a class of their own in place of the `.settings-refresh-picker__dropdown img` selector (`24px` / `32px`, same `{pictureUrl && …}` guard).

## 7. Verification

- [x] 7.1 `npm run lint` and `npm run build` in `frontend/` (build needs nvm's Node 22, not the default v16).
- [x] 7.2 Grep for leftovers: no `object-fit: cover` remains on any selector named in the proposal's surface list, and every `--row-picture-w` has a `--row-picture-h` beside it.
- [x] 7.3 With an anime carrying landscape artwork and one carrying square artwork, walk the surface list from the proposal and confirm each draws the picture whole, at the row's height, and that no row, list, or overlay changed height. Confirm a list of ordinary portrait posters is visually identical to `main`.
- [x] 7.4 Confirm the excluded grids still crop and still tile uniformly: the season/year/search/browse card grid, the Top Anime showcase and card row, the recap podium and score board, and the profile's three poster strips.
