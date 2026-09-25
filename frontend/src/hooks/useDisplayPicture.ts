import { useCallback, useState } from 'react'
import { displayPictureUrl, type PictureTier } from '../utils/anime.ts'

// What an <img> should download for `src`, and what to call when that fails
// (tmdb-artwork, "A failed fitted download falls back"). `src` is the
// picture's identity, its `original` URL for a TMDB picture; `displaySrc` is the
// fitted rendition for the surface's tier, or `src` itself when there is
// nothing to fit (a MAL picture, no picture). `onError` switches to `src` once,
// so the surface tries the original before giving up; an error on `src` itself
// changes nothing.
//
// The fallback is remembered against the `src` it happened for rather than as a
// flag, so a surface that swaps to another picture is back on the fitted
// rendition without an effect to reset it.
export function useDisplayPicture(
  src: string | null | undefined,
  tier: PictureTier,
): { displaySrc: string | undefined; onError: () => void } {
  const [fellBackFor, setFellBackFor] = useState<string | null>(null)

  const fitted = src ? displayPictureUrl(src, tier) : undefined
  const displaySrc = src && fellBackFor === src ? src : fitted

  const onError = useCallback(() => {
    if (src && fitted !== src) setFellBackFor(src)
  }, [src, fitted])

  return { displaySrc, onError }
}
