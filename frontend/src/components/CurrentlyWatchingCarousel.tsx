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

const LOOP_COPIES = 3

// "Currently watching" row on the main page: a horizontal carousel capped to
// 5 visible cards. When more entries exist, three copies of the list are
// rendered back-to-back and the scroll position is silently re-centered
// whenever it drifts out of the middle copy, so native trackpad/touch
// scrolling (and the arrows) appear to loop endlessly in both directions.
// Each card shows the shared watched/total progress bar with its inline plus
// button after the count; the plus stops propagation so it increments
// without navigating, while clicking the rest of the card body navigates
// without incrementing.
export function CurrentlyWatchingCarousel({ items, onEpisodesWatchedChange, onCompleted }: CurrentlyWatchingCarouselProps) {
  const trackRef = useRef<HTMLDivElement>(null)
  const [pendingId, setPendingId] = useState<number | null>(null)
  const [overflowing, setOverflowing] = useState(false)
  const looping = overflowing
  const incrementEpisode = useEpisodeIncrement()

  // Arrows (and looping) only make sense when the row actually overflows —
  // recompute on resize (font/zoom/window changes) and whenever the item
  // count changes. The DOM's own card count tells us how many copies are
  // currently rendered, so the single-copy width stays accurate whether
  // looping is on (3 copies) or off (1 copy).
  useEffect(() => {
    const node = trackRef.current
    if (!node || items.length === 0) return

    function updateOverflow() {
      const copies = Math.max(1, Math.round(node!.children.length / items.length))
      const singleCopyWidth = node!.scrollWidth / copies
      setOverflowing(singleCopyWidth > node!.clientWidth + 1)
    }

    updateOverflow()
    const observer = new ResizeObserver(updateOverflow)
    observer.observe(node)
    return () => observer.disconnect()
  }, [items])

  // While looping, keep the visible position silently re-centered on the
  // middle of the three copies so native scrolling never hits a real
  // start/end. Re-centering jumps by exactly one-copy-width between
  // pixel-identical content, so the jump is invisible.
  useEffect(() => {
    if (!looping) return
    const node = trackRef.current
    if (!node) return

    // The track's own padding is applied once across all three concatenated
    // copies, so scrollWidth / 3 isn't exactly one copy's width — dividing it
    // that way drifts the reposition target off the true snap-point spacing
    // by a couple of pixels, which fights CSS scroll-snap and can flip the
    // position back across the boundary it just crossed. Measuring the real
    // pixel gap between the first card of adjacent copies sidesteps that.
    function oneCopyWidth(): number {
      const first = node!.children[0] as HTMLElement | undefined
      const nextCopyFirst = node!.children[items.length] as HTMLElement | undefined
      if (!first || !nextCopyFirst) return node!.scrollWidth / LOOP_COPIES
      return nextCopyFirst.getBoundingClientRect().left - first.getBoundingClientRect().left
    }

    // A click-triggered `scrollBy({ behavior: 'smooth' })` animation can keep
    // emitting its own trailing 'scroll' events even after we've instantly
    // repositioned the track, so a simple "ignore the very next event" guard
    // can swallow a stray event that actually lands out of bounds. Instead,
    // track the exact target we last set and only suppress an event that
    // matches it; anything else (including a stray mid-animation event)
    // falls through to a fresh bounds check.
    let expectedAfterReposition: number | null = null

    function reposition(target: number) {
      expectedAfterReposition = target
      node!.scrollLeft = target
    }

    function center() {
      reposition(oneCopyWidth())
    }

    function handleScroll() {
      const scrollLeft = node!.scrollLeft
      const expected = expectedAfterReposition
      expectedAfterReposition = null
      if (expected !== null && Math.abs(scrollLeft - expected) < 2) return
      const oneCopy = oneCopyWidth()
      if (oneCopy <= 0) return
      if (scrollLeft < oneCopy) {
        reposition(scrollLeft + oneCopy)
      } else if (scrollLeft >= oneCopy * 2) {
        reposition(scrollLeft - oneCopy)
      }
    }

    center()
    node.addEventListener('scroll', handleScroll)
    const observer = new ResizeObserver(center)
    observer.observe(node)
    return () => {
      node.removeEventListener('scroll', handleScroll)
      observer.disconnect()
    }
  }, [looping, items])

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

  const copies = looping ? [0, 1, 2] : [0]

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
          {copies.flatMap((copy) =>
            items.map((item) => (
              <AnimeCard
                key={`${copy}:${item.animeId}`}
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
            )),
          )}
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
