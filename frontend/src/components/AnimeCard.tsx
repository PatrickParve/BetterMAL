import { Link } from 'react-router-dom'
import type { ReactNode } from 'react'
import { mediaTypeLabel, pickDisplayTitle } from '../utils/anime.ts'
import './AnimeCard.css'

type AnimeCardProps = {
  animeId: number
  title: string
  englishTitle?: string | null
  pictureUrl?: string | null
  /** Overrides the default `/anime/{animeId}` target — used by search's series cards, which link to the series page instead. */
  to?: string
  /** Extra content shown below the title (progress bar, score, etc.) — rendered inside the link. */
  children?: ReactNode
  /** Interactive controls (e.g. an increment button) rendered outside the link so they don't trigger navigation. */
  actions?: ReactNode
  /** Content rendered below the link, outside it — e.g. an editable progress row — so it never navigates. */
  footer?: ReactNode
  className?: string
}

// The single clickable-card building block reused across the dashboard, my
// list, top anime, season, and airing pages — always links to the anime's
// detail page. Three slots: `children` (inside the link — navigates),
// `actions` (absolute, top-right, outside the link), and `footer` (below the
// link, outside it — never navigates). Callers compose page-specific content
// via these.
export function AnimeCard({ animeId, title, englishTitle, pictureUrl, to, children, actions, footer, className }: AnimeCardProps) {
  return (
    <div className={className ? `anime-card ${className}` : 'anime-card'}>
      <Link to={to ?? `/anime/${animeId}`} className="anime-card__link">
        {pictureUrl ? (
          <img src={pictureUrl} alt="" className="anime-card__picture" />
        ) : (
          <div className="anime-card__picture anime-card__picture--placeholder" aria-hidden="true" />
        )}
        <span className="anime-card__title" title={pickDisplayTitle(title, englishTitle)}>
          {pickDisplayTitle(title, englishTitle)}
        </span>
        {children}
      </Link>
      {actions && <div className="anime-card__actions">{actions}</div>}
      {footer}
    </div>
  )
}

// The `{TYPE} · {N} ep` meta line shown on browse cards (season, search) below
// the title.
export function AnimeCardMeta({ mediaType, totalEpisodes }: { mediaType: string | null; totalEpisodes: number | null }) {
  return (
    <span className="anime-card__meta">
      {mediaTypeLabel(mediaType)} · {totalEpisodes ?? '?'} ep
    </span>
  )
}
