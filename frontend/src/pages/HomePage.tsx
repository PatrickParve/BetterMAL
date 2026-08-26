import { useCallback } from 'react'
import { getDashboard } from '../api/client.ts'
import type { MainDashboardDto } from '../api/types.ts'
import { usePageData } from '../hooks/usePageData.ts'
import { CurrentlyWatchingCarousel } from '../components/CurrentlyWatchingCarousel.tsx'
import { AiringTodayList } from '../components/AiringTodayList.tsx'
import { CurrentSeasonSection } from '../components/CurrentSeasonSection.tsx'
import { UpdatesSection } from '../components/UpdatesSection.tsx'
import './HomePage.css'

export function HomePage() {
  const { data: dashboard, setData: setDashboard, reload } = usePageData<MainDashboardDto>('dashboard', getDashboard)

  // The same anime can appear in both "Currently watching" and "Followed
  // shows airing" (a current-season title I'm also watching), so an
  // increment in one must patch the other's watched count too, or the two
  // bars would show different fills until the next reload.
  const handleEpisodesWatchedChange = useCallback((animeId: number, episodesWatched: number) => {
    setDashboard((prev) =>
      prev
        ? {
            ...prev,
            currentlyWatching: prev.currentlyWatching.map((item) =>
              item.animeId === animeId ? { ...item, episodesWatched } : item,
            ),
            currentSeason: prev.currentSeason.map((item) =>
              item.animeId === animeId ? { ...item, episodesWatched } : item,
            ),
          }
        : prev,
    )
  }, [setDashboard])

  if (!dashboard) return null

  return (
    <div className="home-page">
      <CurrentlyWatchingCarousel
        items={dashboard.currentlyWatching}
        onEpisodesWatchedChange={handleEpisodesWatchedChange}
        // Reloads rather than patching: completing an anime moves it out of
        // "Currently watching" and possibly into the current-season section's
        // finished state, a server-computed regrouping no mutation response describes.
        onCompleted={reload}
      />
      <UpdatesSection items={dashboard.updates} />
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
