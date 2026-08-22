import { useEffect, useRef, useState } from 'react'
import { AnimeCard } from './AnimeCard.tsx'
import { ProgressBar } from './ProgressBar.tsx'
import type { CurrentlyWatchingItemDto, IncrementTarget } from '../api/types.ts'
import { useEpisodeIncrement, useSetEpisodesWatched } from '../context/CompletionPromptContext.tsx'
import { hasAiredEpisodes, pickDisplayTitle } from '../utils/anime.ts'
import './CurrentlyWatchingCarousel.css'

type CurrentlyWatchingCarouselProps = {
  items: CurrentlyWatchingItemDto[]
  onEpisodesWatchedChange: (animeId: number, episodesWatched: number) => void
  onCompleted: () => void
}

// "Currently watching" row on the main page: a horizontal carousel bounded
// to exactly 5 visible cards — no part of a 6th card is visible at any
// resting scroll position, at either end. When more entries exist, the row
// scrolls as a plain bounded list — it stops at the first card and at the
// last card, in both
// arrow-click and native trackpad/touch scrolling. Each card shows the
// shared watched/total progress bar and count in the card's `footer` slot,
// outside the card's link, so the whole progress row navigates nowhere while
// clicking the picture or title still opens the detail page.
export function CurrentlyWatchingCarousel({ items, onEpisodesWatchedChange, onCompleted }: CurrentlyWatchingCarouselProps) {
  const trackRef = useRef<HTMLDivElement>(null)
  const [pendingId, setPendingId] = useState<number | null>(null)
  const [overflowing, setOverflowing] = useState(false)
  const incrementEpisode = useEpisodeIncrement()
  const setEpisodesWatched = useSetEpisodesWatched()

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

  function buildTarget(item: CurrentlyWatchingItemDto): IncrementTarget {
    return {
      animeId: item.animeId,
      animeTitle: pickDisplayTitle(item.title, item.englishTitle),
      pictureUrl: item.pictureUrl,
      episodesWatched: item.episodesWatched,
      // main-dashboard: this section now also holds Rewatching entries, so the
      // pre-increment status has to come from the item itself rather than
      // being assumed Watching — otherwise a rewatch reaching the total would
      // incorrectly trip the completion-score prompt.
      previousStatus: item.status,
      // CurrentlyWatchingItemDto carries no score field; the carousel has
      // nothing to pre-fill the completion prompt with.
      currentScore: null,
      onSaved: (saved) => onEpisodesWatchedChange(item.animeId, saved.episodesWatched),
      onCompleted,
    }
  }

  async function increment(item: CurrentlyWatchingItemDto) {
    if (pendingId !== null) return
    setPendingId(item.animeId)
    try {
      await incrementEpisode(buildTarget(item))
    } finally {
      setPendingId(null)
    }
  }

  async function setWatched(item: CurrentlyWatchingItemDto, value: number) {
    if (pendingId !== null) return
    setPendingId(item.animeId)
    try {
      await setEpisodesWatched(buildTarget(item), value)
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
              footer={
                <>
                  {hasAiredEpisodes(item.airingStatus, item.episodesAired) && (
                    <ProgressBar
                      watched={item.episodesWatched}
                      total={item.totalEpisodes}
                      // Gated on airing status, not just an aired count being present:
                      // a finished show's aired count equals its total, so an ungated
                      // fill would paint every finished card's track solid blue.
                      aired={item.currentlyAiring ? item.episodesAired : null}
                      onIncrement={() => increment(item)}
                      onSetWatched={(value) => setWatched(item, value)}
                      max={item.episodesAired ?? item.totalEpisodes}
                      incrementPending={pendingId === item.animeId}
                      incrementLabel={`Increment episodes watched for ${pickDisplayTitle(item.title, item.englishTitle)}`}
                    />
                  )}
                  {item.nextEpisode && (
                    <span className="carousel__countdown">
                      Next ep: in {item.nextEpisode.days} days, {item.nextEpisode.hours} h
                    </span>
                  )}
                </>
              }
            />
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
