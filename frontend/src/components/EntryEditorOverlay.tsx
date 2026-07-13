import { useState, type FormEvent } from 'react'
import { Modal } from './Modal.tsx'
import { updateEntry } from '../api/client.ts'
import type { EntryEditorTarget, UserAnimeEntryEditRequest, WatchStatus } from '../api/types.ts'
import './EntryEditorOverlay.css'

const STATUS_OPTIONS: { value: WatchStatus; label: string }[] = [
  { value: 'Watching', label: 'Watching' },
  { value: 'OnHold', label: 'On hold' },
  { value: 'PlanToWatch', label: 'Plan to watch' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Dropped', label: 'Dropped' },
]

type EntryEditorOverlayProps = {
  target: EntryEditorTarget
  onClose: () => void
}

// The one reusable "edit or add-to-list" overlay used everywhere an edit
// action appears (my list, top anime, season, detail, dashboard). Exposes
// episodes watched, status, score, and rewatch count only — no start/finish
// date fields, since those are set automatically by the backend's date logic.
export function EntryEditorOverlay({ target, onClose }: EntryEditorOverlayProps) {
  const { animeId, animeTitle, totalEpisodes, entry, onSaved } = target
  // Captured once (the parent remounts this component per target via `key`,
  // see EntryEditorContext) so the save handler can tell which fields the
  // user actually touched and send only those — a stale page open in another
  // tab, or a diff accepted mid-session, won't silently revert other fields.
  const initialStatus = entry?.status ?? 'PlanToWatch'
  const initialEpisodesWatched = entry?.episodesWatched ?? 0
  const initialMyScore = entry?.myScore ?? 0
  const initialRewatchCount = entry?.rewatchCount ?? 0

  const [status, setStatus] = useState<WatchStatus>(initialStatus)
  const [episodesWatched, setEpisodesWatched] = useState(initialEpisodesWatched)
  const [myScore, setMyScore] = useState(initialMyScore)
  const [rewatchCount, setRewatchCount] = useState(initialRewatchCount)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const canComplete = totalEpisodes !== null

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSaving(true)
    setError(null)

    const request: UserAnimeEntryEditRequest = {
      status: status !== initialStatus ? status : undefined,
      episodesWatched: episodesWatched !== initialEpisodesWatched ? episodesWatched : undefined,
      myScore: myScore !== initialMyScore ? myScore : undefined,
      rewatchCount: rewatchCount !== initialRewatchCount ? rewatchCount : undefined,
    }
    try {
      const saved = await updateEntry(animeId, request)
      onSaved?.(saved)
      onClose()
    } catch {
      setError('Could not save changes. Please try again.')
      setSaving(false)
    }
  }

  return (
    <Modal onClose={onClose} labelledBy="entry-editor-title">
      <form className="entry-editor" onSubmit={handleSubmit}>
        <h2 id="entry-editor-title" className="entry-editor__title">
          {animeTitle}
        </h2>

        <label className="entry-editor__field">
          <span>Status</span>
          <select value={status} onChange={(event) => setStatus(event.target.value as WatchStatus)}>
            {STATUS_OPTIONS.map((option) => (
              <option key={option.value} value={option.value} disabled={option.value === 'Completed' && !canComplete}>
                {option.label}
              </option>
            ))}
          </select>
        </label>

        <label className="entry-editor__field">
          <span>Episodes watched{totalEpisodes !== null ? ` / ${totalEpisodes}` : ' / ?'}</span>
          <input
            type="number"
            min={0}
            max={totalEpisodes ?? undefined}
            value={episodesWatched}
            onChange={(event) => setEpisodesWatched(Number(event.target.value))}
          />
        </label>

        <label className="entry-editor__field">
          <span>Score</span>
          <select value={myScore} onChange={(event) => setMyScore(Number(event.target.value))}>
            <option value={0}>No score</option>
            {Array.from({ length: 10 }, (_, i) => i + 1).map((score) => (
              <option key={score} value={score}>
                {score}
              </option>
            ))}
          </select>
        </label>

        <label className="entry-editor__field">
          <span>Rewatch count</span>
          <input
            type="number"
            min={0}
            value={rewatchCount}
            onChange={(event) => setRewatchCount(Number(event.target.value))}
          />
        </label>

        {error && <p className="entry-editor__error">{error}</p>}

        <div className="entry-editor__buttons">
          <button type="button" onClick={onClose} disabled={saving}>
            Cancel
          </button>
          <button type="submit" className="entry-editor__save" disabled={saving}>
            {saving ? 'Saving…' : 'Save'}
          </button>
        </div>
      </form>
    </Modal>
  )
}
