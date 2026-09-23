## Why

The recap podium's five cards are fixed poster boxes. When a top-five anime's picture is square, landscape or upright, `uncrop-artwork-everywhere` draws it whole inside the tall portrait box, and blurred copies of the picture fill the space above and below it. The user does not want that on a podium card: no blur, no box behind the picture, and no stretching, just the picture in the middle of the card. They also want the podium's silhouette kept exactly as it is: the first card tallest, then the second, then the third, and the fourth and fifth the same height, whatever shape each picture is.

## What Changes

- **A picture that is not a poster is drawn whole and centred in the card, with nothing behind it.** It has no blurred fill and no box of its own colour behind it, and it is not stretched or cropped. Only the card's own surface shows around it.
- **Card sizes do not change.** Every card is the size it would be with a poster. The podium still steps down from first to third by column width and height, and the fourth and fifth are the same size.
- **The sheens no longer draw a line.** On the gold podium card and on the score board's 10 header, one of the two drifting gradient layers ended in a hard horizontal edge, which showed as a line across the surface. It now fades out instead, so the drift is smooth.
- **All five cards arrive in turn.** The arrival animation staggers ranks 1 to 3 by 80ms each; ranks 4 and 5 now continue the sequence at 240ms and 320ms instead of appearing with the first card.
- **A poster and the placeholder are unchanged.** A poster fills its card exactly as today, and the placeholder for a missing picture keeps the poster-sized box. Until a picture has loaded, its card shows the poster box, as on every other surface.

**Deliberately unchanged:** the recap score board's tiles, the Top anime rank 4–10 cards, the "Currently watching" carousel and every other fixed poster box keep their blurred fill. The Top anime showcase, the airing banner boxes, column grids and row picture slots are untouched too.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `artwork-presentation`: the leftover-space rule gains a narrow exception. The recap podium mounts no fill for any shape, and a picture that is not a poster is drawn whole and centred on the card itself, with no box behind it.
- `list-recaps`: the podium's picture bullet says how a picture that is not a poster is drawn, with the card's size unchanged. New scenarios cover a landscape picture, a square picture and an upright picture.

## Impact

**Frontend only.** No backend, API, DTO, schema or dependency change, and nothing to migrate.

- `frontend/src/components/PosterPicture.tsx`: a new opt-in prop, `noFill`, through which a host says no shape gets a fill. Every other host's behaviour is unchanged.
- `frontend/src/pages/RecapPage.tsx`: the podium's `PosterPicture` passes that prop.
- `frontend/src/pages/RecapPage.css`: a rule drops the picture wrapper's background for an upright or wide picture, the comment on `.recap-podium__picture` is updated, the gold card's sheen gradient is smoothed, and ranks 4 and 5 get arrival delays. No sizing rule changes.
- `frontend/src/components/ScoreBoardOverlay.css`: the 10 header's sheen gradient is smoothed in the same way.
- Untouched: `usePictureShape` and the shape classification, `PosterPicture.css`, `ScoreBoardOverlay.tsx`, `TopAnimePage`, `ProfilePage`, `AnimeCard`, `RowPicture` and `AiringPage`.
