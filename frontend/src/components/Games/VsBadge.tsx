import './VsBadge.css'

type VsBadgeProps = {
  /** `tile` sits on the Games tile's posters, `lobby` on the setup screen's, and `table` between the cards in play. */
  size?: 'tile' | 'lobby' | 'table'
  /** `vs` for Higher or Lower; `question` is the same disc around a question mark, for a game whose posters are hidden, and `note` around a musical note, for a game of songs. */
  kind?: 'vs' | 'question' | 'note'
}

// The one VS medallion (add-games-higher-or-lower design.md D10), shared by the
// Games tile, the setup screen's poster pair and the table, so the three can't
// drift apart. Decorative: the cards say what they are, and the pick buttons are
// named after their anime, so assistive technology skips it. Guess the Anime's
// pair wears the same disc with a question mark (add-guess-the-anime design.md
// D10), and Guess the Opening/Ending's wears a musical note.
export function VsBadge({ size = 'table', kind = 'vs' }: VsBadgeProps) {
  return (
    <span className={`vs-badge vs-badge--${size}${kind === 'question' ? ' vs-badge--question' : ''}${kind === 'note' ? ' vs-badge--note' : ''}`} aria-hidden="true">
      {kind === 'note' ? (
        <svg viewBox="0 0 24 24" className="vs-badge__note" focusable="false">
          <path
            fill="currentColor"
            d="M17.5 3.2a1 1 0 0 0-1.2-.98l-8 1.7A1 1 0 0 0 7.5 4.9v10.75A3.7 3.7 0 0 0 6 15.4c-1.9 0-3.5 1.2-3.5 2.8S4.1 21 6 21s3.5-1.2 3.5-2.8V9.1l6-1.28v5.83a3.7 3.7 0 0 0-1.5-.25c-1.9 0-3.5 1.2-3.5 2.8s1.6 2.8 3.5 2.8 3.5-1.2 3.5-2.8V3.2Z"
          />
        </svg>
      ) : kind === 'question' ? (
        '?'
      ) : (
        'VS'
      )}
    </span>
  )
}
