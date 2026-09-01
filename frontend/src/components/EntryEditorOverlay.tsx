import { useState, type FormEvent } from 'react'
import { DateField } from './DateField.tsx'
import { Modal } from './Modal.tsx'
import { ApiError, deleteEntry, updateEntry } from '../api/client.ts'
import type { EntryEditorTarget, UserAnimeEntryDto, UserAnimeEntryEditRequest, WatchStatus } from '../api/types.ts'
import { useAnimeRank } from '../context/AnimeRankContext.tsx'
import { hasAiredEpisodes, isHandOrderable } from '../utils/anime.ts'
import './EntryEditorOverlay.css'

const STATUS_OPTIONS: { value: WatchStatus; label: string }[] = [
  { value: 'Watching', label: 'Watching' },
  { value: 'Rewatching', label: 'Rewatching' },
  { value: 'OnHold', label: 'On hold' },
  { value: 'PlanToWatch', label: 'Plan to watch' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Dropped', label: 'Dropped' },
]

// design.md D0: Rewatching is reachable only for an anime that has finished
// airing, and only for an entry with durable evidence of having finished it
// at least once — mirrors backend RewatchingEligibility so the option is
// disabled here before a rejected save is ever attempted.
function canEnterRewatching(entry: UserAnimeEntryDto | null, airingStatus: string | null): boolean {
  if (!entry) return false
  const animeFinished = airingStatus === null || airingStatus === 'finished_airing'
  const finishedOnce = entry.completedAt !== null || entry.rewatchCount > 0 || entry.status === 'Completed'
  return animeFinished && finishedOnce
}

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
  const { animeId, animeTitle, totalEpisodes, airingStatus, episodesAired, mediaType, entry, onSaved, onDeleted } = target
  const { openRanking } = useAnimeRank()
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

  // list-editing "Raising progress resumes an entry" (design.md D5): the
  // status control follows a raised count until the user picks a status
  // themselves, at which point their choice stands — so `statusOverride`
  // rather than a plain `useState` drives which is authoritative.
  const [statusOverride, setStatusOverride] = useState<WatchStatus | null>(null)
  const [episodesWatched, setEpisodesWatched] = useState(initialEpisodesWatched)
  const [myScore, setMyScore] = useState(initialMyScore)
  const [rewatchCount, setRewatchCount] = useState(initialRewatchCount)
  const [startedAt, setStartedAt] = useState(initialStartedAt)
  const [completedAt, setCompletedAt] = useState(initialCompletedAt)
  // list-editing: "Ending a rewatch early asks whether it counts" — defaults
  // to not counting, since an abandoned rewatch is the situation that produces
  // this transition (design.md D2).
  const [countsAsRewatch, setCountsAsRewatch] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const [deleting, setDeleting] = useState(false)

  // Mirrors the backend's own completion target (UserAnimeEntryEditService.
  // ApplyEpisodesWatched): aired-so-far while the anime is still airing,
  // since the eventual total isn't reachable yet; the total otherwise.
  const completionTarget = airingStatus === 'currently_airing' ? episodesAired : totalEpisodes
  const resumes =
    episodesWatched > initialEpisodesWatched &&
    episodesWatched !== completionTarget &&
    initialStatus !== 'Watching' && initialStatus !== 'Rewatching' && initialStatus !== 'Completed'
  const status = statusOverride ?? (resumes ? 'Watching' : initialStatus)
  const episodesRaised = episodesWatched > initialEpisodesWatched

  const canComplete = totalEpisodes !== null
  const canRewatch = canEnterRewatching(entry, airingStatus)
  // list-editing: nothing may be tracked against an anime that has aired no
  // episode — composes with (doesn't replace) the fill-target and rewatching
  // eligibility checks above, so an option can be unavailable for either
  // reason (gate-editing-on-aired-episodes design.md D2/D5).
  const hasAired = hasAiredEpisodes(airingStatus, episodesAired)
  // Only a Rewatching entry being ended early (choosing Completed rather than
  // reaching the total) asks this question — never from any other status.
  const endingRewatchEarly = initialStatus === 'Rewatching' && status === 'Completed'

  // anime-ranking: whether the entry as currently shown in the form (not
  // necessarily saved yet) would get a row in the ranking editor — gates the
  // Rank action (list-editing spec, "Ranking an entry from its editor").
  const rankScore = myScore === 0 ? null : myScore
  const canRank = isHandOrderable(rankScore, status, mediaType, hasAired)

  function buildRequest(): UserAnimeEntryEditRequest {
    return {
      // Sent whenever the count is being raised, whatever `status` ends up
      // showing — not just when it differs from initialStatus — so that
      // re-picking the original status from the editor suppresses the
      // server's own resume rule instead of leaving it to infer one
      // (design.md D5, list-editing "Raising progress resumes an entry").
      status: status !== initialStatus || episodesRaised ? status : undefined,
      episodesWatched: episodesWatched !== initialEpisodesWatched ? episodesWatched : undefined,
      myScore: myScore !== initialMyScore ? myScore : undefined,
      rewatchCount: rewatchCount !== initialRewatchCount ? rewatchCount : undefined,
      startedAt: startedAt !== initialStartedAt ? startedAt || null : undefined,
      completedAt: completedAt !== initialCompletedAt ? completedAt || null : undefined,
      countsAsRewatch: endingRewatchEarly ? countsAsRewatch : undefined,
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)

    if (startedAt && completedAt && completedAt < startedAt) {
      setError('Finish date cannot be earlier than start date.')
      return
    }

    setSaving(true)
    try {
      const saved = await updateEntry(animeId, buildRequest())
      onSaved?.(saved)
      onClose()
    } catch (err) {
      setError(err instanceof ApiError && err.reason ? err.reason : 'Could not save changes. Please try again.')
      setSaving(false)
    }
  }

  // The Rank action (list-editing spec): saves any pending change first —
  // so a score just picked in this form is what the ranking editor opens
  // on, per "Ranking follows a score just changed" — then opens the ranking
  // editor over this one rather than closing it.
  async function handleRank() {
    setError(null)

    if (startedAt && completedAt && completedAt < startedAt) {
      setError('Finish date cannot be earlier than start date.')
      return
    }

    // Opens on the whole-library ('all') scope — the entry's own media type
    // gates whether Rank is offered at all (canRank above), not which
    // ranking-editor scope it opens to.
    const request = buildRequest()
    const isDirty = Object.values(request).some((value) => value !== undefined)
    if (!isDirty) {
      openRanking({ animeId, score: rankScore ?? undefined })
      return
    }

    setSaving(true)
    try {
      const saved = await updateEntry(animeId, request)
      onSaved?.(saved)
      setSaving(false)
      openRanking({ animeId, score: saved.myScore ?? undefined })
    } catch (err) {
      setError(err instanceof ApiError && err.reason ? err.reason : 'Could not save changes. Please try again.')
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
    } catch (err) {
      setError(err instanceof ApiError && err.reason ? err.reason : 'Could not remove this entry. Please try again.')
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
            <select value={status} onChange={(event) => setStatusOverride(event.target.value as WatchStatus)}>
              {STATUS_OPTIONS.map((option) => (
                <option
                  key={option.value}
                  value={option.value}
                  disabled={
                    (option.value === 'Completed' && (!canComplete || !hasAired)) ||
                    (option.value === 'Rewatching' && (!canRewatch || !hasAired)) ||
                    ((option.value === 'Dropped' || option.value === 'OnHold') && !hasAired)
                  }
                >
                  {option.label}
                </option>
              ))}
            </select>
          </label>

          {endingRewatchEarly && (
            <label className="entry-editor__field entry-editor__field--checkbox">
              <input
                type="checkbox"
                checked={countsAsRewatch}
                onChange={(event) => setCountsAsRewatch(event.target.checked)}
              />
              <span>Count this as a rewatch</span>
            </label>
          )}

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
            <select
              className={`entry-editor__score-select${myScore ? ' entry-editor__score-select--mine' : ''}`}
              value={myScore}
              onChange={(event) => setMyScore(Number(event.target.value))}
            >
              <option value={0}>No score</option>
              {Array.from({ length: 10 }, (_, i) => i + 1).map((score) => (
                <option key={score} value={score} disabled={!hasAired}>
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
              max={hasAired ? 100 : 0}
              value={rewatchCount}
              onChange={(event) => setRewatchCount(Number(event.target.value))}
            />
          </label>

          {!hasAired && (
            <p className="entry-editor__aired-note">
              This anime hasn't aired an episode yet, so status, score, and rewatch count are limited until it
              does. An existing value can still be cleared.
            </p>
          )}

          <details className="entry-editor__dates">
            <summary>Dates</summary>
            {/* A plain div, not a <label> — DateField renders five labelable
                controls (three selects, two buttons), and a label wrapping
                more than one implicitly associates with only the first of
                them (the year select). Clicking the "Start/Finish date" text
                or a gap between the boxes would then redirect focus to the
                year select, making it look permanently highlighted. Each
                control already carries its own aria-label. */}
            <div className="entry-editor__field">
              <span>Start date</span>
              <DateField value={startedAt} onChange={setStartedAt} label="start date" idPrefix="entry-editor-start-date" />
            </div>
            <div className="entry-editor__field">
              <span>Finish date</span>
              <DateField value={completedAt} onChange={setCompletedAt} label="finish date" idPrefix="entry-editor-finish-date" />
            </div>
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
          {canRank && (
            <button type="button" className="entry-editor__rank" onClick={handleRank} disabled={saving}>
              Rank
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
