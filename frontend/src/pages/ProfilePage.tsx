import { useCallback, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  getProfile,
  getRewatchedSection,
  getRewatchedSeriesSection,
  getTopAnimeSection,
  getTopSeriesSection,
} from '../api/client.ts'
import type {
  OpinionDivergenceItemDto,
  ProfileDto,
  RewatchedSectionDto,
  RewatchedSeriesSectionDto,
  TopAnimeMediaType,
  TopAnimeSectionDto,
  TopSeriesItemDto,
  TopSeriesSectionDto,
} from '../api/types.ts'
import { ScoreChip } from '../components/ScoreChip.tsx'
import { ScoreValue } from '../components/ScoreValue.tsx'
import { AnimeRankOverlay } from '../components/AnimeRankOverlay.tsx'
import { EditHistoryOverlay } from '../components/EditHistoryOverlay.tsx'
import { MalOriginTag } from '../components/MalOriginTag.tsx'
import { ProgressBar } from '../components/ProgressBar.tsx'
import { RankingOverlay, type RankingOverlayRow } from '../components/RankingOverlay.tsx'
import { RowPicture } from '../components/RowPicture.tsx'
import {
  describeSeasonRanking,
  describeYearRanking,
  offeredScores,
  rankByScoreCount,
  RankingSection,
} from '../components/RankingSection.tsx'
import { ScoreDistribution } from '../components/ScoreDistribution.tsx'
import { TruncatedTitle } from '../components/TruncatedTitle.tsx'
import { UnresolvedEpisodesOverlay } from '../components/UnresolvedEpisodesOverlay.tsx'
import { useAnimeRank } from '../context/AnimeRankContext.tsx'
import { usePageData } from '../hooks/usePageData.ts'
import { useRestorableScroll } from '../hooks/useRestorableScroll.ts'
import { useRestorableState } from '../hooks/useRestorableState.ts'
import { formatRewatchTime, formatTimestamp, pickDisplayTitle } from '../utils/anime.ts'
import './ProfilePage.css'

// The strip's defining constant (design.md decision 4): must agree with the
// tile `flex` basis in ProfilePage.css (`calc((100% - 9 * 10px) / 10)`),
// which lays out exactly this many tiles across the strip's visible width.
const STRIP_VISIBLE_TILES = 10

type TopSeriesBasis = 'mine' | 'mal'

const TOP_SERIES_BASIS_TABS: { value: TopSeriesBasis; label: string; family: 'mine' | 'mal' }[] = [
  { value: 'mine', label: 'My score', family: 'mine' },
  { value: 'mal', label: 'MAL score', family: 'mal' },
]

function topSeriesBasisValue(item: TopSeriesItemDto, basis: TopSeriesBasis): number | null {
  return basis === 'mine' ? item.mineMain.value : item.malMain.value
}

// mainLineAiredCount is the main line minus any announced-but-not-yet-aired
// entries — a second season with zero episodes out shouldn't make a series
// read as multi-entry. malMain/mineMain.totalCount would over-count (they
// include not-yet-aired members) and entryCount is main line plus extras.
function mainLineEntryCount(item: TopSeriesItemDto): number {
  return item.mainLineAiredCount
}

function topSeriesBasisScoredCount(item: TopSeriesItemDto, basis: TopSeriesBasis): number {
  return basis === 'mine' ? item.mineMain.scoredCount : item.malMain.scoredCount
}

// Mirrors seriesNullsLast in utils/anime.ts step for step (design.md
// decision 5) — TopSeriesItemDto and SeriesListItemDto are different
// records, so this can't literally reuse that helper, but the two are bound
// by the spec and must move together.
function topSeriesNullsLast(
  get: (item: TopSeriesItemDto) => number | null,
  direction: 'ascending' | 'descending',
): (a: TopSeriesItemDto, b: TopSeriesItemDto) => number {
  return (a, b) => {
    const va = get(a)
    const vb = get(b)
    if (va === null && vb === null) return 0
    if (va === null) return 1
    if (vb === null) return -1
    return direction === 'ascending' ? va - vb : vb - va
  }
}

