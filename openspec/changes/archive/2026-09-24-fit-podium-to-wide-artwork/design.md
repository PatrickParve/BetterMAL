## Context

The recap podium (`RecapPage.tsx` `renderPodiumCard`) draws each top-five card's picture through the shared leaf `PosterPicture`, inside `.recap-podium__picture-frame`. The frame is a portrait box, `padding-top: calc(368 / 260 * 100%)` of the card's picture width, and `.recap-podium__picture` (the `PosterPicture` wrapper) is absolutely positioned to fill it. The podium's silhouette comes from that box: card widths step down across the row and each card's height follows its width, bottom-aligned.

`PosterPicture` classifies the loaded art with `usePictureShape` and puts `poster-picture--{poster|upright|wide}` on its wrapper. For an upright or wide picture it mounts a blurred `poster-picture__fill` copy and sets the art to `object-fit: contain`. So today a landscape top-five picture is drawn as a thin strip across the card with blurred bands above and below it. The podium's wrapper also carries an opaque `var(--bg)` background, which shows as a dark or white block wherever a picture does not cover it.

A host can already opt out of the fill with a prop: the airing banner box does with `whole`, which draws every shape whole on the box's own flat background.

## Goals / Non-Goals

**Goals:**

- A square, landscape or upright podium picture is drawn whole and centred in the card's picture area, not stretched or cropped, with no blurred fill and no box behind it.
- Every card keeps exactly the size it has with a poster, so the first is tallest, then the second, then the third, and the fourth and fifth are equal.
- Posters and placeholders on the podium render exactly as today.
- No other `PosterPicture` host changes.

**Non-Goals:**

- The recap score board's tiles, the Top anime cards and showcase, the profile strips, the browse grids and the carousel.
- The shape classification (`POSTER_MAX_RATIO`, `pictureShapeOf`), and fetching or storing picture dimensions.

## Decisions

### D1 — A `noFill` prop on `PosterPicture` controls the fill; one CSS rule clears the box

`PosterPicture` gains `noFill?: boolean`: mount no blurred fill for any shape. The fill condition becomes:

`!whole && !noFill && shape !== 'poster'`

The wrapper markup and classes and `PosterPicture.css` stay as they are. A non-poster picture already gets `object-fit: contain`, which centres it without stretching or cropping. What is left is the wrapper's own `var(--bg)` background, so `RecapPage.css` adds one rule: `.recap-podium__picture.poster-picture--upright, .recap-podium__picture.poster-picture--wide { background: none }`. The picture then sits on the card's own surface. A poster keeps the opaque background, which it covers entirely. The podium's frame, wrapper and art sizing rules are untouched, so no card can change size.

The picture stays opaque, so the first-ranked card's sheen, which passes behind the link, still never lights the artwork. It now shows around the picture, which the spec allows.

*Alternative rejected:* `whole`, the banner box's prop. It would draw a poster whole too, and a poster must keep filling the box exactly as it does today.

*Alternative rejected:* hiding the fill from the host's CSS (`display: none` on `.poster-picture__fill`) with no prop. The element would still be mounted, which runs against the spec's "no fill mounted".

*Alternative rejected:* keeping the wrapper's background and having the picture sit in a flat box. The user rejected it: they want just the image in the card, with no blocks.

### D2 — Cards keep their poster size; the picture does not set the card's height

An earlier version of this change took the opposite route: a wide picture took its own height at the card's width, and its card got shorter by the height the bands used to take. The user rejected it, because they want the podium's heights to stay as they are with posters, whatever the art's shape. The card's height therefore stays a function of its column width alone, and a wide picture is fitted inside the box rather than the box being fitted to the picture.

This also means nothing about the box's size depends on the picture, so nothing shifts when a picture loads.

### D3 — Only the podium changes

The score board's tiles are also recap cards, but they sit in a strip of equal tiles in the fixed-poster-box family, under the fill rule. The user named the top-five cards, so the podium alone opts out of the fill. Every other host keeps its current treatment.

## Risks / Trade-offs

- [Empty space above and below a wide picture] The picture is drawn at the card's picture width, so a 16:9 picture leaves about 40% of the picture area empty, split above and below. → This is the trade for both fixed card heights and no box or blur. The card's own tinted surface shows there.
- [Very wide art gets short] A 21:9 picture on the narrowest desktop card (#5) is only about 55px tall inside an area about 180px tall. → The picture is still whole and at its true proportions, which the whole-picture rule requires.
- [Before the picture loads] A card starts as a poster-shaped box with the opaque background and takes the bare treatment once the picture is known not to be a poster. → The box is only visible while the picture is loading, and the card's size does not change.
- [Square corners] The art is not clipped to the wrapper's rounded corners, since a centred picture does not reach them. → A wide picture has square corners, as its own art does.
