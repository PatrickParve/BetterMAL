import { useCallback, useEffect, useState } from 'react'
import { getDashboard } from '../api/client.ts'
import type { MainDashboardDto } from '../api/types.ts'
import { CurrentlyWatchingCarousel } from '../components/CurrentlyWatchingCarousel.tsx'
import { AiringTodayList } from '../components/AiringTodayList.tsx'
import { CurrentSeasonSection } from '../components/CurrentSeasonSection.tsx'
import './HomePage.css'

export function HomePage() {
  const [dashboard, setDashboard] = useState<MainDashboardDto | null>(null)

  const loadDashboard = useCallback(() => {
    return getDashboard()
      .then(setDashboard)
      .catch(() => {
        // Main page just stays empty; the user can reload once the backend catches up.
      })
  }, [])

  useEffect(() => {
    loadDashboard()
  }, [loadDashboard])

  const handleEpisodesWatchedChange = useCallback((animeId: number, episodesWatched: number) => {
    setDashboard((prev) =>
      prev
        ? {
            ...prev,
            currentlyWatching: prev.currentlyWatching.map((item) =>
              item.animeId === animeId ? { ...item, episodesWatched } : item,
            ),
          }
        : prev,
    )
  }, [])

  if (!dashboard) return null

  return (
    <div className="home-page">
      <CurrentlyWatchingCarousel
        items={dashboard.currentlyWatching}
        onEpisodesWatchedChange={handleEpisodesWatchedChange}
        onCompleted={loadDashboard}
      />
      <div className="home-page__row">
        <div className="home-page__aside">
          <AiringTodayList items={dashboard.airingToday} />
        </div>
        <div className="home-page__main">
          <CurrentSeasonSection items={dashboard.currentSeason} />
        </div>
      </div>
    </div>
  )
}