// The my-score basis's tie-break chain (design.md decision 5): the same
// chain as compareMyScoreChain in utils/anime.ts, bound to it by the spec —
// not by a shared symbol — and must move with SERIES_SORT_COMPARATORS.myScore
// there. A lower rank number is better, so the rank step is ascending, and a
// series with no rank sorts after every tied series that has one (no
// sentinel value stands in for "no rank"). The final tie-break uses the
// display title, matching the Series page's chain, though neither title is
// visible on a tile — this settles the rule, not the appearance. The
// no-my-average branch below is unreachable in practice: rankTopSeries
// filters those series out before sorting.
const compareByMineAverage = topSeriesNullsLast((item) => item.mineMain.value, 'descending')
const compareByAverageRank = topSeriesNullsLast((item) => item.mainLineAverageRank, 'ascending')
const compareByAiredEpisodes = topSeriesNullsLast((item) => item.mainLineAiredEpisodes, 'descending')
const compareMyScoreChain = (a: TopSeriesItemDto, b: TopSeriesItemDto): number =>
  compareByMineAverage(a, b) ||
  compareByAverageRank(a, b) ||
  compareByAiredEpisodes(a, b) ||
  pickDisplayTitle(a.title, a.englishTitle).localeCompare(pickDisplayTitle(b.title, b.englishTitle), undefined, {
    sensitivity: 'base',
  })

// Basis toggle re-sorts and re-filters the already-loaded array rather than
// refetching (design.md decision 4). A series with no value under the
// selected basis is omitted rather than parked at the end (design.md
// decision 3). The two bases break ties differently: mine uses the
// Series page's My average chain (average ranking position, then main-line
// episodes aired, then display title); mal is unchanged — average
// descending, then scored main-line count descending (an average earned
// across more entries places higher), then raw title case-insensitively.
function rankTopSeries(items: TopSeriesItemDto[], basis: TopSeriesBasis): TopSeriesItemDto[] {
  const eligible = items.filter((item) => topSeriesBasisValue(item, basis) !== null)
  if (basis === 'mine') {
    return eligible.sort(compareMyScoreChain)
  }
  return eligible.sort((a, b) => {
    const valueDiff = topSeriesBasisValue(b, basis)! - topSeriesBasisValue(a, basis)!
    if (valueDiff !== 0) return valueDiff
    const countDiff = topSeriesBasisScoredCount(b, basis) - topSeriesBasisScoredCount(a, basis)
    if (countDiff !== 0) return countDiff
    return a.title.localeCompare(b.title, undefined, { sensitivity: 'base' })
  })
}

// A separate step from rankTopSeries (design.md decision 3) — filtering
// never reorders the survivors, so "which series belong in this strip at
// all" and "how they're ordered" stay independently readable.
function filterMultiEntry(items: TopSeriesItemDto[]): TopSeriesItemDto[] {
  return items.filter((item) => mainLineEntryCount(item) > 1)
}

// The tooltip carries only the "N of M scored" counts, never the averages
// themselves — the values can be behind the hide-scores toggle, and a raw
// title attribute isn't subject to ScoreValue's reveal gating.
function topSeriesCountsTitle(item: TopSeriesItemDto): string {
  return (
    `MAL: ${item.malMain.scoredCount} of ${item.malMain.totalCount} scored · ` +
    `Mine: ${item.mineMain.scoredCount} of ${item.mineMain.totalCount} scored`
  )
}

const MEDIA_TYPE_TABS: { value: TopAnimeMediaType; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'tv', label: 'TV' },
  { value: 'movie', label: 'Movie' },
  { value: 'ova', label: 'OVA' },
  { value: 'ona', label: 'ONA' },
  { value: 'special', label: 'Specials' },
]

// "Most rewatched"'s own scope, widened beyond media type with a Series
// option (design.md D10) — a separate list from MEDIA_TYPE_TABS so "My top
// anime" keeps its unmodified six options. Series sits right after All,
// ahead of the media types, since it's a different axis to slice by rather
// than one more type among them.
type RewatchedScope = TopAnimeMediaType | 'series'

const REWATCHED_SCOPE_TABS: { value: RewatchedScope; label: string }[] = [
  MEDIA_TYPE_TABS[0], // All
  { value: 'series', label: 'Series' },
  ...MEDIA_TYPE_TABS.slice(1),
]

const REWATCHED_EMPTY_MESSAGES: Record<RewatchedScope, string> = {
  all: 'No shows have been rewatched',
  series: 'No series have been rewatched',
  tv: 'No TV shows have been rewatched',
  movie: 'No movies have been rewatched',
  ova: 'No OVAs have been rewatched',
  ona: 'No ONAs have been rewatched',
  special: 'No specials have been rewatched',
}

