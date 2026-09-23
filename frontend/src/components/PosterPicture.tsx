import { usePictureShape } from '../hooks/useLandscapePicture.ts'
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
}

// artwork-presentation: the one place a card, tile or poster box draws its
// picture — RowPicture's counterpart for surfaces whose box isn't a row slot.
// The shape state lives here, in the leaf, for the same reason RowPicture's
// does (uncrop-artwork-everywhere, design D2): several hosts (ProfilePage's
// five strips, TopAnimePage, RecapPage, ScoreBoardOverlay) render many
// pictures from one component, where hooks rules forbid a hook per picture,
// and a picture's load should re-render one wrapper rather than a page.
// Hosts whose own geometry depends on the shape — a grid card spanning two
// columns, a timeline card widening, a strip tile taking its picture's width
// — read it off the wrapper's `poster-picture--{shape}` class with `:has()`
// instead, so no host has to hold per-item state of its own.
//
// The blurred fill behind the art exists only for non-poster shapes
// (design D3): a poster fills its box, so it renders exactly as it did
// before this component, with no extra element. The exception is a banner
// box (`whole`, e.g. the airing slot's picture band): it draws every shape
// whole on the box's own flat background and mounts no fill at all, since a
// blurred copy behind a poster in a wide, narrow box read as clutter. Where
// the fill is mounted it's only after the art has loaded and been classified,
// so it's served from the same cache entry — it never costs a second request.
export function PosterPicture({ src, className, placeholderClassName, alt = '', draggable, loading, whole }: PosterPictureProps) {
  const [shapeRef, shape] = usePictureShape(src)

  if (!src) {
    return <div aria-hidden="true" className={placeholderClassName ?? `${className} ${className}--placeholder`} />
  }

  return (
    <span className={`poster-picture poster-picture--${shape}${whole ? ' poster-picture--whole' : ''} ${className}`}>
      {!whole && shape !== 'poster' && (
        <img className="poster-picture__fill" src={src} alt="" aria-hidden="true" draggable={false} />
      )}
      <img
        ref={shapeRef}
        className="poster-picture__art"
        src={src}
        alt={alt}
        draggable={draggable}
        loading={loading}
      />
    </span>
  )
}
