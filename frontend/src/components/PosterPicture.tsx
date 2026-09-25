import { useDisplayPicture } from '../hooks/useDisplayPicture.ts'
import { usePictureShape } from '../hooks/useLandscapePicture.ts'
import type { PictureTier } from '../utils/anime.ts'
import './PosterPicture.css'

type PosterPictureProps = {
  src: string | null | undefined
  className: string
  /** Replaces the default `{className} {className}--placeholder` when there's no picture. */
  placeholderClassName?: string
  alt?: string
  draggable?: boolean
  loading?: 'lazy' | 'eager'
  /** A banner box: draw every shape whole, a poster included, on the box's own background with no fill. */
  whole?: boolean
  /** Mount no blurred fill for any shape: an upright or wide picture is drawn whole and centred on whatever is behind the box. */
  noFill?: boolean
  /** Which TMDB width the picture downloads at (design D9). `card` fits a grid card or poster box; pass `tile` for a host that draws it at most about 250px wide, or `hero` for one that draws it wider than a card. */
  tier?: Exclude<PictureTier, 'row'>
}

// artwork-presentation: the one place a card, tile or poster box draws its
// picture — RowPicture's counterpart for surfaces whose box isn't a row slot.
// The shape state lives here, in the leaf, for the same reason RowPicture's
// does (uncrop-artwork-everywhere, design D2): several hosts (ProfilePage's
// five strips, TopAnimePage, RecapPage, ScoreBoardOverlay) render many
// pictures from one component, where hooks rules forbid a hook per picture,
// and a picture's load should re-render one wrapper rather than a page.
// Hosts whose own geometry depends on the shape — a More tile spanning two
// columns, a timeline card widening, the Top anime showcase poster taking
// its picture's width — read it off the wrapper's `poster-picture--{shape}`
// class with `:has()` instead, so no host has to hold per-item state of its
// own. A host that leaves its box alone, as the grid cards and the profile
// strips' tiles do, gets the picture drawn whole inside it over the fill.
//
// The blurred fill behind the art exists only for non-poster shapes
// (design D3): a poster fills its box, so it renders exactly as it did
// before this component, with no extra element. The exception is a banner
// box (`whole`, e.g. the airing slot's picture band): it draws every shape
// whole on the box's own flat background and mounts no fill at all, since a
// blurred copy behind a poster in a wide, narrow box read as clutter. The
// recap podium's box (`noFill`) mounts none for any shape: an upright or wide
// picture sits whole and centred on the card itself. Where the fill is
// mounted it's only after the art has loaded and been classified, so it's
// served from the same cache entry — it never costs a second request.
//
// A TMDB picture downloads at the width its `tier` names rather than as the
// `original` file (tmdb-artwork, design D9). The art and the fill use the same
// `displaySrc`, so the fill still costs no second request, and `src` stays
// the picture's identity; a failed fitted download retries the original.
// The picture also fades in over the box's own background as it arrives
// (artwork-presentation, design D10): `poster-picture--pending` holds it
// invisible until it has loaded, `--loaded` fades it in with any fill when it
// was slow, and `--held`, a picture the browser already had or that arrived
// within PICTURE_FADE_DELAY_MS, shows at once.
export function PosterPicture({ src, className, placeholderClassName, alt = '', draggable, loading, whole, noFill, tier = 'card' }: PosterPictureProps) {
  const { displaySrc, onError } = useDisplayPicture(src, tier)
  const [shapeRef, shape, arrival] = usePictureShape(displaySrc)

  if (!src) {
    return <div aria-hidden="true" className={placeholderClassName ?? `${className} ${className}--placeholder`} />
  }

  return (
    <span
      className={`poster-picture poster-picture--${shape} poster-picture--${arrival}${whole ? ' poster-picture--whole' : ''} ${className}`}
    >
      {!whole && !noFill && shape !== 'poster' && (
        <img className="poster-picture__fill" src={displaySrc} alt="" aria-hidden="true" draggable={false} />
      )}
      <img
        ref={shapeRef}
        className="poster-picture__art"
        src={displaySrc}
        alt={alt}
        draggable={draggable}
        loading={loading}
        onError={onError}
      />
    </span>
  )
}
