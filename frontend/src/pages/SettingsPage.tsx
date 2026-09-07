import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import {
  acceptAllHeldChanges,
  acceptHeldChange,
  acceptReconciliationDiff,
  cancelReconciliationDiff,
  declineAllHeldChanges,
  declineHeldChange,
  getAiringFullRefreshStatus,
  getHeldChanges,
  getMalAuthStatus,
  getPendingReconciliationDiff,
  getResyncFromMalStatus,
  getSeriesBulkBuildStatus,
  getSyncStatus,
  refreshAnime,
  runReconciliation,
  syncNow,
  triggerAiringFullRefresh,
  triggerResyncFromMal,
  triggerSeriesBulkBuild,
} from '../api/client.ts'
import type {
  AiringFullRefreshStatusDto,
  AnimeSearchResult,
  HeldChangeDto,
  HeldChangeRecentChangeDto,
  HeldChangeValuesDto,
  MalAuthStatus,
  PendingReconciliationDiffDto,
  ResyncStatusDto,
  SeriesBulkBuildStatusDto,
  SyncStatusDto,
} from '../api/types.ts'
import { RowPicture } from '../components/RowPicture.tsx'
import { useContentFilter } from '../context/ContentFilterContext.tsx'
import { useScoreVisibility } from '../context/ScoreVisibilityContext.tsx'
import { useAnimeSearch } from '../hooks/useAnimeSearch.ts'
import { useClickOutside } from '../hooks/useClickOutside.ts'
import { STATUS_LABELS, formatTimestamp, pickDisplayTitle } from '../utils/anime.ts'
import './SettingsPage.css'

// A named group of controls (design.md decision 10): every control on the
// page belongs to exactly one of these, in an order that runs cheapest/most
// reversible first — instant preferences, the routine sync, the minutes-long
// jobs, the account connection last.
function SettingsGroup({ title, hint, children }: { title: string; hint: string; children: ReactNode }) {
  return (
    <section className="settings-group">
      <div className="settings-group__header">
        <h2>{title}</h2>
        <p className="settings-group__hint">{hint}</p>
      </div>
      <div className="settings-group__body">{children}</div>
    </section>
  )
}

// An instant preference (design.md decision 10): one compact row, control on
// the left, taking effect the moment it's changed — visually distinct from
// SettingsAction's titled block-with-a-button so a reader can tell the two
// apart without reading either's explanation.
function SettingsToggleRow({
  checked,
  onChange,
  label,
  hint,
}: {
  checked: boolean
  onChange: () => void
  label: string
  hint: string
}) {
  return (
    <label className="settings-toggle-row">
      <input type="checkbox" checked={checked} onChange={onChange} />
      <span className="settings-toggle-row__text">
        <span className="settings-toggle-row__label">{label}</span>
        <span className="settings-toggle-row__hint">{hint}</span>
      </span>
    </label>
  )
}

// An action that starts work (design.md decision 10): name, explanation,
// optional run state (a JobProgress for the three background jobs), and one
// button — the titled-block shape SettingsToggleRow's one-line form is built
// to contrast with.
function SettingsAction({
  title,
  hint,
  state,
  button,
}: {
  title: string
  hint: string
  state?: ReactNode
  button: ReactNode
}) {
  return (
    <div className="settings-action">
      <div className="settings-action__info">
        <h3 className="settings-action__title">{title}</h3>
        <p className="settings-action__hint">{hint}</p>
        {state}
      </div>
      <div className="settings-action__control">{button}</div>
    </div>
  )
}

type JobPhase = 'not-started' | 'running' | 'complete' | 'failed'

// Normalises the three jobs' own phase unions (ResyncPhase/AiringFullRefreshPhase
// lack 'Failed'; SeriesBulkBuildPhase has it) into one shape JobProgress reads.
function jobPhase(phase: 'NotStarted' | 'Running' | 'Complete' | 'Failed'): JobPhase {
  switch (phase) {
    case 'NotStarted':
      return 'not-started'
    case 'Running':
      return 'running'
    case 'Complete':
      return 'complete'
    case 'Failed':
      return 'failed'
  }
}

