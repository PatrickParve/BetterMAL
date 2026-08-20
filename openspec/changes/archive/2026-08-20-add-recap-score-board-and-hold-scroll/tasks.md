## 1. Scroll hold

- [x] 1.1 In `frontend/src/hooks/useScrollRestoration.ts`, read `location.state` as `{ keepScroll?: boolean } | null` and skip the fresh-visit `window.scrollTo(0, 0)` when the flag is set, leaving the POP-with-snapshot restore branch untouched (design decision 7).
- [x] 1.2 In that same skip branch, seed the arriving entry with `pageStateStore.putScroll(keyRef.current, window.scrollY)` before returning, so a held position is remembered for the entry rather than left as the fresh snapshot's `0` (design decision 8, spec scenario "Coming back to a held position").
- [x] 1.3 Add the flag to the effect's dependency list alongside `location.key`, and comment the contract at the hook: the flag is opt-in per navigation, the seeding lives in the hook so every future user of it gets it, and a POP carrying the flag is unaffected (restore branch wins; a POP with no snapshot is a fresh document already at 0).
- [x] 1.4 In `RecapPage.tsx`, give `updateParams` a second `options?: { keepScroll?: boolean }` argument that forwards `{ state: { keepScroll: true } }` to `setSearchParams`, leaving the existing single-argument call sites byte-identical.
- [x] 1.5 Pass `{ keepScroll: true }` from the ranking-basis toggle's two buttons and from the media-type `<select>` only — not from the mode tabs, period stepper/selects, time filter, or the availability fallback effect (spec scenario "Changing the period still returns to the top").
- [x] 1.6 Verify by hand: scroll to the top 10, switch basis and type — page holds; step the year, switch mode, switch the time filter — page returns to the top; `TopAnimePage` paging and type switching still return to the top.
- [x] 1.7 Verify the return path: switch basis partway down, open an anime from the top 10, press back — the recap restores at the held position, not at the top; and press back again to confirm the previous basis is restored.

## 2. Tier colour tokens

