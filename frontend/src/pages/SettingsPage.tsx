import { useCallback, useEffect, useRef, useState, type ChangeEvent, type DragEvent, type ReactNode } from 'react'
import {
  acceptAllHeldChanges,
  acceptHeldChange,
  acceptReconciliationDiff,
  ApiError,
  cancelReconciliationDiff,
  declineAllHeldChanges,
  declineHeldChange,
  exportData,
  exportListBackup,
  getHeldChanges,
  getPendingReconciliationDiff,
  getTransferImportStatus,
  importData,
  refreshAnime,
  reportOutcomesSeen,
  runReconciliation,
  syncNow,
  triggerAiringFullRefresh,
  triggerResyncFromMal,
  triggerSeriesBulkBuild,
} from '../api/client.ts'
import type {
  AnimeSearchResult,
  HeldChangeDto,
  HeldChangeRecentChangeDto,
  HeldChangeValuesDto,
  HeldDecisionAction,
  JobPhase,
  PendingReconciliationDiffDto,
  TransferImportFailureDto,
  TransferImportStatusDto,
  WeeklyCheckDto,
} from '../api/types.ts'
import { JobProgressTrack } from '../components/JobProgressTrack.tsx'
import { RowPicture } from '../components/RowPicture.tsx'
import { unseenOutcomes, useAppStatus } from '../context/AppStatusContext.tsx'
import { useContentFilter } from '../context/ContentFilterContext.tsx'
import { useScoreVisibility } from '../context/ScoreVisibilityContext.tsx'
import { useAnimeSearch } from '../hooks/useAnimeSearch.ts'
import { useClickOutside } from '../hooks/useClickOutside.ts'
import { STATUS_LABELS, formatTimestamp, pickDisplayTitle } from '../utils/anime.ts'
import './SettingsPage.css'

// A named group of controls (design.md decision 10): every control on the
// page belongs to exactly one of these, in an order that runs cheapest/most
// reversible first — instant preferences, the routine sync, the minutes-long
// jobs, the transfer between devices, the account connection last.
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
// optional run state (a JobProgress for one of the page's background jobs,
// report-jobs-and-lost-mal-connection settings-page spec "Background jobs
// report progress the same way"), and a button — button is omitted for the
// MyAnimeList list import, which the app starts on its own.
// className/onDrag*/onDrop are only ever passed by the import action, which
// is also a drop target (settings-page spec "Dropping a file starts an
// import") — every other caller leaves them undefined, so no handlers attach.
function SettingsAction({
  title,
  hint,
  state,
  button,
  className,
  onDragOver,
  onDragLeave,
  onDrop,
}: {
  title: string
  hint: string
  state?: ReactNode
  button?: ReactNode
  className?: string
  onDragOver?: (event: DragEvent<HTMLDivElement>) => void
  onDragLeave?: (event: DragEvent<HTMLDivElement>) => void
  onDrop?: (event: DragEvent<HTMLDivElement>) => void
}) {
  return (
    <div
      className={className ? `settings-action ${className}` : 'settings-action'}
      onDragOver={onDragOver}
      onDragLeave={onDragLeave}
      onDrop={onDrop}
    >
      <div className="settings-action__info">
        <h3 className="settings-action__title">{title}</h3>
        <p className="settings-action__hint">{hint}</p>
        {state}
      </div>
      {button && <div className="settings-action__control">{button}</div>}
    </div>
  )
}

// Renders the wording table of design.md D17 (report-jobs-and-lost-mal-
// connection): total is null while the job doesn't yet know how much work
// there is, never zero. retryAt/noRetryPlanned are only ever passed for the
// MyAnimeList list import, the only job that plans its own retry — every
// other caller leaves them undefined, so no "tries again" text is ever
// appended for them.
function progressWords(
  phase: JobPhase,
  done: number,
  total: number | null,
  noun: string,
  error?: string | null,
  retryAt?: string | null,
  noRetryPlanned?: boolean,
): string {
  if (phase === 'Running') {
    if (total === null) return done > 0 ? `Running… ${done} ${noun}` : 'Starting…'
    return `Running… ${done}/${total} ${noun}`
  }
  if (phase === 'Failed') {
    const base =
      total !== null
        ? `Failed after ${done}/${total} ${noun} — ${error ?? 'see backend logs.'}`
        : `Failed — ${error ?? 'see backend logs.'}`
    if (retryAt) return `${base} Tries again at ${formatTimestamp(retryAt)}.`
    if (noRetryPlanned) return `${base} Tries again when the app next starts.`
    return base
  }
  return total !== null ? `Complete — ${done}/${total} ${noun}` : `Complete — ${done} ${noun}`
}

