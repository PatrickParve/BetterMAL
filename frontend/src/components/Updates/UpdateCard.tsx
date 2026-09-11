import { Link } from 'react-router-dom'
import type { AnimeUpdateDto } from '../../api/types.ts'
import { pickDisplayTitle, formatTimestamp } from '../../utils/anime.ts'
import { TruncatedTitle } from '../TruncatedTitle.tsx'
import { buildNewsLines, buildFactLines } from './updateText.ts'
import './UpdateCard.css'

type UpdateCardVariant = 'menu' | 'history'

type UpdateCardProps = {
  item: AnimeUpdateDto
  variant: UpdateCardVariant
  onNavigate?: () => void
  isNew?: boolean
}

// The one card either surface (the navbar dropdown, the history overlay)
// renders an update as (design.md D6) — same picture rule, same text, same
// reason line. The variants differ in exactly two things: the menu clamps
// the title to one line, the history shows it in full plus when it was
// detected.
export function UpdateCard({ item, variant, onNavigate, isNew = false }: UpdateCardProps) {
  const title = pickDisplayTitle(item.title, item.englishTitle)
  const newsLines = buildNewsLines(item)
  const factLines = buildFactLines(item)

  // Three things together, so the marking doesn't rest on colour alone
  // (store-seen-updates-on-server design.md D9): the accent edge on the
  // link, this dot, and the word "New" in the accessible name.
  const badge = isNew ? (
    <span className="update-card__new">
      <span className="update-card__new-dot" aria-hidden="true" />
      New
    </span>
  ) : null

  return (
    <Link to={`/anime/${item.animeId}`} className={'update-card' + (isNew ? ' update-card--new' : '')} onClick={onNavigate}>
      {item.pictureUrl ? (
        <img src={item.pictureUrl} alt="" className="update-card__picture" />
      ) : (
        <span className="update-card__picture update-card__picture--placeholder" aria-hidden="true" />
      )}
      <span className="update-card__text">
        {variant === 'menu' ? (
          <span className="update-card__title-row">
            {badge}
            <TruncatedTitle title={title} lines={1} className="update-card__title" />
          </span>
        ) : (
          <span className="update-card__title update-card__title--full">
            {badge}
            {title}
            <span className="update-card__timestamp">{formatTimestamp(item.detectedAt)}</span>
          </span>
        )}
        {newsLines.map((line, index) => (
          <span key={`news-${index}`} className="update-card__line">
            {line}
          </span>
        ))}
        {factLines.map((line, index) => (
          <span key={`fact-${index}`} className="update-card__line update-card__line--fact">
            {line}
          </span>
        ))}
        <span className="update-card__reason">{item.reason}</span>
      </span>
    </Link>
  )
}
