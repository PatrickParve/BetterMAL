import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { getSeries, rebuildSeries } from '../api/client.ts'
import type {
  SeriesAverageDto,
  SeriesDto,
  SeriesEntryDto,
  SeriesLookupResult,
  SeriesStatus,
  UserAnimeEntryDto,
} from '../api/types.ts'
import { ProgressBar } from '../components/ProgressBar.tsx'
import { ScoreValue } from '../components/ScoreValue.tsx'
import { SeriesEntryRow } from '../components/SeriesEntryRow.tsx'
import { useEntryEditor } from '../context/EntryEditorContext.tsx'
import { useScoreVisibility } from '../context/ScoreVisibilityContext.tsx'
import { usePageData } from '../hooks/usePageData.ts'
import { mediaTypeLabel, pickDisplayTitle } from '../utils/anime.ts'
import './SeriesPage.css'

const NO_INFO = '—'

const SERIES_STATUS_CLASS: Record<SeriesStatus, string> = {
  Ongoing: 'ongoing',
  Upcoming: 'upcoming',
  Finished: 'finished',
  'Finished · sequel upcoming': 'finished',
}

// e.g. 1548 minutes -> "1d 1h 48min" — once a day is present the hours
// segment always shows, even at 0h, matching the design spec's own example
// ("4d 6h 30min").
function formatRuntime(totalSeconds: number): string {
  const totalMinutes = Math.round(totalSeconds / 60)
  const days = Math.floor(totalMinutes / (24 * 60))
  const hours = Math.floor((totalMinutes % (24 * 60)) / 60)
  const minutes = totalMinutes % 60
  const parts: string[] = []
  if (days > 0) parts.push(`${days}d`)
  if (days > 0 || hours > 0) parts.push(`${hours}h`)
  parts.push(`${minutes}min`)
  return parts.join(' ')
}

// A 0 total alongside hasUnknown means every main-line entry's episode count
// is unknown (e.g. the only main-line entry is still airing with no
// published total) — "0+ ep" would read as a real zero padded with a
// lower-bound marker, so it gets its own label instead.
function formatEpisodeTotal(total: number, hasUnknown: boolean): string {
  if (total === 0 && hasUnknown) return 'Unknown'
  return `${total}${hasUnknown ? '+' : ''} ep`
}

function formatRuntimeTotal(seconds: number, hasUnknown: boolean): string {
  if (seconds === 0 && hasUnknown) return 'Unknown'
  return `${formatRuntime(seconds)}${hasUnknown ? '+' : ''}`
}

function formatYearSpan(firstYear: number | null, lastYear: number | null): string {
  if (firstYear === null || lastYear === null) return NO_INFO
  return firstYear === lastYear ? String(firstYear) : `${firstYear} – ${lastYear}`
}

function formatAverage(average: SeriesAverageDto): string {
  if (average.value === null) return 'No score'
  return `${average.value.toFixed(2)} · ${average.scoredCount} of ${average.totalCount} scored`
}

// Mirrors SeriesService.MalAverage/MyAverage server-side exactly, so an
// in-place score edit (5.7) can recompute the four averages locally without
// a refetch and stay consistent with what a reload would show.
function malAverage(entries: SeriesEntryDto[]): SeriesAverageDto {
  const scored = entries.map((e) => e.malScore).filter((s): s is number => s != null)
  return {
    value: scored.length > 0 ? scored.reduce((a, b) => a + b, 0) / scored.length : null,
    scoredCount: scored.length,
    totalCount: entries.length,
  }
}

// MAL's score of 0 means "unscored", not a rating (design.md decision 8).
function mineAverage(entries: SeriesEntryDto[]): SeriesAverageDto {
  const scored = entries.map((e) => e.entry?.myScore).filter((s): s is number => s != null && s > 0)
  return {
    value: scored.length > 0 ? scored.reduce((a, b) => a + b, 0) / scored.length : null,
    scoredCount: scored.length,
    totalCount: entries.length,
  }
}

function recomputeScores(mainLine: SeriesEntryDto[], extras: SeriesEntryDto[]) {
  const all = [...mainLine, ...extras]
  return {
    malMain: malAverage(mainLine),
    malAll: malAverage(all),
    mineMain: mineAverage(mainLine),
    mineAll: mineAverage(all),
  }
}

