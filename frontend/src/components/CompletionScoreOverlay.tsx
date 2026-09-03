import { useState } from 'react'
import { Modal } from './Modal.tsx'
import { RowPicture } from './RowPicture.tsx'
import { updateEntry } from '../api/client.ts'
import type { UserAnimeEntryDto } from '../api/types.ts'
import { useAnimeRank } from '../context/AnimeRankContext.tsx'
import { isHandOrderable } from '../utils/anime.ts'
import './CompletionScoreOverlay.css'

type CompletionScoreOverlayProps = {
  animeId: number
  animeTitle: string
  pictureUrl: string | null
  currentScore: number | null
  // The anime's raw media type — gates save-and-rank (list-editing spec,
  // "The prompt offers to rank"): a Music/CM/PV entry is never
  // hand-orderable, whatever score it's given. null (an unplumbed caller)
  // behaves as "not short-form", same convention as EntryEditorTarget.
  mediaType: string | null
  // Carries the saved entry when a score was actually saved, null on skip
  // (button, Escape, or click-outside) so the caller can patch instead of
  // re-reading.
  onClose: (saved: UserAnimeEntryDto | null) => void
}

// Shown right after a "+" increment completes an entry, so the user can rate
// it in the same moment instead of a separate trip to the entry editor. Built
// on the shared Modal (Esc/click-outside close) like EntryEditorOverlay.
export function CompletionScoreOverlay({
  animeId,
  animeTitle,
  pictureUrl,
  currentScore,
  mediaType,
  onClose,
}: CompletionScoreOverlayProps) {
  const initialScore = currentScore ?? 0
  const [score, setScore] = useState(initialScore)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const { openRanking } = useAnimeRank()

  // The completion prompt only ever opens on an entry reaching Completed
  // (with a finish date), so status and "has aired" are always settled here
  // — the only thing left to gate on is the chosen score and the media type.
  const canRank = isHandOrderable(score === 0 ? null : score, 'Completed', mediaType, true)

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

  // "Save and rank" (list-editing spec, D10): saves by the identical path,
  // closes the prompt (so the caller's view refresh fires now, not once the
  // ranking editor is later dismissed), then opens the ranking editor
  // focused on this anime, on the score just saved. On failure the ranking
  // editor never opens, same as a plain failed save.
  async function handleSaveAndRank() {
    setSaving(true)
    setError(null)
    try {
      const saved = await updateEntry(animeId, { myScore: score })
      onClose(saved)
      // Opens on the whole-library ('all') scope, not narrowed by this
      // anime's own media type — that's a ranking-editor filter, unrelated
      // to the anime's type itself.
      openRanking({ animeId, score })
    } catch {
      setError('Could not save the score. Please try again.')
      setSaving(false)
    }
  }

  return (
    <Modal onClose={() => onClose(null)} labelledBy="completion-score-title">
      <div className="completion-score">
        <RowPicture src={pictureUrl} className="completion-score__picture" />
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
            {canRank && (
              <button type="button" className="completion-score__save-and-rank" onClick={handleSaveAndRank} disabled={saving}>
                {saving ? 'Saving…' : 'Save and rank'}
              </button>
            )}
          </div>
        </div>
      </div>
    </Modal>
  )
}
