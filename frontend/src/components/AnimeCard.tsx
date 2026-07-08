import { Link } from 'react-router-dom'
import type { ReactNode } from 'react'
import './AnimeCard.css'

type AnimeCardProps = {
  animeId: number
  title: string
  pictureUrl?: string | null
  /** Extra content shown below the title (progress bar, score, etc.) — rendered inside the link. */
  children?: ReactNode
  /** Interactive controls (e.g. an increment button) rendered outside the link so they don't trigger navigation. */
  actions?: ReactNode
  className?: string
}

// The single clickable-card building block reused across the dashboard, my
// list, top anime, season, and airing pages — always links to the anime's
// detail page. Callers compose page-specific content via `children`/`actions`.
export function AnimeCard({ animeId, title, pictureUrl, children, actions, className }: AnimeCardProps) {
  return (
    <div className={className ? `anime-card ${className}` : 'anime-card'}>
      <Link to={`/anime/${animeId}`} className="anime-card__link">
        {pictureUrl ? (
          <img src={pictureUrl} alt="" className="anime-card__picture" />
        ) : (
          <div className="anime-card__picture anime-card__picture--placeholder" aria-hidden="true" />
        )}
        <span className="anime-card__title">{title}</span>
        {children}
      </Link>
      {actions && <div className="anime-card__actions">{actions}</div>}
    </div>
  )
}
