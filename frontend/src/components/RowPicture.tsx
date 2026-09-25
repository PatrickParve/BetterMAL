import { useDisplayPicture } from '../hooks/useDisplayPicture.ts'
import { usePictureShape } from '../hooks/useLandscapePicture.ts'
import type { PictureTier } from '../utils/anime.ts'
import './RowPicture.css'

type RowPictureProps = {
  src: string | null | undefined
  className: string
  title?: string
  placeholderClassName?: string
  /** Which TMDB width the picture downloads at (design D9). `row` fits every slot up to about 140px tall; pass `tile` for a taller one, whose wide picture reaches 190px or more. */
  tier?: Extract<PictureTier, 'row' | 'tile'>
}

// artwork-presentation: the one place a row or thumbnail's picture is drawn.
// The orientation state lives here, in the leaf, rather than in each host
// component (design D1) — several call sites (RecapPage, ProfilePage,
// SettingsPage, RankingOverlay/RankingSection) render many pictures from one
// component, and hooks rules forbid a hook per picture there; others
// (MyListRow) are memoised specifically so a keystroke elsewhere doesn't
// redraw every row, which a lifted useState would defeat. Pushing the state
// down here also means a picture's load re-renders one <img>, not a row or
// a whole page. `loading="lazy"` defers the request until the picture nears
// the viewport; it is never `complete` at ref-attach time in that case, so
// the shape hook's `load`-listener branch handles it exactly as it already
// handles a cold cache.
//
// Every non-poster shape takes the width-free path, upright pictures
// included (uncrop-artwork-everywhere, design D1): a 4:5 picture cropped to
// a poster slot loses a fifth of its width. RowPicture.css already handles
// any ratio — width follows the slot's height up to its 16/9 cap — so an
// upright picture simply lands a little wider than a poster.
//
// A TMDB picture downloads at the row width, `w342`, rather than as the
// `original` file (tmdb-artwork, design D9); `src` stays its identity, and a
// failed download retries the original. The two tallest slots, whose wide
// pictures are drawn up to about 240px across, ask for `tile`. A picture that
// is slow to arrive also fades in (artwork-presentation, design D10). An <img>
// can't fade over its own background, since its opacity takes the background
// with it, so the <img> sits in a `row-picture-frame` that carries the host's
// class, its box and the placeholder background. The host only ever sets --row-picture-* and a radius
// on that class, both of which the frame and the <img> inside it share.
export function RowPicture({ src, className, title, placeholderClassName, tier = 'row' }: RowPictureProps) {
  const { displaySrc, onError } = useDisplayPicture(src, tier)
  const [shapeRef, shape, arrival] = usePictureShape(displaySrc)

  if (!src) {
    return (
      <div
        aria-hidden="true"
        className={'row-picture row-picture--placeholder ' + className + ' ' + (placeholderClassName ?? className + '--placeholder')}
      />
    )
  }

  return (
    <span className={`row-picture-frame row-picture-frame--${arrival} ${className}`}>
      <img
        ref={shapeRef}
        src={displaySrc}
        alt=""
        title={title}
        loading="lazy"
        onError={onError}
        className={'row-picture' + (shape !== 'poster' ? ' row-picture--wide' : '')}
      />
    </span>
  )
}
