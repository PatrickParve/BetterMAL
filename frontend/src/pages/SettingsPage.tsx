import { useCallback, useEffect, useRef, useState } from 'react'
import {
  acceptReconciliationDiff,
  cancelReconciliationDiff,
  getAiringFullRefreshStatus,
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
  MalAuthStatus,
  PendingReconciliationDiffDto,
  ResyncStatusDto,
  SeriesBulkBuildStatusDto,
  SyncStatusDto,
} from '../api/types.ts'
import { useContentFilter } from '../context/ContentFilterContext.tsx'
import { useScoreVisibility } from '../context/ScoreVisibilityContext.tsx'
import { useAnimeSearch } from '../hooks/useAnimeSearch.ts'
import { useClickOutside } from '../hooks/useClickOutside.ts'
import { STATUS_LABELS, formatTimestamp } from '../utils/anime.ts'
import './SettingsPage.css'

// Operational/settings page: sync status + manual triggers, pending
// reconciliation-diff review, MAL re-authorization, and on-demand
// force-refresh of a single anime's cached metadata. Every action here is a
// thin wrapper around endpoints that already exist (sections 6/7) — this page
// is the missing UI surface for them.
export function SettingsPage() {
  const [status, setStatus] = useState<SyncStatusDto | null>(null)
  const [diff, setDiff] = useState<PendingReconciliationDiffDto | null>(null)
  const [authStatus, setAuthStatus] = useState<MalAuthStatus | null>(null)
  const [resyncStatus, setResyncStatus] = useState<ResyncStatusDto | null>(null)
  const [airingRefreshStatus, setAiringRefreshStatus] = useState<AiringFullRefreshStatusDto | null>(null)
  const [seriesBulkBuildStatus, setSeriesBulkBuildStatus] = useState<SeriesBulkBuildStatusDto | null>(null)
  const [loading, setLoading] = useState(true)

  const [resyncing, setResyncing] = useState(false)
  const [reconciling, setReconciling] = useState(false)
  const [reviewing, setReviewing] = useState(false)
  const [diffError, setDiffError] = useState<string | null>(null)
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

  if (loading) {
    return <p className="settings-page__loading">Loading…</p>
  }

  return (
    <div className="settings-page">
      <div className="settings-page__header">
        <h1>Settings</h1>
        <p className="settings-page__subtitle">Sync status, corrective tools, and account connections.</p>
      </div>

      <section className="settings-box">
        <h2>Score display</h2>
        <p className="settings-box__hint">
          Reveal MAL scores for shows you've completed or dropped even while the global "hide scores" toggle is on.
        </p>
        <label className="settings-toggle">
          <input
            type="checkbox"
            checked={alwaysShowCompletedScores}
            onChange={toggleAlwaysShowCompletedScores}
          />
          Always show MAL scores for completed and dropped shows
        </label>
      </section>

      <section className="settings-box">
        <h2>Content</h2>
        <p className="settings-box__hint">
          Hides NSFW (MAL Rx) from the seasonal page. R and R+ titles, search results, and anything already in
          your list are unaffected.
        </p>
        <label className="settings-toggle">
          <input type="checkbox" checked={hideHentai} onChange={toggleHideHentai} />
          Hide NSFW
        </label>
      </section>

      <section className="settings-box">
        <h2>Sync status</h2>
        {status ? (
          <dl className="settings-stats">
            <div className="settings-stats__row">
              <dt>Pending / retrying</dt>
              <dd>{status.pendingCount}</dd>
            </div>
            <div className="settings-stats__row">
              <dt>Last successful sync</dt>
              <dd>{formatTimestamp(status.lastSyncedAt)}</dd>
            </div>
          </dl>
        ) : (
          <p className="settings-box__empty">Couldn't load sync status.</p>
        )}
        <div className="settings-box__buttons">
          <button type="button" onClick={handleResyncNow} disabled={resyncing}>
            {resyncing ? 'Resyncing…' : 'Resync now'}
          </button>
          <button type="button" onClick={handleReconcileNow} disabled={reconciling}>
            {reconciling ? 'Reconciling…' : 'Run full reconciliation'}
          </button>
        </div>
      </section>

      <section className="settings-box">
        <h2>Correct imported data</h2>
        <p className="settings-box__hint">
          One-time corrective re-sync: re-fetches your full MyAnimeList and corrects status, score, and episode
          counts, and backfills English title, duration, and source. Takes several minutes; entries with unsynced
          local edits are left untouched.
        </p>
        {resyncStatus && resyncStatus.phase !== 'NotStarted' && (
          <p className="settings-box__hint">
            {resyncStatus.phase === 'Running'
              ? `Resyncing… ${resyncStatus.synced}/${resyncStatus.total}`
              : `Last run complete: ${resyncStatus.synced}/${resyncStatus.total} processed.`}
          </p>
        )}
        <div className="settings-box__buttons">
          <button
            type="button"
            onClick={handleResyncFromMal}
            disabled={startingFullResync || resyncStatus?.phase === 'Running'}
          >
            {resyncStatus?.phase === 'Running' ? 'Resyncing…' : 'Run corrective re-sync'}
          </button>
        </div>
      </section>

      <section className="settings-box">
        <h2>Airing dates</h2>
        <p className="settings-box__hint">
          Re-fetches per-episode airing dates from AniList for every anime in my list, in case something looks
          wrong. Skips shows that have already finished airing and were fetched successfully before — their
          episode dates can't change further. Paced to stay under AniList's rate limit, so a full list can take a
          while; runs in the background.
        </p>
        {airingRefreshStatus && airingRefreshStatus.phase !== 'NotStarted' && (
          <p className="settings-box__hint">
            {airingRefreshStatus.phase === 'Running'
              ? `Refreshing… ${airingRefreshStatus.synced}/${airingRefreshStatus.total}`
              : `Last run complete: ${airingRefreshStatus.synced}/${airingRefreshStatus.total} processed.`}
          </p>
        )}
        <div className="settings-box__buttons">
          <button
            type="button"
            onClick={handleAiringFullRefresh}
            disabled={startingAiringRefresh || airingRefreshStatus?.phase === 'Running'}
          >
            {airingRefreshStatus?.phase === 'Running' ? 'Refreshing…' : 'Refresh all airing dates'}
          </button>
        </div>
      </section>

      <section className="settings-box">
        <h2>Build all series</h2>
        <p className="settings-box__hint">
          Builds a franchise for every anime in my list that isn't part of one yet, so the profile page's Top series
          ranking can be completed on demand instead of only filling in a little on each profile visit. Runs in the
          background; can take a while for a large list.
        </p>
        {seriesBulkBuildStatus && seriesBulkBuildStatus.phase !== 'NotStarted' && (
          <p className="settings-box__hint">
            {seriesBulkBuildStatus.phase === 'Running'
              ? `Building… ${seriesBulkBuildStatus.built}/${seriesBulkBuildStatus.total}`
              : seriesBulkBuildStatus.phase === 'Failed'
                ? `Last run failed after ${seriesBulkBuildStatus.built}/${seriesBulkBuildStatus.total} processed — see backend logs.`
                : `Last run complete: ${seriesBulkBuildStatus.built}/${seriesBulkBuildStatus.total} processed.`}
          </p>
        )}
        <div className="settings-box__buttons">
          <button
            type="button"
            onClick={handleSeriesBulkBuild}
            disabled={startingSeriesBulkBuild || seriesBulkBuildStatus?.phase === 'Running'}
          >
            {seriesBulkBuildStatus?.phase === 'Running' ? 'Building…' : 'Build all series from my list'}
          </button>
        </div>
      </section>

      {diff && (
        <section className="settings-box">
          <h2>Pending reconciliation diff</h2>
          <p className="settings-box__hint">Computed {formatTimestamp(diff.computedAt)} — review before applying.</p>
          <ul className="settings-diff-list">
            {diff.entries.map((entry) => (
              <li key={entry.animeId} className="settings-diff-row">
                {entry.pictureUrl ? (
                  <img src={entry.pictureUrl} alt="" className="settings-diff-row__picture" />
                ) : (
                  <div
                    className="settings-diff-row__picture settings-diff-row__picture--placeholder"
                    aria-hidden="true"
                  />
                )}
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
        </section>
      )}

      <section className="settings-box">
        <h2>MyAnimeList connection</h2>
        <p className="settings-box__hint">{authStatus?.connected ? 'Connected.' : 'Not connected.'}</p>
        <a className="settings-box__link" href="/api/mal-auth/start">
          Re-authorize with MAL
        </a>
      </section>

      <section className="settings-box">
        <h2>Force-refresh anime metadata</h2>
        <AnimeRefreshPicker />
      </section>
    </div>
  )
}

// Search-and-pick input feeding the existing single-anime refresh endpoint —
// same debounced search backing the navbar's SearchBar, just without
// navigation on selection.
function AnimeRefreshPicker() {
  const [query, setQuery] = useState('')
  const [selected, setSelected] = useState<AnimeSearchResult | null>(null)
  const [refreshing, setRefreshing] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const containerRef = useRef<HTMLDivElement>(null)
  const { results: rawResults, open, setOpen } = useAnimeSearch(query)
  // This picker refreshes a single anime's cached metadata — a series has no
  // such target, so its rows are filtered out rather than offered here.
  const results = rawResults.filter((result) => result.kind === 'anime')

  useClickOutside(containerRef, () => setOpen(false))

  function pick(result: AnimeSearchResult) {
    setSelected(result)
    setQuery(result.title)
    setOpen(false)
    setMessage(null)
  }

  async function handleRefresh() {
    if (!selected || refreshing) return
    setRefreshing(true)
    setMessage(null)
    try {
      await refreshAnime(selected.id)
      setMessage(`Refreshed "${selected.title}".`)
    } catch {
      setMessage(`Couldn't refresh "${selected.title}". Please try again.`)
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
          }}
          onFocus={() => results.length > 0 && setOpen(true)}
          aria-label="Search anime to refresh"
        />
        {open && results.length > 0 && (
          <ul className="settings-refresh-picker__dropdown">
            {results.map((result) => (
              <li key={result.id}>
                <button type="button" onClick={() => pick(result)}>
                  {result.pictureUrl && <img src={result.pictureUrl} alt="" />}
                  <span>{result.title}</span>
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
