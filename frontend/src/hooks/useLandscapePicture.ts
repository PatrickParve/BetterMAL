import { useCallback, useState } from 'react'

export type LandscapePictureRef = (node: HTMLImageElement | null) => void

// Orientation isn't known until the picture itself has loaded, so this hands
// back a ref to attach to an <img> plus a flag that flips true once that
// image is known to be wider than it is tall. A callback ref is used rather
// than an <img onLoad> handler because a cached image — the common case on a
// revisit — can finish decoding before React gets around to attaching the
// handler, so onLoad would simply never fire and the picture would silently
// keep its portrait box until the next full reload.
//
// `src` is taken as an argument (rather than read off the node) so the flag
// can be reset the moment it changes: these pages keep the same <img>
// mounted across a route change and just swap its `src`, and recreating the
// ref callback here makes React detach and reattach it against that same
// node, which is where the reset happens — otherwise one anime's landscape
// picture could stay landscape for a beat while the next anime's portrait
// picture is still loading in.
export function useLandscapePicture(src: string | null | undefined): [LandscapePictureRef, boolean] {
  const [isLandscape, setIsLandscape] = useState(false)

  const ref = useCallback<LandscapePictureRef>(
    (node) => {
      if (!node) return
      setIsLandscape(false)

      function checkOrientation() {
        setIsLandscape(node!.naturalWidth > node!.naturalHeight)
      }

      if (node.complete) {
        checkOrientation()
        return
      }
      node.addEventListener('load', checkOrientation)
      return () => node.removeEventListener('load', checkOrientation)
    },
    [src],
  )

  return [ref, isLandscape]
}