// Patches one entry's UserAnimeEntryDto into whichever of mainLine/extras it
// lives in and recomputes the four averages from the result.
function patchSeriesEntry(series: SeriesDto, animeId: number, entry: UserAnimeEntryDto | null): SeriesDto {
  const patch = (entries: SeriesEntryDto[]) => entries.map((e) => (e.animeId === animeId ? { ...e, entry } : e))
  const mainLine = patch(series.mainLine)
  const extras = patch(series.extras)
  return { ...series, mainLine, extras, scores: recomputeScores(mainLine, extras) }
}

function findEntry(series: SeriesDto, animeId: number): SeriesEntryDto | undefined {
  return series.mainLine.find((e) => e.animeId === animeId) ?? series.extras.find((e) => e.animeId === animeId)
}

// Extras arrive from the API pre-sorted by media-type group then aired date
// (SeriesService.ProjectAsync), so grouping is just bucketing consecutive
// same-mediaType runs rather than re-sorting.
function groupExtras(extras: SeriesEntryDto[]): { mediaType: string | null; items: SeriesEntryDto[] }[] {
  const groups: { mediaType: string | null; items: SeriesEntryDto[] }[] = []
  for (const entry of extras) {
    const last = groups[groups.length - 1]
    if (last && last.mediaType === entry.mediaType) last.items.push(entry)
    else groups.push({ mediaType: entry.mediaType, items: [entry] })
  }
  return groups
}

// Mirrors ScoreValue's own per-row `completed` convention (only reveals when
// the user has also turned on "always show completed scores"): a group
// average counts as completed when every member that's actually out —
// finished airing — is marked Completed in my list. Entries not yet aired or
// still airing don't count against it, since they can't be completed yet.
function isGroupCompleted(entries: SeriesEntryDto[]): boolean {
  return entries.filter((e) => e.airingStatus === 'finished_airing').every((e) => e.entry?.status === 'Completed')
}

function MalScoreBox({
  label,
  average,
  completed,
}: {
  label: string
  average: SeriesAverageDto
  completed: boolean
}) {
  return (
    <section className="series-box">
      <h3>{label}</h3>
      <p className="series-box__score">
        <ScoreValue value={average.value} placeholder="No score" completed={completed} />
        {average.value !== null && (
          <span className="series-box__count">
            {' '}
            · {average.scoredCount} of {average.totalCount} scored
          </span>
        )}
      </p>
    </section>
  )
}

function MineScoreBox({ label, average }: { label: string; average: SeriesAverageDto }) {
  return (
    <section className="series-box">
      <h3>{label}</h3>
      <p className="series-box__score">{formatAverage(average)}</p>
    </section>
  )
}

