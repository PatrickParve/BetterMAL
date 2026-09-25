import { useCallback, useState } from 'react'

export type LandscapePictureRef = (node: HTMLImageElement | null) => void

export type PictureShape = 'poster' | 'upright' | 'wide'

// How a picture reached the screen (artwork-presentation, "A picture fades in
// as it arrives"): `pending` until it has loaded, and for good if it fails.
// Then `held` when it is to be drawn at once, because the browser already had
// it the moment the <img> attached or because it arrived within
// PICTURE_FADE_DELAY_MS, and `loaded` when it took longer and is to fade in.
export type PictureArrival = 'held' | 'pending' | 'loaded'

// A picture that arrives within this long of its <img> attaching is drawn at
// once rather than faded in: the fade is for a picture that was visibly slow,
// and on a quick one, a disk-cached picture on a fresh page load most of all,
// it only adds its own duration to the wait. Below the loading indicator's
// delay (useDelayedFlag), so the placeholder is never on screen long enough to
// need a fade over it.
export const PICTURE_FADE_DELAY_MS = 150

// The poster/upright split (artwork-presentation, design D1). Every portrait
// box in the app runs from 2:3 (0.667 — cards, strips) to about 0.73
// (profile rows), and MAL's own posters run from about 0.64 to 0.72, so 3/4
// sits just above all of them: a picture classified as a poster loses at
// most ~11% of its width, in the narrowest 2:3 box, and a typical 0.708 MAL
// poster keeps losing the ~6% it always has. Anything wider is drawn whole.
// One constant rather than a per-slot threshold because row slots declare
// their ratio as CSS calc() over other custom properties, out of reach here.
const POSTER_MAX_RATIO = 3 / 4

// Orientation isn't known until the picture itself has loaded, so this hands
// back a ref to attach to an <img> plus the image's width-to-height ratio
// once it has loaded (null until then). A callback ref is used rather than
// an <img onLoad> handler because a cached image — the common case on a
// revisit — can finish decoding before React gets around to attaching the
// handler, so onLoad would simply never fire and the picture would silently
// keep its portrait box until the next full reload.
//
// `src` is taken as an argument (rather than read off the node) so the ratio
// can be reset the moment it changes: these pages keep the same <img>
// mounted across a route change and just swap its `src`, and recreating the
// ref callback here makes React detach and reattach it against that same
// node, which is where the reset happens — otherwise one anime's landscape
// picture could stay landscape for a beat while the next anime's portrait
// picture is still loading in.
//
// The same moment tells a picture the browser already held (`complete` when
// the ref attaches, the common case on a revisit) from one that arrives later
// (the `load` event), which is what `arrival` reports (design D10): a late one
// is `held` too when it came within PICTURE_FADE_DELAY_MS of attaching. It
// resets with `src` alongside the ratio, and a failed picture stays
// `pending`: a failed <img> is `complete` too, but with no height.
//
// Exported for a host that needs both strict landscape and `arrival` from one
// <img> (the completion prompt, polish-series-header-and-completion-prompt
// design D6) — one callback ref, rather than two attached to the same node.
export function useOrientationPicture(
  src: string | null | undefined,
): [LandscapePictureRef, number | null, PictureArrival] {
  const [ratio, setRatio] = useState<number | null>(null)
  const [arrival, setArrival] = useState<PictureArrival>('pending')

  const ref = useCallback<LandscapePictureRef>(
    (node) => {
      if (!node) return
      setRatio(null)
      setArrival('pending')
      const attachedAt = performance.now()

      function checkOrientation() {
        setRatio(node!.naturalHeight > 0 ? node!.naturalWidth / node!.naturalHeight : null)
      }

      function onLoad() {
        checkOrientation()
        setArrival(performance.now() - attachedAt < PICTURE_FADE_DELAY_MS ? 'held' : 'loaded')
      }

      if (node.complete) {
        checkOrientation()
        if (node.naturalHeight > 0) setArrival('held')
        return
      }
      node.addEventListener('load', onLoad)
      return () => node.removeEventListener('load', onLoad)
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [src],
  )

  return [ref, ratio, arrival]
}

export function isLandscapeRatio(ratio: number | null): boolean {
  return ratio !== null && ratio > 1
}

// Unknown (null) reads as a poster, so every surface first renders exactly
// today's box and only adopts a whole-picture treatment on load (design D8).
export function pictureShapeOf(ratio: number | null): PictureShape {
  if (ratio === null || ratio <= POSTER_MAX_RATIO) return 'poster'
  return ratio < 1 ? 'upright' : 'wide'
}

// Strictly wider than tall — the detail page's and series header's
// landscape layouts, whose specs define landscape that way.
export function useLandscapePicture(src: string | null | undefined): [LandscapePictureRef, boolean] {
  const [ref, ratio] = useOrientationPicture(src)
  return [ref, isLandscapeRatio(ratio)]
}

export function usePictureShape(src: string | null | undefined): [LandscapePictureRef, PictureShape, PictureArrival] {
  const [ref, ratio, arrival] = useOrientationPicture(src)
  return [ref, pictureShapeOf(ratio), arrival]
}
