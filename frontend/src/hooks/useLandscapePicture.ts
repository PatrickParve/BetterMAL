import { useCallback, useState } from 'react'

export type LandscapePictureRef = (node: HTMLImageElement | null) => void

export type PictureShape = 'poster' | 'upright' | 'wide'

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
// Exported for a host that needs both the shape and strict landscape from
// one <img> (the series header, design D7) — one callback ref, rather than
// two attached to the same node.
export function useOrientationPicture(src: string | null | undefined): [LandscapePictureRef, number | null] {
  const [ratio, setRatio] = useState<number | null>(null)

  const ref = useCallback<LandscapePictureRef>(
    (node) => {
      if (!node) return
      setRatio(null)

      function checkOrientation() {
        setRatio(node!.naturalHeight > 0 ? node!.naturalWidth / node!.naturalHeight : null)
      }

      if (node.complete) {
        checkOrientation()
        return
      }
      node.addEventListener('load', checkOrientation)
      return () => node.removeEventListener('load', checkOrientation)
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [src],
  )

  return [ref, ratio]
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

export function usePictureShape(src: string | null | undefined): [LandscapePictureRef, PictureShape] {
  const [ref, ratio] = useOrientationPicture(src)
  return [ref, pictureShapeOf(ratio)]
}
