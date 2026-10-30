import { useEffect, useMemo, useRef, useState } from 'react'
import type { ScoreHistoryDto } from '../api/types.ts'
import { useReconnectRetry } from '../hooks/useReconnectRetry.ts'
import { changesByYear, formatDayMonth, formatMove, formatScore } from '../utils/scoreHistoryChart.ts'
import { pickDisplayTitle } from '../utils/anime.ts'
import { LoadFailedNotice } from './LoadFailedNotice.tsx'
import { LoadingNotice } from './LoadingNotice.tsx'
import { Modal } from './Modal.tsx'
import { RevealControl } from './RevealControl.tsx'
import { ScoreHistoryChart } from './ScoreHistoryChart.tsx'
import './ScoreHistoryOverlay.css'

type ScoreHistoryOverlayProps = {
  /** The anime's or the series' title, which says what the graph is of. */
  title: string
  /** What is plotted, when the title alone doesn't say: "MAL · Main series" for a series. */
  subtitle?: string
  load: () => Promise<ScoreHistoryDto>
  /** Whether the history may be shown; decided by the page afresh on each render. */
  visible: boolean
  onReveal: () => void
  onClose: () => void
}

type LoadState = { status: 'loading' } | { status: 'failed' } | { status: 'ready'; history: ScoreHistoryDto }

// The Score history overlay for an anime and for a series' main line
// (add-score-history-graph design.md D10, D12): a graph above a list of every
// dated change, newest first. While the history may not be shown, a reveal
// control stands in for both and nothing is requested, so a hidden score is
// never in the page or on the wire. Whether it may be shown, and the reveal
// itself, belong to the page: they have to survive closing and reopening this
// overlay, and be dropped when the page is left.
export function ScoreHistoryOverlay({ title, subtitle, load, visible, onReveal, onClose }: ScoreHistoryOverlayProps) {
  const [state, setState] = useState<LoadState>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)

  const loadRef = useRef(load)
  loadRef.current = load

  useEffect(() => {
    if (!visible) return

    let cancelled = false
    loadRef
      .current()
      .then((history) => {
        if (!cancelled) setState({ status: 'ready', history })
      })
      .catch(() => {
        if (!cancelled) setState({ status: 'failed' })
      })
    return () => {
      cancelled = true
    }
  }, [visible, attempt])

  const rearm = useReconnectRetry(visible && state.status === 'failed', retry)

  function retry() {
    setState({ status: 'loading' })
    setAttempt((count) => count + 1)
  }

  const years = useMemo(
    () => (state.status === 'ready' ? changesByYear(state.history.points, state.history.markers) : []),
    [state],
  )

  return (
    <Modal onClose={onClose} labelledBy="score-history-title" className="modal--wide modal--column">
      <div className="score-history">
        <div className="score-history__header">
          <h2 id="score-history-title" className="score-history__title">
            Score history
          </h2>
          <button type="button" className="score-history__close" aria-label="Close score history" onClick={onClose}>
            <CloseIcon />
          </button>
        </div>
        <p className="score-history__subtitle">{subtitle ? `${title} · ${subtitle}` : title}</p>

        {!visible ? (
          <div className="score-history__hidden">
            <span>Scores are hidden</span>
            <RevealControl onReveal={onReveal} label="Reveal score history" />
          </div>
        ) : state.status === 'loading' ? (
          <LoadingNotice className="score-history__empty" />
        ) : state.status === 'failed' ? (
          <LoadFailedNotice
            what="the score history"
            compact
            onRetry={() => {
              rearm()
              retry()
            }}
          />
        ) : state.history.points.length === 0 ? (
          <p className="score-history__empty">No score history yet.</p>
        ) : (
          <>
            <ScoreHistoryChart points={state.history.points} markers={state.history.markers} />
            <div className="score-history__changes scroll-hidden">
              {years.map((year) => (
                <section key={year.year} className="score-history__year">
                  <h3 className="score-history__year-heading">{year.year}</h3>
                  <ul className="score-history__rows">
                    {year.rows.map((row) => (
                      <li key={row.date} className="score-history__row">
                        <span className="score-history__date">{formatDayMonth(row.date)}</span>
                        <span className="score--mal score-history__score">{formatScore(row.score)}</span>
                        <span className="score-history__move">{row.move === null ? '' : formatMove(row.move)}</span>
                        {row.markers.map((marker) => (
                          <span key={marker.animeId} className="score-history__first">
                            First episode of {pickDisplayTitle(marker.title, marker.englishTitle)}
                          </span>
                        ))}
                      </li>
                    ))}
                  </ul>
                </section>
              ))}
            </div>
          </>
        )}
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
