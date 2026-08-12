import { useState, type FormEvent } from 'react'
import { Modal } from './Modal.tsx'
import { deleteEntry, updateEntry } from '../api/client.ts'
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
// episodes watched, status, score, rewatch count, and (behind a disclosure)
// start/finish dates. Also offers Delete — removing the anime from my list
// entirely — but only when editing an existing entry, not while adding one.
export function EntryEditorOverlay({ target, onClose }: EntryEditorOverlayProps) {
  const { animeId, animeTitle, totalEpisodes, entry, onSaved, onDeleted } = target
  // Captured once (the parent remounts this component per target via `key`,
  // see EntryEditorContext) so the save handler can tell which fields the
  // user actually touched and send only those — a stale page open in another
  // tab, or a diff accepted mid-session, won't silently revert other fields.
  const initialStatus = entry?.status ?? 'PlanToWatch'
  const initialEpisodesWatched = entry?.episodesWatched ?? 0
  const initialMyScore = entry?.myScore ?? 0
  const initialRewatchCount = entry?.rewatchCount ?? 0
  const initialStartedAt = entry?.startedAt ?? ''
  const initialCompletedAt = entry?.completedAt ?? ''

  const [status, setStatus] = useState<WatchStatus>(initialStatus)
  const [episodesWatched, setEpisodesWatched] = useState(initialEpisodesWatched)
  const [myScore, setMyScore] = useState(initialMyScore)
  const [rewatchCount, setRewatchCount] = useState(initialRewatchCount)
  const [startedAt, setStartedAt] = useState(initialStartedAt)
  const [completedAt, setCompletedAt] = useState(initialCompletedAt)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const [deleting, setDeleting] = useState(false)

  const canComplete = totalEpisodes !== null

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)

    if (startedAt && completedAt && completedAt < startedAt) {
      setError('Finish date cannot be earlier than start date.')
      return
    }

    setSaving(true)

    const request: UserAnimeEntryEditRequest = {
      status: status !== initialStatus ? status : undefined,
      episodesWatched: episodesWatched !== initialEpisodesWatched ? episodesWatched : undefined,
      myScore: myScore !== initialMyScore ? myScore : undefined,
      rewatchCount: rewatchCount !== initialRewatchCount ? rewatchCount : undefined,
      startedAt: startedAt !== initialStartedAt ? startedAt || null : undefined,
      completedAt: completedAt !== initialCompletedAt ? completedAt || null : undefined,
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

  async function handleConfirmDelete() {
    setError(null)
    setDeleting(true)
    try {
      await deleteEntry(animeId)
      onDeleted?.()
      onClose()
    } catch {
      setError('Could not remove this entry. Please try again.')
      setDeleting(false)
    }
  }

  if (confirmingDelete) {
    return (
      <Modal onClose={onClose} labelledBy="entry-editor-title">
        <div className="entry-editor">
          <h2 id="entry-editor-title" className="entry-editor__title">
            Remove {animeTitle}?
          </h2>
          <p className="entry-editor__confirm-text">
            This removes {animeTitle} from your list and queues its removal from MyAnimeList too.
          </p>

          {error && <p className="entry-editor__error">{error}</p>}

          <div className="entry-editor__buttons">
            <button type="button" onClick={() => setConfirmingDelete(false)} disabled={deleting}>
              Cancel
            </button>
            <button type="button" className="entry-editor__delete-confirm" onClick={handleConfirmDelete} disabled={deleting}>
              {deleting ? 'Removing…' : 'Remove'}
            </button>
          </div>
        </div>
      </Modal>
    )
  }

  return (
    <Modal onClose={onClose} labelledBy="entry-editor-title">
      <form className="entry-editor" onSubmit={handleSubmit}>
        <h2 id="entry-editor-title" className="entry-editor__title">
          {animeTitle}
        </h2>

        <fieldset className="entry-editor__fieldset" disabled={saving}>
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
            <span>Episodes watched</span>
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
              max={100}
              value={rewatchCount}
              onChange={(event) => setRewatchCount(Number(event.target.value))}
            />
          </label>

          <details className="entry-editor__dates">
            <summary>Dates</summary>
            <label className="entry-editor__field">
              <span>Start date</span>
              <input type="date" value={startedAt} onChange={(event) => setStartedAt(event.target.value)} />
            </label>
            <label className="entry-editor__field">
              <span>Finish date</span>
              <input type="date" value={completedAt} onChange={(event) => setCompletedAt(event.target.value)} />
            </label>
          </details>
        </fieldset>

        {error && <p className="entry-editor__error">{error}</p>}

        <div className="entry-editor__buttons">
          {entry && (
            <button
              type="button"
              className="entry-editor__delete"
              onClick={() => {
                setError(null)
                setConfirmingDelete(true)
              }}
              disabled={saving}
            >
              Delete
            </button>
          )}
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