const STAT_LABELS: { key: keyof ProfileDto['stats']; label: string }[] = [
  { key: 'completed', label: 'Completed' },
  { key: 'meanScore', label: 'Mean score' },
  { key: 'totalEntries', label: 'Total entries' },
  { key: 'watching', label: 'Watching' },
  { key: 'rewatching', label: 'Rewatching' },
  { key: 'planToWatch', label: 'Plan to watch' },
  { key: 'onHold', label: 'On-hold' },
  { key: 'dropped', label: 'Dropped' },
  { key: 'days', label: 'Days' },
  { key: 'rewatched', label: 'Rewatched' },
  { key: 'episodes', label: 'Episodes' },
  { key: 'movies', label: 'Movies' },
]

function formatStatValue(key: keyof ProfileDto['stats'], value: number | null): string {
  if (value === null) return '—'
  if (key === 'days') return value.toFixed(1)
  if (key === 'meanScore') return value.toFixed(2)
  return String(value)
}

// Drag-to-scroll for a horizontal poster strip: a mouse-down on the strip
// starts tracking, mouse-move scrolls it and flags a drag once the pointer
// has moved past a small threshold, and onItemClick suppresses the
// resulting navigation click so a drag doesn't also open the tile. Scroll
// offset recording and restoration is `useRestorableScroll`'s; this composes
// that hook's ref callback with its own. `restoreKey` identifies this
// strip's section (and, for sections with a view control, the control's
// current value) so its offset is recorded and restored independently of
// the page's other strips (design.md decision 3).
function useStripScroll(restoreKey: string) {
  const elRef = useRef<HTMLDivElement | null>(null)
  const drag = useRef({ isDown: false, startX: 0, scrollLeft: 0, dragged: false })
  const scrollRef = useRestorableScroll(restoreKey, 'horizontal')

  function onMouseDown(event: React.MouseEvent<HTMLDivElement>) {
    const el = elRef.current
    if (!el) return
    drag.current = { isDown: true, startX: event.pageX, scrollLeft: el.scrollLeft, dragged: false }
  }

  function onMouseMove(event: React.MouseEvent<HTMLDivElement>) {
    const state = drag.current
    const el = elRef.current
    if (!state.isDown || !el) return
    event.preventDefault()
    const delta = event.pageX - state.startX
    if (Math.abs(delta) > 3) state.dragged = true
    el.scrollLeft = state.scrollLeft - delta
  }

  function onMouseUp() {
    drag.current.isDown = false
  }

  function onItemClick(event: React.MouseEvent) {
    if (drag.current.dragged) {
      event.preventDefault()
    }
  }

  // Wiring lives in a ref callback, not an effect: the strip's div mounts
  // and unmounts as its section switches between loading and loaded (the
  // conditional rendering above), so "the DOM node exists" isn't something
  // an effect dependency list can express — the callback fires exactly when
  // React attaches or detaches the node, whatever render that happens on.
  // Both this strip's own ref and useRestorableScroll's run on the same
  // attach/detach.
  const setRef = useCallback(
    (el: HTMLDivElement | null) => {
      elRef.current = el
      scrollRef(el)
    },
    [scrollRef],
  )

  return {
    ref: setRef,
    handlers: { onMouseDown, onMouseMove, onMouseUp, onMouseLeave: onMouseUp },
    onItemClick,
  }
}

function DivergenceList({ items }: { items: OpinionDivergenceItemDto[] }) {
  if (items.length === 0) {
    return <p className="profile-page__section-empty">Nothing here yet.</p>
  }
  return (
    <ul className="divergence-list scroll-hidden">
      {items.map((item) => (
        <li key={item.animeId} className="profile-list-row">
          <Link to={`/anime/${item.animeId}`} className="profile-list-row__link">
            <RowPicture src={item.pictureUrl} className="profile-list-row__picture" />
            <span className="profile-list-row__title" title={pickDisplayTitle(item.title, item.englishTitle)}>
              {pickDisplayTitle(item.title, item.englishTitle)}
            </span>
          </Link>
          <span className="profile-list-row__trailing">
            Me <span className="score--mine">{item.myScore}</span> · MAL{' '}
            <span className="score--mal">
              <ScoreValue value={item.malScore} completed={item.isCompleted} />
            </span>
          </span>
        </li>
      ))}
    </ul>
  )
}

