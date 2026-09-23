import { useLayoutEffect, useRef, useState, type FocusEvent, type MouseEvent as ReactMouseEvent } from 'react'
import { createPortal } from 'react-dom'
import { Link } from 'react-router-dom'
import type { RecapRowDto } from '../api/types.ts'
import { useRestorableScroll } from '../hooks/useRestorableScroll.ts'
import { mediaTypeLabel, pickDisplayTitle, scoreTier } from '../utils/anime.ts'
import { Modal } from './Modal.tsx'
import { PosterPicture } from './PosterPicture.tsx'
import './ScoreBoardOverlay.css'

export type ScoreBoardGroup = { score: number; items: RecapRowDto[] }

type ScoreBoardOverlayProps = {
  title: string
  groups: ScoreBoardGroup[]
  onClose: () => void
}

const VIEWPORT_MARGIN = 8
const CARD_OFFSET = 8

type CardTarget = { item: RecapRowDto; score: number; rect: DOMRect } | null

// Opens from the rating distribution (design.md decision 5) and lays the
// same set the distribution counts out as poster art, grouped by score. The
// component owns no filtering of its own — `groups` is `scoreGroupsOf`'s
// output handed straight through, so the board's slot counts and the
// distribution's row counts can never disagree.
export function ScoreBoardOverlay({ title, groups, onClose }: ScoreBoardOverlayProps) {
  const [card, setCard] = useState<CardTarget>(null)
  const cardRef = useRef<HTMLDivElement>(null)
  const [cardPosition, setCardPosition] = useState<{ left: number; top: number } | null>(null)
  const scrollRef = useRestorableScroll('scoreBoard', 'vertical')

  // Runs before paint so the card's first visible frame is already clamped
  // to the viewport (mirrors TruncatedTitle's tooltip). Portalled rather
  // than positioned in flow: `.modal` is overflow-y: auto, a scroll
  // container in both axes, so an in-flow card would be clipped at a slot's
  // edge (design.md decision 6).
  useLayoutEffect(() => {
    if (!card) {
      setCardPosition(null)
      return
    }
    const el = cardRef.current
    const width = el?.offsetWidth ?? 0
    const height = el?.offsetHeight ?? 0
    const { rect } = card
    const above = rect.top - height - CARD_OFFSET
    const top = above >= VIEWPORT_MARGIN ? above : rect.bottom + CARD_OFFSET
    const left = rect.left + rect.width / 2 - width / 2
    setCardPosition({
      left: Math.min(Math.max(VIEWPORT_MARGIN, left), window.innerWidth - width - VIEWPORT_MARGIN),
      top: Math.min(Math.max(VIEWPORT_MARGIN, top), window.innerHeight - height - VIEWPORT_MARGIN),
    })
  }, [card])

  // Anchored to the tile's own rect rather than the pointer, so onFocus can
  // show the same card a keyboard user needs — a keyboard focus has no
  // pointer coordinates.
  function showCard(item: RecapRowDto, score: number, target: HTMLElement) {
    setCard({ item, score, rect: target.getBoundingClientRect() })
  }

  function hideCard() {
    setCard(null)
  }

  return (
    <Modal onClose={onClose} labelledBy="score-board-title" className="modal--board" contentRef={scrollRef}>
      <div className="score-board">
        <div className="score-board__header">
          <h2 id="score-board-title" className="score-board__title">
            {title}
          </h2>
          <button type="button" className="score-board__close" onClick={onClose} aria-label="Close">
            &times;
          </button>
        </div>

        <div className="score-board__slots">
          {[...groups].reverse().map((group) => {
            const tier = scoreTier(group.score)
            return (
              <section key={group.score} className={`score-board__slot score-board__slot--${tier}`}>
                <div className="score-board__slot-header">
                  <span className="score-board__slot-score">{group.score}</span>
                  <span className="score-board__slot-count">{group.items.length} anime</span>
                </div>
                {group.items.length === 0 ? (
                  <p className="score-board__slot-empty">Nothing scored {group.score}</p>
                ) : (
                  <div className="score-board__grid">
                    {group.items.map((item) => {
                      const displayTitle = pickDisplayTitle(item.title, item.englishTitle)
                      return (
                        <Link
                          key={item.animeId}
                          to={`/anime/${item.animeId}`}
                          className="score-board__tile"
                          aria-label={displayTitle}
                          onMouseEnter={(e: ReactMouseEvent<HTMLAnchorElement>) =>
                            showCard(item, group.score, e.currentTarget)
                          }
                          onMouseLeave={hideCard}
                          onFocus={(e: FocusEvent<HTMLAnchorElement>) => showCard(item, group.score, e.currentTarget)}
                          onBlur={hideCard}
                        >
                          <PosterPicture src={item.pictureUrl} loading="lazy" className="score-board__poster" />
                        </Link>
                      )
                    })}
                  </div>
                )}
              </section>
            )
          })}
        </div>
      </div>

      {card &&
        createPortal(
          <div
            ref={cardRef}
            className="score-board__card"
            style={
              cardPosition
                ? { left: cardPosition.left, top: cardPosition.top }
                : { left: card.rect.left, top: card.rect.top, visibility: 'hidden' }
            }
          >
            <span className="score-board__card-title">{pickDisplayTitle(card.item.title, card.item.englishTitle)}</span>
            <span className="score-board__card-meta">
              Score {card.score} &middot; {mediaTypeLabel(card.item.mediaType)}
            </span>
          </div>,
          document.body,
        )}
    </Modal>
  )
}
