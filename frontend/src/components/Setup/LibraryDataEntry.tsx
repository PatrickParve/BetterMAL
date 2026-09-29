import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getSetupStatus } from '../../api/client.ts'
import type { SetupSkippedDto, SetupStatusDto } from '../../api/types.ts'
import { LoadFailedNotice } from '../LoadFailedNotice.tsx'
import { LoadingNotice } from '../LoadingNotice.tsx'
import { SetupIssues } from './SetupIssues.tsx'
import { SetupSteps } from './SetupStepRow.tsx'
import { SETUP_STEPS } from './setupFormat.ts'
import './LibraryDataEntry.css'

// How often Settings re-reads setup's status while any of its work is going: the
// setup screen's own read at half the pace, since nothing here waits on it.
const LIBRARY_DATA_POLL_INTERVAL_MS = 2000

// Whether anything is still going or waiting to be retried, and so worth another read.
// A step that reads Waiting on a finished install is idle, not pending: only the airing
// drain and a step actually running, paused or waiting on retries count.
function isWorking(status: SetupStatusDto): boolean {
  return (
    status.airingDraining ||
    SETUP_STEPS.some(({ key }) => {
      const step = status.steps[key]
      return step.phase === 'Running' || step.phase === 'Paused' || step.waitingRetry !== null
    }) ||
    status.services.some((s) => s.down || s.throttledUntil !== null)
  )
}

function skipReason(skipped: SetupSkippedDto): string {
  return skipped.reason === 'NotOnMal'
    ? "MyAnimeList doesn't have it"
    : `MyAnimeList lists it with a status this app doesn't recognize${skipped.malStatus ? ` (“${skipped.malStatus}”)` : ''}`
}

// Settings' Library data entry (settings-page "Data tools shows what setup brought in and
// what is still going"): the steps of setup that still have something left, drawn as the
// setup screen draws them (none once all four are done, since a finished library has nothing
// to report on them), its issues with Retry now, and the anime setup skipped for good. It reads the same setup status
// as the setup screen, with a poll of its own rather than AppStatusProvider's, because
// the navbar has to stay silent about this work (navbar-settings-status). Every 2 s
// while anything runs or waits, and once otherwise. It offers no button to start setup,
// since setup runs once.
export function LibraryDataEntry() {
  const [status, setStatus] = useState<SetupStatusDto | null>(null)
  const [failed, setFailed] = useState(false)

  const read = useCallback(() => {
    getSetupStatus()
      .then((next) => {
        setStatus(next)
        setFailed(false)
      })
      .catch(() => setFailed(true))
  }, [])

  useEffect(() => {
    read()
  }, [read])

  const working = status !== null && isWorking(status)
  useEffect(() => {
    if (!working) return
    const interval = setInterval(read, LIBRARY_DATA_POLL_INTERVAL_MS)
    return () => clearInterval(interval)
  }, [working, read])

  // A failed read with nothing held is a failure, not a section still loading
  // (page-load-states); once something is held a failed poll keeps it.
  if (status === null) {
    return failed ? (
      <LoadFailedNotice what="library data" onRetry={read} compact />
    ) : (
      <LoadingNotice className="library-data__loading" />
    )
  }

  return (
    <div className="library-data">
      <SetupSteps status={status} onlyUnfinished />
      <SetupIssues status={status} onRetried={read} />
      <div className="library-data__skipped">
        <h4 className="library-data__skipped-title">Skipped anime</h4>
        {status.skipped.length === 0 ? (
          <p className="library-data__none">No anime were skipped.</p>
        ) : (
          <ul className="library-data__skipped-list">
            {status.skipped.map((skipped) => (
              <li key={`${skipped.animeId}-${skipped.reason}`} className="library-data__skipped-item">
                <Link to={`/anime/${skipped.animeId}`}>{skipped.title}</Link>
                <span className="library-data__skipped-reason">{skipReason(skipped)}</span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}
