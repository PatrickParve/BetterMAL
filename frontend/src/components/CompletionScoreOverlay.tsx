import { useState } from 'react'
import { Modal } from './Modal.tsx'
import { updateEntry } from '../api/client.ts'
import type { UserAnimeEntryDto } from '../api/types.ts'
import './CompletionScoreOverlay.css'

type CompletionScoreOverlayProps = {
  animeId: number
  animeTitle: string
  pictureUrl: string | null
  currentScore: number | null
  // Carries the saved entry when a score was actually saved, null on skip
  // (button, Escape, or click-outside) so the caller can patch instead of
  // re-reading.
  onClose: (saved: UserAnimeEntryDto | null) => void
}

// Shown right after a "+" increment completes an entry, so the user can rate
// it in the same moment instead of a separate trip to the entry editor. Built
// on the shared Modal (Esc/click-outside close) like EntryEditorOverlay.
export function CompletionScoreOverlay({ animeId, animeTitle, pictureUrl, currentScore, onClose }: CompletionScoreOverlayProps) {
  const initialScore = currentScore ?? 0
  const [score, setScore] = useState(initialScore)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleSave() {
    if (score === initialScore) {
      onClose(null)
      return
    }
    setSaving(true)
    setError(null)
    try {
      const saved = await updateEntry(animeId, { myScore: score })
      onClose(saved)
    } catch {
      setError('Could not save the score. Please try again.')
      setSaving(false)
    }
  }

  return (
    <Modal onClose={() => onClose(null)} labelledBy="completion-score-title">
      <div className="completion-score">
        {pictureUrl ? (
          <img src={pictureUrl} alt="" className="completion-score__picture" />
        ) : (
          <div className="completion-score__picture completion-score__picture--placeholder" aria-hidden="true" />
        )}
        <div className="completion-score__body">
          <h2 id="completion-score-title" className="completion-score__title">
            {animeTitle}
          </h2>
          <p className="completion-score__hint">You finished this one — want to give it a score?</p>
          <label className="completion-score__field">
            <span>Score</span>
            <select value={score} onChange={(event) => setScore(Number(event.target.value))}>
              <option value={0}>No score</option>
              {Array.from({ length: 10 }, (_, i) => i + 1).map((value) => (
                <option key={value} value={value}>
                  {value}
                </option>
              ))}
            </select>
          </label>

          {error && <p className="completion-score__error">{error}</p>}

          <div className="completion-score__buttons">
            <button type="button" onClick={() => onClose(null)} disabled={saving}>
              Skip
            </button>
            <button type="button" className="completion-score__save" onClick={handleSave} disabled={saving}>
              {saving ? 'Saving…' : 'Save'}
            </button>
          </div>
        </div>
      </div>
    </Modal>
  )
}