// How long a completed outcome has to stay visible on this page before the
// reporting effect (below, design.md D9) reports it as seen — long enough
// that landing on the page is good evidence it was actually looked at,
// rather than an instant poll marking it seen and clearing the navbar's dot
// before I noticed anything.
const OUTCOME_SEEN_DWELL_MS = 3000

// The shared background-job readout (design.md decision 10, extended by
// report-jobs-and-lost-mal-connection design.md D17): the wording beside a
// JobProgressTrack (navbar-settings-status-indicator design.md D10), used
// identically by every background job the page shows so a reader learns to
// read it once. A job that has never run shows nothing rather than a zeroed
// state, and a failed run is marked visually distinct rather than differing
// only in wording.
function JobProgress({
  phase,
  done,
  total,
  noun,
  error,
  retryAt,
  noRetryPlanned,
  finishedAt,
  outcomeSeen,
}: {
  phase: JobPhase
  done: number
  total: number | null
  noun: string
  error?: string | null
  retryAt?: string | null
  noRetryPlanned?: boolean
  finishedAt: string | null
  outcomeSeen: boolean
}) {
  // Whether a completed run shows at all is decided once — the first time
  // this run (identified by finishedAt) is seen — and frozen for the rest of
  // this page load: the reporting effect below marking it seen a few seconds
  // from now must not make this row vanish out from under someone still
  // looking at it. A run already reported seen before this page loaded (an
  // earlier visit's report already landed, or the app itself started with
  // it already flagged) never shows at all; a fresh one stays visible for
  // the rest of this page's life regardless of what gets reported meanwhile,
  // and only a later page load re-checks whether it's since been seen. Not
  // applied to Failed — a failure still wants my attention, so it never
  // disappears on its own.
  const [frozenRun, setFrozenRun] = useState(finishedAt)
  const [frozenSeen, setFrozenSeen] = useState(outcomeSeen)
  if (finishedAt !== frozenRun) {
    setFrozenRun(finishedAt)
    setFrozenSeen(outcomeSeen)
  }

  if (phase === 'NotStarted') return null
  if (phase === 'Complete' && finishedAt !== null && frozenSeen) return null
  const text = progressWords(phase, done, total, noun, error, retryAt, noRetryPlanned)
  // JobProgressTrack moves continuously only when its own total is null
  // (design D10) — that has to mean "running, total still unknown," not
  // "ended without ever learning one," so a total that's still null once the
  // run has ended is passed through as zero.
  let trackDone = done
  let trackTotal = phase === 'Running' ? total : (total ?? 0)
  if (phase === 'Complete' && !trackTotal) {
    // A completed run whose total was never meaningful — reconciliation
    // never learns one, and a run with nothing to do reports 0 — would
    // otherwise render that flat, empty bar right next to the word
    // "Complete", reading as broken rather than done. A job with a real
    // total already reaches 100% on its own once done catches up, so this
    // only overrides the falsy-total edge case.
    trackDone = 1
    trackTotal = 1
  }
  return (
    <div className={phase === 'Failed' ? 'job-progress job-progress--failed' : 'job-progress'}>
      <JobProgressTrack done={trackDone} total={trackTotal} valueText={text} />
      <span className="job-progress__counts">{text}</span>
    </div>
  )
}