- [x] 2.1 In `frontend/src/index.css`, add `--tier-apex`, `--tier-apex-bg`, `--tier-apex-border` and the matching `--tier-red-*` and `--tier-blue-*` triples to `:root`, placed after the `--medal-*` block, following the existing `colour / rgba(…, 0.12) / rgba(…, 0.45)` shape (design decision 3).
- [x] 2.2 Add `--tier-apex-sheen` — the near-white the apex gradient mixes with — beside them, with its own dark-theme value.
- [x] 2.3 Add dark-theme values for all ten tokens in the existing `@media (prefers-color-scheme: dark)` block, in the same position.
- [x] 2.4 Choose `--tier-blue` distinctly cooler/cyan-leaning than `--mal` (#2563eb) and comment why (design decision 4); do **not** add gold/silver/bronze tiers — the board aliases `--medal-gold|silver|bronze`, which already solve the light-theme silver problem.
- [x] 2.5 Check every tier's colour and tint against `--bg` and `--code-bg` in both themes: legible numeral on its own tint, six tiers tellable apart, apex distinguishable from a selected `.recap-page__tab`, `--tier-blue` distinguishable from `--mal` (spec scenario "Both themes").

## 3. One grouping pass

- [x] 3.1 In `RecapPage.tsx`, replace `scoreBucketsOf(items)` with `scoreGroupsOf(items): { score: number; items: RecapRowDto[] }[]` — the same ten ascending slots, skipping `myScore == null`, entries within a slot sorted by `title.localeCompare` (design decisions 1 and 2).
- [x] 3.2 Keep the existing `useMemo` shape: memoise the groups off `recap`, and derive the distribution's buckets from them as `groups.map((g) => ({ score: g.score, count: g.items.length }))` so `<ScoreDistribution>` receives exactly the `ScoreDistributionBucketDto[]` it does today.
- [x] 3.3 Carry the comment that already explains the selection rule (every included entry of the period, not the top 10's media-type-narrowed set) onto the new function, and note that the board and the distribution now share it by construction (spec scenario "The board and the distribution agree").

## 4. The overlay component

- [x] 4.1 Create `frontend/src/components/ScoreBoardOverlay.tsx` taking `{ title, groups, onClose }`, wrapping `<Modal className="modal--board" labelledBy="score-board-title">` with a heading and a total ("N anime scored") subtitle, and a close button — mirroring `RankingOverlay`'s structure (design decision 5).
- [x] 4.2 Render the ten slots high-to-low by reversing the ascending groups, each as a `<section>` with a header carrying the score numeral and the slot's count, and a poster grid beneath (spec: "The score board lays a period out by score").
- [x] 4.3 Add a local `scoreTier(score)` returning `'apex' | 'red' | 'blue' | 'gold' | 'silver' | 'bronze'` for 10 / 9 / 8 / 7 / 6–5 / 4–1, applied as a `score-board__slot--<tier>` modifier (design decision 3).
- [x] 4.4 Render every slot including empty ones, with an explicit "Nothing scored N" note in an empty slot so it cannot read as still loading (spec scenario "A slot nothing was scored").
- [x] 4.5 Render each poster as a `<Link to={/anime/:id}>` with `aria-label` set to `pickDisplayTitle(...)`, an `alt=""` `<img loading="lazy">`, and the same placeholder `<div>` treatment the top-10 rows use when `pictureUrl` is null; call `onClose` on click, as `RankingOverlay`'s rows do.
- [x] 4.6 Create `frontend/src/components/ScoreBoardOverlay.css`: slot layout, the per-tier `--tier`/`--tier-bg`/`--tier-border` aliases, a `position: sticky` slot header within the modal's scroll container, and a `repeat(auto-fill, minmax(52px, …))` poster grid with `aspect-ratio: 2 / 3` tiles.
- [x] 4.7 Draw the apex slot's rail and numeral as `linear-gradient(135deg, var(--tier-apex), var(--tier-apex-sheen) 50%, var(--tier-apex))` with a slow sheen sweep in the podium's motion vocabulary, and add the `@media (prefers-reduced-motion: reduce)` block switching the sweep and the poster lift off (design decision 3, spec scenario "Reduced motion").
- [x] 4.8 In `frontend/src/components/Modal.css`, add `.modal--board { max-width: min(1080px, 100%); }` beside `.modal--wide`, leaving `.modal` and `.modal--wide` untouched.

## 5. The poster hover card

- [x] 5.1 In `ScoreBoardOverlay.tsx`, add a portalled card following `TruncatedTitle`'s pattern: `createPortal(…, document.body)`, `position: fixed`, positioned in a `useLayoutEffect` so its first painted frame is already clamped to the viewport (design decision 6 — the modal is a scroll container in both axes, so an in-flow card would be clipped).
- [x] 5.2 Anchor it to the tile's `getBoundingClientRect()` rather than to the pointer, so `onFocus`/`onBlur` can show the same card a keyboard user needs; prefer placement above the tile, flip below when there is no room, then clamp on both axes with the same viewport margin `TruncatedTitle` uses.
- [x] 5.3 Fill the card with `pickDisplayTitle(...)`, the slot's score, and `mediaTypeLabel(item.mediaType)`. Do **not** render a MAL score: `ScoreValue`'s hidden state is an interactive reveal button and the card is `pointer-events: none` (design decision 6).
- [x] 5.4 Give the card `pointer-events: none` and a z-index in the same band as `.truncated-title__tooltip` (1000), so it clears `.modal-backdrop`'s 100 and never fights the tile it describes.
- [x] 5.5 Add the poster lift on `:hover`/`:focus-visible` as a compositor-only `transform`, with the tile's own stacking raised so a lifted poster is not overlapped by its neighbours.
- [x] 5.6 Verify at the board's edges: first slot, last slot, leftmost and rightmost tile of a full row — the whole card visible in every case (spec scenario "A poster at the edge of the board").

## 6. Wiring into the recap

- [x] 6.1 In `RecapPage.tsx`, add a `boardOpen` boolean beside the existing `overlay` state, and render `{boardOpen && <ScoreBoardOverlay … onClose={() => setBoardOpen(false)} />}` beside the existing `{overlay && <RankingOverlay …/>}` (design decision 5).
- [x] 6.2 Wrap `renderDistribution`'s heading in the existing `.recap-page__section-header` and add the "Score board" button on its right, styled like the section's other controls; leave the `<ScoreDistribution>` call and its `hrefForScore` untouched.
- [x] 6.3 Disable the button with an explanatory `title` when the period has no scored entries (total across the groups is zero) rather than hiding it, following the time filter's disabled idiom (spec scenario "A period with nothing scored").
- [x] 6.4 Title the board for its period — the same `periodLabel` the page heading uses — so an opened board names what it is showing.
- [x] 6.5 Add any needed `.recap-page__section-header` spacing for the distribution section in `RecapPage.css`, checking the lead grid's narrow aside column and the sub-1024px collapse where the aside goes full width.

## 7. Verification

- [x] 7.1 Run `npm run lint` and `npm run build` in `frontend/` under Node 22 (`nvm use 22` — the default Node 16 cannot run Vite).
- [x] 7.2 Check a slot's tile count against the matching distribution row's count, on a season recap, a yearly recap under each time filter, and a multi-year recap (spec scenario "The board and the distribution agree").
- [x] 7.3 Narrow the top 10 to one media type, reopen the board, and confirm every included anime is still placed (spec scenario "The media-type control does not narrow the board").
- [x] 7.4 Confirm an unscored included anime appears in no slot, and that an anime with no poster art still occupies a placeholder tile counted in its slot.
- [x] 7.5 Check Escape, backdrop click, and the close button; confirm the recap behind the board does not move and is at the same scroll position after closing (spec: "The score board is dismissible…").
- [x] 7.6 Open the board on a large multi-year period: it scrolls within itself, slot headers stay identifiable while scrolling, and off-screen posters do not fetch until scrolled to.
- [x] 7.7 Tab through a slot: every poster focusable, focus ring visible, the card shows on focus, and following one opens the anime page with the board closed.
- [x] 7.8 Repeat the board and the podium checks with reduced motion switched on at the OS level: no sweep, no lift, cards still shown, everything still followable.
- [x] 7.9 Re-check the spec-covered behaviour the change sits beside: distribution rows still drill through to my list with their counts agreeing, the top 10's basis and type controls still re-rank exactly as before, and back/forward restore on other pages is unaffected.
