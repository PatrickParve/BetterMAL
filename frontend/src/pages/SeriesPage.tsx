import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  ApiError,
  getSeries,
  moveFavouriteAdjacent,
  rebuildSeries,
  refreshSeriesPictures,
  resetSeriesPicture,
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
import { RevealControl } from '../components/RevealControl.tsx'
import { ScoreChip } from '../components/ScoreChip.tsx'
import { ScoreValue } from '../components/ScoreValue.tsx'
import { SeriesCompletionBadge } from '../components/SeriesCompletionBadge.tsx'
import { SeriesExtraTile } from '../components/SeriesExtraTile.tsx'
import { SeriesStatusPill } from '../components/SeriesStatusPill.tsx'
import { SeriesTimeline } from '../components/SeriesTimeline.tsx'
import { SeriesTitlePickerOverlay } from '../components/SeriesTitlePickerOverlay.tsx'
import { useActionFailure } from '../context/ActionFailureContext.tsx'
import { useEntryEditor } from '../context/EntryEditorContext.tsx'
import { useScoreVisibility } from '../context/ScoreVisibilityContext.tsx'
import { useLandscapePicture } from '../hooks/useLandscapePicture.ts'
import { usePageData } from '../hooks/usePageData.ts'
import { useRestorableState } from '../hooks/useRestorableState.ts'
import { useScoreReveal } from '../hooks/useScoreReveal.ts'
import {
  formatEpisodeTotal,
  formatRuntime,
  formatYearSpan,
  isScoreRevealableStatus,
  MEDIA_TYPE_ORDER,
  mediaTypeLabel,
  pickDisplayTitle,
} from '../utils/anime.ts'
import { deriveSeriesStats, effectiveWatchedEpisodes, realExtras } from '../utils/seriesStats.ts'
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

// realMembers must already be the real extras (realExtras from
// seriesStats.ts), not the raw display list — related entries the server
// excludes from every average (design.md decision 6 of
// polish-series-page-more-and-routes).
function recomputeScores(mainLine: SeriesEntryDto[], realMembers: SeriesEntryDto[]) {
  const all = [...mainLine, ...realMembers]
  return {
    malMain: malAverage(mainLine),
    malAll: malAverage(all),
    mineMain: mineAverage(mainLine),
    mineAll: mineAverage(all),
  }
}

// Mirrors SeriesService's tie-break rule for my-highest-score ties: sorted
// by each entry's globalRank — its place in the anime-ranking capability's
// whole-library ranking (polish-... design.md D?) — ascending, so the
// favourite list agrees with, and a series-page reorder can move, the same
// ranking the ranking editor produces. An entry outside that ranking (never
// hand-ordered) sorts last, same as the server's tie-break. malScore never
// changes through a row edit, so highestMalScoreAnimeIds needs no equivalent
// here. realMembers — see recomputeScores above.
function recomputeMyHighestIds(mainLine: SeriesEntryDto[], realMembers: SeriesEntryDto[]): number[] {
  const all = [...mainLine, ...realMembers]
  const scored = all.filter((e) => (e.entry?.myScore ?? 0) > 0)
  if (scored.length === 0) return []

  const max = Math.max(...scored.map((e) => e.entry!.myScore!))
  const tied = scored.filter((e) => e.entry!.myScore === max)
  return tied
    .slice()
    .sort((a, b) => (a.globalRank ?? Infinity) - (b.globalRank ?? Infinity))
    .map((e) => e.animeId)
}

// Mirrors BuildStats' TiedTopIds for mostRewatchedAnimeIds with no
// tie-break key: every entry whose rewatch count ties the maximum (above
// zero), in watch order — main line then real extras, the order the two
// arrays are already in (design.md decision 4). A rewatch count is
// editable, so this stat goes stale without a recomputation on every edit,
// unlike highestMalScoreAnimeIds.
function recomputeMostRewatchedIds(mainLine: SeriesEntryDto[], realMembers: SeriesEntryDto[]): number[] {
  const all = [...mainLine, ...realMembers]
  const rewatched = all.filter((e) => (e.entry?.rewatchCount ?? 0) > 0)
  if (rewatched.length === 0) return []

  const max = Math.max(...rewatched.map((e) => e.entry!.rewatchCount))
  return rewatched.filter((e) => e.entry!.rewatchCount === max).map((e) => e.animeId)
}