// The shared background-job readout (design.md decision 10): a
// role="progressbar" track filled done/total plus the counts beside it,
// used identically by all three background jobs so a reader learns to read
// it once. A job that has never run shows nothing rather than a zeroed
// state, and a failed run is marked visually distinct rather than differing
// only in wording.
function JobProgress({ phase, done, total, noun }: { phase: JobPhase; done: number; total: number; noun: string }) {
  if (phase === 'not-started') return null
  const pct = total > 0 ? Math.min(100, Math.round((done / total) * 100)) : 0
  return (
    <div className={phase === 'failed' ? 'job-progress job-progress--failed' : 'job-progress'}>
      <div
        className="job-progress__track"
        role="progressbar"
        aria-valuenow={done}
        aria-valuemin={0}
        aria-valuemax={total}
      >
        <div className="job-progress__fill" style={{ width: `${pct}%` }} />
      </div>
      <span className="job-progress__counts">
        {phase === 'running'
          ? `Running… ${done}/${total} ${noun}`
          : phase === 'failed'
            ? `Failed after ${done}/${total} ${noun} — see backend logs.`
            : `Complete — ${done}/${total} ${noun}`}
      </span>
    </div>
  )
}

// Mirrors the backend's ActivityFeedComposer.Summarize for the small,
// uncollapsed slice of history a held row shows (design.md D4) — same
// ChangeType/ChangeDetail parsing, minus the episode-run collapsing and
// completion/score merging that only matter across a whole feed.
function summarizeHeldChange(change: HeldChangeRecentChangeDto): string {
  const detail = change.changeDetail
  switch (change.changeType) {
    case 'Added': {
      const prefix = 'Added as '
      const status = detail?.startsWith(prefix) ? (detail.slice(prefix.length) as keyof typeof STATUS_LABELS) : null
      if (status && status in STATUS_LABELS) return `Added to list as ${STATUS_LABELS[status]}`
      break
    }
    case 'Completed':
      return 'Completed'
    case 'StatusChanged': {
      const separator = ' -> '
      const separatorIndex = detail?.indexOf(separator) ?? -1
      if (detail && separatorIndex >= 0) {
        const from = detail.slice(0, separatorIndex) as keyof typeof STATUS_LABELS
        const to = detail.slice(separatorIndex + separator.length) as keyof typeof STATUS_LABELS
        if (from in STATUS_LABELS && to in STATUS_LABELS) return `${STATUS_LABELS[from]} → ${STATUS_LABELS[to]}`
      }
      break
    }
    case 'Removed':
      return 'Removed from list'
  }
  return detail ?? change.changeType.replace(/([a-z])([A-Z])/g, '$1 $2')
}

// The six pushed fields, rendered the same way the reconciliation diff
// renders them, so the two review surfaces read as one pattern.
function formatHeldValues(values: HeldChangeValuesDto): string {
  return `${STATUS_LABELS[values.status]}, ${values.episodesWatched} ep${values.myScore !== null ? `, score ${values.myScore}` : ''}`
}