// Franchise overview page: header, four score averages, series-wide stats, a
// numbered main-line watch order, a More section for extras, a per-entry
// MAL-vs-mine comparison strip, and a Rebuild control. Loads through
// usePageData('series:{animeId}', …) so back/forward restore works like
// every other page; row edits are applied in place via setData rather than
// a refetch (design.md decision 11, tasks 5.7/3.9).
export function SeriesPage() {
  const { animeId: animeIdParam } = useParams()
  const animeId = Number(animeIdParam)
  const { data, loading, setData } = usePageData<SeriesLookupResult>(`series:${animeId}`, () => getSeries(animeId))
  const [rebuilding, setRebuilding] = useState(false)
  const { openEditor } = useEntryEditor()
  const { hidden } = useScoreVisibility()

  function patchSeries(updater: (series: SeriesDto) => SeriesDto) {
    setData((prev) => (prev && prev.found ? { found: true, series: updater(prev.series) } : prev))
  }

  async function handleRebuild() {
    if (rebuilding) return
    setRebuilding(true)
    try {
      const series = await rebuildSeries(animeId)
      setData({ found: true, series })
    } catch {
      // Leave the page showing whatever was already loaded; the user can retry.
    } finally {
      setRebuilding(false)
    }
  }

  function handleEdit(entry: SeriesEntryDto) {
    openEditor({
      animeId: entry.animeId,
      animeTitle: pickDisplayTitle(entry.title, entry.englishTitle),
      totalEpisodes: entry.totalEpisodes,
      entry: entry.entry,
      onSaved: (saved) => patchSeries((series) => patchSeriesEntry(series, entry.animeId, saved)),
      onDeleted: () => patchSeries((series) => patchSeriesEntry(series, entry.animeId, null)),
    })
  }

  if (Number.isNaN(animeId)) {
    return <p className="series-page__empty">Anime not found.</p>
  }

  if (loading) {
    return <p className="series-page__loading">Loading…</p>
  }

  if (!data) {
    return <p className="series-page__empty">Couldn't load this series.</p>
  }

  if (!data.found) {
    return <p className="series-page__empty">This anime isn't part of a series.</p>
  }

  const series = data.series
  const { scores, stats } = series
  const displayTitle = pickDisplayTitle(series.title, series.englishTitle)

  const longestGapFrom =
    stats.longestGapFromAnimeId !== null ? findEntry(series, stats.longestGapFromAnimeId) : undefined
  const longestGapTo = stats.longestGapToAnimeId !== null ? findEntry(series, stats.longestGapToAnimeId) : undefined
  const highestMal = stats.highestMalScoreAnimeId !== null ? findEntry(series, stats.highestMalScoreAnimeId) : undefined
  const myHighest = stats.myHighestScoreAnimeId !== null ? findEntry(series, stats.myHighestScoreAnimeId) : undefined

  return (
    <div className="series-page">
      <div className="series-page__header">
        {series.pictureUrl ? (
          <img src={series.pictureUrl} alt="" className="series-page__picture" />
        ) : (
          <div className="series-page__picture series-page__picture--placeholder" aria-hidden="true" />
        )}
        <div className="series-page__header-info">
          <span className="series-page__label">Series</span>
          <h1>{displayTitle}</h1>
          <div className="series-page__header-meta">
            <span className={`series-page__status-pill series-page__status-pill--${SERIES_STATUS_CLASS[series.status]}`}>
              {series.status}
            </span>
            <span className="series-page__year-span">{formatYearSpan(series.firstYear, series.lastYear)}</span>
          </div>
        </div>
      </div>

      <div className="series-page__rebuild-row">
        <button type="button" className="series-page__rebuild" onClick={handleRebuild} disabled={rebuilding}>
          {rebuilding ? 'Rebuilding…' : 'Rebuild'}
        </button>
        {series.isPartial && <span className="series-page__notice">Some entries couldn't be loaded yet.</span>}
        {series.isTruncated && <span className="series-page__notice">This series was too large to show in full.</span>}
      </div>

      <div className="series-page__score-boxes">
        <MalScoreBox label="MAL · main series" average={scores.malMain} completed={isGroupCompleted(series.mainLine)} />
        <MalScoreBox
          label="MAL · everything"
          average={scores.malAll}
          completed={isGroupCompleted([...series.mainLine, ...series.extras])}
        />
        <MineScoreBox label="Mine · main series" average={scores.mineMain} />
        <MineScoreBox label="Mine · everything" average={scores.mineAll} />
      </div>

      <section className="series-box">
        <h2>Series stats</h2>
        <dl className="series-page__stats-grid">
          <div>
            <dt>Main series episodes</dt>
            <dd>{formatEpisodeTotal(stats.mainLineEpisodeTotal, stats.hasUnknownEpisodeCounts)}</dd>
          </div>
          <div>
            <dt>Main series runtime</dt>
            <dd>{formatRuntimeTotal(stats.mainLineRuntimeSeconds, stats.hasUnknownEpisodeCounts)}</dd>
          </div>
          {stats.extrasCount > 0 && (
            <>
              <div>
                <dt>Extras episodes</dt>
                <dd>{stats.extrasEpisodeTotal} ep</dd>
              </div>
              <div>
                <dt>Extras runtime</dt>
                <dd>{formatRuntime(stats.extrasRuntimeSeconds)}</dd>
              </div>
            </>
          )}
          <div>
            <dt>My progress</dt>
            <dd>
              <ProgressBar
                watched={stats.myWatchedEpisodes}
                total={stats.mainLineEpisodeTotal > 0 ? stats.mainLineEpisodeTotal : null}
              />
            </dd>
          </div>
          <div>
            <dt>Entries completed</dt>
            <dd>
              {stats.entriesCompleted} of {stats.mainLineCount}
            </dd>
          </div>
          <div>
            <dt>Time watched</dt>
            <dd>{formatRuntime(stats.myWatchedSeconds)}</dd>
          </div>
          <div>
            <dt>Time left</dt>
            <dd>{formatRuntime(Math.max(0, stats.mainLineRuntimeSeconds - stats.myWatchedSeconds))}</dd>
          </div>
          {longestGapFrom && longestGapTo && stats.longestGapDays !== null && (
            <div>
              <dt>Longest gap</dt>
              <dd>
                {stats.longestGapDays} days between{' '}
                <Link to={`/anime/${longestGapFrom.animeId}`}>
                  {pickDisplayTitle(longestGapFrom.title, longestGapFrom.englishTitle)}
                </Link>{' '}
                and{' '}
                <Link to={`/anime/${longestGapTo.animeId}`}>
                  {pickDisplayTitle(longestGapTo.title, longestGapTo.englishTitle)}
                </Link>
              </dd>
            </div>
          )}
          {highestMal && (
            <div>
              <dt>Highest MAL score</dt>
              <dd>
                <Link to={`/anime/${highestMal.animeId}`}>
                  {pickDisplayTitle(highestMal.title, highestMal.englishTitle)}
                </Link>{' '}
                · <ScoreValue value={highestMal.malScore} />
              </dd>
            </div>
          )}
          {myHighest && (
            <div>
              <dt>My favourite</dt>
              <dd>
                <Link to={`/anime/${myHighest.animeId}`}>
                  {pickDisplayTitle(myHighest.title, myHighest.englishTitle)}
                </Link>{' '}
                · {myHighest.entry?.myScore}
              </dd>
            </div>
          )}
          {stats.studios.length > 0 && (
            <div>
              <dt>Studios</dt>
              <dd>{stats.studios.join(', ')}</dd>
            </div>
          )}
          {stats.genres.length > 0 && (
            <div>
              <dt>Genres</dt>
              <dd>{stats.genres.join(', ')}</dd>
            </div>
          )}
        </dl>
      </section>

      <section className="series-box">
        <h2>Main series</h2>
        <ol className="series-page__list">
          {series.mainLine.map((entry, index) => (
            <SeriesEntryRow key={entry.animeId} entry={entry} rank={index + 1} onEdit={handleEdit} />
          ))}
        </ol>
      </section>

      {series.extras.length > 0 && (
        <section className="series-box">
          <h2>More</h2>
          {groupExtras(series.extras).map((group, index) => (
            <div key={`${group.mediaType}-${index}`} className="series-page__extras-group">
              <h3>{mediaTypeLabel(group.mediaType)}</h3>
              <ul className="series-page__list">
                {group.items.map((entry) => (
                  <SeriesEntryRow key={entry.animeId} entry={entry} onEdit={handleEdit} />
                ))}
              </ul>
            </div>
          ))}
        </section>
      )}

      {series.mainLine.length > 0 && (
        <section className="series-box">
          <h2>Score comparison</h2>
          <div className="series-page__score-strip">
            {hidden ? (
              <p className="series-page__score-strip-hidden-note">MAL scores are hidden while the toggle is on.</p>
            ) : (
              <div className="series-page__score-strip-row series-page__score-strip-row--mal">
                {series.mainLine.map((entry) => (
                  <div
                    key={entry.animeId}
                    className="series-page__score-strip-bar"
                    style={{ height: `${((entry.malScore ?? 0) / 10) * 100}%` }}
                    title={entry.malScore != null ? `MAL ${entry.malScore.toFixed(2)}` : 'No MAL score'}
                  />
                ))}
              </div>
            )}
            <div className="series-page__score-strip-row series-page__score-strip-row--mine">
              {series.mainLine.map((entry) => (
                <div
                  key={entry.animeId}
                  className="series-page__score-strip-bar series-page__score-strip-bar--mine"
                  style={{ height: `${((entry.entry?.myScore ?? 0) / 10) * 100}%` }}
                  title={entry.entry?.myScore ? `Me ${entry.entry.myScore}` : 'No score'}
                />
              ))}
            </div>
          </div>
        </section>
      )}
    </div>
  )
}
