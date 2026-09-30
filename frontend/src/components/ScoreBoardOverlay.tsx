import { Link } from 'react-router-dom'
import type { RecapRowDto } from '../api/types.ts'
import { useRestorableScroll } from '../hooks/useRestorableScroll.ts'
import { pickDisplayTitle, scoreTier } from '../utils/anime.ts'
import { Modal } from './Modal.tsx'
import { PosterPicture } from './PosterPicture.tsx'
import './ScoreBoardOverlay.css'

export type ScoreBoardGroup = { score: number; items: RecapRowDto[] }

type ScoreBoardOverlayProps = {
  title: string
  groups: ScoreBoardGroup[]
  onClose: () => void
}

// Opens from the rating distribution (design.md decision 5) and lays the
// same set the distribution counts out as poster art, grouped by score. The
// board leaves out the scores that hold no anime and does no other
// filtering — `groups` is `scoreGroupsOf`'s output otherwise handed straight
// through, so a slot's count and its distribution row's count can never
// disagree.
export function ScoreBoardOverlay({ title, groups, onClose }: ScoreBoardOverlayProps) {
  const scrollRef = useRestorableScroll('scoreBoard', 'vertical')
  const slots = groups.filter((group) => group.items.length > 0).reverse()

  return (
    <Modal onClose={onClose} labelledBy="score-board-title" className="modal--board" contentRef={scrollRef}>
      <div className="score-board">
        <div className="score-board__header">
          <h2 id="score-board-title" className="score-board__title">
            {title}
          </h2>
          <button type="button" className="score-board__close" onClick={onClose} aria-label="Close score board">
            <CloseIcon />
          </button>
        </div>

        <div className="score-board__slots">
          {slots.length === 0 ? (
            <p className="score-board__empty">Nothing scored in this period.</p>
          ) : (
            slots.map((group) => {
              const tier = scoreTier(group.score)
              return (
                <section key={group.score} className={`score-board__slot score-board__slot--${tier}`}>
                  <div className="score-board__slot-header">
                    <span className="score-board__slot-score">{group.score}</span>
                    <span className="score-board__slot-count">{group.items.length} anime</span>
                  </div>
                  <div className="score-board__grid">
                    {group.items.map((item) => (
                      <Link
                        key={item.animeId}
                        to={`/anime/${item.animeId}`}
                        className="score-board__tile"
                        aria-label={pickDisplayTitle(item.title, item.englishTitle)}
                      >
                        <PosterPicture src={item.pictureUrl} loading="lazy" className="score-board__poster" />
                      </Link>
                    ))}
                  </div>
                </section>
              )
            })
          )}
        </div>
      </div>
    </Modal>
  )
}

function CloseIcon() {
  return (
    <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <line x1="6" y1="6" x2="18" y2="18" />
      <line x1="18" y1="6" x2="6" y2="18" />
    </svg>
  )
}
