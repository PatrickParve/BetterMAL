import './VsBadge.css'

type VsBadgeProps = {
  /** `tile` sits on the Games tile's posters, `lobby` on the setup screen's, and `table` between the cards in play. */
  size?: 'tile' | 'lobby' | 'table'
}

// The one VS medallion (add-games-higher-or-lower design.md D10), shared by the
// Games tile, the setup screen's poster pair and the table, so the three can't
// drift apart. Decorative: the cards say what they are, and the pick buttons are
// named after their anime, so assistive technology skips it.
export function VsBadge({ size = 'table' }: VsBadgeProps) {
  return (
    <span className={`vs-badge vs-badge--${size}`} aria-hidden="true">
      VS
    </span>
  )
}
