import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  getSeries,
  rebuildSeries,
  refreshSeriesPictures,
  setSeriesFavouriteOrder,
  setSeriesPicture,
  setSeriesTitle,
} from '../api/client.ts'
import type {
  SeriesAverageDto,
  SeriesDto,
  SeriesEntryDto,
  SeriesLookupResult,
  SeriesProgressBadge,
  SeriesSlotDto,
  SeriesStatsDto,
  UserAnimeEntryDto,
} from '../api/types.ts'
import { AiringProgressBar } from '../components/AiringProgressBar.tsx'
import { PicturePickerOverlay } from '../components/PicturePickerOverlay.tsx'
import { ProgressBar } from '../components/ProgressBar.tsx'
import { ScoreChip } from '../components/ScoreChip.tsx'
import { ScoreValue } from '../components/ScoreValue.tsx'
import { SeriesCompletionBadge } from '../components/SeriesCompletionBadge.tsx'
import { SeriesExtraTile } from '../components/SeriesExtraTile.tsx'
import { SeriesStatusPill } from '../components/SeriesStatusPill.tsx'
import { SeriesTimeline } from '../components/SeriesTimeline.tsx'
import { SeriesTitlePickerOverlay } from '../components/SeriesTitlePickerOverlay.tsx'
import { useEntryEditor } from '../context/EntryEditorContext.tsx'
import { useLandscapePicture } from '../hooks/useLandscapePicture.ts'
import { usePageData } from '../hooks/usePageData.ts'
import { useRestorableState } from '../hooks/useRestorableState.ts'
import {
  formatEpisodeTotal,
  formatRuntime,
  formatYearSpan,
  isScoreRevealableStatus,
  MEDIA_TYPE_ORDER,
  mediaTypeLabel,
  pickDisplayTitle,
} from '../utils/anime.ts'
import './SeriesPage.css'

const MAX_REBUILD_ROUNDS = 12

function formatRuntimeTotal(seconds: number, hasUnknown: boolean): string {
  if (seconds === 0 && hasUnknown) return 'Unknown'
  return `${formatRuntime(seconds)}${hasUnknown ? '+' : ''}`
}

