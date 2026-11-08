import { Link } from 'react-router-dom'
import type { RecapRankingPosterDto } from '../api/types.ts'
import { RowPicture } from './RowPicture.tsx'
import './HighlightCard.css'

// Which colour a card's rail and its hover treatment are drawn in
// (list-recaps "Recap highlights row"): the genre or studio family for the cards
// that open those lists, the accent for the card that opens an anime, and the
// neutral stat-tile rail for the one that leads nowhere.
export type HighlightRail = 'genre' | 'studio' | 'accent' | 'neutral'

type HighlightCardProps = {
  label: string
  // The card's name or figure: a genre, a studio, an anime's title, "51 episodes".
  value: string
  // The one short line under it ("58 anime", "5 scored · 8.40").
  line: string
  posters: RecapRankingPosterDto[]
  rail: HighlightRail
  // Exactly one of the two at most: a link to a page of the app, or a button that
  // opens something on this page. With neither the card is a plain box that leads
  // nowhere and is not a keyboard stop.
  to?: string
  onOpen?: () => void
}

// One card of the recap's highlights row (add-genre-and-studio-stats design.md
// D11). It shares the stat tiles' box (padding, border, corner, background) and
// their rail: a 3px marker inside the padding box, coloured when the card leads
// somewhere and neutral when it does not. Hovering changes only colours and a
// shadow, never the card's box or position, and the same treatment shows on
// keyboard focus.
export function HighlightCard({ label, value, line, posters, rail, to, onOpen }: HighlightCardProps) {
  const content = (
    <>
      <span className="highlight-card__label">{label}</span>
      <span className="highlight-card__body">
        <span className="highlight-card__text">
          {/* The whole name is on hover: it wraps to two lines and ends in an
              ellipsis rather than growing the card. */}
          <span className="highlight-card__value" title={value}>
            {value}
          </span>
          <span className="highlight-card__line">{line}</span>
        </span>
        {posters.length > 0 && (
          <span className="highlight-card__posters">
            {posters.map((poster) => (
              <RowPicture
                key={poster.animeId}
                src={poster.pictureUrl}
                className="highlight-card__poster"
                title={poster.title}
                placeholderClassName="highlight-card__poster-placeholder"
              />
            ))}
          </span>
        )}
      </span>
    </>
  )

  const className = `highlight-card highlight-card--${rail}${rail === 'genre' ? ' family--genre' : ''}${
    rail === 'studio' ? ' family--studio' : ''
  }`

  if (to !== undefined) {
    return (
      <Link to={to} className={className}>
        {content}
      </Link>
    )
  }
  if (onOpen !== undefined) {
    return (
      <button type="button" className={className} onClick={onOpen}>
        {content}
      </button>
    )
  }
  return <div className={className}>{content}</div>
}
