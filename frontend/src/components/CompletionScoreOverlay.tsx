import { useState } from 'react'
import { Modal } from './Modal.tsx'
import { updateEntry } from '../api/client.ts'
import type { UserAnimeEntryDto } from '../api/types.ts'
import { useAnimeRank } from '../context/AnimeRankContext.tsx'
import { useDisplayPicture } from '../hooks/useDisplayPicture.ts'
import { isLandscapeRatio, useOrientationPicture } from '../hooks/useLandscapePicture.ts'
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
  // null means Cancel, Escape, or a click outside — the caller must not
  // treat that as a commit. Otherwise carries the saved entry, so the caller
  // can patch instead of re-reading.
  onClose: (saved: UserAnimeEntryDto | null) => void
}

// Shown right after a "+" increment completes an entry, so the user can rate
// it in the same moment instead of a separate trip to the entry editor. Built
// on the shared Modal (Esc/click-outside close) like EntryEditorOverlay.
//
// The picture is drawn whole under list-editing's rule for this prompt (design
// D6 of polish-series-header-and-completion-prompt), no longer as a row slot:
// one of two fixed widths, its own height, so RowPicture (height-bound) is not
// used. The <img> sits in a frame that carries the placeholder surface, since
// an <img> can't fade over its own background, the same recipe as
// RowPicture.css. One callback ref serves both facts the prompt needs from the
// <img>, strict landscape and how the picture arrived. It downloads at the
// `tile` width, enough for the wider of the two boxes.
export function CompletionScoreOverlay({
  animeId,
  animeTitle,
  pictureUrl,
  currentScore,
  mediaType,
  onClose,
}: CompletionScoreOverlayProps) {
  const [score, setScore] = useState(currentScore ?? 0)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const { openRanking } = useAnimeRank()
  const { displaySrc, onError } = useDisplayPicture(pictureUrl, 'tile')
  const [pictureRef, pictureRatio, arrival] = useOrientationPicture(displaySrc)
  const isLandscape = isLandscapeRatio(pictureRatio)

  // The completion prompt only ever opens on an entry reaching Completed
  // (with a finish date), so status and "has aired" are always settled here
  // — the only thing left to gate on is the chosen score and the media type.
  const canRank = isHandOrderable(score === 0 ? null : score, 'Completed', mediaType, true)

  // Save always closes as a save (list-editing spec, "Saving or dismissing
  // the completion score prompt"): even an unchanged score is sent, since the
  // server treats that as a no-op and Save must not act like Cancel.
  async function handleSave() {
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
    <Modal onClose={() => onClose(null)} labelledBy="completion-score-title" className="modal--wide">
      <div className={`completion-score${isLandscape ? ' completion-score--landscape' : ''}`}>
        {pictureUrl ? (
          <span className={`completion-score__picture-frame completion-score__picture-frame--${arrival}`}>
            <img ref={pictureRef} src={displaySrc} alt="" onError={onError} className="completion-score__picture" />
          </span>
        ) : (
          <div aria-hidden="true" className="completion-score__picture completion-score__picture--placeholder" />
        )}
        <div className="completion-score__body">
          <h2 id="completion-score-title" className="completion-score__title">
            {animeTitle}
          </h2>
          <p className="completion-score__hint">You finished this one — want to give it a score?</p>
          <label className="completion-score__field">
            <span>Score</span>
            <select
              className={`completion-score__select${score > 0 ? ' completion-score__select--mine' : ''}`}
              value={score}
              onChange={(event) => setScore(Number(event.target.value))}
            >
              <option value={0}>No score</option>
              {Array.from({ length: 10 }, (_, i) => i + 1).map((value) => (
                <option key={value} value={value}>
                  {value}
                </option>
              ))}
            </select>
          </label>

          {error && <p className="completion-score__error">{error}</p>}
        </div>

        {/* Cancel, Save and rank, Save: Save stays last, so it doesn't move
            when a score change withdraws Save and rank. */}
        <div className="completion-score__buttons">
          <button type="button" className="completion-score__cancel" onClick={() => onClose(null)} disabled={saving}>
            Cancel
          </button>
          {canRank && (
            <button type="button" className="completion-score__save-and-rank" onClick={handleSaveAndRank} disabled={saving}>
              {saving ? 'Saving…' : 'Save and rank'}
            </button>
          )}
          <button type="button" className="completion-score__save" onClick={handleSave} disabled={saving}>
            {saving ? 'Saving…' : 'Save'}
          </button>
        </div>
      </div>
    </Modal>
  )
}
