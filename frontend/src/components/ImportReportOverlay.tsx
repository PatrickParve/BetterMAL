import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Modal } from './Modal.tsx'
import type {
  TransferImportFailureDto,
  TransferImportFailureKind,
  TransferImportReportDto,
  TransferImportStatusDto,
  TransferReportAnimeDto,
} from '../api/types.ts'
import { formatTimestamp, pickDisplayTitle } from '../utils/anime.ts'
import './ImportReportOverlay.css'

type ImportReportOverlayProps = {
  // A finished import that has a report.
  status: TransferImportStatusDto & { report: TransferImportReportDto }
  onClose: () => void
}

type Tab = 'ranking' | 'fetched' | 'failed'

// What couldn't be applied is grouped by kind, in this order (simplify-
// settings-and-first-fetch-states D4).
const FAILURE_GROUPS: { kind: TransferImportFailureKind; heading: string }[] = [
  { kind: 'ChosenPicture', heading: 'Chosen pictures' },
  { kind: 'SeriesTitle', heading: 'Series titles' },
  { kind: 'SeriesPicture', heading: 'Series pictures' },
  { kind: 'EditHistory', heading: 'Edit history' },
  { kind: 'Ranking', heading: 'Ranking' },
]

// The import's report in full (settings-page "The import's outcome is a
// closeable summary with details on request"), opened from the summary card:
// one tab for each part of the report that has entries. Anime link to their
// pages and close the overlay on the way, so it also closes by leaving.
export function ImportReportOverlay({ status, onClose }: ImportReportOverlayProps) {
  const { report } = status
  const rankingCount = report.rankingAdded.length + report.rankingRemoved.length

  const tabs: { id: Tab; label: string }[] = []
  if (rankingCount > 0) tabs.push({ id: 'ranking', label: `Ranking (${rankingCount})` })
  if (report.fetched.length > 0) tabs.push({ id: 'fetched', label: `Fetched (${report.fetched.length})` })
  if (report.failures.length > 0) tabs.push({ id: 'failed', label: `Couldn't be applied (${report.failures.length})` })

  // Opens on the part that needs a decision, when there is one.
  const [selected, setSelected] = useState<Tab | null>(() =>
    report.failures.length > 0 ? 'failed' : (tabs[0]?.id ?? null),
  )
  const tab = tabs.some((t) => t.id === selected) ? selected : (tabs[0]?.id ?? null)

  return (
    <Modal onClose={onClose} labelledBy="import-report-title" className="modal--wide">
      <div className="import-report-overlay">
        <div className="import-report-overlay__header">
          <div className="import-report-overlay__heading">
            <h2 id="import-report-title" className="import-report-overlay__title">
              Import from {status.deviceName ?? 'the other device'}
            </h2>
            <span className="import-report-overlay__subtitle">Exported {formatTimestamp(status.exportedAt)}</span>
          </div>
          <button type="button" className="import-report-overlay__close" aria-label="Close" onClick={onClose}>
            <CloseIcon />
          </button>
        </div>

        <div className="import-report-overlay__tabs" role="group" aria-label="Sections of the report">
          {tabs.map((t) => (
            <button
              key={t.id}
              type="button"
              className={tab === t.id ? 'import-report-overlay__tab import-report-overlay__tab--active' : 'import-report-overlay__tab'}
              aria-pressed={tab === t.id}
              onClick={() => setSelected(t.id)}
            >
              {t.label}
            </button>
          ))}
        </div>

        <div className="import-report-overlay__frame">
          <div className="import-report-overlay__scroll scroll-y">
            {tab === 'ranking' && (
              <>
                <AnimeSection heading="Added" anime={report.rankingAdded} onClose={onClose} />
                <AnimeSection heading="Removed" anime={report.rankingRemoved} onClose={onClose} />
              </>
            )}
            {tab === 'fetched' && <AnimeSection anime={report.fetched} onClose={onClose} />}
            {tab === 'failed' &&
              FAILURE_GROUPS.map(({ kind, heading }) => (
                <FailureSection
                  key={kind}
                  heading={heading}
                  failures={report.failures.filter((failure) => failure.kind === kind)}
                  onClose={onClose}
                />
              ))}
          </div>
        </div>
      </div>
    </Modal>
  )
}

function AnimeSection({
  heading,
  anime,
  onClose,
}: {
  heading?: string
  anime: TransferReportAnimeDto[]
  onClose: () => void
}) {
  if (anime.length === 0) return null
  return (
    <section className="import-report-overlay__section">
      {heading && (
        <h3 className="import-report-overlay__section-heading">
          {heading} ({anime.length})
        </h3>
      )}
      <ul className="import-report-overlay__list">
        {anime.map((entry) => (
          <li key={entry.animeId} className="import-report-overlay__row">
            <Link to={`/anime/${entry.animeId}`} className="import-report-overlay__link" onClick={onClose}>
              {pickDisplayTitle(entry.title, entry.englishTitle)}
            </Link>
          </li>
        ))}
      </ul>
    </section>
  )
}

function FailureSection({
  heading,
  failures,
  onClose,
}: {
  heading: string
  failures: TransferImportFailureDto[]
  onClose: () => void
}) {
  if (failures.length === 0) return null
  return (
    <section className="import-report-overlay__section">
      <h3 className="import-report-overlay__section-heading">
        {heading} ({failures.length})
      </h3>
      <ul className="import-report-overlay__list">
        {failures.map((failure, index) => (
          <li key={index} className="import-report-overlay__row">
            <FailureName failure={failure} onClose={onClose} />
            {' — '}
            {failure.reason}
            {failure.kind === 'EditHistory' && historyCount(failure.what) && (
              <span className="import-report-overlay__note"> ({historyCount(failure.what)})</span>
            )}
          </li>
        ))}
      </ul>
    </section>
  )
}

// An anime is a link to its page. A series is plain text: the series route is
// keyed by an anime and the report holds only the series' id. A subject this
// device has no title for is named by its id.
function FailureName({ failure, onClose }: { failure: TransferImportFailureDto; onClose: () => void }) {
  if (failure.subject === 'Series') {
    const name = failure.title ? pickDisplayTitle(failure.title, failure.englishTitle) : `Series ${failure.id}`
    return <span className="import-report-overlay__name">{name}</span>
  }
  const name = failure.title ? pickDisplayTitle(failure.title, failure.englishTitle) : `Anime ${failure.id}`
  return (
    <Link to={`/anime/${failure.id}`} className="import-report-overlay__link" onClick={onClose}>
      {name}
    </Link>
  )
}

// `what` for edit history reads "edit history (3 records)"; the reason alone
// would lose how many records were skipped.
function historyCount(what: string): string | null {
  return what.match(/\(([^)]+)\)/)?.[1] ?? null
}

function CloseIcon() {
  return (
    <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <line x1="6" y1="6" x2="18" y2="18" />
      <line x1="18" y1="6" x2="6" y2="18" />
    </svg>
  )
}
