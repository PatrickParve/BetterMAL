import { useEffect, useRef, useState } from 'react'
import { AnimeCard } from './AnimeCard.tsx'
import { ProgressBar } from './ProgressBar.tsx'
import type { CurrentlyWatchingItemDto, IncrementTarget, PhantomCompletion, UserAnimeEntryEditRequest } from '../api/types.ts'
import { useEpisodeIncrement, useSetEpisodesWatched } from '../context/CompletionPromptContext.tsx'
import { hasAiredEpisodes, pickDisplayTitle } from '../utils/anime.ts'
import './CurrentlyWatchingCarousel.css'

type CurrentlyWatchingCarouselProps = {
  items: CurrentlyWatchingItemDto[]
  onEpisodesWatchedChange: (animeId: number, episodesWatched: number) => void
  onCompleted: () => void
}

// main-dashboard, "A completion left in Currently watching can be undone
// from its card" (design D5): turns a phantom-completion record into the
// corrective edit that undoes it, sent alongside the lowered count.
function buildCorrectiveEdit(record: {
  completion: PhantomCompletion
  completedAtBefore: string | null
}): Omit<UserAnimeEntryEditRequest, 'episodesWatched'> {
  const { completion, completedAtBefore } = record
  if (completion.kind === 'FirstCompletion') {
    // An explicit status different from Completed skips the automatic
    // transitions and takes ApplyStatus's last branch, landing back in
    // Watching with the finish date restored.
    return { status: 'Watching', completedAt: completedAtBefore }
  }
  // No status sent, so the automatic drop below the total lands in
  // Rewatching on its own; the rewatch count is set back to what it was
  // before this completion raised it by one.
  return { rewatchCount: completion.rewatchCountAfter - 1, completedAt: completedAtBefore }
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
  const [bounds, setBounds] = useState({ overflowing: false, atStart: true, atEnd: true })
  // Completions left in the row without a saved score, keyed by anime id,
  // each with the card's finish date as loaded (main-dashboard: "A
  // completion left in Currently watching can be undone from its card").
  const [phantoms, setPhantoms] = useState<ReadonlyMap<number, { completion: PhantomCompletion; completedAtBefore: string | null }>>(
    () => new Map(),
  )
  const incrementEpisode = useEpisodeIncrement()
  const setEpisodesWatched = useSetEpisodesWatched()

  // Arrows only make sense when the row actually overflows, and each one
  // disables once the row can no longer move further in its direction —
  // recompute on resize (font/zoom/window changes), on scroll (arrow click,
  // trackpad, touch) and whenever the item count changes.
  useEffect(() => {
    const node = trackRef.current
    if (!node || items.length === 0) return

    function updateBounds() {
      const { scrollLeft, scrollWidth, clientWidth } = node!
      setBounds({
        overflowing: scrollWidth > clientWidth + 1,
        atStart: scrollLeft <= 1,
        atEnd: scrollLeft >= scrollWidth - clientWidth - 1,
      })
    }

    updateBounds()
    const observer = new ResizeObserver(updateBounds)
    observer.observe(node)
    node.addEventListener('scroll', updateBounds)
    return () => {
      observer.disconnect()
      node.removeEventListener('scroll', updateBounds)
    }
  }, [items])

  // Count patches keep an id in `items`, so only a server read prunes a
  // record — that's when a card's completion is finally reflected as gone.
  useEffect(() => {
    setPhantoms((prev) => {
      const ids = new Set(items.map((item) => item.animeId))
      let changed = false
      const next = new Map(prev)
      for (const animeId of next.keys()) {
        if (!ids.has(animeId)) {
          next.delete(animeId)
          changed = true
        }
      }
      return changed ? next : prev
    })
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
    const phantom = phantoms.get(item.animeId)
    return {
      animeId: item.animeId,
      animeTitle: pickDisplayTitle(item.title, item.englishTitle),
      pictureUrl: item.pictureUrl,
      episodesWatched: item.episodesWatched,
      // main-dashboard: this section also holds Rewatching entries, so the
      // pre-increment status has to come from the item itself rather than
      // being assumed Watching — otherwise a rewatch reaching the total would
      // incorrectly trip the completion-score prompt. Home never patches
      // status itself, only the count (onSaved below), so this always
      // reflects the entry's real status, which the undo below relies on
      // (design D6).
      previousStatus: item.status,
      currentScore: item.myScore,
      // CurrentlyWatchingItemDto carries no media type — the completion
      // prompt's save-and-rank action stays hidden for a Music/CM/PV entry
      // completed from here (null means unavailable, not "not short-form").
      mediaType: null,
      onSaved: (saved) => {
        onEpisodesWatchedChange(item.animeId, saved.episodesWatched)
        // Only runs after a successful save, so a failed undo keeps its
        // record and a retry sends the same corrective edit.
        setPhantoms((prev) => {
          if (!prev.has(item.animeId)) return prev
          const next = new Map(prev)
          next.delete(item.animeId)
          return next
        })
      },
      onCompleted,
      onPhantomCompleted: (completion) =>
        setPhantoms((prev) => new Map(prev).set(item.animeId, { completion, completedAtBefore: item.completedAt })),
      extraEdit: phantom ? buildCorrectiveEdit(phantom) : undefined,
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
        {bounds.overflowing && (
          <button
            type="button"
            className="carousel__arrow"
            onClick={() => scroll(-1)}
            disabled={bounds.atStart}
            aria-label="Scroll left"
          >
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
        {bounds.overflowing && (
          <button
            type="button"
            className="carousel__arrow"
            onClick={() => scroll(1)}
            disabled={bounds.atEnd}
            aria-label="Scroll right"
          >
            ›
          </button>
        )}
      </div>
    </section>
  )
}