// Mirrors formatEpisodeTotal's lower-bound marker but worded for the
// progress readout rather than the episode-total stat.
function formatProgressTotal(total: number, hasUnknown: boolean): string {
  if (total === 0 && hasUnknown) return 'unknown total'
  return `${total}${hasUnknown ? '+' : ''} total`
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

// Fills in each slot's default alternative for any slot the reader's own
// restored pick doesn't name — including a pick naming an anime that is no
// longer one of that slot's alternatives (series-page spec "A stale pick is
// ignored"), which a rebuild can produce.
function resolveSeriesPick(slots: SeriesSlotDto[], pick: Record<number, number>): Record<number, number> {
  const resolved: Record<number, number> = {}
  for (const slot of slots) {
    const picked = pick[slot.slotKey]
    resolved[slot.slotKey] = picked !== undefined && slot.alternativeAnimeIds.includes(picked) ? picked : slot.defaultBranchHeadAnimeId
  }
  return resolved
}

// Mirrors SeriesService.VisibleMainLineMembers exactly (design.md D4/D6): a
// trunk entry (no branchHeadAnimeId at all) is always shown; a branch entry
// is shown only under the slot combination that picked its own head.
function visibleMainLine(mainLine: SeriesEntryDto[], resolvedPick: Record<number, number>): SeriesEntryDto[] {
  const pickedHeadIds = new Set(Object.values(resolvedPick))
  return mainLine.filter((e) => e.branchHeadAnimeId === null || pickedHeadIds.has(e.branchHeadAnimeId))
}

function isDefaultPick(slots: SeriesSlotDto[], resolvedPick: Record<number, number>): boolean {
  return slots.every((slot) => resolvedPick[slot.slotKey] === slot.defaultBranchHeadAnimeId)
}

// Resolves the SeriesStatsDto for the reader's current pick (design.md D6,
// task 8.4). Deliberately not a client-side re-derivation — the server
// already computed one SeriesStatsDto per admissible combination
// (statsByPick), so this just looks the matching one up. At the default pick
// — no slots, or every slot still on its default — this returns series.stats
// directly rather than the equal-but-distinct statsByPick entry, so an
// in-place score/status edit (patchSeriesEntry) keeps showing live here
// exactly as it did before this capability; only after the reader actively
// switches a slot away from its default does this read from the server's
// last-fetched statsByPick snapshot instead.
function statsForPick(series: SeriesDto, resolvedPick: Record<number, number>): SeriesStatsDto {
  if (isDefaultPick(series.slots, resolvedPick)) return series.stats
  const wanted = series.slots.map((slot) => resolvedPick[slot.slotKey])
  const match = series.statsByPick.find(
    (byPick) => byPick.branchHeadAnimeIds.length === wanted.length && byPick.branchHeadAnimeIds.every((id, i) => id === wanted[i]),
  )
  return match?.stats ?? series.stats
}

// Display labels for RelationGroup's raw PascalCase enum names
// (split-series-by-version design.md decision 4), in the fixed display order
// SeriesRelationGroupOrder.cs defines — mirrored here since the extras array
// already arrives in that order (see groupExtras below).
const RELATION_GROUP_LABELS: Record<string, string> = {
  AlternativeVersion: 'Alternative version',
  AlternativeSetting: 'Alternative setting',
  Prequel: 'Prequel',
  Sequel: 'Sequel',
  ParentStory: 'Parent story',
  SideStory: 'Side story',
  FullStory: 'Full story',
  Summary: 'Summary',
  SpinOff: 'Spin-off',
  Character: 'Character',
  Adaptation: 'Adaptation',
  Other: 'Other',
}

function relationGroupLabel(relationGroup: string | null): string {
  return (relationGroup && RELATION_GROUP_LABELS[relationGroup]) || 'Other'
}

// Both real extra members and related entries arrive from the API already
// merged into one array, pre-sorted by relation group (in the fixed display
// order) then aired date within it (SeriesService.BuildDisplayExtrasAsync),
// so grouping is just bucketing consecutive same-relationGroup runs rather
// than re-sorting (split-series-by-version tasks 7.2/10.2 — replaces the
// former media-type grouping).
function groupExtras(extras: SeriesEntryDto[]): { relationGroup: string | null; items: SeriesEntryDto[] }[] {
  const groups: { relationGroup: string | null; items: SeriesEntryDto[] }[] = []
  for (const entry of extras) {
    const last = groups[groups.length - 1]
    if (last && last.relationGroup === entry.relationGroup) last.items.push(entry)
    else groups.push({ relationGroup: entry.relationGroup, items: [entry] })
  }
  return groups
}

function extrasGroupKey(group: { relationGroup: string | null }, index: number): string {
  return `${group.relationGroup}-${index}`
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

// An entry's effective watched-episode figure (polish-rewatch design.md D2):
// a Rewatching entry counts as fully watched — the greater of its own
// episodesWatched and its aired-so-far figure — since entering Rewatching
// resets episodesWatched to 0. Every other status reads episodesWatched as
// recorded. Mirrors the backend's WatchMath.EffectiveWatchedEpisodes exactly,
// using this entry's own airedEpisodes (null-means-unknown, treated as "fall
// back to episodesWatched") rather than restating that fallback here.
function effectiveWatchedEpisodes(entry: SeriesEntryDto): number {
  const watched = entry.entry?.episodesWatched ?? 0
  if (entry.entry?.status !== 'Rewatching') return watched
  return Math.max(watched, entry.airedEpisodes ?? watched)
}

// Computed client-side from the main-line entry array rather than a server
// stat (redesign-series-page design.md decision 1/2): it depends on episodesWatched
// and status, both of which an in-place row edit changes, so deriving it from
// the entry array the page already patches keeps it current for free. Returns
// the same { badge, behind } shape the Series page's cards get from the
// server (add-series-browser design.md D5), so both render through the one
// shared SeriesCompletionBadge component and can never visually disagree.
//
// Six-step precedence (polish-series-badges-and-filters design.md D1),
// mirrored exactly by the backend's SeriesRankingIndex.ProgressBadge:
// Completed, Dropped (D2 — the most recently aired Dropped entry with
// nothing aired after it ever watched), Caught up/N behind (generalized
// over the whole aired main line, not just a currently-airing entry),
// Unwatched (D3 — nothing watched at all, decided before Dropped can be
// ruled out by broadcast data it doesn't need), no badge as the fallback.
// A Rewatching main-line entry counts as fully watched throughout (D2 of
// polish-rewatch-more-and-filters) via effectiveWatchedEpisodes above, and
// rule (1) additionally accepts Rewatching alongside Completed so starting a
// rewatch of a finished franchise doesn't downgrade its badge.
//
// Takes the picked/visible main line, not series.mainLine (rebuild-series-
// by-story-component design.md D6): an unpicked branch's episodes are no
// more "mine to watch" than an unpicked route's runtime is "mine to watch"
// in the time-left figure beside it, so the badge stays consistent with that
// figure rather than freezing at whatever the default route showed.
function completionBadge(series: SeriesDto, mainLine: SeriesEntryDto[]): { badge: SeriesProgressBadge; behind: number | null } {
  const airedMembers = mainLine
    .filter((e) => e.airingStatus === 'finished_airing' || e.airingStatus === 'currently_airing')
    .slice()
    .sort((a, b) => a.order - b.order)
  const finishedAiring = airedMembers.filter((e) => e.airingStatus === 'finished_airing')

  // 1. Completed: the whole series is done and every finished-airing
  // main-line entry is marked Completed or Rewatching. Skipped (not
  // vacuously true) when nothing has finished airing, since it also requires
  // at least one such entry — a single still-running entry falls through to
  // the rules below.
  if (
    series.status === 'Finished' &&
    finishedAiring.length > 0 &&
    finishedAiring.every((e) => e.entry?.status === 'Completed' || e.entry?.status === 'Rewatching')
  ) {
    return { badge: 'Completed', behind: null }
  }

  // 2. Dropped: the most recently aired Dropped entry, with nothing aired
  // after it ever watched — a drop later resumed and watched past doesn't
  // count (a later entry marked Rewatching counts as watched past it, via
  // effectiveWatchedEpisodes). Decided from watch status alone, never
  // blocked by an unknown broadcast count.
  const droppedEntries = airedMembers.filter((e) => e.entry?.status === 'Dropped')
  if (droppedEntries.length > 0) {
    const lastDroppedOrder = Math.max(...droppedEntries.map((e) => e.order))
    const nothingWatchedAfter = airedMembers
      .filter((e) => e.order > lastDroppedOrder)
      .every((e) => effectiveWatchedEpisodes(e) === 0)
    if (nothingWatchedAfter) return { badge: 'Dropped', behind: null }
  }

  const watchedTotal = mainLine.reduce((sum, e) => sum + effectiveWatchedEpisodes(e), 0)

  // 5. Unwatched: something has aired but nothing has ever been watched,
  // and (2) didn't already claim the case.
  if (watchedTotal === 0) {
    return airedMembers.length > 0 ? { badge: 'Unwatched', behind: null } : { badge: 'None', behind: null }
  }

  // 3/4. Caught up / N behind, generalized over every aired main-line entry
  // rather than only a currently-airing one. AiredEpisodes does no
  // estimation — an unknown broadcast count means this can't be computed
  // reliably, so it shows nothing rather than inventing a figure.
  if (airedMembers.some((e) => e.airedEpisodes === null)) return { badge: 'None', behind: null }

  const airedTotal = airedMembers.reduce((sum, e) => sum + (e.airedEpisodes ?? 0), 0)
  const behind = Math.max(0, airedTotal - watchedTotal)

  return behind === 0 ? { badge: 'CaughtUp', behind: null } : { badge: 'Behind', behind }
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
  const [showPicturePicker, setShowPicturePicker] = useState(false)
  const [showTitlePicker, setShowTitlePicker] = useState(false)

  // Bounded pool backfill (design D6) — mirrors the anime detail page's
  // picture backfill: fire once per series while members remain unfetched,
  // then merge the server's fresh identity (pictureOptions/titleOptions/
  // picturesPendingCount) into state without a reload.
  const seriesPictureRefreshRequestedForRef = useRef<number | null>(null)
  useEffect(() => {
    if (!data?.found) return
    const series = data.series
    if (series.picturesPendingCount <= 0) return
    if (seriesPictureRefreshRequestedForRef.current === series.seriesId) return
    seriesPictureRefreshRequestedForRef.current = series.seriesId
    refreshSeriesPictures(animeId)
      .then(() => getSeries(animeId))
      .then((result) => {
        if (!result.found) return
        setData((prev) =>
          prev && prev.found && prev.series.seriesId === result.series.seriesId ? { found: true, series: result.series } : prev,
        )
      })
      .catch(() => {
        // Leave picturesPendingCount as the server last reported it — a
        // later visit's read re-evaluates and retries.
      })
  }, [data, animeId, setData])

  // More-section view state (design.md decision 3): `mineOnly` is the "in my
  // list" filter, on by default; `collapsedGroups` is per-group collapse,
  // all expanded by default; `unfilteredGroups` tracks groups where "+N
  // more" was used to see past the filter without disabling it everywhere.
  // Restorable like the page's other view controls (polish-rewatch-more-
  // and-filters design.md D3): the `pageStateStore` snapshot holds live
  // object references and never serialises, so the `Set` and `Record`
  // survive as-is, and keying by `location.key` already keeps two different
  // series' pages from sharing this state. A fresh visit still seeds the
  // documented defaults below, since useRestorableState falls back to
  // `initial` whenever there's no snapshot to restore from.
  const [mineOnly, setMineOnly] = useRestorableState('moreMineOnly', true)
  const [collapsedGroups, setCollapsedGroups] = useRestorableState<Record<string, boolean>>('moreCollapsedGroups', {})
  const [unfilteredGroups, setUnfilteredGroups] = useRestorableState<Set<string>>('moreUnfilteredGroups', new Set())
  // The media-type filter row above More (series-page spec "The More section
  // offers media-type filter buttons", tasks 10.3-10.5): none selected by
  // default, restorable beside the other view controls above. A restored type
  // no extra of this render carries is ignored below rather than cleaned up
  // here, the same graceful-staleness pattern collapsedGroups/unfilteredGroups
  // already use for a group key that no longer exists.
  const [selectedMediaTypes, setSelectedMediaTypes] = useRestorableState<Set<string>>('moreSelectedMediaTypes', new Set())
  // The version-slot picker's own pick, per slot key (series-page spec "The
  // More section's view state is restored with the page"), restorable beside
  // the view controls above. Raw and possibly stale — resolveSeriesPick below
  // fills in a slot's default for any key it doesn't name and ignores a pick
  // naming an anime that is no longer one of that slot's alternatives (task
  // 8.5), the same graceful-staleness pattern selectedMediaTypes above uses.
  const [pick, setPick] = useRestorableState<Record<number, number>>('seriesPick', {})
  // series-page "A More group's heading opens that group in full"
  // (design.md D2): the "in my list" control reports itself on only while
  // the filter is actually in force across every group — opening any one
  // group in full (via its heading) makes this read off.
  const filterActive = mineOnly && unfilteredGroups.size === 0

  // Scroll-on-open (design.md D4, tasks.md 6.4-6.6): which group's heading
  // was just opened, so a layout effect below can scroll it to the top of
  // the viewport once its newly revealed tiles have been laid out. A plain
  // ref/state pair, not restorable — this is a one-shot action consumed
  // within the same render pass, not view state to bring back on return.
  const groupHeadingRefs = useRef<Map<string, HTMLHeadingElement>>(new Map())
  const [pendingScrollGroupKey, setPendingScrollGroupKey] = useState<string | null>(null)

  // Runs after the group's tiles have committed — they're what makes the
  // page tall enough to reach the target — and scrolls the window (not
  // `scrollIntoView`, which would silently pick the nearest scrollable
  // ancestor; the page's horizontally-scrolling timeline above the More
  // section makes that ambiguous). No offset, since nothing on this page is
  // sticky or fixed. When the target exceeds the maximum scroll offset,
  // `window.scrollTo` clamps on its own — that clamp *is* "get as far down
  // as possible" when the group is too near the end to reach the top.
  useLayoutEffect(() => {
    if (pendingScrollGroupKey === null) return
    const heading = groupHeadingRefs.current.get(pendingScrollGroupKey)
    if (heading) {
      const target = heading.getBoundingClientRect().top + window.scrollY
      window.scrollTo(0, target)
    }
    setPendingScrollGroupKey(null)
  }, [pendingScrollGroupKey])

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

  // Both apply optimistically (spec "A chosen picture applies immediately")
  // and affect only the series (spec "Choosing does not touch the members"):
  // neither writes to any member anime's own pictureUrl/title.
  function handlePickSeriesPicture(url: string) {
    if (!data?.found) return
    const seriesId = data.series.seriesId
    patchSeries((series) => ({ ...series, pictureUrl: url, selectedPictureUrl: url }))
    setSeriesPicture(seriesId, url).catch(() => {
      // The picker already closed; a later refresh/reload re-syncs if the
      // save failed server-side.
    })
  }

  function handlePickSeriesTitle(title: string) {
    if (!data?.found) return
    const seriesId = data.series.seriesId
    patchSeries((series) => ({ ...series, title, englishTitle: null, selectedTitle: title }))
    setSeriesTitle(seriesId, title).catch(() => {
      // The picker already closed; a later refresh/reload re-syncs if the
      // save failed server-side.
    })
  }

  function handleEdit(entry: SeriesEntryDto) {
    openEditor({
      animeId: entry.animeId,
      animeTitle: pickDisplayTitle(entry.title, entry.englishTitle),
      totalEpisodes: entry.totalEpisodes,
      airingStatus: entry.airingStatus,
      episodesAired: entry.airedEpisodes,
      mediaType: entry.mediaType,
      entry: entry.entry,
      onSaved: (saved) => patchSeries((series) => patchSeriesEntry(series, entry.animeId, saved)),
      onDeleted: () => patchSeries((series) => patchSeriesEntry(series, entry.animeId, null)),
    })
  }

  // series-versions "Picking an alternative SHALL change only what the page
  // shows and the figures... It SHALL NOT rebuild the series, SHALL NOT
  // change any anime's membership, and SHALL NOT change the series' root,
  // title or picture" — a plain local state write, no request.
  function handlePickAlternative(slotKey: number, animeId: number) {
    setPick((prev) => ({ ...prev, [slotKey]: animeId }))
  }

  // series-page "A More group's heading opens that group in full"
  // (design.md D1): the heading has one job — open this group — with
  // collapse as its off state. A group already showing everything
  // collapses; anything else expands, and, while the filter is on, is
  // exempted from it so the heading can show extras the filter would
  // otherwise hide. Only the opening branch schedules a scroll (design.md
  // D4) — collapsing dismisses content, it doesn't move the page.
  function handleExtrasGroupHeadingClick(key: string, showsAll: boolean) {
    if (showsAll) {
      setCollapsedGroups((prev) => ({ ...prev, [key]: true }))
      return
    }
    setCollapsedGroups((prev) => ({ ...prev, [key]: false }))
    if (mineOnly) setUnfilteredGroups((prev) => new Set(prev).add(key))
    setPendingScrollGroupKey(key)
  }

  // Multi-select toggle for the media-type filter row (task 10.3): selecting
  // narrows every group to that type, alongside whatever else is already
  // selected; selecting none narrows nothing.
  function toggleMediaType(mediaType: string) {
    setSelectedMediaTypes((prev) => {
      const next = new Set(prev)
      if (next.has(mediaType)) next.delete(mediaType)
      else next.add(mediaType)
      return next
    })
  }

  // Reveals a group's remaining tiles from its "+N more" control, which is
  // only ever offered on an expanded group hiding some of its own behind the
  // filter (design.md D3) — so this is always the unfilter case; a collapsed
  // group's heading is what opens it now (D1).
  function revealGroupTiles(key: string) {
    setUnfilteredGroups((prev) => new Set(prev).add(key))
  }

  // design.md D2: filterActive reports whether the filter is actually in
  // force everywhere, not just the stored intent — so opening one group in
  // full (D1) immediately reads as "off" here too. Pressing while active
  // turns the filter off; pressing while inactive turns it on and resets
  // every per-group override, returning the section to the state a freshly
  // opened series page is in (both directions already reset the same way).
  function toggleMineOnly() {
    setMineOnly(!filterActive)
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
  const { scores } = series
  // The reader's pick, resolved to a concrete alternative per slot (defaults
  // filled in, a stale one ignored — series-page spec "A stale pick is
  // ignored"), and the two things it drives: which main-line entries are
  // shown (rebuild-series-by-story-component design.md D4) and which
  // statsByPick combination describes them (design.md D6). Score averages
  // deliberately keep reading `scores` above, untouched by any pick.
  const resolvedPick = resolveSeriesPick(series.slots, pick)
  const mainLineVisible = visibleMainLine(series.mainLine, resolvedPick)
  const stats = statsForPick(series, resolvedPick)
  const displayTitle = pickDisplayTitle(series.title, series.englishTitle)
  const { badge, behind } = completionBadge(series, mainLineVisible)
  const isRunning = series.status === 'Airing' || series.status === 'Ongoing'
  // The bar and readout's own aired figure, summed straight from each
  // entry's airedEpisodes — distinct from stats.mainLineAiredEpisodes, which
  // excludes a member with an unknown total so the *episode-total* stat never
  // undercounts what it claims to be exact. A show like One Piece (unknown
  // total, known aired count) would otherwise read "0 aired" and show no
  // blue fill at all, despite the row right below it, and the home/detail
  // pages, all showing the real count. Scoped to the picked/visible main
  // line, same as stats above, so it never counts an unpicked branch's aired
  // episodes into this route's bar.
  const mainLineAiredEpisodes = mainLineVisible.reduce((sum, e) => sum + (e.airedEpisodes ?? 0), 0)

  // Time watched and time left are now shown independently (design D11): with
  // rewatches counted, a finished franchise that's been rewatched is exactly
  // where "time watched" is interesting, so it no longer hides just because
  // there's nothing left to watch. Time left keeps hiding at zero, except
  // when the runtime total itself is unknown, in which case a zero time left
  // reports missing data rather than a finished series.
  const timeWatchedSeconds = stats.myWatchedSeconds + stats.myRewatchedSeconds
  const timeLeftSeconds = Math.max(0, stats.mainLineRuntimeSeconds - stats.myWatchedSeconds)
  const runtimeUnknown = stats.mainLineRuntimeSeconds === 0 && stats.hasUnknownEpisodeCounts
  const showTimeWatched = timeWatchedSeconds > 0
  const showTimeLeft = timeLeftSeconds > 0 || runtimeUnknown

  const highestMalEntries = stats.highestMalScoreAnimeIds
    .map((id) => findEntry(series, id))
    .filter((e): e is SeriesEntryDto => e !== undefined)
  const myHighestEntries = stats.myHighestScoreAnimeIds
    .map((id) => findEntry(series, id))
    .filter((e): e is SeriesEntryDto => e !== undefined)
  const mostRewatchedEntries = stats.mostRewatchedAnimeIds
    .map((id) => findEntry(series, id))
    .filter((e): e is SeriesEntryDto => e !== undefined)

  // Media-type filter (series-page spec "The More section offers media-type
  // filter buttons", tasks 10.3-10.5): one button per type actually present
  // among the extras/related entries, in the app's fixed media-type order
  // plus any exotic type that order doesn't name (alphabetical tail) and a
  // trailing catch-all for an untyped entry. A restored selection naming a
  // type nothing here carries is dropped rather than erroring (task 10.5).
  const presentMediaTypes = new Set(series.extras.map((e) => e.mediaType ?? 'unknown'))
  const availableMediaTypes = [
    ...MEDIA_TYPE_ORDER.filter((type) => presentMediaTypes.has(type)),
    ...[...presentMediaTypes].filter((type) => !MEDIA_TYPE_ORDER.includes(type) && type !== 'unknown').sort(),
    ...(presentMediaTypes.has('unknown') ? ['unknown'] : []),
  ]
  const activeMediaTypes = new Set([...selectedMediaTypes].filter((type) => presentMediaTypes.has(type)))
  const typeFilterActive = activeMediaTypes.size > 0
  function typeAdmits(entry: SeriesEntryDto): boolean {
    return !typeFilterActive || activeMediaTypes.has(entry.mediaType ?? 'unknown')
  }

  const extrasGroups = groupExtras(series.extras)
  // Membership, not status, per group's visible tiles (design.md decision 3);
  // narrowed further by the type filter, which composes with it rather than
  // replacing it (task 10.4). Dropped and Plan-to-watch entries count exactly
  // like Completed ones.
  const extrasGroupView = extrasGroups
    .map((group, index) => {
      const key = extrasGroupKey(group, index)
      const isCollapsed = collapsedGroups[key] ?? false
      const isUnfiltered = unfilteredGroups.has(key)
      const typeAdmitted = group.items.filter(typeAdmits)
      // design.md D1: the group's heading opens it in full unless it's
      // already showing everything, in which case the heading collapses it.
      const showsAll = !isCollapsed && (!mineOnly || isUnfiltered)
      const visibleItems = isCollapsed
        ? []
        : mineOnly && !isUnfiltered
          ? typeAdmitted.filter((e) => e.entry != null)
          : typeAdmitted
      return { group, key, isCollapsed, showsAll, visibleItems, typeAdmittedCount: typeAdmitted.length }
    })
    // While a type is selected, a group left with nothing the type filter
    // admits isn't rendered at all — a column of empty headings would tell
    // the reader nothing (task 10.4).
    .filter(({ typeAdmittedCount }) => !typeFilterActive || typeAdmittedCount > 0)
  const nothingHidden = extrasGroupView.every(
    ({ visibleItems, typeAdmittedCount }) => visibleItems.length === typeAdmittedCount,
  )

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
        {isRunning ? (
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
          showAired={isRunning}
        />
      </div>
    </>
  )

  return (
    <div className="series-page">
      {showPicturePicker && (
        <PicturePickerOverlay
          title="Choose picture"
          options={series.pictureOptions}
          current={series.pictureUrl}
          onPick={handlePickSeriesPicture}
          onClose={() => setShowPicturePicker(false)}
          note={
            series.picturesPendingCount > 0
              ? `${series.picturesPendingCount} member${series.picturesPendingCount === 1 ? '' : 's'} not yet fetched — more pictures may appear on a later visit.`
              : undefined
          }
        />
      )}

      {showTitlePicker && (
        <SeriesTitlePickerOverlay
          offeredTitles={series.titleOptions}
          current={displayTitle}
          onPick={handlePickSeriesTitle}
          onClose={() => setShowTitlePicker(false)}
        />
      )}

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
            <SeriesStatusPill status={series.status} />
            <SeriesCompletionBadge badge={badge} behindEpisodes={behind} />
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
            {series.pictureOptions.length > 1 && (
              <button type="button" className="series-page__stats-action" onClick={() => setShowPicturePicker(true)}>
                Choose picture
              </button>
            )}
            <button type="button" className="series-page__stats-action" onClick={() => setShowTitlePicker(true)}>
              Choose title
            </button>
            <button type="button" className="series-page__stats-action" onClick={handleRebuild} disabled={rebuilding}>
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
          {showTimeWatched && (
            <div>
              <dt>Time watched</dt>
              <dd>{formatRuntime(timeWatchedSeconds)}</dd>
            </div>
          )}
          {showTimeLeft && (
            <div>
              <dt>Time left</dt>
              <dd>{formatRuntime(timeLeftSeconds)}</dd>
            </div>
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

      {mainLineVisible.length > 0 && (
        <section className="series-box">
          <h2>Main series</h2>
          <SeriesTimeline
            entries={mainLineVisible}
            allEntries={series.mainLine}
            slots={series.slots}
            onPick={handlePickAlternative}
            onEdit={handleEdit}
          />
        </section>
      )}

      {series.extras.length > 0 && (
        <section className="series-box">
          <div className="series-page__more-header">
            <h2>More</h2>
            <div className="series-page__more-controls">
              <button
                type="button"
                className={`series-page__toggle-mine${filterActive ? ' series-page__toggle-mine--active' : ''}`}
                aria-pressed={filterActive}
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
          {availableMediaTypes.length > 0 && (
            <div className="series-page__type-filter" aria-label="Filter by media type (multi-select)">
              {availableMediaTypes.map((type) => {
                const active = activeMediaTypes.has(type)
                return (
                  <button
                    key={type}
                    type="button"
                    aria-pressed={active}
                    className={`series-page__type-filter-button${active ? ' series-page__type-filter-button--active' : ''}`}
                    onClick={() => toggleMediaType(type)}
                  >
                    {mediaTypeLabel(type === 'unknown' ? null : type)}
                  </button>
                )
              })}
            </div>
          )}
          {extrasGroupView.map(({ group, key, isCollapsed, showsAll, visibleItems, typeAdmittedCount }) => {
            const groupId = `series-extras-${key}`
            const hiddenCount = typeAdmittedCount - visibleItems.length
            // design.md D3: the hidden-count control belongs only to a group
            // that is showing something and hiding the rest — a group
            // showing nothing (collapsed, or nothing of mine in it) offers
            // no control; its heading opens it instead.
            const showHiddenCountControl = !isCollapsed && visibleItems.length > 0 && hiddenCount > 0
            return (
              <div key={key} className="series-page__extras-group">
                <h3
                  ref={(el) => {
                    if (el) groupHeadingRefs.current.set(key, el)
                    else groupHeadingRefs.current.delete(key)
                  }}
                >
                  <button
                    type="button"
                    className="series-page__extras-group-toggle"
                    aria-expanded={!isCollapsed}
                    aria-controls={groupId}
                    onClick={() => handleExtrasGroupHeadingClick(key, showsAll)}
                  >
                    <span className="series-page__extras-group-caret" aria-hidden="true">
                      {isCollapsed ? '▸' : '▾'}
                    </span>
                    {relationGroupLabel(group.relationGroup)} ({typeAdmittedCount})
                  </button>
                </h3>
                {visibleItems.length > 0 && (
                  <ul id={groupId} className="series-page__extras-grid">
                    {visibleItems.map((entry) => (
                      <SeriesExtraTile key={entry.animeId} entry={entry} onEdit={handleEdit} />
                    ))}
                  </ul>
                )}
                {showHiddenCountControl && (
                  <button type="button" className="series-page__extras-group-hint" onClick={() => revealGroupTiles(key)}>
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