// Profile page: five read-only boxes (stats, rating distribution, latest
// updates, my top anime, opinion divergence) plus two overlays (full edit
// history, top-anime tie-break selection) — everything computed server-side
// from cached Postgres data.
export function ProfilePage() {
  const { data: profile, loading } = usePageData<ProfileDto>('profile', getProfile)
  const { openRanking } = useAnimeRank()

  // Each media-type tab is its own resource key, not just a view control on
  // top of one shared fetch — so restoring a page left on "TV" shows TV data
  // instead of a background refresh of "All" silently swapping the grid out
  // from under a tab that still reads as selected.
  const [mediaType, setMediaType] = useRestorableState<TopAnimeMediaType>('mediaType', 'all')
  const {
    data: topAnime,
    loading: topAnimeLoading,
    reload: reloadTopAnime,
  } = usePageData<TopAnimeSectionDto>(`top-anime:${mediaType}`, () => getTopAnimeSection(mediaType))

  // Untouched by the Series scope (design.md D10): selecting Series only
  // changes rewatchedScope below, leaving this — and the resource key it
  // drives — exactly as it was, so switching into and back out of Series
  // never re-fetches a media-type scope that isn't even shown.
  const [rewatchedMediaType, setRewatchedMediaType] = useRestorableState<TopAnimeMediaType>(
    'rewatchedMediaType',
    'all',
  )
  const { data: rewatched, loading: rewatchedLoading } = usePageData<RewatchedSectionDto>(
    `rewatched:${rewatchedMediaType}`,
    () => getRewatchedSection(rewatchedMediaType),
  )

  // The view control for "Most rewatched" (design.md D10): a superset of
  // rewatchedMediaType that also selects Series. Selecting a media-type tab
  // updates both this and rewatchedMediaType together; selecting Series
  // updates only this one.
  const [rewatchedScope, setRewatchedScope] = useRestorableState<RewatchedScope>('rewatchedScope', 'all')
  function selectRewatchedScope(value: RewatchedScope) {
    setRewatchedScope(value)
    if (value !== 'series') setRewatchedMediaType(value)
  }

  // Its own usePageData key (design.md D10), toggled between the real key
  // and an inert one so the fetch — and the SeriesMembers join behind it —
  // only actually runs while Series is selected, not on every profile visit.
  const { data: rewatchedSeries, loading: rewatchedSeriesLoading } = usePageData<RewatchedSeriesSectionDto>(
    rewatchedScope === 'series' ? 'rewatched-series' : 'rewatched-series:idle',
    () => (rewatchedScope === 'series' ? getRewatchedSeriesSection() : Promise.resolve({ items: [] })),
  )

  // Switching media-type tabs picks a new resource key, and usePageData
  // clears `data` to null until that key's fetch resolves — fine for a page
  // navigation, but between tabs on the same page it reads as the strip
  // collapsing and popping back open. Keeping the last-loaded section on
  // screen until the new one arrives keeps the strip's height (and the tabs
  // around it) stable across the switch instead of visibly jumping.
  const topAnimeDisplayRef = useRef<TopAnimeSectionDto | null>(null)
  if (topAnime) topAnimeDisplayRef.current = topAnime
  const displayedTopAnime = topAnime ?? topAnimeDisplayRef.current

  const rewatchedDisplayRef = useRef<RewatchedSectionDto | null>(null)
  if (rewatched) rewatchedDisplayRef.current = rewatched
  const displayedRewatched = rewatched ?? rewatchedDisplayRef.current

  // Mirrors rewatchedDisplayRef above, so switching into and out of Series
  // doesn't collapse the strip either (design.md D10).
  const rewatchedSeriesDisplayRef = useRef<RewatchedSeriesSectionDto | null>(null)
  if (rewatchedSeries) rewatchedSeriesDisplayRef.current = rewatchedSeries
  const displayedRewatchedSeries = rewatchedSeries ?? rewatchedSeriesDisplayRef.current

  // Loaded once — the basis toggle re-sorts/re-filters this same array
  // client-side rather than refetching (design.md decision 4), so there's no
  // need for the "hold the last section on screen" workaround the other two
  // strips use to avoid collapsing on a filter change.
  const { data: topSeries, loading: topSeriesLoading } = usePageData<TopSeriesSectionDto>(
    'top-series',
    getTopSeriesSection,
  )
  const [topSeriesBasis, setTopSeriesBasis] = useRestorableState<TopSeriesBasis>('topSeriesBasis', 'mine')
  const [topSeriesMultiOnly, setTopSeriesMultiOnly] = useRestorableState<boolean>('topSeriesMultiOnly', false)
  const rankedTopSeries = topSeries ? rankTopSeries(topSeries.items, topSeriesBasis) : []
  const displayedTopSeries = topSeriesMultiOnly ? filterMultiEntry(rankedTopSeries) : rankedTopSeries

  // The favourites score filter (design.md decision D7): each ranking holds
  // its own selection, restored on back-navigation and defaulting to All on
  // a fresh visit. A restored selection naming a score the reloaded ranking
  // no longer offers falls back to All rather than trusting the stored
  // value and showing an empty ranking.
  const [favouriteYearsScoreRaw, setFavouriteYearsScore] = useRestorableState<number | null>(
    'favouriteYearsScore',
    null,
  )
  const [favouriteSeasonsScoreRaw, setFavouriteSeasonsScore] = useRestorableState<number | null>(
    'favouriteSeasonsScore',
    null,
  )
  const offeredFavouriteYearScores = offeredScores(profile?.favouriteYears ?? [])
  const favouriteYearsScore =
    favouriteYearsScoreRaw !== null && offeredFavouriteYearScores.includes(favouriteYearsScoreRaw)
      ? favouriteYearsScoreRaw
      : null
  const offeredFavouriteSeasonScores = offeredScores(profile?.favouriteSeasons ?? [])
  const favouriteSeasonsScore =
    favouriteSeasonsScoreRaw !== null && offeredFavouriteSeasonScores.includes(favouriteSeasonsScoreRaw)
      ? favouriteSeasonsScoreRaw
      : null

  const [showHistory, setShowHistory] = useState(false)
  const [showTopAnimeSelect, setShowTopAnimeSelect] = useState(false)
  const [showUnresolvedEpisodes, setShowUnresolvedEpisodes] = useState(false)
  // Only one ranking overlay can be open at a time, so a single slot serves
  // both Favourite seasons and Favourite years (same pattern as RecapPage's
  // ranking pairs).
  const [rankingOverlay, setRankingOverlay] = useState<{
    title: string
    rows: RankingOverlayRow[]
    family?: 'year' | 'season'
  } | null>(null)
  const topAnimeStripScroll = useStripScroll(`top-anime:${mediaType}`)
  // Keyed on rewatchedScope, not rewatchedMediaType, so Series keeps its own
  // scroll offset independent of whichever media type was last selected
  // (design.md D10/task 10.6).
  const rewatchedStripScroll = useStripScroll(`rewatched:${rewatchedScope}`)
  const topSeriesStripScroll = useStripScroll('top-series')

  if (loading) {
    return <p className="profile-page__loading">Loading…</p>
  }

  if (!profile) {
    return <p className="profile-page__empty">Couldn't load profile data.</p>
  }

  return (
    <div className="profile-page">
      <div className="profile-page__header">
        <h1>Profile</h1>
        <p className="profile-page__subtitle">Your stats, ratings, and favorites, all in one place.</p>
      </div>

      <section className="profile-box">
        <h2>Episode progress</h2>
        {profile.episodeProgress.episodesTotal === 0 ? (
          <p className="profile-page__section-empty">
            No anime in your list has a published episode count yet.
          </p>
        ) : (
          <>
            <div className="profile-page__episode-progress">
              <ProgressBar
                watched={profile.episodeProgress.episodesWatched}
                total={profile.episodeProgress.episodesTotal}
              />
            </div>
            {profile.episodeProgress.unresolvedEntries > 0 && (
              <p className="profile-page__episode-progress-note">
                {`${profile.episodeProgress.unresolvedEntries} ${
                  profile.episodeProgress.unresolvedEntries === 1 ? 'entry' : 'entries'
                } whose episode count isn't known yet`}{' '}
                <button
                  type="button"
                  className="profile-page__unresolved-button"
                  onClick={() => setShowUnresolvedEpisodes(true)}
                >
                  View
                </button>
              </p>
            )}
          </>
        )}
      </section>

      <div className="profile-page__top-row">
        <section className="profile-box">
          <h2>Anime stats</h2>
          <dl className="profile-stats">
            {STAT_LABELS.map(({ key, label }) => (
              <div key={key} className="profile-stats__row">
                <dt>{label}</dt>
                <dd>{formatStatValue(key, profile.stats[key])}</dd>
              </div>
            ))}
          </dl>
        </section>

        <section className="profile-box">
          <h2>Rating distribution</h2>
          <ScoreDistribution
            buckets={profile.scoreDistribution.buckets}
            meanScore={profile.scoreDistribution.meanScore}
          />
        </section>

        <section className="profile-box">
          <div className="profile-box__header-row">
            <h2>Latest updates</h2>
            <button type="button" className="profile-box__control" onClick={() => setShowHistory(true)}>
              Full history
            </button>
          </div>
          {profile.recentActivity.length === 0 ? (
            <p className="profile-page__section-empty">No activity yet.</p>
          ) : (
            <ul className="activity-feed scroll-hidden">
              {profile.recentActivity.map((item) => (
                <li key={item.id} className="profile-list-row">
                  <Link to={`/anime/${item.animeId}`} className="profile-list-row__link">
                    <RowPicture src={item.pictureUrl} className="profile-list-row__picture" />
                    <span className="profile-list-row__info">
                      <TruncatedTitle
                        title={pickDisplayTitle(item.animeTitle, item.animeEnglishTitle)}
                        lines={1}
                        className="profile-list-row__title"
                      />
                      <span className="profile-list-row__meta">
                        <span className="profile-list-row__meta-text">{item.summary}</span>
                        <MalOriginTag source={item.source} />
                      </span>
                    </span>
                  </Link>
                  <span className="profile-list-row__trailing">{formatTimestamp(item.timestamp)}</span>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      <section className="profile-box">
        <div className="profile-box__header-row">
          <h2>My top anime</h2>
          {displayedTopAnime && displayedTopAnime.tiers.some((tier) => tier.members.length > 1) && (
            <button type="button" className="profile-box__control" onClick={() => setShowTopAnimeSelect(true)}>
              Rank
            </button>
          )}
        </div>

        <div className="profile-media-tabs" role="tablist" aria-label="Filter by media type">
          {MEDIA_TYPE_TABS.map((tab) => (
            <button
              key={tab.value}
              type="button"
              role="tab"
              aria-selected={mediaType === tab.value}
              className={
                mediaType === tab.value ? 'profile-media-tabs__tab profile-media-tabs__tab--active' : 'profile-media-tabs__tab'
              }
              onClick={() => setMediaType(tab.value)}
            >
              {tab.label}
            </button>
          ))}
        </div>

        {!displayedTopAnime && topAnimeLoading ? null : !displayedTopAnime || displayedTopAnime.items.length === 0 ? (
          <p className="profile-page__section-empty">
            {mediaType === 'all'
              ? 'Score some anime to build your top list.'
              : `No scored ${MEDIA_TYPE_TABS.find((tab) => tab.value === mediaType)?.label} yet.`}
          </p>
        ) : (
          <div
            className={
              displayedTopAnime.items.length <= STRIP_VISIBLE_TILES ? 'top-anime-strip top-anime-strip--fits' : 'top-anime-strip'
            }
            ref={topAnimeStripScroll.ref}
            {...topAnimeStripScroll.handlers}
          >
            {displayedTopAnime.items.map((item) => (
              <Link
                key={item.animeId}
                to={`/anime/${item.animeId}`}
                className="top-anime-strip__item"
                draggable={false}
                onClick={topAnimeStripScroll.onItemClick}
              >
                {item.pictureUrl ? (
                  <img
                    src={item.pictureUrl}
                    alt={pickDisplayTitle(item.title, item.englishTitle)}
                    className="top-anime-strip__picture"
                    draggable={false}
                  />
                ) : (
                  <div
                    className="top-anime-strip__picture top-anime-strip__picture--placeholder"
                    aria-hidden="true"
                  />
                )}
                <span className="top-anime-strip__score">{item.myScore}</span>
              </Link>
            ))}
          </div>
        )}
      </section>

      <section className="profile-box">
        <div className="profile-box__header-row">
          <h2>Top series</h2>
          <button
            type="button"
            className={
              topSeriesMultiOnly ? 'profile-box__control profile-box__control--active' : 'profile-box__control'
            }
            aria-pressed={topSeriesMultiOnly}
            onClick={() => setTopSeriesMultiOnly(!topSeriesMultiOnly)}
          >
            Multi-entry only
          </button>
        </div>

        <div className="profile-media-tabs" role="tablist" aria-label="Rank by">
          {TOP_SERIES_BASIS_TABS.map((tab) => (
            <button
              key={tab.value}
              type="button"
              role="tab"
              aria-selected={topSeriesBasis === tab.value}
              className={
                (topSeriesBasis === tab.value
                  ? 'profile-media-tabs__tab profile-media-tabs__tab--active'
                  : 'profile-media-tabs__tab') + ` family--${tab.family}`
              }
              onClick={() => setTopSeriesBasis(tab.value)}
            >
              {tab.label}
            </button>
          ))}
        </div>

        {!topSeries && topSeriesLoading ? null : !topSeries || topSeries.items.length === 0 ? (
          <p className="profile-page__section-empty">
            Series are still being discovered from your list. Use{' '}
            <Link to="/settings">"Build all series from my list"</Link> on the Settings page to fill this in now.
          </p>
        ) : rankedTopSeries.length === 0 ? (
          <p className="profile-page__section-empty">
            None of your series have a {topSeriesBasis === 'mine' ? 'my-score' : 'MAL'} main-line average yet.
          </p>
        ) : displayedTopSeries.length === 0 ? (
          <p className="profile-page__section-empty">
            The multi-entry filter left nothing to show. Switch it off to see single-entry series.
          </p>
        ) : (
          <div
            className={
              displayedTopSeries.length <= STRIP_VISIBLE_TILES
                ? 'top-series-strip top-series-strip--fits'
                : 'top-series-strip'
            }
            ref={topSeriesStripScroll.ref}
            {...topSeriesStripScroll.handlers}
          >
            {displayedTopSeries.map((item) => (
              <Link
                key={item.seriesId}
                to={`/series/${item.seriesId}`}
                className="top-series-strip__item"
                draggable={false}
                title={topSeriesCountsTitle(item)}
                onClick={topSeriesStripScroll.onItemClick}
              >
                {item.pictureUrl ? (
                  <img
                    src={item.pictureUrl}
                    alt={pickDisplayTitle(item.title, item.englishTitle)}
                    className="top-series-strip__picture"
                    draggable={false}
                  />
                ) : (
                  <div
                    className="top-series-strip__picture top-series-strip__picture--placeholder"
                    aria-hidden="true"
                  />
                )}
                <span className="top-series-strip__chips">
                  <ScoreChip role="mal" size="compact">
                    <ScoreValue value={item.malMain.value} completed={item.malRevealed} />
                  </ScoreChip>
                  <ScoreChip role="mine" size="compact">
                    {item.mineMain.value !== null ? item.mineMain.value.toFixed(2) : '—'}
                  </ScoreChip>
                </span>
              </Link>
            ))}
          </div>
        )}
      </section>

      <section className="profile-box">
        <h2>Most rewatched</h2>

        <div className="profile-media-tabs" role="tablist" aria-label="Filter by media type">
          {REWATCHED_SCOPE_TABS.map((tab) => (
            <button
              key={tab.value}
              type="button"
              role="tab"
              aria-selected={rewatchedScope === tab.value}
              className={
                rewatchedScope === tab.value
                  ? 'profile-media-tabs__tab profile-media-tabs__tab--active'
                  : 'profile-media-tabs__tab'
              }
              onClick={() => selectRewatchedScope(tab.value)}
            >
              {tab.label}
            </button>
          ))}
        </div>

        {rewatchedScope === 'series' ? (
          !displayedRewatchedSeries && rewatchedSeriesLoading ? null : !displayedRewatchedSeries ||
            displayedRewatchedSeries.items.length === 0 ? (
            <p className="profile-page__section-empty">{REWATCHED_EMPTY_MESSAGES.series}</p>
          ) : (
            <div
              className={
                displayedRewatchedSeries.items.length <= STRIP_VISIBLE_TILES
                  ? 'rewatched-strip rewatched-strip--fits'
                  : 'rewatched-strip'
              }
              ref={rewatchedStripScroll.ref}
              {...rewatchedStripScroll.handlers}
            >
              {displayedRewatchedSeries.items.map((item) => (
                <Link
                  key={item.seriesId}
                  to={`/series/${item.seriesId}`}
                  className="rewatched-strip__item"
                  draggable={false}
                  onClick={rewatchedStripScroll.onItemClick}
                >
                  {item.pictureUrl ? (
                    <img
                      src={item.pictureUrl}
                      alt={pickDisplayTitle(item.title, item.englishTitle)}
                      className="rewatched-strip__picture"
                      draggable={false}
                    />
                  ) : (
                    <div
                      className="rewatched-strip__picture rewatched-strip__picture--placeholder"
                      aria-hidden="true"
                    />
                  )}
                  <span className="rewatched-strip__count rewatched-strip__count--time">
                    {formatRewatchTime(item.rewatchSeconds)}
                  </span>
                </Link>
              ))}
            </div>
          )
        ) : !displayedRewatched && rewatchedLoading ? null : !displayedRewatched || displayedRewatched.items.length === 0 ? (
          <p className="profile-page__section-empty">{REWATCHED_EMPTY_MESSAGES[rewatchedMediaType]}</p>
        ) : (
          <div
            className={
              displayedRewatched.items.length <= STRIP_VISIBLE_TILES
                ? 'rewatched-strip rewatched-strip--fits'
                : 'rewatched-strip'
            }
            ref={rewatchedStripScroll.ref}
            {...rewatchedStripScroll.handlers}
          >
            {displayedRewatched.items.map((item) => (
              <Link
                key={item.animeId}
                to={`/anime/${item.animeId}`}
                className="rewatched-strip__item"
                draggable={false}
                onClick={rewatchedStripScroll.onItemClick}
              >
                {item.pictureUrl ? (
                  <img
                    src={item.pictureUrl}
                    alt={pickDisplayTitle(item.title, item.englishTitle)}
                    className="rewatched-strip__picture"
                    draggable={false}
                  />
                ) : (
                  <div
                    className="rewatched-strip__picture rewatched-strip__picture--placeholder"
                    aria-hidden="true"
                  />
                )}
                <span className="rewatched-strip__count">{item.rewatchCount}</span>
              </Link>
            ))}
          </div>
        )}
      </section>

      {profile.favouriteSeasons.length === 0 && profile.favouriteYears.length === 0 ? (
        <section className="profile-box">
          <h2>Favourites</h2>
          <p className="profile-page__section-empty">
            Score anime with a known air date to see your favourite seasons and years.
          </p>
        </section>
      ) : (
        <div className="profile-page__favourites-row">
          <section className="profile-box family--year">
            <RankingSection
              title="Favourite years"
              noun="years"
              rows={
                favouriteYearsScore === null
                  ? profile.favouriteYears.map((row) => describeYearRanking(row))
                  : rankByScoreCount(profile.favouriteYears, favouriteYearsScore).map((row) =>
                      describeYearRanking(row, favouriteYearsScore),
                    )
              }
              onSeeAll={setRankingOverlay}
              family="year"
              scoreFilter={{
                scores: offeredFavouriteYearScores,
                selected: favouriteYearsScore,
                onSelect: setFavouriteYearsScore,
              }}
            />
          </section>
          <section className="profile-box family--season">
            <RankingSection
              title="Favourite seasons"
              noun="seasons"
              rows={
                favouriteSeasonsScore === null
                  ? profile.favouriteSeasons.map((row) => describeSeasonRanking(row))
                  : rankByScoreCount(profile.favouriteSeasons, favouriteSeasonsScore).map((row) =>
                      describeSeasonRanking(row, favouriteSeasonsScore),
                    )
              }
              onSeeAll={setRankingOverlay}
              family="season"
              scoreFilter={{
                scores: offeredFavouriteSeasonScores,
                selected: favouriteSeasonsScore,
                onSelect: setFavouriteSeasonsScore,
              }}
            />
          </section>
        </div>
      )}

      <div className="profile-page__divergence-row">
        <section className="profile-box family--mal">
          <h2 className="section-band">They liked it, I didn't</h2>
          <DivergenceList items={profile.theyLikedItIDidnt} />
        </section>
        <section className="profile-box family--mine">
          <h2 className="section-band">I liked it, they didn't</h2>
          <DivergenceList items={profile.iLikedItTheyDidnt} />
        </section>
      </div>

      {rankingOverlay && (
        <RankingOverlay
          title={rankingOverlay.title}
          rows={rankingOverlay.rows}
          family={rankingOverlay.family}
          onClose={() => setRankingOverlay(null)}
        />
      )}
      {showHistory && <EditHistoryOverlay onClose={() => setShowHistory(false)} />}
      {showUnresolvedEpisodes && (
        <UnresolvedEpisodesOverlay
          entries={profile.episodeProgress.unresolvedAnime}
          onClose={() => setShowUnresolvedEpisodes(false)}
        />
      )}
      {showTopAnimeSelect && topAnime && (
        <AnimeRankOverlay
          mode="top"
          section={topAnime}
          mediaType={mediaType}
          mediaTypeLabel={MEDIA_TYPE_TABS.find((tab) => tab.value === mediaType)?.label ?? 'All'}
          onSaved={() => {
            setShowTopAnimeSelect(false)
            reloadTopAnime()
          }}
          onRankWholeLibrary={() => {
            setShowTopAnimeSelect(false)
            openRanking({ mediaType, onSaved: reloadTopAnime })
          }}
        />
      )}
    </div>
  )
}
