import { useEffect, useId, useState } from 'react'
import type { HigherOrLowerCardDto } from '../../api/types.ts'
import { mediaTypeLabel, pickDisplayTitle } from '../../utils/anime.ts'
import { PosterPicture } from '../PosterPicture.tsx'
import { TruncatedTitle } from '../TruncatedTitle.tsx'
import { REVEAL_MS } from './higherLowerTiming.ts'
import './HigherLowerCard.css'

export type HigherLowerCardMotion = 'leaving' | 'to-left' | 'entering'
export type HigherLowerCardVerdict = 'correct' | 'wrong'

type HigherLowerCardProps = {
  card: HigherOrLowerCardDto
  /** The score to show, or null while it's hidden. */
  score: number | null
  /** The score has just been revealed: count it up to its value rather than showing it at once. */
  counting: boolean
  /** Set on the card that was picked, once the verdict is up. */
  verdict: HigherLowerCardVerdict | null
  /** The other card, once the verdict is up. */
  dimmed: boolean
  motion: HigherLowerCardMotion | null
  /** No pick is possible right now: sent, or being judged. */
  locked: boolean
  inMyList: boolean
  /** False under Only my list, where every card would say the same. */
  showListMark: boolean
  adding: boolean
  onPick: () => void
  onAdd: () => void
}

// The score counts up from zero to its value over REVEAL_MS with an ease-out
// (design.md D11). Everything else it shows is the value as it is.
function useDisplayedScore(score: number | null, counting: boolean): number | null {
  const [displayed, setDisplayed] = useState(score)

  useEffect(() => {
    if (score === null || !counting) {
      setDisplayed(score)
      return
    }
    const start = performance.now()
    let frame = 0
    const tick = (now: number) => {
      const t = Math.min(1, (now - start) / REVEAL_MS)
      setDisplayed(score * (1 - (1 - t) ** 3))
      if (t < 1) frame = requestAnimationFrame(tick)
    }
    frame = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(frame)
  }, [score, counting])

  return displayed
}

// One anime on the table (higher-or-lower "A round shows two anime side by
// side"). The picture, score pill, title and meta line are one button, which is
// the pick; the actions row sits outside it so Add to list is never a pick. The score pill is its own markup rather than ScoreValue: the game
// shows a revealed score whatever the hide toggle says, with no reveal control,
// and it keeps the MAL blue even on the card that glows green (design.md D12).
// The pill is the same size hidden and shown, so a reveal moves nothing.
export function HigherLowerCard({
  card,
  score,
  counting,
  verdict,
  dimmed,
  motion,
  locked,
  inMyList,
  showListMark,
  adding,
  onPick,
  onAdd,
}: HigherLowerCardProps) {
  const scoreId = useId()
  const displayed = useDisplayedScore(score, counting)
  const title = pickDisplayTitle(card.title, card.englishTitle)
  const meta = [card.mediaType ? mediaTypeLabel(card.mediaType) : null, card.year].filter((part) => part !== null).join(' · ')

  const className = [
    'higher-lower-card',
    verdict ? `higher-lower-card--${verdict}` : null,
    dimmed ? 'higher-lower-card--other' : null,
    motion ? `higher-lower-card--${motion}` : null,
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <article className={className}>
      {/* aria-disabled rather than disabled while locked, so focus isn't lost
          (design.md D16). The score is the description, so the left card's
          known score is heard with its name. */}
      <button
        type="button"
        className="higher-lower-card__pick"
        aria-label={`Pick ${title}`}
        aria-describedby={score === null ? undefined : scoreId}
        aria-disabled={locked}
        onClick={onPick}
      >
        <span className="higher-lower-card__frame">
          <PosterPicture src={card.pictureUrl} className="higher-lower-card__picture" draggable={false} />
          <span className={`higher-lower-card__score${score === null ? '' : ' higher-lower-card__score--known'}`} id={scoreId}>
            {displayed === null ? (
              '?'
            ) : (
              <>
                <span className="visually-hidden">MAL score </span>
                {displayed.toFixed(2)}
              </>
            )}
          </span>
        </span>
        <TruncatedTitle title={title} lines={2} className="higher-lower-card__title" />
        <span className="higher-lower-card__meta">{meta}</span>
      </button>

      <div className="higher-lower-card__actions">
        {inMyList ? (
          showListMark && <span className="higher-lower-card__in-list">✓ In my list</span>
        ) : (
          <button type="button" className="higher-lower-card__add" disabled={adding} onClick={onAdd}>
            + Add to list
          </button>
        )}
      </div>
    </article>
  )
}
