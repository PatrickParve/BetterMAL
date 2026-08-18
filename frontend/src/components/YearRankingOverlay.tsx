import { Link } from 'react-router-dom'
import type { RecapYearRankingDto } from '../api/types.ts'
import { Modal } from './Modal.tsx'
import './YearRankingOverlay.css'

type YearRankingOverlayProps = {
  rankings: RecapYearRankingDto[]
  onClose: () => void
}

// The recap page's year ranking shows five at a time; this overlay lists
// every qualifying year in rank order, opened when more than five qualify
// (tasks.md 6.10).
export function YearRankingOverlay({ rankings, onClose }: YearRankingOverlayProps) {
  return (
    <Modal onClose={onClose} labelledBy="year-ranking-overlay-title" className="modal--wide">
      <div className="year-ranking-overlay">
        <h2 id="year-ranking-overlay-title" className="year-ranking-overlay__title">
          Year ranking
        </h2>

        <ol className="year-ranking-overlay__list">
          {rankings.map((row, index) => (
            <li key={row.year} className="year-ranking-overlay__row">
              <Link
                to={`/recap?mode=yearly&year=${row.year}&filter=aired`}
                className="year-ranking-overlay__link"
                onClick={onClose}
              >
                <span className="year-ranking-overlay__rank">#{index + 1}</span>
                <span className="year-ranking-overlay__label">{row.year}</span>
                <span className="year-ranking-overlay__meta">
                  {row.scoredCount} scored &middot; {row.weightedScore.toFixed(2)}
                </span>
                {row.topPosters.length > 0 && (
                  <span className="year-ranking-overlay__posters">
                    {row.topPosters.map((p) =>
                      p.pictureUrl ? (
                        <img key={p.animeId} src={p.pictureUrl} alt="" title={p.title} />
                      ) : (
                        <span
                          key={p.animeId}
                          className="year-ranking-overlay__poster-placeholder"
                          title={p.title}
                        />
                      ),
                    )}
                  </span>
                )}
              </Link>
            </li>
          ))}
        </ol>

        <div className="year-ranking-overlay__buttons">
          <button type="button" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </Modal>
  )
}
