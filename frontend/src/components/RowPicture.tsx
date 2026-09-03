import { useWidePicture } from '../hooks/useLandscapePicture.ts'
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
// a whole page.
export function RowPicture({ src, className, title, placeholderClassName }: RowPictureProps) {
  const [wideRef, isWide] = useWidePicture(src)

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
      ref={wideRef}
      src={src}
      alt=""
      title={title}
      className={'row-picture' + (isWide ? ' row-picture--wide' : '') + ' ' + className}
    />
  )
}
