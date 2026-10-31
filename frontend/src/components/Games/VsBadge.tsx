import './VsBadge.css'

type VsBadgeProps = {
  /** `tile` sits on the Games tile's posters, `lobby` on the setup screen's, and `table` between the cards in play. */
  size?: 'tile' | 'lobby' | 'table'
  /** `vs` for Higher or Lower; `question` is the same disc around a question mark, for a game whose posters are hidden. */
  kind?: 'vs' | 'question'
}

// The one VS medallion (add-games-higher-or-lower design.md D10), shared by the
// Games tile, the setup screen's poster pair and the table, so the three can't
// drift apart. Decorative: the cards say what they are, and the pick buttons are
// named after their anime, so assistive technology skips it. Guess the Anime's
// pair wears the same disc with a question mark (add-guess-the-anime design.md
// D10).
export function VsBadge({ size = 'table', kind = 'vs' }: VsBadgeProps) {
  return (
    <span className={`vs-badge vs-badge--${size}${kind === 'question' ? ' vs-badge--question' : ''}`} aria-hidden="true">
      {kind === 'question' ? '?' : 'VS'}
    </span>
  )
}