// Operational/settings page: sync status + manual triggers, pending
// reconciliation-diff review, MAL re-authorization, and on-demand
// force-refresh of a single anime's cached metadata. Every action here is a
// thin wrapper around endpoints that already exist (sections 6/7) — this page
// is the missing UI surface for them.
export function SettingsPage() {
  const [status, setStatus] = useState<SyncStatusDto | null>(null)
  const [diff, setDiff] = useState<PendingReconciliationDiffDto | null>(null)
  const [heldChanges, setHeldChanges] = useState<HeldChangeDto[] | null>(null)
  const [authStatus, setAuthStatus] = useState<MalAuthStatus | null>(null)
  const [resyncStatus, setResyncStatus] = useState<ResyncStatusDto | null>(null)
  const [airingRefreshStatus, setAiringRefreshStatus] = useState<AiringFullRefreshStatusDto | null>(null)
  const [seriesBulkBuildStatus, setSeriesBulkBuildStatus] = useState<SeriesBulkBuildStatusDto | null>(null)
  const [loading, setLoading] = useState(true)

  const [resyncing, setResyncing] = useState(false)
  const [reconciling, setReconciling] = useState(false)
  const [reviewing, setReviewing] = useState(false)
  const [diffError, setDiffError] = useState<string | null>(null)
  const [heldActingId, setHeldActingId] = useState<number | 'all' | null>(null)
  const [heldError, setHeldError] = useState<string | null>(null)
  const [startingFullResync, setStartingFullResync] = useState(false)
  const [startingAiringRefresh, setStartingAiringRefresh] = useState(false)
  const [startingSeriesBulkBuild, setStartingSeriesBulkBuild] = useState(false)

  const { alwaysShowCompletedScores, toggleAlwaysShowCompletedScores } = useScoreVisibility()
  const { hideHentai, toggleHideHentai } = useContentFilter()

  const load = useCallback(() => {
    return Promise.all([
      getSyncStatus()
        .then(setStatus)
        .catch(() => setStatus(null)),
      getPendingReconciliationDiff()
        .then(setDiff)
        .catch(() => setDiff(null)),
      getHeldChanges()
        .then(setHeldChanges)
        .catch(() => setHeldChanges(null)),
      getMalAuthStatus()
        .then(setAuthStatus)
        .catch(() => setAuthStatus(null)),
      getResyncFromMalStatus()
        .then(setResyncStatus)
        .catch(() => setResyncStatus(null)),
      getAiringFullRefreshStatus()
        .then(setAiringRefreshStatus)
        .catch(() => setAiringRefreshStatus(null)),
      getSeriesBulkBuildStatus()
        .then(setSeriesBulkBuildStatus)
        .catch(() => setSeriesBulkBuildStatus(null)),
    ])
  }, [])

  useEffect(() => {
    load().finally(() => setLoading(false))
  }, [load])

  // Poll while a full re-sync is in flight (~1 anime/sec, so several minutes) —
  // stops as soon as the backend reports it's no longer running.
  useEffect(() => {
    if (resyncStatus?.phase !== 'Running') return
    const id = setInterval(() => {
      getResyncFromMalStatus()
        .then(setResyncStatus)
        .catch(() => {})
    }, 2000)
    return () => clearInterval(id)
  }, [resyncStatus?.phase])

  // Poll while a manual airing-data refresh is in flight (paced through
  // AniList's rate limit, so a full list can take a while) — stops as soon as
  // the backend reports it's no longer running.
  useEffect(() => {
    if (airingRefreshStatus?.phase !== 'Running') return
    const id = setInterval(() => {
      getAiringFullRefreshStatus()
        .then(setAiringRefreshStatus)
        .catch(() => {})
    }, 2000)
    return () => clearInterval(id)
  }, [airingRefreshStatus?.phase])

  // Poll while the "build all series from my list" run is in flight — stops
  // as soon as the backend reports it's no longer running.
  useEffect(() => {
    if (seriesBulkBuildStatus?.phase !== 'Running') return
    const id = setInterval(() => {
      getSeriesBulkBuildStatus()
        .then(setSeriesBulkBuildStatus)
        .catch(() => {})
    }, 2000)
    return () => clearInterval(id)
  }, [seriesBulkBuildStatus?.phase])

  async function handleResyncNow() {
    if (resyncing) return
    setResyncing(true)
    try {
      await syncNow()
      await load()
    } catch {
      // Leave the page showing whatever status was already there.
    } finally {
      setResyncing(false)
    }
  }

  async function handleResyncFromMal() {
    if (startingFullResync || resyncStatus?.phase === 'Running') return
    setStartingFullResync(true)
    try {
      setResyncStatus(await triggerResyncFromMal())
    } catch {
      // Leave whatever status was already there; the button stays retryable.
    } finally {
      setStartingFullResync(false)
    }
  }

  async function handleAiringFullRefresh() {
    if (startingAiringRefresh || airingRefreshStatus?.phase === 'Running') return
    setStartingAiringRefresh(true)
    try {
      setAiringRefreshStatus(await triggerAiringFullRefresh())
    } catch {
      // Leave whatever status was already there; the button stays retryable.
    } finally {
      setStartingAiringRefresh(false)
    }
  }

  async function handleSeriesBulkBuild() {
    if (startingSeriesBulkBuild || seriesBulkBuildStatus?.phase === 'Running') return
    setStartingSeriesBulkBuild(true)
    try {
      setSeriesBulkBuildStatus(await triggerSeriesBulkBuild())
    } catch {
      // Leave whatever status was already there; the button stays retryable.
    } finally {
      setStartingSeriesBulkBuild(false)
    }
  }

  async function handleReconcileNow() {
    if (reconciling) return
    setReconciling(true)
    try {
      await runReconciliation()
      await load()
    } catch {
      // Leave the page showing whatever status was already there.
    } finally {
      setReconciling(false)
    }
  }

  async function handleAcceptDiff() {
    if (reviewing) return
    setReviewing(true)
    setDiffError(null)
    try {
      await acceptReconciliationDiff()
      await load()
    } catch {
      setDiffError('Could not apply the diff. Please try again.')
    } finally {
      setReviewing(false)
    }
  }

  async function handleCancelDiff() {
    if (reviewing) return
    setReviewing(true)
    setDiffError(null)
    try {
      await cancelReconciliationDiff()
      await load()
    } catch {
      setDiffError('Could not discard the diff. Please try again.')
    } finally {
      setReviewing(false)
    }
  }

  async function handleAcceptHeld(animeId: number) {
    if (heldActingId !== null) return
    setHeldActingId(animeId)
    setHeldError(null)
    try {
      const result = await acceptHeldChange(animeId)
      if (!result.applied) setHeldError(result.error ?? 'Could not apply that change. Please try again.')
      await load()
    } catch {
      setHeldError('Could not apply that change. Please try again.')
    } finally {
      setHeldActingId(null)
    }
  }

  async function handleDeclineHeld(animeId: number) {
    if (heldActingId !== null) return
    setHeldActingId(animeId)
    setHeldError(null)
    try {
      const result = await declineHeldChange(animeId)
      if (!result.applied) setHeldError(result.error ?? 'Could not discard that change. Please try again.')
      await load()
    } catch {
      setHeldError('Could not discard that change. Please try again.')
    } finally {
      setHeldActingId(null)
    }
  }

  async function handleAcceptAllHeld() {
    if (heldActingId !== null) return
    setHeldActingId('all')
    setHeldError(null)
    try {
      const result = await acceptAllHeldChanges()
      if (result.stillHeld > 0) {
        setHeldError(`${result.stillHeld} change${result.stillHeld === 1 ? '' : 's'} could not be applied and stayed held.`)
      }
      await load()
    } catch {
      setHeldError('Could not apply the held changes. Please try again.')
    } finally {
      setHeldActingId(null)
    }
  }

  async function handleDeclineAllHeld() {
    if (heldActingId !== null) return
    setHeldActingId('all')
    setHeldError(null)
    try {
      const result = await declineAllHeldChanges()
      if (result.stillHeld > 0) {
        setHeldError(`${result.stillHeld} change${result.stillHeld === 1 ? '' : 's'} could not be discarded and stayed held.`)
      }
      await load()
    } catch {
      setHeldError('Could not discard the held changes. Please try again.')
    } finally {
      setHeldActingId(null)
    }
  }

  if (loading) {
    return <p className="settings-page__loading">Loading…</p>
  }

  return (
    <div className="settings-page">
      <div className="settings-page__header">
        <h1>Settings</h1>
        <p className="settings-page__subtitle">Sync status, corrective tools, and account connections.</p>
      </div>

      <SettingsGroup title="Preferences" hint="Change how the app displays things for you. Takes effect immediately.">
        <SettingsToggleRow
          checked={alwaysShowCompletedScores}
          onChange={toggleAlwaysShowCompletedScores}
          label="Always show MAL scores for completed and dropped shows"
          hint={`Reveal MAL scores for shows you've completed or dropped even while the global "hide scores" toggle is on.`}
        />
        <SettingsToggleRow
          checked={hideHentai}
          onChange={toggleHideHentai}
          label="Hide NSFW"
          hint="Hides NSFW (MAL Rx) from the seasonal page. R and R+ titles, search results, and anything already in your list are unaffected."
        />
      </SettingsGroup>

      <SettingsGroup title="Sync" hint="The state of your ongoing MyAnimeList sync, and the actions that drive it.">
        {status ? (
          <dl className="settings-stats">
            <div className="settings-stats__row">
              <dt>Pending / retrying</dt>
              <dd>{status.pendingCount}</dd>
            </div>
            <div className="settings-stats__row">
              <dt>Held for review</dt>
              {/* design.md D14: taken from the held-list payload, not
                  status.heldCount, so it never disagrees with the rows below. */}
              <dd>{heldChanges?.length ?? 0}</dd>
            </div>
            <div className="settings-stats__row">
              <dt>Last successful sync</dt>
              <dd>{formatTimestamp(status.lastSyncedAt)}</dd>
            </div>
          </dl>
        ) : (
          <p className="settings-box__empty">Couldn't load sync status.</p>
        )}

        <SettingsAction
          title="Sync now"
          hint={
            heldChanges && heldChanges.length > 0
              ? "Pushes your own unsent edits to MyAnimeList right away instead of waiting for the next scheduled sync. Sends nothing else, and changes nothing on your list locally. Changes held for review below are not among what it pushes — they're waiting on your decision."
              : 'Pushes your own unsent edits to MyAnimeList right away instead of waiting for the next scheduled sync. Sends nothing else, and changes nothing on your list locally.'
          }
          button={
            <button type="button" onClick={handleResyncNow} disabled={resyncing}>
              {resyncing ? 'Resyncing…' : 'Resync now'}
            </button>
          }
        />

        <SettingsAction
          title="Run full reconciliation"
          hint="Fetches your current MyAnimeList list, compares it against what's stored locally, and presents the differences below for you to accept or decline — changes nothing on your list until you do."
          button={
            <button type="button" onClick={handleReconcileNow} disabled={reconciling}>
              {reconciling ? 'Reconciling…' : 'Run full reconciliation'}
            </button>
          }
        />

        {heldChanges && heldChanges.length > 0 && (
          <div className="settings-subsection">
            <h3 className="settings-subsection__title">Changes held for review</h3>
            <p className="settings-subsection__hint">
              These are changes from a previous session that never reached MyAnimeList and are waiting on your
              decision. Accepting sends the anime's stored values to MyAnimeList now, overwriting what MyAnimeList
              holds for it. Declining discards the unsent change and takes MyAnimeList's current value for that
              anime instead — unless MyAnimeList holds no entry for it, in which case declining removes the anime
              from your list locally. Anything applied here appears in Latest updates and the full edit history,
              marked as coming from MyAnimeList.
            </p>
            <ul className="settings-held-list">
              {heldChanges.map((item) => {
                const displayTitle = pickDisplayTitle(item.title, item.englishTitle)
                const busy = heldActingId === item.animeId || heldActingId === 'all'
                const destructiveDecline = item.kind === 'Entry' && item.remoteValues === null && !item.remoteUnavailable
                return (
                  <li key={item.animeId} className="settings-held-row">
                    <RowPicture src={item.pictureUrl} className="settings-held-row__picture" />
                    <div className="settings-held-row__body">
                      <span className="settings-held-row__title" title={displayTitle}>
                        {displayTitle}
                      </span>
                      <span className="settings-held-row__meta">
                        {item.kind === 'Removal' ? 'Queued removal' : 'Unsent edit'} — held since{' '}
                        {formatTimestamp(item.heldAt)}
                      </span>
                      <ul className="settings-held-row__changes">
                        {item.recentChanges.map((change, index) => (
                          <li key={index}>
                            {summarizeHeldChange(change)} — {formatTimestamp(change.timestamp)}
                          </li>
                        ))}
                        {item.additionalChangeCount > 0 && <li>and {item.additionalChangeCount} more…</li>}
                      </ul>
                      <span className="settings-held-row__meta">
                        Would send: {item.localValues ? formatHeldValues(item.localValues) : 'remove from MyAnimeList'}
                      </span>
                      <span className="settings-held-row__meta">
                        MyAnimeList currently holds:{' '}
                        {item.remoteUnavailable
                          ? "couldn't be read"
                          : item.remoteValues
                            ? formatHeldValues(item.remoteValues)
                            : 'no entry for this anime'}
                      </span>
                      {destructiveDecline && (
                        <span className="settings-box__error">
                          MyAnimeList holds no entry for this anime — declining removes it from your list locally.
                        </span>
                      )}
                    </div>
                    <div className="settings-box__buttons settings-box__buttons--column">
                      <button type="button" onClick={() => handleDeclineHeld(item.animeId)} disabled={busy}>
                        {heldActingId === item.animeId ? 'Working…' : 'Decline'}
                      </button>
                      <button type="button" onClick={() => handleAcceptHeld(item.animeId)} disabled={busy}>
                        {heldActingId === item.animeId ? 'Working…' : 'Accept'}
                      </button>
                    </div>
                  </li>
                )
              })}
            </ul>
            {heldError && <p className="settings-box__error">{heldError}</p>}
            <div className="settings-box__buttons">
              <button type="button" onClick={handleDeclineAllHeld} disabled={heldActingId !== null}>
                Decline all
              </button>
              <button type="button" onClick={handleAcceptAllHeld} disabled={heldActingId !== null}>
                Accept all
              </button>
            </div>
          </div>
        )}

        {diff && (
          <div className="settings-subsection">
            <h3 className="settings-subsection__title">Pending reconciliation diff</h3>
            <p className="settings-subsection__hint">
              Computed {formatTimestamp(diff.computedAt)} — review before applying. Accepting applies exactly
              the differences listed below and touches nothing else on your list; declining discards them and
              applies none of them. Anything you accept appears in Latest updates and the full edit history,
              marked as coming from MyAnimeList.
            </p>
            <ul className="settings-diff-list">
              {diff.entries.map((entry) => (
                <li key={entry.animeId} className="settings-diff-row">
                  <RowPicture src={entry.pictureUrl} className="settings-diff-row__picture" />
                  <span className="settings-diff-row__title" title={entry.title}>
                    {entry.title}
                  </span>
                  <span className="settings-diff-row__detail">
                    {entry.changeType === 'Added' ? 'New entry' : 'Updated'} — {STATUS_LABELS[entry.status]},{' '}
                    {entry.episodesWatched} ep{entry.myScore !== null ? `, score ${entry.myScore}` : ''}
                  </span>
                </li>
              ))}
            </ul>
            {diffError && <p className="settings-box__error">{diffError}</p>}
            <div className="settings-box__buttons">
              <button type="button" onClick={handleCancelDiff} disabled={reviewing}>
                Cancel
              </button>
              <button type="button" onClick={handleAcceptDiff} disabled={reviewing}>
                {reviewing ? 'Applying…' : 'Accept'}
              </button>
            </div>
          </div>
        )}
      </SettingsGroup>

      <SettingsGroup title="Data tools" hint="Long-running corrective and backfill jobs.">
        <SettingsAction
          title="Correct imported data"
          hint="One-time corrective re-sync: re-fetches your full MyAnimeList and full anime details, then immediately overwrites the local status, episode count, score, and dates for every anime — with no review step — and creates entries for anime not yet tracked locally. Also backfills English title, duration, and source. Takes several minutes; entries with unsynced local edits are left untouched. Anything it applies appears in Latest updates and the full edit history, marked as coming from MyAnimeList."
          state={
            <JobProgress
              phase={resyncStatus ? jobPhase(resyncStatus.phase) : 'not-started'}
              done={resyncStatus?.synced ?? 0}
              total={resyncStatus?.total ?? 0}
              noun="processed"
            />
          }
          button={
            <button
              type="button"
              onClick={handleResyncFromMal}
              disabled={startingFullResync || resyncStatus?.phase === 'Running'}
            >
              {resyncStatus?.phase === 'Running' ? 'Resyncing…' : 'Run corrective re-sync'}
            </button>
          }
        />

        <SettingsAction
          title="Airing dates"
          hint="Re-fetches per-episode airing dates from AniList for every anime in my list, in case something looks wrong. Skips shows that have already finished airing and were fetched successfully before — their episode dates can't change further. Paced to stay under AniList's rate limit, so a full list can take a while; runs in the background."
          state={
            <JobProgress
              phase={airingRefreshStatus ? jobPhase(airingRefreshStatus.phase) : 'not-started'}
              done={airingRefreshStatus?.synced ?? 0}
              total={airingRefreshStatus?.total ?? 0}
              noun="processed"
            />
          }
          button={
            <button
              type="button"
              onClick={handleAiringFullRefresh}
              disabled={startingAiringRefresh || airingRefreshStatus?.phase === 'Running'}
            >
              {airingRefreshStatus?.phase === 'Running' ? 'Refreshing…' : 'Refresh all airing dates'}
            </button>
          }
        />

        <SettingsAction
          title="Build all series"
          hint="Builds a franchise for every anime in my list that isn't part of one yet, so the profile page's Top series ranking can be completed on demand instead of only filling in a little on each profile visit. Runs in the background; can take a while for a large list."
          state={
            <JobProgress
              phase={seriesBulkBuildStatus ? jobPhase(seriesBulkBuildStatus.phase) : 'not-started'}
              done={seriesBulkBuildStatus?.built ?? 0}
              total={seriesBulkBuildStatus?.total ?? 0}
              noun="processed"
            />
          }
          button={
            <button
              type="button"
              onClick={handleSeriesBulkBuild}
              disabled={startingSeriesBulkBuild || seriesBulkBuildStatus?.phase === 'Running'}
            >
              {seriesBulkBuildStatus?.phase === 'Running' ? 'Building…' : 'Build all series from my list'}
            </button>
          }
        />

        <div className="settings-subsection">
          <h3 className="settings-subsection__title">Force-refresh anime metadata</h3>
          <p className="settings-subsection__hint">Search for a specific anime to refresh its cached metadata immediately.</p>
          <AnimeRefreshPicker />
        </div>
      </SettingsGroup>

      <SettingsGroup title="Account" hint="Your MyAnimeList connection.">
        <p className="settings-box__hint">{authStatus?.connected ? 'Connected.' : 'Not connected.'}</p>
        <a className="settings-box__link" href="/api/mal-auth/start">
          Re-authorize with MAL
        </a>
      </SettingsGroup>
    </div>
  )
}

