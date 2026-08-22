import { useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { getSeries, rebuildSeries, setSeriesFavouriteOrder } from '../api/client.ts'
import type {
  SeriesAverageDto,
  SeriesDto,
  SeriesEntryDto,
  SeriesLookupResult,
  SeriesStatus,
  UserAnimeEntryDto,
} from '../api/types.ts'
import { AiringProgressBar } from '../components/AiringProgressBar.tsx'
import { ProgressBar } from '../components/ProgressBar.tsx'
import { ScoreChip } from '../components/ScoreChip.tsx'
import { ScoreValue } from '../components/ScoreValue.tsx'
import { SeriesExtraTile } from '../components/SeriesExtraTile.tsx'
import { SeriesTimeline } from '../components/SeriesTimeline.tsx'
import { useEntryEditor } from '../context/EntryEditorContext.tsx'
import { useLandscapePicture } from '../hooks/useLandscapePicture.ts'
import { usePageData } from '../hooks/usePageData.ts'
import { formatRuntime, isScoreRevealableStatus, mediaTypeLabel, pickDisplayTitle } from '../utils/anime.ts'
import './SeriesPage.css'

const NO_INFO = '—'
const MAX_REBUILD_ROUNDS = 12

const SERIES_STATUS_CLASS: Record<SeriesStatus, string> = {
  Ongoing: 'ongoing',
  Upcoming: 'upcoming',
  Finished: 'finished',
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

// Mirrors formatEpisodeTotal's lower-bound marker (design.md decision 3) but
// worded for the progress readout rather than the episode-total stat.
function formatProgressTotal(total: number, hasUnknown: boolean): string {
  if (total === 0 && hasUnknown) return 'unknown total'
  return `${total}${hasUnknown ? '+' : ''} total`
}

function formatYearSpan(firstYear: number | null, lastYear: number | null): string {
  if (firstYear === null || lastYear === null) return NO_INFO
  return firstYear === lastYear ? String(firstYear) : `${firstYear} – ${lastYear}`
}

function formatAverage(average: SeriesAverageDto): string {
  if (average.value === null) return 'No score'
  return average.value.toFixed(2)
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

// Mirrors SeriesService's tie-break rule for my-highest-score ties: an
// entry already present in the previous (server-ordered) tie list keeps its
// relative order; an entry newly entering the tie has no known favourite
// rank client-side, so it's unranked — appended in watch order, same as the
// backend does for an unranked entry (task 6.6). malScore never changes
// through a row edit, so highestMalScoreAnimeIds needs no equivalent here.
function recomputeMyHighestIds(prevIds: number[], mainLine: SeriesEntryDto[], extras: SeriesEntryDto[]): number[] {
  const all = [...mainLine, ...extras]
  const scored = all.filter((e) => (e.entry?.myScore ?? 0) > 0)
  if (scored.length === 0) return []

  const max = Math.max(...scored.map((e) => e.entry!.myScore!))
  const tied = scored.filter((e) => e.entry!.myScore === max)
  const tiedIds = new Set(tied.map((e) => e.animeId))

  const known = prevIds.filter((id) => tiedIds.has(id))
  const knownSet = new Set(known)
  const newlyTied = tied.filter((e) => !knownSet.has(e.animeId)).map((e) => e.animeId)
  return [...known, ...newlyTied]
}

// Patches one entry's UserAnimeEntryDto into whichever of mainLine/extras it
// lives in and recomputes the four averages and the my-favourite tie list
// from the result — an edit can change both (task 6.6). The completion badge
// is *not* recomputed here: it derives straight from series.mainLine on
// every render (completionBadge below), so patching the entry array is
// already enough to keep it current.
function patchSeriesEntry(series: SeriesDto, animeId: number, entry: UserAnimeEntryDto | null): SeriesDto {
  const patch = (entries: SeriesEntryDto[]) => entries.map((e) => (e.animeId === animeId ? { ...e, entry } : e))
  const mainLine = patch(series.mainLine)
  const extras = patch(series.extras)
  const myHighestScoreAnimeIds = recomputeMyHighestIds(series.stats.myHighestScoreAnimeIds, mainLine, extras)
  return {
    ...series,
    mainLine,
    extras,
    scores: recomputeScores(mainLine, extras),
    stats: { ...series.stats, myHighestScoreAnimeIds },
  }
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

function extrasGroupKey(group: { mediaType: string | null }, index: number): string {
  return `${group.mediaType}-${index}`
}

// Mirrors ScoreValue's own per-row `completed` convention (only reveals when
// the user has also turned on "always show completed scores"): a group
// average counts as settled when it has at least one finished-airing member
// and every finished-airing member is marked Completed or Dropped in my
// list — Dropped counts identically to Completed (score-visibility).
// Entries not yet aired or still airing don't count against it, since they
// can't be settled yet; a finished-airing entry that's not in my list at
// all still fails, since `entry` is undefined there.
function isGroupCompleted(entries: SeriesEntryDto[]): boolean {
  const finishedAiring = entries.filter((e) => e.airingStatus === 'finished_airing')
  return finishedAiring.length > 0 && finishedAiring.every((e) => isScoreRevealableStatus(e.entry?.status))
}

// Reveal precedence for a MAL average (design.md decision 9): finishing the
// whole main line reveals both averages unconditionally; short of that, a
// group reveals only when it's fully completed *and* nothing in the entire
// series is currently airing — an airing movie or special suppresses even
// the main-series average.
//
// `mainLineCompletedByMe` alone isn't enough for "finished the main line": it
// only checks entries that have *finished* airing, so it stays true while a
// main-line season is itself mid-run (the "Caught up" badge state) — that's
// not "finishing the main line", it's being caught up on a main line that's
// still going. Rule 1 is only meant for the case a main-line-airing spin-off
// happens *after* the main line is genuinely done, so it additionally
// requires no main-line member to be currently airing.
function malGroupRevealed(group: SeriesEntryDto[], series: SeriesDto): boolean {
  const mainLineAiring = series.mainLine.some((e) => e.airingStatus === 'currently_airing')
  if (series.stats.mainLineCompletedByMe && !mainLineAiring) return true
  const anySeriesAiring = [...series.mainLine, ...series.extras].some((e) => e.airingStatus === 'currently_airing')
  return isGroupCompleted(group) && !anySeriesAiring
}

type CompletionBadge = { label: string; className: string }

// Computed client-side from series.mainLine rather than a server stat
// (redesign-series-page design.md decision 1/2): it depends on episodesWatched
// and status, both of which an in-place row edit changes, so deriving it from
// the entry array the page already patches keeps it current for free.
function completionBadge(series: SeriesDto): CompletionBadge | null {
  const finishedAiring = series.mainLine.filter((e) => e.airingStatus === 'finished_airing')
  const currentlyAiring = series.mainLine.filter((e) => e.airingStatus === 'currently_airing')

  // A finished-airing entry I haven't completed rules out every badge state —
  // "you haven't watched this series" isn't news the header needs to shout.
  // Vacuously true when nothing has finished airing yet (e.g. a franchise
  // whose main line is a single still-running entry, like One Piece), so it
  // doesn't block the behind-count below.
  if (!finishedAiring.every((e) => e.entry?.status === 'Completed')) return null
  // Nothing in the main line has aired at all yet — there's nothing to be
  // caught up on or behind on.
  if (finishedAiring.length === 0 && currentlyAiring.length === 0) return null

  if (series.status === 'Finished') {
    return { label: 'Completed', className: 'completed' }
  }

  // EpisodesAiredAsOfAsync does no estimation — an unknown broadcast count
  // means the page can't tell whether I'm current, so it says nothing rather
  // than claiming "Caught up" or inventing a behind count.
  if (currentlyAiring.some((e) => e.airedEpisodes === null)) return null

  const behind = currentlyAiring.reduce(
    (sum, e) => sum + Math.max(0, (e.airedEpisodes ?? 0) - (e.entry?.episodesWatched ?? 0)),
    0,
  )

  return behind === 0 ? { label: 'Caught up', className: 'caught-up' } : { label: `${behind} behind`, className: 'behind' }
}

function MalScoreChip({ label, average, completed }: { label: string; average: SeriesAverageDto; completed: boolean }) {
  return (
    <ScoreChip role="mal" label={label}>
      <ScoreValue value={average.value} placeholder="No score" completed={completed} />
    </ScoreChip>
  )
}

function MineScoreChip({ label, average }: { label: string; average: SeriesAverageDto }) {
  return (
    <ScoreChip role="mine" label={label}>
      {formatAverage(average)}
    </ScoreChip>
  )
}

// Named watched/aired/total figures beside the progress bar (design.md
// decision 3) — the bar itself carries no inline label on this page. The
// aired figure is omitted while nothing is airing, since it would otherwise
// just repeat the total.
function ProgressReadout({
  watched,
  aired,
  total,
  hasUnknownTotal,
  showAired,
}: {
  watched: number
  aired: number
  total: number
  hasUnknownTotal: boolean
  showAired: boolean
}) {
  return (
    <div className="series-page__progress-readout">
      <span className="series-page__progress-figure series-page__progress-figure--mine">
        <span className="series-page__progress-dot" aria-hidden="true" />
        {watched} watched
      </span>
      {showAired && (
        <span className="series-page__progress-figure series-page__progress-figure--mal">
          <span className="series-page__progress-dot" aria-hidden="true" />
          {aired} aired
        </span>
      )}
      <span className="series-page__progress-figure series-page__progress-figure--total">
        of {formatProgressTotal(total, hasUnknownTotal)}
      </span>
    </div>
  )
}

// Franchise overview page: a hero header (poster, status, personal badge,
// external links, score chips, main-line progress), series-wide stats, a
// timeline ribbon, a numbered main-line watch order, a collapsible More
// section for extras, and a Rebuild control. Loads through
// usePageData('series:{animeId}', …) so back/forward restore works like
// every other page; row/tile edits are applied in place via setData rather
// than a refetch (design.md decision 11, tasks 5.7/3.9).
export function SeriesPage() {
  const { animeId: animeIdParam } = useParams()
  const animeId = Number(animeIdParam)
  const { data, loading, setData } = usePageData<SeriesLookupResult>(`series:${animeId}`, () => getSeries(animeId))
  const [rebuilding, setRebuilding] = useState(false)
  const [rebuildCount, setRebuildCount] = useState<number | null>(null)
  const { openEditor } = useEntryEditor()
  const [pictureRef, isLandscapePicture] = useLandscapePicture(data?.found ? data.series.pictureUrl : null)

  // More-section view state (design.md decision 3): `mineOnly` is the "in my
  // list" filter, on by default; `collapsedGroups` is per-group collapse,
  // all expanded by default; `unfilteredGroups` tracks groups where "+N
  // more" was used to see past the filter without disabling it everywhere.
  // All three are per-mount, transient state like the rest of this page's
  // view state — they reset on navigation rather than persisting.
  const [mineOnly, setMineOnly] = useState(true)
  const [collapsedGroups, setCollapsedGroups] = useState<Record<string, boolean>>({})
  const [unfilteredGroups, setUnfilteredGroups] = useState<Set<string>>(new Set())

  // Stops an in-flight rebuild loop from issuing another round once the page
  // has navigated away (design.md decision 2) — a round already in flight is
  // simply discarded rather than cancelled mid-request.
  const abortedRef = useRef(false)
  useEffect(() => {
    abortedRef.current = false
    return () => {
      abortedRef.current = true
    }
  }, [])

  function patchSeries(updater: (series: SeriesDto) => SeriesDto) {
    setData((prev) => (prev && prev.found ? { found: true, series: updater(prev.series) } : prev))
  }

  // Issues rounds — each spending the server's fixed rebuild budget — for as
  // long as the series is still partial and the previous round grew it, so a
  // franchise needing more fetches than one round allows completes in one
  // click instead of repeated clicking (design.md decision 2).
  async function handleRebuild() {
    if (rebuilding || !data?.found) return
    setRebuilding(true)
    try {
      let previousCount = data.series.mainLine.length + data.series.extras.length
      for (let round = 0; round < MAX_REBUILD_ROUNDS; round++) {
        if (abortedRef.current) return
        const series = await rebuildSeries(animeId)
        if (abortedRef.current) return
        setData({ found: true, series })

        const count = series.mainLine.length + series.extras.length
        setRebuildCount(count)
        if (!series.isPartial || count <= previousCount) break
        previousCount = count
      }
    } catch {
      // Leave the page showing whatever was already loaded; the user can retry.
    } finally {
      if (!abortedRef.current) {
        setRebuilding(false)
        setRebuildCount(null)
      }
    }
  }

  // Reorders locally first so the buttons feel immediate, then persists the
  // whole ordered list; a failed save reverts to the order that was actually
  // stored (design.md decision 11).
  async function handleReorderFavourite(seriesId: number, currentOrder: number[], index: number, direction: -1 | 1) {
    const targetIndex = index + direction
    if (targetIndex < 0 || targetIndex >= currentOrder.length) return

    const reordered = [...currentOrder]
    ;[reordered[index], reordered[targetIndex]] = [reordered[targetIndex], reordered[index]]

    patchSeries((series) => ({ ...series, stats: { ...series.stats, myHighestScoreAnimeIds: reordered } }))
    try {
      await setSeriesFavouriteOrder(seriesId, reordered)
    } catch {
      patchSeries((series) => ({ ...series, stats: { ...series.stats, myHighestScoreAnimeIds: currentOrder } }))
    }
  }

  function handleEdit(entry: SeriesEntryDto) {
    openEditor({
      animeId: entry.animeId,
      animeTitle: pickDisplayTitle(entry.title, entry.englishTitle),
      totalEpisodes: entry.totalEpisodes,
      airingStatus: entry.airingStatus,
      episodesAired: entry.airedEpisodes,
      entry: entry.entry,
      onSaved: (saved) => patchSeries((series) => patchSeriesEntry(series, entry.animeId, saved)),
      onDeleted: () => patchSeries((series) => patchSeriesEntry(series, entry.animeId, null)),
    })
  }

  function toggleExtrasGroup(key: string) {
    setCollapsedGroups((prev) => ({ ...prev, [key]: !prev[key] }))
  }

  // Reveals a group's remaining tiles from its "+N more" control: a
  // collapsed group simply uncollapses (into whatever the filter currently
  // shows); a group hidden only by the "in my list" filter is instead added
  // to unfilteredGroups, so this one group shows everything while every
  // other group keeps following the filter (design.md decision 3).
  function revealGroupTiles(key: string, wasCollapsed: boolean) {
    if (wasCollapsed) {
      setCollapsedGroups((prev) => ({ ...prev, [key]: false }))
    } else {
      setUnfilteredGroups((prev) => new Set(prev).add(key))
    }
  }

  // Every click resets the per-group overrides and expands every group, so
  // the effect of the toggle — in either direction — is always visible
  // rather than hidden behind a collapsed or overridden group.
  function toggleMineOnly() {
    setMineOnly((prev) => !prev)
    setUnfilteredGroups(new Set())
    setCollapsedGroups({})
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
  const badge = completionBadge(series)
  const isOngoing = series.status === 'Ongoing'
  // The bar and readout's own aired figure, summed straight from each
  // entry's airedEpisodes — distinct from stats.mainLineAiredEpisodes, which
  // excludes a member with an unknown total so the *episode-total* stat never
  // undercounts what it claims to be exact. A show like One Piece (unknown
  // total, known aired count) would otherwise read "0 aired" and show no
  // blue fill at all, despite the row right below it, and the home/detail
  // pages, all showing the real count.
  const mainLineAiredEpisodes = series.mainLine.reduce((sum, e) => sum + (e.airedEpisodes ?? 0), 0)

  // "Time watched"/"Time left" hide together once there's nothing left to
  // watch (design.md decision 7) — except when the runtime total itself is
  // unknown, in which case a zero time left reports missing data rather than
  // a finished series, so both stats stay visible.
  const timeLeftSeconds = Math.max(0, stats.mainLineRuntimeSeconds - stats.myWatchedSeconds)
  const runtimeUnknown = stats.mainLineRuntimeSeconds === 0 && stats.hasUnknownEpisodeCounts
  const showTimeStats = timeLeftSeconds > 0 || runtimeUnknown

  const highestMalEntries = stats.highestMalScoreAnimeIds
    .map((id) => findEntry(series, id))
    .filter((e): e is SeriesEntryDto => e !== undefined)
  const myHighestEntries = stats.myHighestScoreAnimeIds
    .map((id) => findEntry(series, id))
    .filter((e): e is SeriesEntryDto => e !== undefined)
  const mostRewatchedEntries = stats.mostRewatchedAnimeIds
    .map((id) => findEntry(series, id))
    .filter((e): e is SeriesEntryDto => e !== undefined)

  const extrasGroups = groupExtras(series.extras)
  // Membership, not status, per group's visible tiles (design.md decision 3):
  // Dropped and Plan-to-watch entries count exactly like Completed ones.
  const extrasGroupView = extrasGroups.map((group, index) => {
    const key = extrasGroupKey(group, index)
    const isCollapsed = collapsedGroups[key] ?? false
    const isUnfiltered = unfilteredGroups.has(key)
    const visibleItems = isCollapsed ? [] : mineOnly && !isUnfiltered ? group.items.filter((e) => e.entry != null) : group.items
    return { group, key, isCollapsed, visibleItems }
  })
  const nothingHidden = extrasGroupView.every(({ group, visibleItems }) => visibleItems.length === group.items.length)

  function toggleAllExtrasGroups() {
    if (nothingHidden) {
      const next: Record<string, boolean> = {}
      extrasGroups.forEach((group, index) => {
        next[extrasGroupKey(group, index)] = true
      })
      setCollapsedGroups(next)
      setUnfilteredGroups(new Set())
    } else {
      setMineOnly(false)
      setUnfilteredGroups(new Set())
      const next: Record<string, boolean> = {}
      extrasGroups.forEach((group, index) => {
        next[extrasGroupKey(group, index)] = false
      })
      setCollapsedGroups(next)
    }
  }

  const scoreAndProgress = (
    <>
      <div className="series-page__score-chips">
        <MalScoreChip
          label="MAL · Main series"
          average={scores.malMain}
          completed={malGroupRevealed(series.mainLine, series)}
        />
        {series.extras.length > 0 && (
          <MalScoreChip
            label="MAL · Everything"
            average={scores.malAll}
            completed={malGroupRevealed([...series.mainLine, ...series.extras], series)}
          />
        )}
        <MineScoreChip label="Mine · Main series" average={scores.mineMain} />
        {series.extras.length > 0 && <MineScoreChip label="Mine · Everything" average={scores.mineAll} />}
      </div>

      <div className="series-page__progress">
        {isOngoing ? (
          <AiringProgressBar
            aired={mainLineAiredEpisodes}
            watched={stats.myWatchedEpisodes}
            total={stats.mainLineEpisodeTotal > 0 ? stats.mainLineEpisodeTotal : null}
            finished={false}
            labelMode="none"
          />
        ) : (
          <ProgressBar
            watched={stats.myWatchedEpisodes}
            total={stats.mainLineEpisodeTotal > 0 ? stats.mainLineEpisodeTotal : null}
          />
        )}
        <ProgressReadout
          watched={stats.myWatchedEpisodes}
          aired={mainLineAiredEpisodes}
          total={stats.mainLineEpisodeTotal}
          hasUnknownTotal={stats.hasUnknownEpisodeCounts}
          showAired={isOngoing}
        />
      </div>
    </>
  )

  return (
    <div className="series-page">
      <div className={`series-page__header${isLandscapePicture ? ' series-page__header--landscape' : ''}`}>
        {series.pictureUrl ? (
          <img
            ref={pictureRef}
            src={series.pictureUrl}
            alt=""
            className={`series-page__picture${isLandscapePicture ? ' series-page__picture--landscape' : ''}`}
          />
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
            {badge && (
              <span className={`series-page__completion-badge series-page__completion-badge--${badge.className}`}>
                {badge.label}
              </span>
            )}
            <span className="series-page__year-span">{formatYearSpan(series.firstYear, series.lastYear)}</span>
          </div>
          <div className="series-page__links">
            <a
              href={`https://myanimelist.net/anime/${series.rootAnimeId}`}
              target="_blank"
              rel="noreferrer"
              className="series-page__related-link"
            >
              MyAnimeList
            </a>
            <a
              href={
                series.rootAniListId != null
                  ? `https://anilist.co/anime/${series.rootAniListId}`
                  : `https://anilist.co/search/anime?search=${encodeURIComponent(displayTitle)}`
              }
              target="_blank"
              rel="noreferrer"
              className="series-page__related-link"
            >
              AniList
            </a>
            <a
              href={`https://seriesgraph.com/show/search/${encodeURIComponent(displayTitle)}`}
              target="_blank"
              rel="noreferrer"
              className="series-page__related-link"
            >
              SeriesGraph
            </a>
          </div>

          {!isLandscapePicture && scoreAndProgress}
        </div>
        {isLandscapePicture && <div className="series-page__header-below">{scoreAndProgress}</div>}
      </div>

      <section className="series-box">
        <div className="series-page__stats-header">
          <h2>Series stats</h2>
          <div className="series-page__rebuild-row">
            <button type="button" className="series-page__rebuild" onClick={handleRebuild} disabled={rebuilding}>
              {rebuilding ? (rebuildCount !== null ? `Rebuilding… ${rebuildCount} entries` : 'Rebuilding…') : 'Rebuild'}
            </button>
            {series.isPartial && <span className="series-page__notice">Some entries couldn't be loaded yet.</span>}
            {series.isTruncated && <span className="series-page__notice">This series was too large to show in full.</span>}
          </div>
        </div>
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
            <dt>Entries completed</dt>
            <dd className="series-page__entries-completed">
              <span className="series-page__entries-completed-row">
                <span className="series-page__entries-completed-label">Main Series</span>
                {stats.entriesCompleted} of {stats.mainLineCount}
              </span>
              {stats.extrasCount > 0 && (
                <span className="series-page__entries-completed-row">
                  <span className="series-page__entries-completed-label">Extras</span>
                  {stats.extrasCompleted} of {stats.extrasCount}
                </span>
              )}
            </dd>
          </div>
          {showTimeStats && (
            <>
              <div>
                <dt>Time watched</dt>
                <dd>{formatRuntime(stats.myWatchedSeconds)}</dd>
              </div>
              <div>
                <dt>Time left</dt>
                <dd>{formatRuntime(timeLeftSeconds)}</dd>
              </div>
            </>
          )}
          {highestMalEntries.length > 0 && (
            <div>
              <dt>Highest MAL score</dt>
              <dd>
                <ul className="series-page__tie-list">
                  {highestMalEntries.map((entry) => {
                    // Settled status is enough — Completed or Dropped — since
                    // a dropped entry frequently carries no score of my own
                    // and requiring one would hide the stat indefinitely.
                    // Both statuses close the door on being spoiled about
                    // which entry is the series' best, which is what this
                    // box withholds the entry's title and link to guard
                    // against (design.md decision 7).
                    const revealed = isScoreRevealableStatus(entry.entry?.status)
                    return (
                      <li key={entry.animeId}>
                        {revealed ? (
                          <>
                            <Link to={`/anime/${entry.animeId}`}>{pickDisplayTitle(entry.title, entry.englishTitle)}</Link>{' '}
                            ·{' '}
                            <span className="score--mal">
                              <ScoreValue value={entry.malScore} completed={revealed} />
                            </span>
                          </>
                        ) : (
                          <span className="series-page__tie-list-placeholder">Not yet watched</span>
                        )}
                      </li>
                    )
                  })}
                </ul>
              </dd>
            </div>
          )}
          {mostRewatchedEntries.length > 0 && (
            <div>
              <dt>Most rewatched</dt>
              <dd>
                <ul className="series-page__tie-list">
                  {mostRewatchedEntries.map((entry) => (
                    <li key={entry.animeId}>
                      <Link to={`/anime/${entry.animeId}`}>{pickDisplayTitle(entry.title, entry.englishTitle)}</Link>{' '}
                      · ×{entry.entry?.rewatchCount}
                    </li>
                  ))}
                </ul>
              </dd>
            </div>
          )}
          {myHighestEntries.length > 0 && (
            <div>
              <dt>My favourite</dt>
              <dd>
                <ul className="series-page__tie-list">
                  {myHighestEntries.map((entry, index) => (
                    <li key={entry.animeId}>
                      <Link to={`/anime/${entry.animeId}`}>{pickDisplayTitle(entry.title, entry.englishTitle)}</Link>{' '}
                      · <span className="score--mine">{entry.entry?.myScore}</span>
                      {myHighestEntries.length > 1 && (
                        <span className="series-page__reorder-buttons">
                          <button
                            type="button"
                            onClick={() =>
                              handleReorderFavourite(series.seriesId, stats.myHighestScoreAnimeIds, index, -1)
                            }
                            disabled={index === 0}
                            aria-label={`Move ${pickDisplayTitle(entry.title, entry.englishTitle)} up`}
                          >
                            ▲
                          </button>
                          <button
                            type="button"
                            onClick={() =>
                              handleReorderFavourite(series.seriesId, stats.myHighestScoreAnimeIds, index, 1)
                            }
                            disabled={index === myHighestEntries.length - 1}
                            aria-label={`Move ${pickDisplayTitle(entry.title, entry.englishTitle)} down`}
                          >
                            ▼
                          </button>
                        </span>
                      )}
                    </li>
                  ))}
                </ul>
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

      {series.mainLine.length > 0 && (
        <section className="series-box">
          <h2>Main series</h2>
          <SeriesTimeline entries={series.mainLine} onEdit={handleEdit} />
        </section>
      )}

      {series.extras.length > 0 && (
        <section className="series-box">
          <div className="series-page__more-header">
            <h2>More</h2>
            <div className="series-page__more-controls">
              <button
                type="button"
                className={`series-page__toggle-mine${mineOnly ? ' series-page__toggle-mine--active' : ''}`}
                aria-pressed={mineOnly}
                onClick={toggleMineOnly}
              >
                In my list
              </button>
              <button type="button" className="series-page__toggle-all" onClick={toggleAllExtrasGroups}>
                {extrasGroups.length > 1
                  ? nothingHidden
                    ? 'Collapse all'
                    : 'Expand all'
                  : nothingHidden
                    ? 'Collapse'
                    : 'Expand'}
              </button>
            </div>
          </div>
          {extrasGroupView.map(({ group, key, isCollapsed, visibleItems }) => {
            const groupId = `series-extras-${key}`
            const hiddenCount = group.items.length - visibleItems.length
            return (
              <div key={key} className="series-page__extras-group">
                <h3>
                  <button
                    type="button"
                    className="series-page__extras-group-toggle"
                    aria-expanded={!isCollapsed}
                    aria-controls={groupId}
                    onClick={() => toggleExtrasGroup(key)}
                  >
                    <span className="series-page__extras-group-caret" aria-hidden="true">
                      {isCollapsed ? '▸' : '▾'}
                    </span>
                    {mediaTypeLabel(group.mediaType)} ({group.items.length})
                  </button>
                </h3>
                {visibleItems.length > 0 && (
                  <ul id={groupId} className="series-page__extras-grid">
                    {visibleItems.map((entry) => (
                      <SeriesExtraTile key={entry.animeId} entry={entry} onEdit={handleEdit} />
                    ))}
                  </ul>
                )}
                {hiddenCount > 0 && (
                  <button
                    type="button"
                    className="series-page__extras-group-hint"
                    onClick={() => revealGroupTiles(key, isCollapsed)}
                  >
                    +{hiddenCount} more
                  </button>
                )}
              </div>
            )
          })}
        </section>
      )}
    </div>
  )
}
