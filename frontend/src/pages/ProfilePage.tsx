import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { getProfile, getRewatchedSection, getTopAnimeSection } from '../api/client.ts'
import type {
  OpinionDivergenceItemDto,
  ProfileDto,
  RewatchedSectionDto,
  TopAnimeMediaType,
  TopAnimeSectionDto,
} from '../api/types.ts'
import { ScoreValue } from '../components/ScoreValue.tsx'
import { EditHistoryOverlay } from '../components/EditHistoryOverlay.tsx'
import { TopAnimeSelectionOverlay } from '../components/TopAnimeSelectionOverlay.tsx'
import { CHANGE_TYPE_LABELS, formatTimestamp, pickDisplayTitle } from '../utils/anime.ts'
import './ProfilePage.css'

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
  { key: 'days', label: 'Days' },
  { key: 'meanScore', label: 'Mean score' },
  { key: 'watching', label: 'Watching' },
  { key: 'completed', label: 'Completed' },
  { key: 'onHold', label: 'On-hold' },
  { key: 'dropped', label: 'Dropped' },
  { key: 'planToWatch', label: 'Plan to watch' },
  { key: 'totalEntries', label: 'Total entries' },
  { key: 'rewatched', label: 'Rewatched' },
  { key: 'episodes', label: 'Episodes' },
]

function formatStatValue(key: keyof ProfileDto['stats'], value: number | null): string {
  if (value === null) return '—'
  if (key === 'days') return value.toFixed(1)
  if (key === 'meanScore') return value.toFixed(2)
  return String(value)
}

// Shared drag-to-scroll behavior for a horizontal poster strip: a mouse-down
// on the strip starts tracking, mouse-move scrolls it and flags a drag once
// the pointer has moved past a small threshold, and onItemClick suppresses
// the resulting navigation click so a drag doesn't also open the tile.
function useDragScroll() {
  const scrollRef = useRef<HTMLDivElement>(null)
  const drag = useRef({ isDown: false, startX: 0, scrollLeft: 0, dragged: false })

  function onMouseDown(event: React.MouseEvent<HTMLDivElement>) {
    const el = scrollRef.current
    if (!el) return
    drag.current = { isDown: true, startX: event.pageX, scrollLeft: el.scrollLeft, dragged: false }
  }

  function onMouseMove(event: React.MouseEvent<HTMLDivElement>) {
    const state = drag.current
    const el = scrollRef.current
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

  return {
    ref: scrollRef,
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
            Me {item.myScore} · MAL <ScoreValue value={item.malScore} completed={item.isCompleted} />
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
  const [profile, setProfile] = useState<ProfileDto | null>(null)
  const [topAnime, setTopAnime] = useState<TopAnimeSectionDto | null>(null)
  const [mediaType, setMediaType] = useState<TopAnimeMediaType>('all')
  const [rewatched, setRewatched] = useState<RewatchedSectionDto | null>(null)
  const [rewatchedMediaType, setRewatchedMediaType] = useState<TopAnimeMediaType>('all')
  const [loading, setLoading] = useState(true)
  const [showHistory, setShowHistory] = useState(false)
  const [showTopAnimeSelect, setShowTopAnimeSelect] = useState(false)
  const topAnimeDragScroll = useDragScroll()
  const rewatchedDragScroll = useDragScroll()

  function loadProfile() {
    return getProfile()
      .then((data) => {
        setProfile(data)
        setTopAnime(data.topAnime)
        setRewatched(data.rewatched)
      })
      .catch(() => {
        // Page just stays empty; nothing else to react to here.
      })
  }

  function loadTopAnimeSection(type: TopAnimeMediaType) {
    return getTopAnimeSection(type)
      .then(setTopAnime)
      .catch(() => {
        // Section just stays as-is; nothing else to react to here.
      })
  }

  function selectMediaType(type: TopAnimeMediaType) {
    setMediaType(type)
    loadTopAnimeSection(type)
  }

  function loadRewatchedSection(type: TopAnimeMediaType) {
    return getRewatchedSection(type)
      .then(setRewatched)
      .catch(() => {
        // Section just stays as-is; nothing else to react to here.
      })
  }

  function selectRewatchedMediaType(type: TopAnimeMediaType) {
    setRewatchedMediaType(type)
    loadRewatchedSection(type)
  }

  useEffect(() => {
    loadProfile().finally(() => setLoading(false))
  }, [])

  if (loading) {
    return <p className="profile-page__loading">Loading…</p>
  }

  if (!profile) {
    return <p className="profile-page__empty">Couldn't load profile data.</p>
  }

  const totalRated = profile.scoreDistribution.buckets.reduce((sum, b) => sum + b.count, 0)

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
            {[...profile.scoreDistribution.buckets].reverse().map((bucket) => (
              <div key={bucket.score} className="score-distribution__row">
                <span className="score-distribution__label">{bucket.score}</span>
                <div className="score-distribution__bar-track">
                  <div
                    className="score-distribution__bar"
                    style={{ width: `${totalRated > 0 ? (bucket.count / totalRated) * 100 : 0}%` }}
                  />
                </div>
                <span className="score-distribution__count">{bucket.count}</span>
              </div>
            ))}
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
            <ul className="activity-feed">
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
                      <span
                        className="profile-list-row__title"
                        title={pickDisplayTitle(item.animeTitle, item.animeEnglishTitle)}
                      >
                        {pickDisplayTitle(item.animeTitle, item.animeEnglishTitle)}
                      </span>
                      <span className="profile-list-row__meta">
                        {CHANGE_TYPE_LABELS[item.changeType] ?? item.changeType}
                        {item.changeDetail ? ` — ${item.changeDetail}` : ''}
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
          {topAnime && topAnime.tiers.some((tier) => tier.members.length > 1) && (
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
              onClick={() => selectMediaType(tab.value)}
            >
              {tab.label}
            </button>
          ))}
        </div>

        {!topAnime || topAnime.items.length === 0 ? (
          <p className="profile-page__section-empty">
            {mediaType === 'all'
              ? 'Score some anime to build your top list.'
              : `No scored ${MEDIA_TYPE_TABS.find((tab) => tab.value === mediaType)?.label} yet.`}
          </p>
        ) : (
          <div className="top-anime-strip" ref={topAnimeDragScroll.ref} {...topAnimeDragScroll.handlers}>
            {topAnime.items.map((item) => (
              <Link
                key={item.animeId}
                to={`/anime/${item.animeId}`}
                className="top-anime-strip__item"
                draggable={false}
                onClick={topAnimeDragScroll.onItemClick}
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
              onClick={() => selectRewatchedMediaType(tab.value)}
            >
              {tab.label}
            </button>
          ))}
        </div>

        {!rewatched || rewatched.items.length === 0 ? (
          <p className="profile-page__section-empty">{REWATCHED_EMPTY_MESSAGES[rewatchedMediaType]}</p>
        ) : (
          <div className="rewatched-strip" ref={rewatchedDragScroll.ref} {...rewatchedDragScroll.handlers}>
            {rewatched.items.map((item) => (
              <Link
                key={item.animeId}
                to={`/anime/${item.animeId}`}
                className="rewatched-strip__item"
                draggable={false}
                onClick={rewatchedDragScroll.onItemClick}
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
            loadTopAnimeSection(mediaType)
          }}
        />
      )}
    </div>
  )
}
