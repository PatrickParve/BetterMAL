import { useEffect, useRef, useState } from 'react'
import { AnimeCard } from './AnimeCard.tsx'
import { IncrementButton } from './IncrementButton.tsx'
import { updateEntry } from '../api/client.ts'
import type { CurrentlyWatchingItemDto } from '../api/types.ts'
import { pickDisplayTitle } from '../utils/anime.ts'
import './CurrentlyWatchingCarousel.css'

type CurrentlyWatchingCarouselProps = {
  items: CurrentlyWatchingItemDto[]
  onEpisodesWatchedChange: (animeId: number, episodesWatched: number) => void
}

const SCROLL_AMOUNT = 340

// "Currently watching" row on the main page: a horizontal carousel scrolled
// via the left/right arrows. The plus button sits inline after the episode
// count and stops propagation so it increments without navigating; clicking
// the rest of the card body navigates without incrementing.
export function CurrentlyWatchingCarousel({ items, onEpisodesWatchedChange }: CurrentlyWatchingCarouselProps) {
  const trackRef = useRef<HTMLDivElement>(null)
  const [pendingId, setPendingId] = useState<number | null>(null)
  const [overflowing, setOverflowing] = useState(false)

  // Arrows only make sense when the row actually overflows — recompute on
  // resize (font/zoom/window changes) and whenever the item count changes.
  useEffect(() => {
    const node = trackRef.current
    if (!node) return

    function updateOverflow() {
      setOverflowing(node!.scrollWidth > node!.clientWidth)
    }

    updateOverflow()
    const observer = new ResizeObserver(updateOverflow)
    observer.observe(node)
    return () => observer.disconnect()
  }, [items])

  if (items.length === 0) return null

  function scroll(direction: 1 | -1) {
    trackRef.current?.scrollBy({ left: direction * SCROLL_AMOUNT, behavior: 'smooth' })
  }

  async function increment(item: CurrentlyWatchingItemDto) {
    if (pendingId !== null) return
    setPendingId(item.animeId)
    try {
      const saved = await updateEntry(item.animeId, { episodesWatched: item.episodesWatched + 1 })
      onEpisodesWatchedChange(item.animeId, saved.episodesWatched)
    } catch {
      // Leave the count as-is; the debounced sync/retry path handles durability once a save does go through.
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
          {items.map((item) => {
            const atMax = item.totalEpisodes !== null && item.episodesWatched >= item.totalEpisodes
            return (
              <AnimeCard
                key={item.animeId}
                animeId={item.animeId}
                title={item.title}
                englishTitle={item.englishTitle}
                pictureUrl={item.pictureUrl}
                className="carousel__card"
              >
                <span className="carousel__progress-row">
                  <span className="carousel__progress">
                    {item.episodesWatched}/{item.totalEpisodes ?? '?'}
                  </span>
                  <IncrementButton
                    onIncrement={() => increment(item)}
                    disabled={pendingId === item.animeId || atMax}
                    label={`Increment episodes watched for ${pickDisplayTitle(item.title, item.englishTitle)}`}
                  />
                </span>
                {item.nextEpisode && (
                  <span className="carousel__countdown">
                    Next ep: in {item.nextEpisode.days} days, {item.nextEpisode.hours} h
                  </span>
                )}
              </AnimeCard>
            )
          })}
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
