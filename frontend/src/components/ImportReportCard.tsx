import type { TransferImportReportDto, TransferImportStatusDto } from '../api/types.ts'
import { formatTimestamp } from '../utils/anime.ts'
import './ImportReportCard.css'

type ImportReportCardProps = {
  // An ended import: Complete, or Failed.
  status: TransferImportStatusDto
  onClose: () => void
  onDetails: () => void
  // Set when the last attempt to close the card failed on the server; the
  // card is back on screen with this line and its × works again.
  closeError?: string | null
}

// The file import's outcome (settings-page "The import's outcome is a
// closeable summary with details on request"): where the file came from and
// one line of counts. It lists no anime itself — "See details" opens the
// ImportReportOverlay for that. Closing is the caller's business (it is kept
// on the server), so this only reports the click.
export function ImportReportCard({ status, onClose, onDetails, closeError }: ImportReportCardProps) {
  const failed = status.phase === 'Failed'
  const report = status.report
  const counts = report ? countsLine(report) : null

  return (
    <div className={failed ? 'import-report-card import-report-card--failed' : 'import-report-card'}>
      <div className="import-report-card__body">
        {failed ? (
          <p className="import-report-card__line">
            Import failed: {trimTrailingPeriod(status.error ?? 'Unknown error')}. Nothing from the file was applied.
          </p>
        ) : (
          <>
            <p className="import-report-card__source">
              From {status.deviceName ?? 'the other device'} · exported {formatTimestamp(status.exportedAt)}
            </p>
            <p className="import-report-card__line">{counts ?? 'Nothing to report.'}</p>
          </>
        )}
        {closeError && (
          <p className="import-report-card__error" role="alert">
            {closeError}
          </p>
        )}
        {!failed && counts !== null && (
          <button type="button" className="import-report-card__details" onClick={onDetails}>
            See details
          </button>
        )}
      </div>
      <button type="button" className="import-report-card__close" aria-label="Close" onClick={onClose}>
        <CloseIcon />
      </button>
    </div>
  )
}

// The non-zero counts, joined; null when the report holds nothing at all.
function countsLine(report: TransferImportReportDto): string | null {
  const parts = [
    report.rankingAdded.length > 0 ? `${report.rankingAdded.length} added to ranking` : null,
    report.rankingRemoved.length > 0 ? `${report.rankingRemoved.length} removed` : null,
    report.fetched.length > 0 ? `${report.fetched.length} fetched` : null,
    report.failures.length > 0 ? `${report.failures.length} couldn't be applied` : null,
  ].filter((part): part is string => part !== null)
  return parts.length > 0 ? parts.join(' · ') : null
}

// A reason that already ends in a full stop would otherwise read "..".
function trimTrailingPeriod(reason: string): string {
  return reason.replace(/[.\s]+$/, '')
}

function CloseIcon() {
  return (
    <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <line x1="6" y1="6" x2="18" y2="18" />
      <line x1="18" y1="6" x2="6" y2="18" />
    </svg>
  )
}
