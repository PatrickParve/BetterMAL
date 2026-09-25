import { useCallback } from 'react'
import { getDashboard } from '../api/client.ts'
import type { MainDashboardDto } from '../api/types.ts'
import { usePageData } from '../hooks/usePageData.ts'
import { LoadFailedNotice } from '../components/LoadFailedNotice.tsx'
import { LoadingNotice } from '../components/LoadingNotice.tsx'
import { CurrentlyWatchingCarousel } from '../components/CurrentlyWatchingCarousel.tsx'
import { AiringTodayList } from '../components/AiringTodayList.tsx'
import { CurrentSeasonSection } from '../components/CurrentSeasonSection.tsx'
import './HomePage.css'

export function HomePage() {
  const {
    data: dashboard,
    failed,
    retry,
    setData: setDashboard,
    reload,
  } = usePageData<MainDashboardDto>('dashboard', getDashboard)

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

  // Never a blank page below the navbar: a read still in flight is the
  // (delayed) loading notice, and one that failed says so with Try again.
  if (!dashboard) {
    return failed ? (
      <LoadFailedNotice what="the dashboard" onRetry={retry} />
    ) : (
      <LoadingNotice className="home-page__loading" />
    )
  }

  return (
    <div className="home-page">
      <CurrentlyWatchingCarousel
        items={dashboard.currentlyWatching}
        onEpisodesWatchedChange={handleEpisodesWatchedChange}
        // Runs only once a score is saved through the completion prompt —
        // that's what actually moves the anime out of "Currently watching".
        // A completion left uncommitted (a silent scored rewatch, or a
        // cancelled prompt) stays in place and can be undone from the card
        // (main-dashboard: "A completion left in Currently watching can be
        // undone from its card").
        onCompleted={reload}
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
