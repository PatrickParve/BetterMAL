import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getProfile } from '../api/client.ts'
import type { OpinionDivergenceItemDto, ProfileDto } from '../api/types.ts'
import { ScoreValue } from '../components/ScoreValue.tsx'
import { EditHistoryOverlay } from '../components/EditHistoryOverlay.tsx'
import { TopAnimeSelectionOverlay } from '../components/TopAnimeSelectionOverlay.tsx'
import { pickDisplayTitle } from '../utils/anime.ts'
import './ProfilePage.css'

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

const CHANGE_TYPE_LABELS: Record<string, string> = {
  Added: 'Added',
  StatusChanged: 'Status changed',
  EpisodeIncremented: 'Episode watched',
  ScoreChanged: 'Score changed',
  Completed: 'Completed',
  RewatchCountChanged: 'Rewatch count changed',
}

function formatStatValue(key: keyof ProfileDto['stats'], value: number | null): string {
  if (value === null) return '—'
  if (key === 'days') return value.toFixed(1)
  if (key === 'meanScore') return value.toFixed(2)
  return String(value)
}

function formatTimestamp(timestamp: string): string {
  return new Date(timestamp).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
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
            <span className="profile-list-row__title">{pickDisplayTitle(item.title, item.englishTitle)}</span>
          </Link>
          <span className="profile-list-row__trailing">
            Me {item.myScore} · MAL <ScoreValue value={item.malScore} />
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
  const [loading, setLoading] = useState(true)
  const [showHistory, setShowHistory] = useState(false)
  const [showTopAnimeSelect, setShowTopAnimeSelect] = useState(false)

  function loadProfile() {
    return getProfile()
      .then(setProfile)
      .catch(() => {
        // Page just stays empty; nothing else to react to here.
      })
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

  const maxBucketCount = Math.max(1, ...profile.scoreDistribution.buckets.map((b) => b.count))

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
                    style={{ width: `${(bucket.count / maxBucketCount) * 100}%` }}
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
                      <span className="profile-list-row__title">{pickDisplayTitle(item.animeTitle, item.animeEnglishTitle)}</span>
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
          {profile.topAnime.candidates.length > 0 && (
            <button type="button" className="profile-box__control" onClick={() => setShowTopAnimeSelect(true)}>
              Edit selection
            </button>
          )}
        </div>
        {profile.topAnime.items.length === 0 ? (
          <p className="profile-page__section-empty">Score some anime to build your top list.</p>
        ) : (
          <ol className="top-anime-mini-list">
            {profile.topAnime.items.map((item, index) => (
              <li key={item.animeId} className="profile-list-row">
                <span className="profile-list-row__rank">#{index + 1}</span>
                <Link to={`/anime/${item.animeId}`} className="profile-list-row__link">
                  {item.pictureUrl ? (
                    <img src={item.pictureUrl} alt="" className="profile-list-row__picture" />
                  ) : (
                    <div
                      className="profile-list-row__picture profile-list-row__picture--placeholder"
                      aria-hidden="true"
                    />
                  )}
                  <span className="profile-list-row__title">{pickDisplayTitle(item.title, item.englishTitle)}</span>
                </Link>
                <span className="profile-list-row__trailing">{item.myScore}</span>
              </li>
            ))}
          </ol>
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
      {showTopAnimeSelect && (
        <TopAnimeSelectionOverlay
          section={profile.topAnime}
          onClose={() => setShowTopAnimeSelect(false)}
          onSaved={() => {
            setShowTopAnimeSelect(false)
            loadProfile()
          }}
        />
      )}
    </div>
  )
}
