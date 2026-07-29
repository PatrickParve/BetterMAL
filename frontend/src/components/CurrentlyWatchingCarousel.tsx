import { useEffect, useRef, useState } from 'react'
import { AnimeCard } from './AnimeCard.tsx'
import { ProgressBar } from './ProgressBar.tsx'
import type { CurrentlyWatchingItemDto } from '../api/types.ts'
import { useEpisodeIncrement } from '../context/CompletionPromptContext.tsx'
import { pickDisplayTitle } from '../utils/anime.ts'
import './CurrentlyWatchingCarousel.css'

type CurrentlyWatchingCarouselProps = {
  items: CurrentlyWatchingItemDto[]
  onEpisodesWatchedChange: (animeId: number, episodesWatched: number) => void
  onCompleted: () => void
}

// "Currently watching" row on the main page: a horizontal carousel capped to
// 5 visible cards. When more entries exist, the row scrolls as a plain
// bounded list — it stops at the first card and at the last card, in both
// arrow-click and native trackpad/touch scrolling. Each card shows the
// shared watched/total progress bar with its inline plus button after the
// count; the plus stops propagation so it increments without navigating,
// while clicking the rest of the card body navigates without incrementing.
export function CurrentlyWatchingCarousel({ items, onEpisodesWatchedChange, onCompleted }: CurrentlyWatchingCarouselProps) {
  const trackRef = useRef<HTMLDivElement>(null)
  const [pendingId, setPendingId] = useState<number | null>(null)
  const [overflowing, setOverflowing] = useState(false)
  const incrementEpisode = useEpisodeIncrement()

  // Arrows only make sense when the row actually overflows — recompute on
  // resize (font/zoom/window changes) and whenever the item count changes.
  useEffect(() => {
    const node = trackRef.current
    if (!node || items.length === 0) return

    function updateOverflow() {
      setOverflowing(node!.scrollWidth > node!.clientWidth + 1)
    }

    updateOverflow()
    const observer = new ResizeObserver(updateOverflow)
    observer.observe(node)
    return () => observer.disconnect()
  }, [items])

  if (items.length === 0) return null

  // Step by exactly one card width so an arrow click always lands on the
  // next/previous card, never a partial one — rounding to the nearest card
  // boundary first keeps this exact even after a manual trackpad/drag scroll
  // has left the position off-grid.
  function scroll(direction: 1 | -1) {
    const node = trackRef.current
    if (!node) return
    const first = node.children[0] as HTMLElement | undefined
    const second = node.children[1] as HTMLElement | undefined
    if (!first || !second) return
    const step = second.getBoundingClientRect().left - first.getBoundingClientRect().left
    if (step <= 0) return
    const target = Math.round(node.scrollLeft / step) * step + direction * step
    node.scrollTo({ left: target, behavior: 'smooth' })
  }

  async function increment(item: CurrentlyWatchingItemDto) {
    if (pendingId !== null) return
    setPendingId(item.animeId)
    try {
      await incrementEpisode({
        animeId: item.animeId,
        animeTitle: pickDisplayTitle(item.title, item.englishTitle),
        pictureUrl: item.pictureUrl,
        episodesWatched: item.episodesWatched,
        // The dashboard's currently-watching items are all Status == Watching
        // by construction (MainDashboardService filters on it), and the DTO
        // carries no status field, so the pre-increment status is known here.
        previousStatus: 'Watching',
        // CurrentlyWatchingItemDto carries no score field; the carousel has
        // nothing to pre-fill the completion prompt with.
        currentScore: null,
        onSaved: (saved) => onEpisodesWatchedChange(item.animeId, saved.episodesWatched),
        onCompleted,
      })
    } finally {
      setPendingId(null)
    }
  }

  return (
    <section className="dashboard-section dashboard-section--carousel">
      <h2>Currently watching</h2>
      <div className="carousel">
        {overflowing && (
          <button type="button" className="carousel__arrow" onClick={() => scroll(-1)} aria-label="Scroll left">
            ‹
          </button>
        )}
        <div className="carousel__track" ref={trackRef}>
          {items.map((item) => (
            <AnimeCard
              key={item.animeId}
              animeId={item.animeId}
              title={item.title}
              englishTitle={item.englishTitle}
              pictureUrl={item.pictureUrl}
              className="carousel__card"
            >
              <ProgressBar
                watched={item.episodesWatched}
                total={item.totalEpisodes}
                onIncrement={() => increment(item)}
                incrementPending={pendingId === item.animeId}
                incrementLabel={`Increment episodes watched for ${pickDisplayTitle(item.title, item.englishTitle)}`}
              />
              {item.nextEpisode && (
                <span className="carousel__countdown">
                  Next ep: in {item.nextEpisode.days} days, {item.nextEpisode.hours} h
                </span>
              )}
            </AnimeCard>
          ))}
        </div>
        {overflowing && (
          <button type="button" className="carousel__arrow" onClick={() => scroll(1)} aria-label="Scroll right">
            ›
          </button>
        )}
      </div>
    </section>
  )
}
