import { Link } from 'react-router-dom'
import { Modal } from './Modal.tsx'
import './RankingOverlay.css'

export type RankingOverlayRow = {
  key: string
  label: string
  meta: string
  to: string
  posters: { animeId: number; title: string; pictureUrl: string | null }[]
}

type RankingOverlayProps = {
  title: string
  rows: RankingOverlayRow[]
  onClose: () => void
}

// The recap page's rankings show five at a time; this overlay lists every
// qualifying row in rank order, opened when more than five qualify
// (design.md decision 6). One generic component serves the season, year,
// and time-watched rankings alike — each caller maps its own DTO into the
// normalised row shape via a describe() function shared with its inline
// five-row list, so the two representations of one ranking can never drift.
export function RankingOverlay({ title, rows, onClose }: RankingOverlayProps) {
  return (
    <Modal onClose={onClose} labelledBy="ranking-overlay-title" className="modal--wide">
      <div className="ranking-overlay">
        <h2 id="ranking-overlay-title" className="ranking-overlay__title">
          {title}
        </h2>

        <ol className="ranking-overlay__list">
          {rows.map((row, index) => (
            <li key={row.key} className="ranking-overlay__row">
              <Link to={row.to} className="ranking-overlay__link" onClick={onClose}>
                <span className="ranking-overlay__rank">#{index + 1}</span>
                <span className="ranking-overlay__label">{row.label}</span>
                <span className="ranking-overlay__meta">{row.meta}</span>
                {row.posters.length > 0 && (
                  <span className="ranking-overlay__posters">
                    {row.posters.map((p) =>
                      p.pictureUrl ? (
                        <img key={p.animeId} src={p.pictureUrl} alt="" title={p.title} />
                      ) : (
                        <span key={p.animeId} className="ranking-overlay__poster-placeholder" title={p.title} />
                      ),
                    )}
                  </span>
                )}
              </Link>
            </li>
          ))}
        </ol>

        <div className="ranking-overlay__buttons">
          <button type="button" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </Modal>
  )
}
