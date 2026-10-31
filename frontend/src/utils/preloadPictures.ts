import { displayPictureUrl, type PictureTier } from './anime.ts'

// Loads still in flight are held here, so nothing can collect an image while its
// download runs.
const inFlight = new Set<HTMLImageElement>()

// Starts downloading and decoding these pictures at the size a surface of this
// tier will ask for, so the <img> that draws them later finds them in the cache
// and shows at once instead of fading in (artwork-presentation "A picture fades
// in as it arrives"). The URL goes through the same displayPictureUrl the
// surface uses, so both ask for the very same file. Resolves when every picture
// is ready, a failed one counting as ready, or after `maxWaitMs`, whichever
// comes first: a slow picture must never hold a screen back for long. Callers
// that only want the head start leave the promise alone.
export function preloadPictures(
  urls: (string | null | undefined)[],
  tier: PictureTier,
  maxWaitMs: number,
): Promise<void> {
  const loads = urls
    .filter((url): url is string => !!url)
    .map(
      (url) =>
        new Promise<void>((resolve) => {
          const image = new Image()
          inFlight.add(image)
          const done = () => {
            inFlight.delete(image)
            resolve()
          }
          image.src = displayPictureUrl(url, tier)
          // decode() settles for a failed picture too, by rejecting.
          image.decode().then(done, done)
        }),
    )
  const timeout = new Promise<void>((resolve) => setTimeout(resolve, maxWaitMs))
  return Promise.race([Promise.all(loads).then(() => undefined), timeout])
}
