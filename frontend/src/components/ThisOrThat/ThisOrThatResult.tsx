import { useEffect, useState } from 'react'
import type { ThisOrThatResultRowDto } from '../../api/types.ts'
import { pickDisplayTitle } from '../../utils/anime.ts'
import { RowPicture } from '../RowPicture.tsx'
import '../Games/GameButton.css'
import './ThisOrThatResult.css'

type ThisOrThatResultProps = {
  rows: ThisOrThatResultRowDto[]
  canUndo: boolean
  /** A request is in flight: every button takes no press. */
  busy: boolean
  onSave: () => void
  onKeep: () => void
  onUndo: () => void
}

// How far a row moved from the score's current order, drawn and spoken: more than
// colour tells up from down (this-or-that "A finished ranking shows its new order
// before saving").
function movement(row: ThisOrThatResultRowDto): { kind: 'up' | 'down' | 'same'; shown: string; spoken: string } {
  const moved = row.previousPosition - row.position
  if (moved > 0) return { kind: 'up', shown: `▲ ${moved}`, spoken: `up ${moved}` }
  if (moved < 0) return { kind: 'down', shown: `▼ ${-moved}`, spoken: `down ${-moved}` }
  return { kind: 'same', shown: '–', spoken: 'no change' }
}

// The result screen (this-or-that, design D11): the score's anime in the order the
// picks gave them, each with its new position and how far it moved. Save this order
// and Keep my old order sit above the list so neither needs a scroll through 149
// rows; an order that came out as it already was says so and offers Done, which
// ends the ranking without writing. Nothing has been written until Save.
export function ThisOrThatResult({ rows, canUndo, busy, onSave, onKeep, onUndo }: ThisOrThatResultProps) {
  const unchanged = rows.every((row) => row.position === row.previousPosition)

  // Set once the screen is up rather than rendered with it, so a screen reader hears it.
  const [announcement, setAnnouncement] = useState('')
  useEffect(() => setAnnouncement('Your new order is ready'), [])

  return (
    <section className="this-or-that-result" aria-labelledby="this-or-that-result-title">
      <div className="this-or-that-result__bar">
        <h2 id="this-or-that-result-title" className="this-or-that-result__title">
          Your new order
        </h2>
        <div className="this-or-that-result__actions">
          {unchanged ? (
            <button type="button" className="game-button game-button--small" aria-disabled={busy} onClick={busy ? undefined : onKeep}>
              Done
            </button>
          ) : (
            <>
              <button type="button" className="game-button game-button--small" aria-disabled={busy} onClick={busy ? undefined : onSave}>
                Save this order
              </button>
              <button type="button" className="game-outline-button" aria-disabled={busy} onClick={busy ? undefined : onKeep}>
                Keep my old order
              </button>
            </>
          )}
          {canUndo && (
            <button type="button" className="game-outline-button" aria-disabled={busy} onClick={busy ? undefined : onUndo}>
              Undo
            </button>
          )}
        </div>
      </div>
      {unchanged && <p className="this-or-that-result__same">Same order as before</p>}

      <ol className="this-or-that-result__list">
        {rows.map((row) => {
          const moved = movement(row)
          const title = pickDisplayTitle(row.title, row.englishTitle)
          return (
            <li key={row.animeId} className="this-or-that-result__row">
              <span className="this-or-that-result__position">{row.position}</span>
              <RowPicture src={row.pictureUrl} className="this-or-that-result__picture" />
              <span className="this-or-that-result__name" title={title}>
                {title}
              </span>
              <span className={`this-or-that-result__moved this-or-that-result__moved--${moved.kind}`}>
                <span aria-hidden="true">{moved.shown}</span>
                <span className="visually-hidden">{moved.spoken}</span>
              </span>
            </li>
          )
        })}
      </ol>
      <p className="visually-hidden" aria-live="polite">
        {announcement}
      </p>
    </section>
  )
}
