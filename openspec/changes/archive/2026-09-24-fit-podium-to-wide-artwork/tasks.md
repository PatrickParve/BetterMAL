## 1. PosterPicture

- [x] 1.1 `frontend/src/components/PosterPicture.tsx`: add `noFill?: boolean` to `PosterPictureProps`, with a doc comment in the style of `whole`'s. Change the fill's mount condition to `!whole && !noFill && shape !== 'poster'` (design D1). Leave the wrapper classes, the art `<img>` and the placeholder branch as they are.
- [x] 1.2 Same file, header comment: add the podium's `noFill` case to the paragraph on the blurred fill (no fill for any shape there; an upright or wide picture sits whole and centred on the card itself).

## 2. Recap podium

- [x] 2.1 `frontend/src/pages/RecapPage.tsx` `renderPodiumCard`: pass `noFill` to the podium's `PosterPicture`. Make no other change to the card.
- [x] 2.2 `frontend/src/pages/RecapPage.css`: after `.recap-podium__picture`, add `.recap-podium__picture.poster-picture--upright, .recap-podium__picture.poster-picture--wide { background: none }` (design D1), and update the comment above `.recap-podium__picture` to say a non-poster picture sits whole and centred on the card with no fill and no box. Change no sizing rule: the frame, wrapper and art keep the poster box, so no card changes size. A poster and the placeholder must match neither selector.
- [x] 2.3 `frontend/src/pages/RecapPage.css`, `.recap-podium__card--gold::after`, and `frontend/src/components/ScoreBoardOverlay.css`, `.score-board__slot--apex .score-board__slot-header::after` (the score board's 10 tier has the same wave): replace the second sheen layer's linear band, whose hard top edge drew a line across the gold card as it drifted, with a radial gradient that fades out at its top and sides. Keep the tile sizes and the 20s loop, so the sheen still repeats seamlessly. The Top anime page's wave already uses full-height layers and is not touched.
- [x] 2.4 Same file, after `.recap-podium__card--bronze`: give ranks 4 and 5 arrival delays of 240ms and 320ms (`:nth-child(4)` and `:nth-child(5)`, since both share the `--plain` class), continuing the 80ms step from the medals so all five cards arrive in rank order.

## 3. Verification

- [x] 3.1 `npm run lint` and `npm run build` in `frontend/` (the build needs nvm's Node 22, not the default v16).
- [x] 3.2 By eye on a recap whose top five include a landscape (about 16:9), a square, an upright (about 4:5), a poster and a very wide (about 21:9) picture. The landscape, square, very wide and upright cards show the whole picture, unstretched and centred, with no blurred bands and no dark or white box behind it. The poster card is unchanged. Badges, titles, score boxes (all the same size) and the gold card's sheen are unchanged, and the sheen stays off the artwork.
- [x] 3.3 Heights: every card is exactly the height it has in a podium of five posters (first tallest, then second, then third, and the fourth and fifth equal), and every card's lower edge is on one line. Check the 5-across desktop width, the ≤900px 3+2 layout and the ≤560px single column, in Chromium and in WebKit, with nothing cropped and no sideways scroll.
- [x] 3.4 Load pass: with the browser cache disabled, open a recap whose first-ranked picture is landscape. Nothing changes size or moves when the picture loads. Reload with the cache enabled and change the ranking basis: the picture is drawn bare straight away, and the arrival animation still runs.
- [x] 3.5 Hover and motion pass: hover a wide card and check that the glow matches a poster card's. With reduced motion switched on, the podium shows no motion and remains fully legible.
- [x] 3.6 Unchanged pass: the recap score board's tiles still show a landscape picture inside the poster box over the blurred fill. Only the podium passes `noFill`, so no other surface's code path changes.
- [x] 3.7 Both themes: in the light and dark themes, a non-poster podium card's picture sits cleanly on the card with no box, fringe or halo around it.
- [x] 3.8 Sheen lines: on the gold podium card and on the score board's 10 header, no straight horizontal edge remains in the sheen at any point of its loop (the largest row-to-row brightness step across the score board header is under 0.4 levels, against 3.5 to 5.5 before), in both themes and in Chromium and WebKit, and the drift is still visible at normal contrast.
- [x] 3.9 Arrival stagger: on first open and after switching the ranking basis, cards 1 to 5 begin to appear in rank order, 80ms apart, and with reduced motion none animate.