// The Sync group's "Weekly check" readout row (settings-page spec "The
// weekly check is reported in one line").
function formatWeeklyCheck(weekly: WeeklyCheckDto | null | undefined): string {
  if (!weekly) return 'Not run yet'
  const time = formatTimestamp(weekly.lastRunAt)
  if (weekly.failed === true) return `${time} — failed: ${weekly.error ?? 'unknown error'}`
  if (weekly.failed === false) return `${time} — no problems`
  return time
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

// Which jobs the Settings page reloads other state for when they leave
// Running (design.md D16 of report-jobs-and-lost-mal-connection) — syncNow
// and listImport dropped out once the sync figures below started reading
// straight from the shared status (navbar-settings-status-indicator D1),
// which the poll already keeps current.
type TrackedJobPhases = {
  reconcile: JobPhase
  heldDecision: JobPhase
  fileImport: JobPhase
}

// Operational/settings page: sync status + manual triggers, pending
// reconciliation-diff review, MAL re-authorization, and on-demand
// force-refresh of a single anime's cached metadata. Every job's state, and
// the sync group's figures, come from the app-wide status read
// (context/AppStatusContext, mounted once in AppShell) rather than a poll of
// this page's own, so this page, the navbar, another browser, and a run
// started by the app itself always agree (background-jobs "A job's state is
// read from the server").
export function SettingsPage() {
  const [diff, setDiff] = useState<PendingReconciliationDiffDto | null>(null)
  const [heldChanges, setHeldChanges] = useState<HeldChangeDto[] | null>(null)
  const [importStatus, setImportStatus] = useState<TransferImportStatusDto | null>(null)
  const [initialLoading, setInitialLoading] = useState(true)

  const [startingSyncNow, setStartingSyncNow] = useState(false)
  const [startingReconcile, setStartingReconcile] = useState(false)
  const [reviewing, setReviewing] = useState(false)
  const [diffError, setDiffError] = useState<string | null>(null)
  const [heldActingId, setHeldActingId] = useState<number | null>(null)
  const [startingHeldAction, setStartingHeldAction] = useState<HeldDecisionAction | null>(null)
  const [heldError, setHeldError] = useState<string | null>(null)
  const [startingFullResync, setStartingFullResync] = useState(false)
  const [startingAiringRefresh, setStartingAiringRefresh] = useState(false)
  const [startingSeriesBulkBuild, setStartingSeriesBulkBuild] = useState(false)
  const [exporting, setExporting] = useState(false)
  const [exportError, setExportError] = useState<string | null>(null)
  const [exportedFileName, setExportedFileName] = useState<string | null>(null)
  const [backingUp, setBackingUp] = useState(false)
  const [backupError, setBackupError] = useState<string | null>(null)
  const [backupFileName, setBackupFileName] = useState<string | null>(null)
  const [importing, setImporting] = useState(false)
  const [importRefusal, setImportRefusal] = useState<string | null>(null)
  const [importDragOver, setImportDragOver] = useState(false)
  const importInputRef = useRef<HTMLInputElement>(null)

  const { status: appStatus, applyJob, applyWeeklyOutcomeSeen } = useAppStatus()

  const { alwaysShowCompletedScores, toggleAlwaysShowCompletedScores } = useScoreVisibility()
  const { hideHentai, toggleHideHentai } = useContentFilter()

  const load = useCallback(() => {
    return Promise.all([
      getPendingReconciliationDiff()
        .then(setDiff)
        .catch(() => setDiff(null)),
      getHeldChanges()
        .then(setHeldChanges)
        .catch(() => setHeldChanges(null)),
      getTransferImportStatus()
        .then(setImportStatus)
        .catch(() => setImportStatus(null)),
    ])
  }, [])

  useEffect(() => {
    load().finally(() => setInitialLoading(false))
  }, [load])

  const loading = initialLoading || appStatus === null

  // Reloads what each job changes once it leaves Running, from a ref of
  // previous phases rather than a state variable, so this never itself
  // triggers a re-render (design.md D16 table).
  const prevJobPhasesRef = useRef<TrackedJobPhases | null>(null)
  useEffect(() => {
    if (!appStatus) return
    const jobs = appStatus.jobs
    const prev = prevJobPhasesRef.current
    if (prev) {
      if (prev.reconcile === 'Running' && jobs.reconcile.phase !== 'Running') {
        void getPendingReconciliationDiff()
          .then(setDiff)
          .catch(() => {})
      }
      if (prev.heldDecision === 'Running' && jobs.heldDecision.phase !== 'Running') {
        void getHeldChanges()
          .then(setHeldChanges)
          .catch(() => {})
      }
      if (prev.fileImport === 'Running' && jobs.fileImport.phase !== 'Running') {
        void getTransferImportStatus()
          .then(setImportStatus)
          .catch(() => {})
      }
    }
    prevJobPhasesRef.current = {
      reconcile: jobs.reconcile.phase,
      heldDecision: jobs.heldDecision.phase,
      fileImport: jobs.fileImport.phase,
    }
  }, [appStatus])

  // Reports what this page is showing as seen (design.md D9), after it's
  // been showing it a little while (OUTCOME_SEEN_DWELL_MS) — long enough to
  // be sure I was actually on the page and not just a moment's poll —
  // deriving the outcomes not yet seen from the shared status and skipping
  // any already scheduled in this page's lifetime (a run's
  // finishedAt/lastRunAt is a stable key: scheduledOutcomesRef tracks
  // in-flight timers across every poll tick, which is why this can't just
  // use a plain state/effect pair — a poll landing mid-wait must not restart
  // the clock, and one landing after the server's flag flips must not
  // schedule a second report). Merges a successful report back into the
  // shared status at once so the navbar's dot clears without waiting for the
  // next poll; a failed report forgets its key so the next poll retries.
  // Leaving the page before the wait elapses cancels it — the countdown
  // starts over on the next visit. Either way this says nothing to the
  // reader, and JobProgress's own row is unaffected by any of it once shown
  // (frozen at first sight, above).
  const scheduledOutcomesRef = useRef<Map<string, ReturnType<typeof setTimeout>>>(new Map())
  const appStatusRef = useRef(appStatus)
  useEffect(() => {
    appStatusRef.current = appStatus
  }, [appStatus])
  useEffect(() => {
    const scheduled = scheduledOutcomesRef.current
    return () => {
      for (const timeoutId of scheduled.values()) clearTimeout(timeoutId)
      scheduled.clear()
    }
  }, [])
  useEffect(() => {
    if (!appStatus) return
    const scheduled = scheduledOutcomesRef.current
    const outcomes = unseenOutcomes(appStatus)

    for (const { name, finishedAt } of outcomes.jobs) {
      const key = `${name}:${finishedAt}`
      if (scheduled.has(key)) continue
      scheduled.set(
        key,
        setTimeout(() => {
          scheduled.delete(key)
          reportOutcomesSeen({ jobs: [{ name, finishedAt }] })
            .then(() => {
              const current = appStatusRef.current?.jobs[name]
              if (current && current.finishedAt === finishedAt) applyJob(name, { ...current, outcomeSeen: true })
            })
            .catch(() => {})
        }, OUTCOME_SEEN_DWELL_MS),
      )
    }

    const weeklyLastRunAt = outcomes.weeklyCheckLastRunAt
    if (weeklyLastRunAt !== null) {
      const key = `weekly:${weeklyLastRunAt}`
      if (!scheduled.has(key)) {
        scheduled.set(
          key,
          setTimeout(() => {
            scheduled.delete(key)
            reportOutcomesSeen({ weeklyCheckLastRunAt: weeklyLastRunAt })
              .then(() => {
                if (appStatusRef.current?.weeklyCheck?.lastRunAt === weeklyLastRunAt) applyWeeklyOutcomeSeen()
              })
              .catch(() => {})
          }, OUTCOME_SEEN_DWELL_MS),
        )
      }
    }
  }, [appStatus, applyJob, applyWeeklyOutcomeSeen])

  async function handleSyncNow() {
    if (startingSyncNow || appStatus?.jobs.syncNow.phase === 'Running') return
    setStartingSyncNow(true)
    try {
      applyJob('syncNow', await syncNow())
    } catch {
      // Leave whatever status was already there; the button stays retryable.
    } finally {
      setStartingSyncNow(false)
    }
  }

  async function handleReconcileNow() {
    if (startingReconcile || appStatus?.jobs.reconcile.phase === 'Running') return
    setStartingReconcile(true)
    try {
      applyJob('reconcile', await runReconciliation())
    } catch {
      // Leave whatever status was already there; the button stays retryable.
    } finally {
      setStartingReconcile(false)
    }
  }

  async function handleResyncFromMal() {
    if (startingFullResync || appStatus?.jobs.resync.phase === 'Running') return
    setStartingFullResync(true)
    try {
      applyJob('resync', await triggerResyncFromMal())
    } catch {
      // Leave whatever status was already there; the button stays retryable.
    } finally {
      setStartingFullResync(false)
    }
  }

  async function handleAiringFullRefresh() {
    if (startingAiringRefresh || appStatus?.jobs.airingRefresh.phase === 'Running') return
    setStartingAiringRefresh(true)
    try {
      applyJob('airingRefresh', await triggerAiringFullRefresh())
    } catch {
      // Leave whatever status was already there; the button stays retryable.
    } finally {
      setStartingAiringRefresh(false)
    }
  }

  async function handleSeriesBulkBuild() {
    if (startingSeriesBulkBuild || appStatus?.jobs.seriesBuild.phase === 'Running') return
    setStartingSeriesBulkBuild(true)
    try {
      applyJob('seriesBuild', await triggerSeriesBulkBuild())
    } catch {
      // Leave whatever status was already there; the button stays retryable.
    } finally {
      setStartingSeriesBulkBuild(false)
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

  const heldDecisionRunning = appStatus?.jobs.heldDecision.phase === 'Running'

  async function handleAcceptHeld(animeId: number) {
    if (heldActingId !== null || heldDecisionRunning) return
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
    if (heldActingId !== null || heldDecisionRunning) return
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
    if (startingHeldAction || heldDecisionRunning) return
    setStartingHeldAction('Accept')
    setHeldError(null)
    try {
      applyJob('heldDecision', await acceptAllHeldChanges())
    } catch {
      setHeldError('Could not apply the held changes. Please try again.')
    } finally {
      setStartingHeldAction(null)
    }
  }

  async function handleDeclineAllHeld() {
    if (startingHeldAction || heldDecisionRunning) return
    setStartingHeldAction('Decline')
    setHeldError(null)
    try {
      applyJob('heldDecision', await declineAllHeldChanges())
    } catch {
      setHeldError('Could not discard the held changes. Please try again.')
    } finally {
      setStartingHeldAction(null)
    }
  }

  // Saves the file via a temporary object-URL anchor rather than a plain
  // `<a href>` link, so a failure shows the page's own in-place error
  // instead of a bare browser error page (design.md D7). Shared by the
  // export and the list backup (design.md D8 of export-my-list-backup).
  function saveFile(blob: Blob, fileName: string) {
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = fileName
    anchor.click()
    URL.revokeObjectURL(url)
  }

  async function handleExport() {
    if (exporting) return
    setExporting(true)
    setExportError(null)
    setExportedFileName(null)
    try {
      const { blob, fileName } = await exportData()
      saveFile(blob, fileName)
      setExportedFileName(fileName)
    } catch {
      setExportError('The export could not be produced. Please try again.')
    } finally {
      setExporting(false)
    }
  }

  async function handleListBackup() {
    if (backingUp) return
    setBackingUp(true)
    setBackupError(null)
    setBackupFileName(null)
    try {
      const { blob, fileName } = await exportListBackup()
      saveFile(blob, fileName)
      setBackupFileName(fileName)
    } catch {
      setBackupError('The backup could not be produced. Please try again.')
    } finally {
      setBackingUp(false)
    }
  }

  const importRunning = appStatus?.jobs.fileImport.phase === 'Running'

  // Shared by the file picker and the drop target (settings-page spec
  // "Choosing a file starts an import" / "Dropping a file starts an
  // import") — a running import takes neither. The response already carries
  // the fresh Running state (design.md D1 of report-jobs-and-lost-mal-
  // connection), so it's applied to both the transfer-specific status (for
  // its report) and the shared job store at once, rather than waiting for
  // the next poll.
  async function handleImportFile(file: File) {
    if (importing || importRunning) return
    setImporting(true)
    setImportRefusal(null)
    try {
      const result = await importData(file)
      setImportStatus(result)
      applyJob('fileImport', {
        phase: result.phase,
        done: result.done,
        total: result.total,
        error: result.error,
        startedAt: null,
        finishedAt: null,
        retryAt: null,
        outcomeSeen: false,
      })
    } catch (err) {
      setImportRefusal(err instanceof ApiError && err.reason ? err.reason : 'The import was refused. Please try again.')
    } finally {
      setImporting(false)
    }
  }

  function handleChooseImportFile() {
    importInputRef.current?.click()
  }

  function handleImportFileInputChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    // Reset so choosing the same file again still fires this handler.
    event.target.value = ''
    if (file) void handleImportFile(file)
  }

  function handleImportDragOver(event: DragEvent<HTMLDivElement>) {
    event.preventDefault()
    if (importRunning) return
    setImportDragOver(true)
  }

  function handleImportDragLeave() {
    setImportDragOver(false)
  }

  function handleImportDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault()
    setImportDragOver(false)
    if (importRunning) return
    const file = event.dataTransfer.files?.[0]
    if (file) void handleImportFile(file)
  }

  // failure.title is null when this device never learned it (design.md D13).
  function importFailureLabel(failure: TransferImportFailureDto): string {
    if (failure.title) return pickDisplayTitle(failure.title, failure.englishTitle)
    return failure.subject === 'Series' ? `Series ${failure.id}` : `Anime ${failure.id}`
  }

  if (loading || !appStatus) {
    return <p className="settings-page__loading">Loading…</p>
  }

  const jobs = appStatus.jobs

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
        {jobs.listImport.phase !== 'NotStarted' && (
          <SettingsAction
            title="MyAnimeList list import"
            hint="Brings in anime on your MyAnimeList list that this device doesn't have yet — runs when the app starts and after you re-authorize."
            state={
              <JobProgress
                phase={jobs.listImport.phase}
                done={jobs.listImport.done}
                total={jobs.listImport.total}
                noun="anime"
                error={jobs.listImport.error}
                retryAt={jobs.listImport.retryAt}
                noRetryPlanned={jobs.listImport.phase === 'Failed' && jobs.listImport.retryAt === null}
                finishedAt={jobs.listImport.finishedAt}
                outcomeSeen={jobs.listImport.outcomeSeen}
              />
            }
          />
        )}

        <dl className="settings-stats">
          <div className="settings-stats__row">
            <dt>Pending / retrying</dt>
            <dd>{appStatus.sync.pendingCount}</dd>
          </div>
          <div className="settings-stats__row">
            <dt>Held for review</dt>
            {/* design.md D14: taken from the held-list payload, not
                sync.heldCount, so it never disagrees with the rows below. */}
            <dd>{heldChanges?.length ?? 0}</dd>
          </div>
          <div className="settings-stats__row">
            <dt>Last successful sync</dt>
            <dd>{formatTimestamp(appStatus.sync.lastSyncedAt)}</dd>
          </div>
          <div className="settings-stats__row">
            <dt>Weekly check</dt>
            <dd>{formatWeeklyCheck(appStatus.weeklyCheck)}</dd>
          </div>
        </dl>

        <SettingsAction
          title="Sync now"
          hint={
            heldChanges && heldChanges.length > 0
              ? "Pushes your own unsent edits to MyAnimeList right away instead of waiting for the next scheduled sync. Sends nothing else, and changes nothing on your list locally. Changes held for review below are not among what it pushes — they're waiting on your decision."
              : 'Pushes your own unsent edits to MyAnimeList right away instead of waiting for the next scheduled sync. Sends nothing else, and changes nothing on your list locally.'
          }
          state={
            <JobProgress
              phase={jobs.syncNow.phase}
              done={jobs.syncNow.done}
              total={jobs.syncNow.total}
              noun="sent"
              error={jobs.syncNow.error}
              finishedAt={jobs.syncNow.finishedAt}
              outcomeSeen={jobs.syncNow.outcomeSeen}
            />
          }
          button={
            <button type="button" onClick={handleSyncNow} disabled={startingSyncNow || jobs.syncNow.phase === 'Running'}>
              {jobs.syncNow.phase === 'Running' ? 'Resyncing…' : 'Resync now'}
            </button>
          }
        />

        <SettingsAction
          title="Run full reconciliation"
          hint="Fetches your current MyAnimeList list, compares it against what's stored locally, and presents the differences below for you to accept or decline — changes nothing on your list until you do."
          state={
            <JobProgress
              phase={jobs.reconcile.phase}
              done={jobs.reconcile.done}
              total={jobs.reconcile.total}
              noun="anime read"
              error={jobs.reconcile.error}
              finishedAt={jobs.reconcile.finishedAt}
              outcomeSeen={jobs.reconcile.outcomeSeen}
            />
          }
          button={
            <button type="button" onClick={handleReconcileNow} disabled={startingReconcile || jobs.reconcile.phase === 'Running'}>
              {jobs.reconcile.phase === 'Running' ? 'Reconciling…' : 'Run full reconciliation'}
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
              from your list locally. What declining applies is recorded in your edit history, like any other
              change you make here.
            </p>
            <ul className="settings-held-list">
              {heldChanges.map((item) => {
                const displayTitle = pickDisplayTitle(item.title, item.englishTitle)
                const busy = heldActingId === item.animeId || heldDecisionRunning
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
            <JobProgress
              phase={jobs.heldDecision.phase}
              done={jobs.heldDecision.done}
              total={jobs.heldDecision.total}
              noun="decided"
              error={jobs.heldDecision.error}
              finishedAt={jobs.heldDecision.finishedAt}
              outcomeSeen={jobs.heldDecision.outcomeSeen}
            />
            {heldError && <p className="settings-box__error">{heldError}</p>}
            <div className="settings-box__buttons">
              <button type="button" onClick={handleDeclineAllHeld} disabled={heldDecisionRunning || startingHeldAction !== null}>
                Decline all
              </button>
              <button type="button" onClick={handleAcceptAllHeld} disabled={heldDecisionRunning || startingHeldAction !== null}>
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
              applies none of them. Nothing you accept is recorded in Latest updates or the full edit history.
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
          hint="One-time corrective re-sync: re-fetches your full MyAnimeList and full anime details, then immediately overwrites the local status, episode count, score, and dates for every anime — with no review step — and creates entries for anime not yet tracked locally. Also backfills English title, duration, and source. Takes several minutes; entries with unsynced local edits are left untouched. Nothing it applies is recorded in Latest updates or the full edit history."
          state={
            <JobProgress
              phase={jobs.resync.phase}
              done={jobs.resync.done}
              total={jobs.resync.total}
              noun="processed"
              error={jobs.resync.error}
              finishedAt={jobs.resync.finishedAt}
              outcomeSeen={jobs.resync.outcomeSeen}
            />
          }
          button={
            <button type="button" onClick={handleResyncFromMal} disabled={startingFullResync || jobs.resync.phase === 'Running'}>
              {jobs.resync.phase === 'Running' ? 'Resyncing…' : 'Run corrective re-sync'}
            </button>
          }
        />

        <SettingsAction
          title="Airing dates"
          hint="Re-fetches per-episode airing dates from AniList for every anime in my list, in case something looks wrong. Skips shows that have already finished airing and were fetched successfully before — their episode dates can't change further. Paced to stay under AniList's rate limit, so a full list can take a while; runs in the background."
          state={
            <JobProgress
              phase={jobs.airingRefresh.phase}
              done={jobs.airingRefresh.done}
              total={jobs.airingRefresh.total}
              noun="processed"
              error={jobs.airingRefresh.error}
              finishedAt={jobs.airingRefresh.finishedAt}
              outcomeSeen={jobs.airingRefresh.outcomeSeen}
            />
          }
          button={
            <button type="button" onClick={handleAiringFullRefresh} disabled={startingAiringRefresh || jobs.airingRefresh.phase === 'Running'}>
              {jobs.airingRefresh.phase === 'Running' ? 'Refreshing…' : 'Refresh all airing dates'}
            </button>
          }
        />

        <SettingsAction
          title="Build all series"
          hint="Builds a franchise for every anime in my list that isn't part of one yet, so the profile page's Top series ranking can be completed on demand instead of only filling in a little on each profile visit. Runs in the background; can take a while for a large list."
          state={
            <JobProgress
              phase={jobs.seriesBuild.phase}
              done={jobs.seriesBuild.done}
              total={jobs.seriesBuild.total}
              noun="processed"
              error={jobs.seriesBuild.error}
              finishedAt={jobs.seriesBuild.finishedAt}
              outcomeSeen={jobs.seriesBuild.outcomeSeen}
            />
          }
          button={
            <button type="button" onClick={handleSeriesBulkBuild} disabled={startingSeriesBulkBuild || jobs.seriesBuild.phase === 'Running'}>
              {jobs.seriesBuild.phase === 'Running' ? 'Building…' : 'Build all series from my list'}
            </button>
          }
        />

        <div className="settings-subsection">
          <h3 className="settings-subsection__title">Force-refresh anime metadata</h3>
          <p className="settings-subsection__hint">Search for a specific anime to refresh its cached metadata immediately.</p>
          <AnimeRefreshPicker />
        </div>
      </SettingsGroup>

      <SettingsGroup
        title="Files"
        hint="Save a copy of your list, or move what only this app holds between your devices, as a file."
      >
        <SettingsAction
          title="Back up my list"
          hint="Saves every anime in your list — its status, progress, score, dates and rewatch count — as a file. Rewatching stays Rewatching, which MyAnimeList can't hold. Holds nothing else: not your ranking, pictures or edit history, and not your MyAnimeList connection. It's a copy to keep: the app never imports it, and it isn't the file for your other device. Producing it changes nothing here and sends nothing anywhere; the browser saves the file."
          state={
            <>
              {backupFileName && <p className="settings-box__hint">Saved {backupFileName}.</p>}
              {backupError && <p className="settings-box__error">{backupError}</p>}
            </>
          }
          button={
            <button type="button" onClick={handleListBackup} disabled={backingUp}>
              {backingUp ? 'Backing up…' : 'Back up'}
            </button>
          }
        />

        <SettingsAction
          title="Export to a file"
          hint="Holds your ranking, chosen anime pictures, chosen series titles and pictures, and your edit history. Holds nothing from MyAnimeList — not your MyAnimeList connection, and not your display preferences. Producing it changes nothing here and sends nothing anywhere; the browser saves the file, and getting it to your other device is up to you."
          state={
            <>
              {exportedFileName && <p className="settings-box__hint">Saved {exportedFileName}.</p>}
              {exportError && <p className="settings-box__error">{exportError}</p>}
            </>
          }
          button={
            <button type="button" onClick={handleExport} disabled={exporting}>
              {exporting ? 'Exporting…' : 'Export'}
            </button>
          }
        />

        <SettingsAction
          title="Import from a file"
          hint="Merges a file exported on your other device into this one: the edit history is combined, each chosen picture and title goes to whichever device changed it last, and the ranking is replaced whole by whichever device arranged it last. It asks nothing before applying and cannot be undone — export this device first to keep a copy of what it holds. Runs in the background, and can take minutes when anime have to be fetched from MyAnimeList."
          className={importDragOver ? 'settings-action--drop-target' : undefined}
          onDragOver={handleImportDragOver}
          onDragLeave={handleImportDragLeave}
          onDrop={handleImportDrop}
          state={
            <>
              <JobProgress
                phase={jobs.fileImport.phase}
                done={jobs.fileImport.done}
                total={jobs.fileImport.total}
                noun="fetches"
                error={
                  jobs.fileImport.phase === 'Failed'
                    ? `${jobs.fileImport.error ?? 'Unknown error'}. Nothing from the file was applied.`
                    : jobs.fileImport.error
                }
                finishedAt={jobs.fileImport.finishedAt}
                outcomeSeen={jobs.fileImport.outcomeSeen}
              />
              {importRefusal && <p className="settings-box__error">{importRefusal}</p>}
              {jobs.fileImport.phase === 'Complete' && importStatus?.report && (
                <div className="settings-import-report">
                  <p className="settings-box__hint">
                    From {importStatus.deviceName ?? 'the other device'} — exported{' '}
                    {formatTimestamp(importStatus.exportedAt)}
                  </p>
                  {importStatus.report.rankingAdded.length === 0 &&
                  importStatus.report.rankingRemoved.length === 0 &&
                  importStatus.report.fetched.length === 0 &&
                  importStatus.report.failures.length === 0 ? (
                    <p className="settings-box__hint">Nothing to report.</p>
                  ) : (
                    <>
                      {importStatus.report.rankingAdded.length > 0 && (
                        <div className="settings-import-report__section">
                          <h4 className="settings-import-report__label">Added to the ranking</h4>
                          <ul className="settings-import-report__list">
                            {importStatus.report.rankingAdded.map((anime) => (
                              <li key={anime.animeId}>{pickDisplayTitle(anime.title, anime.englishTitle)}</li>
                            ))}
                          </ul>
                        </div>
                      )}
                      {importStatus.report.rankingRemoved.length > 0 && (
                        <div className="settings-import-report__section">
                          <h4 className="settings-import-report__label">Removed from the ranking</h4>
                          <ul className="settings-import-report__list">
                            {importStatus.report.rankingRemoved.map((anime) => (
                              <li key={anime.animeId}>{pickDisplayTitle(anime.title, anime.englishTitle)}</li>
                            ))}
                          </ul>
                        </div>
                      )}
                      {importStatus.report.fetched.length > 0 && (
                        <div className="settings-import-report__section">
                          <h4 className="settings-import-report__label">Fetched for the first time</h4>
                          <ul className="settings-import-report__list">
                            {importStatus.report.fetched.map((anime) => (
                              <li key={anime.animeId}>{pickDisplayTitle(anime.title, anime.englishTitle)}</li>
                            ))}
                          </ul>
                        </div>
                      )}
                      {importStatus.report.failures.length > 0 && (
                        <div className="settings-import-report__section">
                          <h4 className="settings-import-report__label">Could not be applied</h4>
                          <ul className="settings-import-report__list">
                            {importStatus.report.failures.map((failure, index) => (
                              <li key={index}>
                                {importFailureLabel(failure)} — {failure.what}: {failure.reason}
                              </li>
                            ))}
                          </ul>
                        </div>
                      )}
                    </>
                  )}
                </div>
              )}
              <input
                ref={importInputRef}
                type="file"
                accept=".json,application/json"
                className="settings-import-input"
                onChange={handleImportFileInputChange}
              />
            </>
          }
          button={
            <button type="button" onClick={handleChooseImportFile} disabled={importing || importRunning}>
              {importing || importRunning ? 'Importing…' : 'Choose file…'}
            </button>
          }
        />
      </SettingsGroup>

      <SettingsGroup title="Account" hint="Your MyAnimeList connection.">
        {appStatus.malConnection.state === 'Lost' ? (
          <p className="settings-box__error">
            The connection to MyAnimeList was lost on {formatTimestamp(appStatus.malConnection.lostAt)} — MyAnimeList
            stopped accepting this app's login. Your changes are kept here but aren't being sent to MyAnimeList, and
            anime added on MyAnimeList elsewhere aren't being brought in. Re-authorize to reconnect.
          </p>
        ) : (
          <p className="settings-box__hint">{appStatus.malConnection.state === 'Connected' ? 'Connected.' : 'Not connected.'}</p>
        )}
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