// Search-and-pick input feeding the existing single-anime refresh endpoint —
// same debounced search backing the navbar's SearchBar, just without
// navigation on selection.
type AnimeOnlySearchResult = Extract<AnimeSearchResult, { kind: 'anime' }>

function AnimeRefreshPicker() {
  const [query, setQuery] = useState('')
  const [selected, setSelected] = useState<AnimeOnlySearchResult | null>(null)
  const [refreshing, setRefreshing] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const containerRef = useRef<HTMLDivElement>(null)
  const { results: rawResults, open, dismiss, reopen } = useAnimeSearch(query)
  // This picker refreshes a single anime's cached metadata — a series has no
  // such target, so its rows are filtered out rather than offered here.
  const results = rawResults.filter(
    (result): result is AnimeOnlySearchResult => result.kind === 'anime',
  )

  useClickOutside(containerRef, dismiss)

  function pick(result: AnimeOnlySearchResult) {
    setSelected(result)
    setQuery(pickDisplayTitle(result.title, result.englishTitle))
    dismiss()
    setMessage(null)
  }

  async function handleRefresh() {
    if (!selected || refreshing) return
    setRefreshing(true)
    setMessage(null)
    const displayTitle = pickDisplayTitle(selected.title, selected.englishTitle)
    try {
      await refreshAnime(selected.id)
      setMessage(`Refreshed "${displayTitle}".`)
    } catch {
      setMessage(`Couldn't refresh "${displayTitle}". Please try again.`)
    } finally {
      setRefreshing(false)
    }
  }

  return (
    <div className="settings-refresh-picker">
      <div className="settings-refresh-picker__search" ref={containerRef}>
        <input
          type="search"
          placeholder="Search anime…"
          value={query}
          onChange={(event) => {
            setQuery(event.target.value)
            setSelected(null)
            reopen()
          }}
          onFocus={reopen}
          aria-label="Search anime to refresh"
        />
        {open && results.length > 0 && (
          <ul className="settings-refresh-picker__dropdown">
            {results.map((result) => (
              <li key={result.id}>
                <button type="button" onClick={() => pick(result)}>
                  {result.pictureUrl && <RowPicture src={result.pictureUrl} className="settings-refresh-picker__thumb" />}
                  <span>{pickDisplayTitle(result.title, result.englishTitle)}</span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
      <button
        type="button"
        className="settings-refresh-picker__button"
        onClick={handleRefresh}
        disabled={!selected || refreshing}
      >
        {refreshing ? 'Refreshing…' : 'Refresh'}
      </button>
      {message && <p className="settings-box__hint">{message}</p>}
    </div>
  )
}
