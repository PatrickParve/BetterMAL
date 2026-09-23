## 1. PosterPicture whole mode

- [x] 1.1 `frontend/src/components/PosterPicture.tsx`: add an optional `whole?: boolean` prop. With `whole`, add `poster-picture--whole` to the wrapper and mount no fill for any shape. Without it, keep today's `shape !== 'poster'` condition exactly, and keep the `usePictureShape` hook (design D2). Extend the leaf comment: a banner box draws every shape whole on its own flat background and mounts no fill.
- [x] 1.2 `PosterPicture.css`: add `.poster-picture--whole > .poster-picture__art { object-fit: contain; }` with a short comment naming the banner box and that no fill is mounted for it. Nothing else changes for existing hosts.

## 2. Airing slot card markup

- [x] 2.1 `frontend/src/pages/AiringPage.tsx`: replace the slot body with, in this DOM order inside the `Link`: the time (`airing-slot__time`), the band (`<PosterPicture src={slot.pictureUrl} className="airing-slot__art" whole loading="lazy" />`), the title (`airing-slot__title`), and the episode pill (`airing-slot__episode`). DOM order keeps the accessible name reading time, title, episode (design D4). Remove the `RowPicture` import and the `__body` / `__info` wrappers. Keep `formatEpisodeLabel`, `pickDisplayTitle`, the `key` and the link target unchanged.
- [x] 2.2 Add a comment at the slot saying it is a card with a banner-box picture above the title, and why: a seventh-of-a-page column can't share its width between a picture and its text. Note that time and episode share a header strip above the band, never over the picture.

## 3. Airing slot card styles

- [x] 3.1 `AiringPage.css`: make `.airing-day__slots > li` `container-type: inline-size` (design D3). Its width always comes from its parent, never its content.
- [x] 3.2 Restyle `.airing-slot` as a grid: `grid-template-columns: auto minmax(0, 1fr)`, `grid-template-areas: "time episode" "band band" "title title"`, no padding, and the existing 1px border, 8px radius, hover and focus-visible rules kept (design D4). Define `--strip-x`, `--strip-mid` and `--strip-y` on it.
- [x] 3.3 Add `.airing-slot__art`: `grid-area: band`, `height: clamp(64px, 56.25cqi, 96px)`, `background: var(--code-bg)`. No radius, since the band is flush with the strip and the title. The no-picture `div` carries `.airing-slot__art` as well as its `--placeholder` modifier, so it gets the same box with no separate rule.
- [x] 3.4 Restyle `.airing-slot__time` and `.airing-slot__episode` as the header strip. Time: `grid-area: time`, `align-self: center`, muted 12px text with tabular numerals, margins from the custom properties. Episode: `grid-area: episode`, `align-self: center; justify-self: end`, the plain pill (`var(--code-bg)`, 12px, `padding: 1px 7px`, fully rounded), `box-sizing: border-box`, `max-width: calc(100% - var(--strip-x) - var(--strip-mid))`, `nowrap` plus `overflow: hidden` plus ellipsis. Add a comment that `border-box` with the max-width is what keeps the label inside the card.
- [x] 3.5 Add a `@container (max-width: 120px)` rule that tightens the strip: `--strip-x: 5px`, `--strip-mid: 2px`, `--strip-y: 5px`, 11px text, and `padding: 1px 5px` on the pill. Comment that the narrowest seven-column card is about 95px wide, where the default strip would squeeze even `Ep 12` to an ellipsis.
- [x] 3.6 Restyle `.airing-slot__title`: `grid-area: title`, `min-width: 0`, `padding: 8px 10px 0` with an 8px bottom margin (bottom padding would show a clamped third line), 13px, keeping the two-line clamp, `line-height: 1.3` and the two-line `min-height`.
- [x] 3.7 Delete the now-unused `.airing-slot__body`, `.airing-slot__thumb`, `.airing-slot__thumb--placeholder` and `.airing-slot__info` rules.
- [x] 3.8 In the existing `@media (max-width: 800px)` block, set `.airing-page__grid` to `align-items: stretch` so each day spans the page, and make `.airing-day__slots` `display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr))`, keeping its 6px gap, with a comment on why it's `auto-fill` and not `auto-fit` (design D5).

## 4. Verification

- [x] 4.1 Type-check, lint and build the frontend with Node 22 (`nvm use 22`; the default Node 16 can't run Vite): `npm run lint` and `npm run build` in `frontend/`. Both must pass clean.
- [x] 4.2 Check by eye on a week holding a poster, a 16:9 landscape picture, a square picture, a very wide banner picture, a slot with no picture, an `Ep —` slot and a long merged range, and on the real week. Check at viewport widths of 801, 1024, 1280, 1440 and 1920px. Every slot in the week is the same size. Every picture is whole, posters included, on the band's flat background with no fill. The title spans the card width on two reserved lines. The strip stays inside the card, nothing is drawn over a picture, and a long range ends in an ellipsis rather than past the edge. Nothing shifts as pictures load.
- [x] 4.3 Check by eye at 800px and below (for example 768px and 375px): each day's cards tile in equal tracks that wrap, a day with one slot doesn't stretch it, and nothing scrolls sideways.
- [x] 4.4 Check by eye in the light and dark themes: the strip and pill read clearly, the flat band suits both, and the hover, focus ring and today header look as they did.
- [x] 4.5 Check the default `PosterPicture` path is untouched: compare the Season, Year, Top, Recap, Profile, Series and Home pages against an export of `HEAD`, pixel for pixel. Also check the dashboard's "Airing today" list is unchanged.
