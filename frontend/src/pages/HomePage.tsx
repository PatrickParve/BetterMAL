import { useCallback, useEffect, useState } from 'react'
import { getDashboard } from '../api/client.ts'
import type { MainDashboardDto } from '../api/types.ts'
import { CurrentlyWatchingCarousel } from '../components/CurrentlyWatchingCarousel.tsx'
import { AiringTodayList } from '../components/AiringTodayList.tsx'
import { CurrentSeasonSection } from '../components/CurrentSeasonSection.tsx'
import './HomePage.css'

export function HomePage() {
  const [dashboard, setDashboard] = useState<MainDashboardDto | null>(null)

  useEffect(() => {
    let cancelled = false
    getDashboard()
      .then((data) => {
        if (!cancelled) setDashboard(data)
      })
      .catch(() => {
        // Main page just stays empty; the user can reload once the backend catches up.
      })
    return () => {
      cancelled = true
    }
  }, [])

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
      />
      <AiringTodayList items={dashboard.airingToday} />
      <CurrentSeasonSection items={dashboard.currentSeason} />
    </div>
  )
}