// Patches one entry's UserAnimeEntryDto into whichever of mainLine/extras it
// lives in and recomputes the four averages and the my-favourite/most-
// rewatched tie lists from the result — an edit can change any of them
// (task 6.6). Every recomputation is scoped to the real extras, not the raw
// display list, matching the member basis BuildStats itself uses
// (design.md decision 6). The completion badge is *not* recomputed here: it
// derives straight from series.mainLine on every render (completionBadge
// below), so patching the entry array is already enough to keep it current.
function patchSeriesEntry(series: SeriesDto, animeId: number, entry: UserAnimeEntryDto | null): SeriesDto {
  const patch = (entries: SeriesEntryDto[]) => entries.map((e) => (e.animeId === animeId ? { ...e, entry } : e))
  const mainLine = patch(series.mainLine)
  const extras = patch(series.extras)
  const realMembers = realExtras(extras)
  const myHighestScoreAnimeIds = recomputeMyHighestIds(mainLine, realMembers)
  const mostRewatchedAnimeIds = recomputeMostRewatchedIds(mainLine, realMembers)
  return {
    ...series,
    mainLine,
    extras,
    scores: recomputeScores(mainLine, realMembers),
    stats: { ...series.stats, myHighestScoreAnimeIds, mostRewatchedAnimeIds },
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
// (statsByPick), so this just looks the matching one up. This is the *base*
// deriveSeriesStats (seriesStats.ts) then patches with edit-sensitive
// figures (polish-series-page-more-and-routes design.md decision 5) — that
// derivation, not which snapshot this function picks, is what now keeps an
// edit live on every route; this function still supplies everything else
// (episode/runtime totals, aired figures, the longest gap, studios, genres).
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
  const reportFailure = useActionFailure()
  const [pictureRef, isLandscapePicture] = useLandscapePicture(data?.found ? data.series.pictureUrl : null)
  const [showPicturePicker, setShowPicturePicker] = useState(false)
  const [showTitlePicker, setShowTitlePicker] = useState(false)
  // Called unconditionally here, ahead of the loading/not-found guards below,
  // since both are hooks (react/rules-of-hooks) — unlike highestMalSettled
  // (derived from `series`, computed after the guards near highestMalEntries
  // below), neither depends on data having loaded.
  const { hidden } = useScoreVisibility()
  const [highestMalRevealed, revealHighestMal] = useScoreReveal()

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

  // More-section view state: `mineOnly` is the "in my list" filter, **off**
  // by default (fix-score-reveal-and-nplus1 design.md D3) — a freshly opened
  // series page narrows nothing on the user's behalf; `collapsedGroups` is
  // per-group collapse, all collapsed by default (polish-series-page-more-
  // and-routes design.md decision 1 — an absent key reads as collapsed) but
  // now also the thing activating the "in my list" control writes to,
  // opening every group that holds one of my extras (polish-detail-dates-
  // and-error-messages design.md D9); `unfilteredGroups` tracks groups where
  // "+N more" was used to see past the filter without disabling it
  // everywhere.
  // Restorable like the page's other view controls (polish-rewatch-more-
  // and-filters design.md D3): the `pageStateStore` snapshot holds live
  // object references and never serialises, so the `Set` and `Record`
  // survive as-is, and keying by `location.key` already keeps two different
  // series' pages from sharing this state. A fresh visit still seeds the
  // documented defaults below, since useRestorableState falls back to
  // `initial` whenever there's no snapshot to restore from.
  const [mineOnly, setMineOnly] = useRestorableState('moreMineOnly', false)
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

  // Scroll-on-pick: picking an alternative changes the stats box above the
  // Main series section (favourites, entries completed, the time figures),
  // which can change height and shift the Main series box up or down
  // depending on where the reader had scrolled to when they clicked. Pinning
  // the box to the top of the viewport on every pick — the same
  // scroll-to-top-of-viewport move the More section's group headings use
  // above — keeps the reader oriented on the box they just acted on, and
  // makes a second switch a no-op scroll once the box is already there.
  const mainLineBoxRef = useRef<HTMLElement>(null)
  const [pendingScrollToMainLine, setPendingScrollToMainLine] = useState(false)

  useLayoutEffect(() => {
    if (!pendingScrollToMainLine) return
    const box = mainLineBoxRef.current
    if (box) {
      const target = box.getBoundingClientRect().top + window.scrollY
      window.scrollTo(0, target)
    }
    setPendingScrollToMainLine(false)
  }, [pendingScrollToMainLine])

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
  // change into the whole-library ranking (design.md decision 11): the
  // demoted entry moves to sit immediately after the promoted one in their
  // shared score tier there too, so the favourite list — sorted by that same
  // ranking — and the ranking editor never disagree. A failed save reverts
  // to the order that was actually stored, and says so.
  async function handleReorderFavourite(currentOrder: number[], index: number, direction: -1 | 1, movedTitle: string) {
    const targetIndex = index + direction
    if (targetIndex < 0 || targetIndex >= currentOrder.length) return

    const reordered = [...currentOrder]
    ;[reordered[index], reordered[targetIndex]] = [reordered[targetIndex], reordered[index]]

    const promotedAnimeId = direction === -1 ? currentOrder[index] : currentOrder[targetIndex]
    const demotedAnimeId = direction === -1 ? currentOrder[targetIndex] : currentOrder[index]

    patchSeries((series) => ({ ...series, stats: { ...series.stats, myHighestScoreAnimeIds: reordered } }))
    try {
      await moveFavouriteAdjacent(promotedAnimeId, demotedAnimeId)
    } catch (err) {
      patchSeries((series) => ({ ...series, stats: { ...series.stats, myHighestScoreAnimeIds: currentOrder } }))
      reportFailure({
        title: `Couldn't move ${movedTitle} in your favourites`,
        reason: err instanceof ApiError ? err.reason : null,
      })
    }
  }

  // Both apply optimistically (spec "A chosen picture applies immediately")
  // and affect only the series (spec "Choosing does not touch the members"):
  // neither writes to any member anime's own pictureUrl/title.
  function handlePickSeriesPicture(url: string) {
    if (!data?.found) return
    const seriesId = data.series.seriesId
    const seriesTitle = pickDisplayTitle(data.series.title, data.series.englishTitle)
    patchSeries((series) => ({ ...series, pictureUrl: url, selectedPictureUrl: url }))
    setSeriesPicture(seriesId, url).catch((err) => {
      // The optimistic change stays on screen until a reload, and the
      // notice is what tells you it wasn't saved (design D2).
      reportFailure({
        title: `Couldn't save the picture for ${seriesTitle}`,
        reason: err instanceof ApiError ? err.reason : null,
      })
    })
  }

  // Not optimistic like the pick above — the root member's MAL picture that
  // clearing reverts to isn't known client-side (design D7), so both fields
  // patch from the response instead.
  function handleClearSeriesPicture() {
    if (!data?.found) return
    const seriesId = data.series.seriesId
    const seriesTitle = pickDisplayTitle(data.series.title, data.series.englishTitle)
    resetSeriesPicture(seriesId)
      .then(({ pictureUrl, selectedPictureUrl }) => {
        patchSeries((series) => ({ ...series, pictureUrl, selectedPictureUrl }))
      })
      .catch((err) => {
        // The page keeps its current picture, and the notice says why.
        reportFailure({
          title: `Couldn't reset the picture for ${seriesTitle}`,
          reason: err instanceof ApiError ? err.reason : null,
        })
      })
  }

  function handlePickSeriesTitle(title: string) {
    if (!data?.found) return
    const seriesId = data.series.seriesId
    const seriesTitle = pickDisplayTitle(data.series.title, data.series.englishTitle)
    patchSeries((series) => ({ ...series, title, englishTitle: null, selectedTitle: title }))
    setSeriesTitle(seriesId, title).catch((err) => {
      // The optimistic change stays on screen until a reload, and the
      // notice is what tells you it wasn't saved (design D2).
      reportFailure({
        title: `Couldn't rename ${seriesTitle} to ${title}`,
        reason: err instanceof ApiError ? err.reason : null,
      })
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
    setPendingScrollToMainLine(true)
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

  // Reveals a group's remaining tiles from its "+N more" control, which is
  // only ever offered on an expanded group hiding some of its own behind the
  // filter (design.md D3) — so this is always the unfilter case; a collapsed
  // group's heading is what opens it now (D1).
  function revealGroupTiles(key: string) {
    setUnfilteredGroups((prev) => new Set(prev).add(key))
  }

  if (Number.isNaN(animeId)) {
    return <p className="series-page__empty">Series not found.</p>
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
  // deriveSeriesStats replaces the pick-scoped snapshot's edit-sensitive
  // fields with client computations over the page's own entry arrays
  // (design.md decision 5), so an in-place edit and a route switch can never
  // resurrect a stale server-delivered figure.
  const stats = deriveSeriesStats(statsForPick(series, resolvedPick), mainLineVisible, realExtras(series.extras))
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

  // Read from series.stats, not the pick-resolved stats above: BuildStats
  // computes all three tie lists over the whole, unfiltered main line, so
  // every statsByPick entry already carries identical values (design.md
  // decision 4) — reading series.stats directly is what lets a favourite
  // reorder and a rewatch-count edit, which only ever write series.stats,
  // take effect on a non-default route too.
  const highestMalEntries = series.stats.highestMalScoreAnimeIds
    .map((id) => findEntry(series, id))
    .filter((e): e is SeriesEntryDto => e !== undefined)
  // Whole-box gate for the Highest MAL score stat (design D5): the identical
  // settledness call the main-series MAL average chip makes below, over the
  // same whole unfiltered main line — recomputed every render, never stored,
  // so a newly currently_airing entry re-hides an auto-shown box on the next
  // render with no separate code path. `!hidden` (composed at the gate
  // itself) makes the whole thing conditional on the score toggle, replacing
  // a placeholder that was unconditional on it — deliberate, and a loosening.
  const highestMalSettled = malGroupRevealed(series.mainLine, series)
  const myHighestEntries = series.stats.myHighestScoreAnimeIds
    .map((id) => findEntry(series, id))
    .filter((e): e is SeriesEntryDto => e !== undefined)
  const mostRewatchedEntries = series.stats.mostRewatchedAnimeIds
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
  // series-page "a media type that no extra of mine carries SHALL NOT be
  // selectable" (fix-score-reveal-and-nplus1 design.md D5): the types my own
  // extras actually carry, regardless of the type filter. Used to disable a
  // type button while the "in my list" filter is on and to narrow the
  // selection when the filter is turned on.
  const myMediaTypes = new Set(
    series.extras.filter((e) => e.entry != null).map((e) => e.mediaType ?? 'unknown'),
  )

  const extrasGroups = groupExtras(series.extras)
  // Membership, not status, per group's visible tiles (design.md decision 3);
  // narrowed further by the type filter, which composes with it rather than
  // replacing it (task 10.4). Dropped and Plan-to-watch entries count exactly
  // like Completed ones.
  const extrasGroupView = extrasGroups
    .map((group, index) => {
      const key = extrasGroupKey(group, index)
      const isCollapsed = collapsedGroups[key] ?? true
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

  // series-page "The control SHALL report which of those two states the
  // filter is in" (fix-score-reveal-and-nplus1 design.md D3): reads as on
  // exactly while the filter is actually in force everywhere, i.e. on with
  // no per-group exemption. Group expansion plays no part — opening or
  // collapsing a group, from a heading, the expand/collapse-all control, or
  // a media-type button, never changes what this reads while the filter
  // itself is unchanged.
  const filterActive = mineOnly && unfilteredGroups.size === 0

  // Multi-select toggle for the media-type filter row (design.md decision 3):
  // selecting narrows every group to that type, alongside whatever else is
  // already selected; selecting none narrows nothing. Adding a type also
  // opens every group holding at least one entry of it, so the entries it
  // admits are actually rendered rather than only counted in a heading;
  // removing a type changes no group's collapsed state. Neither direction
  // touches `unfilteredGroups` — only a group heading grants that exemption.
  // Needs `extrasGroups` to know which keys to open, hence its placement
  // below the early returns, beside `toggleAllExtrasGroups`.
  function toggleMediaType(mediaType: string) {
    const adding = !selectedMediaTypes.has(mediaType)
    setSelectedMediaTypes((prev) => {
      const next = new Set(prev)
      if (next.has(mediaType)) next.delete(mediaType)
      else next.add(mediaType)
      return next
    })
    if (adding) {
      const keysToOpen = extrasGroups
        .map((group, index) => ({ group, key: extrasGroupKey(group, index) }))
        .filter(({ group }) => group.items.some((e) => (e.mediaType ?? 'unknown') === mediaType))
        .map(({ key }) => key)
      setCollapsedGroups((prev) => {
        const next = { ...prev }
        keysToOpen.forEach((key) => {
          next[key] = false
        })
        return next
      })
    }
  }

  // series-page "Activating the control while it reads as on SHALL turn the
  // filter off" / "Activating it while off SHALL show my entries" (fix-
  // score-reveal-and-nplus1 design.md D4, D5): a plain two-way toggle over
  // one fact — whether the filter is in force.
  //
  // While on: turn the filter off and touch no collapsed state, so every
  // expanded group widens to all its extras and nothing the user opened is
  // put away. "Collapse all" remains the way to put the section away.
  //
  // While off: turn the filter on, drop every per-group exemption, and open
  // every group holding at least one extra of mine that the *narrowed* type
  // filter admits. The selection is narrowed to `myMediaTypes` first — a type
  // nothing of mine carries is dropped rather than left selected over an
  // empty group — and `keysToOpen` is computed from that same narrowed set
  // so the selection and the opened groups agree within this render, per
  // D5's invariant. An empty narrowed selection means no type restriction, so
  // the section shows my extras across every group.
  function toggleMineOnly() {
    if (filterActive) {
      setMineOnly(false)
      return
    }
    setMineOnly(true)
    setUnfilteredGroups(new Set())
    const narrowedTypes = new Set([...activeMediaTypes].filter((type) => myMediaTypes.has(type)))
    setSelectedMediaTypes(narrowedTypes)
    const narrowedTypeAdmits = (entry: SeriesEntryDto) =>
      narrowedTypes.size === 0 || narrowedTypes.has(entry.mediaType ?? 'unknown')
    const next: Record<string, boolean> = {}
    extrasGroups.forEach((group, index) => {
      const holdsMine = group.items.some((e) => e.entry != null && narrowedTypeAdmits(e))
      next[extrasGroupKey(group, index)] = !holdsMine
    })
    setCollapsedGroups(next)
  }

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
          onClear={series.selectedPictureUrl != null ? handleClearSeriesPicture : undefined}
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
              // series.seriesId is the root entry's MAL id (key-series-by-root-
              // anime-id design.md D1/D7), so it's also the anime link target.
              href={`https://myanimelist.net/anime/${series.seriesId}`}
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
          {(showTimeWatched || showTimeLeft) && (
            <div>
              <dt>Time</dt>
              <dd className="series-page__time-stat">
                {showTimeWatched && (
                  <span className="series-page__time-stat-row">
                    <span className="series-page__time-stat-label">Watched:</span>
                    {formatRuntime(timeWatchedSeconds)}
                  </span>
                )}
                {showTimeLeft && (
                  <span className="series-page__time-stat-row">
                    <span className="series-page__time-stat-label">Left:</span>
                    {formatRuntime(timeLeftSeconds)}
                  </span>
                )}
              </dd>
            </div>
          )}
          {highestMalEntries.length > 0 && (
            <div>
              <dt>Highest MAL score</dt>
              <dd>
                {/* Whole-box gate (design D5), not a per-entry one: naming the
                    tied-highest entry of an unfinished franchise is a
                    comparative claim about entries not yet reached, which can
                    bias anticipation or change once an airing season finishes
                    — true even of an entry already completed, so nothing
                    inside this box is shown piecemeal while it's ungated.
                    `!hidden` is what makes the whole gate conditional on the
                    score toggle in the first place (score-visibility). */}
                {!hidden || highestMalSettled || highestMalRevealed ? (
                  <ul className="series-page__tie-list">
                    {highestMalEntries.map((entry) => (
                      <li key={entry.animeId}>
                        <Link to={`/anime/${entry.animeId}`}>{pickDisplayTitle(entry.title, entry.englishTitle)}</Link>{' '}
                        ·{' '}
                        <span className="score--mal">
                          <ScoreValue value={entry.malScore} completed={isScoreRevealableStatus(entry.entry?.status)} />
                        </span>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <span className="series-page__tie-list-reveal">
                    <RevealControl onReveal={revealHighestMal} label="Reveal the series' highest MAL score" />
                  </span>
                )}
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
                              handleReorderFavourite(
                                series.stats.myHighestScoreAnimeIds,
                                index,
                                -1,
                                pickDisplayTitle(entry.title, entry.englishTitle),
                              )
                            }
                            disabled={index === 0}
                            aria-label={`Move ${pickDisplayTitle(entry.title, entry.englishTitle)} up`}
                          >
                            ▲
                          </button>
                          <button
                            type="button"
                            onClick={() =>
                              handleReorderFavourite(
                                series.stats.myHighestScoreAnimeIds,
                                index,
                                1,
                                pickDisplayTitle(entry.title, entry.englishTitle),
                              )
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
        <section
          ref={mainLineBoxRef}
          className={`series-box series-page__main-line-box${series.slots.length > 0 ? ' series-page__main-line-box--sloted' : ''}`}
        >
          <h2>Main series</h2>
          <SeriesTimeline
            entries={mainLineVisible}
            allEntries={series.mainLine}
            slots={series.slots}
            resolvedPick={resolvedPick}
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
                const label = mediaTypeLabel(type === 'unknown' ? null : type)
                // series-page "a media type that no extra of mine carries
                // SHALL NOT be selectable" while the filter is on (design.md
                // D5): disabled, with the reason in its accessible name
                // rather than only in its greyed-out styling.
                const unavailable = mineOnly && !myMediaTypes.has(type)
                return (
                  <button
                    key={type}
                    type="button"
                    aria-pressed={active}
                    disabled={unavailable}
                    aria-label={unavailable ? `${label} (none of mine is this type)` : undefined}
                    className={`series-page__type-filter-button${active ? ' series-page__type-filter-button--active' : ''}`}
                    onClick={() => toggleMediaType(type)}
                  >
                    {label}
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
