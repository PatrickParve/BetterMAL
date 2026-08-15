import { useCallback, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { getProfile, getRewatchedSection, getTopAnimeSection, getTopSeriesSection } from '../api/client.ts'
import type {
  OpinionDivergenceItemDto,
  ProfileDto,
  RewatchedSectionDto,
  TopAnimeMediaType,
  TopAnimeSectionDto,
  TopSeriesItemDto,
  TopSeriesSectionDto,
} from '../api/types.ts'
import { ScoreChip } from '../components/ScoreChip.tsx'
import { ScoreValue } from '../components/ScoreValue.tsx'
import { EditHistoryOverlay } from '../components/EditHistoryOverlay.tsx'
import { TopAnimeSelectionOverlay } from '../components/TopAnimeSelectionOverlay.tsx'
import { TruncatedTitle } from '../components/TruncatedTitle.tsx'
import { usePageData } from '../hooks/usePageData.ts'
import { useRestorableState } from '../hooks/useRestorableState.ts'
import { usePageState } from '../state/PageStateContext.tsx'
import * as pageStateStore from '../state/pageStateStore.ts'
import { formatTimestamp, pickDisplayTitle } from '../utils/anime.ts'
import './ProfilePage.css'

// The strip's defining constant (design.md decision 4): must agree with the
// tile `flex` basis in ProfilePage.css (`calc((100% - 9 * 10px) / 10)`),
// which lays out exactly this many tiles across the strip's visible width.
const STRIP_VISIBLE_TILES = 10

type TopSeriesBasis = 'mine' | 'mal'

const TOP_SERIES_BASIS_TABS: { value: TopSeriesBasis; label: string }[] = [
  { value: 'mine', label: 'My score' },
  { value: 'mal', label: 'MAL score' },
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

// Basis toggle re-sorts and re-filters the already-loaded array rather than
// refetching (design.md decision 4): average descending, then scored
// main-line count descending (an average earned across more entries places
// higher), then title case-insensitively. A series with no value under the
// selected basis is omitted rather than parked at the end (design.md
// decision 3).
function rankTopSeries(items: TopSeriesItemDto[], basis: TopSeriesBasis): TopSeriesItemDto[] {
  return items
    .filter((item) => topSeriesBasisValue(item, basis) !== null)
    .sort((a, b) => {
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

const REWATCHED_EMPTY_MESSAGES: Record<TopAnimeMediaType, string> = {
  all: 'No shows have been rewatched',
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
  { key: 'planToWatch', label: 'Plan to watch' },
  { key: 'onHold', label: 'On-hold' },
  { key: 'dropped', label: 'Dropped' },
  { key: 'days', label: 'Days' },
  { key: 'rewatched', label: 'Rewatched' },
  { key: 'episodes', label: 'Episodes' },
]

function formatStatValue(key: keyof ProfileDto['stats'], value: number | null): string {
  if (value === null) return '—'
  if (key === 'days') return value.toFixed(1)
  if (key === 'meanScore') return value.toFixed(2)
  return String(value)
}

// `<1%` covers a bucket that has anime in it but rounds down to nothing —
// showing a flat 0% there would read as "no anime has this score", which is
// false. No share at all renders when nothing is rated, since a percentage
// of zero is a meaningless comparison.
function formatShare(count: number, totalRated: number): string | null {
  if (totalRated === 0) return null
  const rounded = Math.round((count / totalRated) * 100)
  if (count > 0 && rounded === 0) return '<1%'
  return `${rounded}%`
}

// Drag-to-scroll plus scroll-offset restoration for a horizontal poster
// strip: a mouse-down on the strip starts tracking, mouse-move scrolls it and
// flags a drag once the pointer has moved past a small threshold, and
// onItemClick suppresses the resulting navigation click so a drag doesn't
// also open the tile. `restoreKey` identifies this strip's section (and, for
// sections with a view control, the control's current value) so its offset
// is recorded and restored independently of the page's other strips
// (design.md decision 3).
function useStripScroll(restoreKey: string) {
  const elRef = useRef<HTMLDivElement | null>(null)
  const drag = useRef({ isDown: false, startX: 0, scrollLeft: 0, dragged: false })
  const { key, isRestore, snapshot } = usePageState()

  // Read by the ref callback below, which — unlike an effect keyed on
  // `restoreKey` — only runs when React actually attaches or detaches the
  // strip's DOM node. A strip whose section is still loading on first mount
  // (topAnime/rewatched, held empty until their fetch resolves) attaches
  // that node later, on a render an effect with an unrelated dependency list
  // would never repeat for; refs sidestep that by always being current
  // whenever the callback next fires.
  const keyRef = useRef(key)
  keyRef.current = key
  const restoreKeyRef = useRef(restoreKey)
  restoreKeyRef.current = restoreKey
  const isRestoreRef = useRef(isRestore)
  isRestoreRef.current = isRestore
  const snapshotRef = useRef(snapshot)
  snapshotRef.current = snapshot

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
  const setRef = useCallback((el: HTMLDivElement | null) => {
    elRef.current = el
    if (!el) return
    const node = el

    // Recorded synchronously, same reasoning as the page's own scroll
    // position (useScrollRestoration decision 1).
    function onScroll() {
      pageStateStore.putStripScroll(keyRef.current, restoreKeyRef.current, node.scrollLeft)
    }
    node.addEventListener('scroll', onScroll, { passive: true })

    // Applied on a restore only, once the strip's tiles are laid out —
    // usePageData seeds restored data synchronously, so on the first paint
    // of a restore the tiles are already in the DOM and the offset is
    // reachable. A cheap analogue of the page-level retry loop, without a
    // timer: a ResizeObserver re-applies the offset if the strip was too
    // narrow to reach it when first measured, and stops for good once it's
    // reached or the user scrolls the strip themselves. On a fresh visit
    // this does nothing, leaving the strip at its start.
    let observer: ResizeObserver | undefined
    let onUserInput: (() => void) | undefined

    if (isRestoreRef.current) {
      const target = snapshotRef.current.strips.get(restoreKeyRef.current)
      if (target !== undefined) {
        let done = false
        let stopped = false

        const attempt = () => {
          if (done || stopped) return
          const reachable = node.scrollWidth - node.clientWidth >= target
          node.scrollLeft = target
          if (reachable) done = true
        }
        attempt()

        observer = new ResizeObserver(() => attempt())
        observer.observe(node)

        onUserInput = () => {
          stopped = true
        }
        node.addEventListener('wheel', onUserInput, { passive: true })
        node.addEventListener('touchstart', onUserInput, { passive: true })
        node.addEventListener('mousedown', onUserInput)
      }
    }

    return () => {
      node.removeEventListener('scroll', onScroll)
      observer?.disconnect()
      if (onUserInput) {
        node.removeEventListener('wheel', onUserInput)
        node.removeEventListener('touchstart', onUserInput)
        node.removeEventListener('mousedown', onUserInput)
      }
    }
  }, [])

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
    <ul className="divergence-list">
      {items.map((item) => (
        <li key={item.animeId} className="profile-list-row">
          <Link to={`/anime/${item.animeId}`} className="profile-list-row__link">
            {item.pictureUrl ? (
              <img src={item.pictureUrl} alt="" className="profile-list-row__picture" />
            ) : (
              <div className="profile-list-row__picture profile-list-row__picture--placeholder" aria-hidden="true" />
            )}
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

  const [rewatchedMediaType, setRewatchedMediaType] = useRestorableState<TopAnimeMediaType>(
    'rewatchedMediaType',
    'all',
  )
  const { data: rewatched, loading: rewatchedLoading } = usePageData<RewatchedSectionDto>(
    `rewatched:${rewatchedMediaType}`,
    () => getRewatchedSection(rewatchedMediaType),
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

  const [showHistory, setShowHistory] = useState(false)
  const [showTopAnimeSelect, setShowTopAnimeSelect] = useState(false)
  const topAnimeStripScroll = useStripScroll(`top-anime:${mediaType}`)
  const rewatchedStripScroll = useStripScroll(`rewatched:${rewatchedMediaType}`)
  const topSeriesStripScroll = useStripScroll('top-series')

  if (loading) {
    return <p className="profile-page__loading">Loading…</p>
  }

  if (!profile) {
    return <p className="profile-page__empty">Couldn't load profile data.</p>
  }

  const totalRated = profile.scoreDistribution.buckets.reduce((sum, b) => sum + b.count, 0)
  const maxBucketCount = Math.max(0, ...profile.scoreDistribution.buckets.map((b) => b.count))

  return (
    <div className="profile-page">
      <h1>Profile</h1>

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
          <div className="score-distribution">
            {[...profile.scoreDistribution.buckets].reverse().map((bucket) => {
              const share = formatShare(bucket.count, totalRated)
              return (
                <div key={bucket.score} className="score-distribution__row">
                  <span className="score-distribution__label">{bucket.score}</span>
                  <div className="score-distribution__bar-track">
                    <div
                      className="score-distribution__bar"
                      style={{ width: `${maxBucketCount > 0 ? (bucket.count / maxBucketCount) * 100 : 0}%` }}
                    />
                  </div>
                  <span className="score-distribution__count">
                    {bucket.count}
                    {share ? ` (${share})` : ''}
                  </span>
                </div>
              )
            })}
          </div>
          <p className="score-distribution__mean">
            Mean score: {profile.scoreDistribution.meanScore?.toFixed(2) ?? '—'}
          </p>
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
            <ul className="activity-feed scroll-y">
              {profile.recentActivity.map((item) => (
                <li key={item.id} className="profile-list-row">
                  <Link to={`/anime/${item.animeId}`} className="profile-list-row__link">
                    {item.pictureUrl ? (
                      <img src={item.pictureUrl} alt="" className="profile-list-row__picture" />
                    ) : (
                      <div
                        className="profile-list-row__picture profile-list-row__picture--placeholder"
                        aria-hidden="true"
                      />
                    )}
                    <span className="profile-list-row__info">
                      <TruncatedTitle
                        title={pickDisplayTitle(item.animeTitle, item.animeEnglishTitle)}
                        lines={1}
                        className="profile-list-row__title"
                      />
                      <span className="profile-list-row__meta">{item.summary}</span>
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
              Edit order
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
                topSeriesBasis === tab.value
                  ? 'profile-media-tabs__tab profile-media-tabs__tab--active'
                  : 'profile-media-tabs__tab'
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
                to={`/series/${item.rootAnimeId}`}
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
          {MEDIA_TYPE_TABS.map((tab) => (
            <button
              key={tab.value}
              type="button"
              role="tab"
              aria-selected={rewatchedMediaType === tab.value}
              className={
                rewatchedMediaType === tab.value
                  ? 'profile-media-tabs__tab profile-media-tabs__tab--active'
                  : 'profile-media-tabs__tab'
              }
              onClick={() => setRewatchedMediaType(tab.value)}
            >
              {tab.label}
            </button>
          ))}
        </div>

        {!displayedRewatched && rewatchedLoading ? null : !displayedRewatched || displayedRewatched.items.length === 0 ? (
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

      <div className="profile-page__divergence-row">
        <section className="profile-box">
          <h2>They liked it, I didn't</h2>
          <DivergenceList items={profile.theyLikedItIDidnt} />
        </section>
        <section className="profile-box">
          <h2>I liked it, they didn't</h2>
          <DivergenceList items={profile.iLikedItTheyDidnt} />
        </section>
      </div>

      {showHistory && <EditHistoryOverlay onClose={() => setShowHistory(false)} />}
      {showTopAnimeSelect && topAnime && (
        <TopAnimeSelectionOverlay
          section={topAnime}
          mediaType={mediaType}
          mediaTypeLabel={MEDIA_TYPE_TABS.find((tab) => tab.value === mediaType)?.label ?? 'All'}
          onClose={() => setShowTopAnimeSelect(false)}
          onSaved={() => {
            setShowTopAnimeSelect(false)
            reloadTopAnime()
          }}
        />
      )}
    </div>
  )
}
