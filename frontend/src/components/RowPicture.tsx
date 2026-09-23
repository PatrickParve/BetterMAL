import { usePictureShape } from '../hooks/useLandscapePicture.ts'
import './RowPicture.css'

type RowPictureProps = {
  src: string | null | undefined
  className: string
  title?: string
  placeholderClassName?: string
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
export function RowPicture({ src, className, title, placeholderClassName }: RowPictureProps) {
  const [shapeRef, shape] = usePictureShape(src)

  if (!src) {
    return (
      <div
        aria-hidden="true"
        className={'row-picture row-picture--placeholder ' + className + ' ' + (placeholderClassName ?? className + '--placeholder')}
      />
    )
  }

  return (
    <img
      ref={shapeRef}
      src={src}
      alt=""
      title={title}
      loading="lazy"
      className={'row-picture' + (shape !== 'poster' ? ' row-picture--wide' : '') + ' ' + className}
    />
  )
}
