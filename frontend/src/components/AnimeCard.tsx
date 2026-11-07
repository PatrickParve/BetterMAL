import { Link } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useScoreVisibility } from '../context/ScoreVisibilityContext.tsx'
import { mediaTypeLabel, pickDisplayTitle } from '../utils/anime.ts'
import { PosterPicture } from './PosterPicture.tsx'
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
        <PosterPicture src={pictureUrl} className="anime-card__picture" />
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

// The MAL score at the end of a browse card's meta line: two decimals, in the MAL
// score colour. While scores are globally hidden it is omitted entirely — no value,
// no placeholder, no reveal control — rather than using ScoreValue, whose hidden
// branch always renders a reveal button
// (score-visibility spec's browse-card exception; "always show completed/dropped
// scores" is deliberately not consulted here, since these cards carry no watch
// status). It exists once so every browse card hides its score by the same rule:
// the anime cards' score below and the search page's series cards, whose figure is
// the series' main-line MAL average.
//
// With no score the slot is empty on the season and year pages (season-browser "An
// anime with no MAL score shows nothing in that slot"), and a dash on the search page,
// which asks for it with `showMissing` (navigation-and-search "Full search results page").
// The dash is the app's own mark for a value with none (ScoreValue's `—`), drawn in the
// neutral text colour rather than the MAL blue, and named "No score" for a screen reader.
// It shows only while scores are shown: hidden, a card carries nothing in the score's
// place, whether or not the anime has one, so hiding scores leaves no furniture behind.
export function BrowseCardScore({ malScore, showMissing = false }: { malScore: number | null; showMissing?: boolean }) {
  const { hidden } = useScoreVisibility()
  if (hidden) return null
  if (malScore == null) {
    if (!showMissing) return null
    return (
      <span className="anime-card__meta-score anime-card__meta-score--none" title="No MAL score">
        <span aria-hidden="true">—</span>
        <span className="visually-hidden">No score</span>
      </span>
    )
  }
  return <span className="anime-card__meta-score score--mal">{malScore.toFixed(2)}</span>
}

// The `{TYPE} · {N} ep` meta line shown on browse cards (season, search) below
// the title, with the anime's MAL score trailing at the line's end. `showMissingScore`
// puts a dash there for an anime with no score (the search page); without it the slot
// stays empty, as it does on the season and year pages.
export function AnimeCardMeta({
  mediaType,
  totalEpisodes,
  malScore,
  showMissingScore = false,
}: {
  mediaType: string | null
  totalEpisodes: number | null
  malScore: number | null
  showMissingScore?: boolean
}) {
  return (
    <span className="anime-card__meta">
      <span>
        {mediaTypeLabel(mediaType)} · {totalEpisodes ?? '?'} ep
      </span>
      <BrowseCardScore malScore={malScore} showMissing={showMissingScore} />
    </span>
  )
}
