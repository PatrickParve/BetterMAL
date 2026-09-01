import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import type { IncrementTarget, UserAnimeEntryDto } from '../api/types.ts'
import { ApiError, updateEntry } from '../api/client.ts'
import { CompletionScoreOverlay } from '../components/CompletionScoreOverlay.tsx'
import { useActionFailure } from './ActionFailureContext.tsx'

type PromptState = {
  animeId: number
  animeTitle: string
  pictureUrl: string | null
  currentScore: number | null
  // anime-ranking: carried through so the prompt can decide whether
  // save-and-rank is offered for the chosen score (add-anime-ranking
  // tasks.md 6.5) — the entry's status is always Completed by the time this
  // prompt opens, so only the media type needs to travel with it.
  mediaType: string | null
  onClosed: (saved: UserAnimeEntryDto | null) => void
}

type CompletionPromptContextValue = {
  increment: (target: IncrementTarget) => Promise<void>
  setEpisodesWatched: (target: IncrementTarget, value: number) => Promise<void>
}

const CompletionPromptContext = createContext<CompletionPromptContextValue | null>(null)

// Mounts a single overlay instance at the app root, mirroring
// EntryEditorContext. Both the "+" button and the inline episode-count edit
// route through here — via useEpisodeIncrement() or useSetEpisodesWatched()
// — instead of calling updateEntry directly, so the completion prompt can't
// be forgotten at a new call site.
export function CompletionPromptProvider({ children }: { children: ReactNode }) {
  const [prompt, setPrompt] = useState<PromptState | null>(null)
  const reportFailure = useActionFailure()

  // Closes over reportFailure alongside setPrompt; both are stable for the
  // life of the app (reportFailure via ActionFailureProvider's own stable
  // useCallback chain), so this identity still never changes.
  const setEpisodesWatched = useCallback(async (target: IncrementTarget, value: number) => {
    let saved: UserAnimeEntryDto
    try {
      saved = await updateEntry(target.animeId, { episodesWatched: value })
    } catch (err) {
      // Leave the count as-is; the notice is what accounts for it
      // (action-failure-notices: "The system SHALL NOT silently roll an
      // action back and say nothing").
      reportFailure({
        title: `Couldn't update ${target.animeTitle}`,
        reason: err instanceof ApiError ? err.reason : null,
      })
      return
    }
    target.onSaved(saved)

    // list-editing: "Completion prompt fires only on entering Completed" — a
    // rewatch reaching the total re-completes the entry but already carries a
    // score from its first viewing, so it's excluded here alongside an entry
    // that was already Completed. `completedAt` being null distinguishes a
    // currently-airing anime "caught up" on what's aired (gate-editing-on-
    // aired-episodes: no finish date while still airing) from an actual
    // finish — the former isn't done yet, so it shouldn't be asked to score.
    const justCompleted =
      target.previousStatus !== 'Completed' &&
      target.previousStatus !== 'Rewatching' &&
      saved.status === 'Completed' &&
      saved.completedAt !== null
    if (!justCompleted) return

    setPrompt({
      animeId: target.animeId,
      animeTitle: target.animeTitle,
      pictureUrl: target.pictureUrl,
      currentScore: target.currentScore,
      mediaType: target.mediaType,
      onClosed: (saved) => target.onCompleted?.(saved),
    })
  }, [reportFailure])

  const increment = useCallback(
    (target: IncrementTarget) => setEpisodesWatched(target, target.episodesWatched + 1),
    [setEpisodesWatched],
  )

  function handleClose(saved: UserAnimeEntryDto | null) {
    const onClosed = prompt?.onClosed
    setPrompt(null)
    onClosed?.(saved)
  }

  const value = useMemo(() => ({ increment, setEpisodesWatched }), [increment, setEpisodesWatched])

  return (
    <CompletionPromptContext.Provider value={value}>
      {children}
      {prompt && (
        <CompletionScoreOverlay
          key={prompt.animeId}
          animeId={prompt.animeId}
          animeTitle={prompt.animeTitle}
          pictureUrl={prompt.pictureUrl}
          currentScore={prompt.currentScore}
          mediaType={prompt.mediaType}
          onClose={handleClose}
        />
      )}
    </CompletionPromptContext.Provider>
  )
}

export function useEpisodeIncrement(): (target: IncrementTarget) => Promise<void> {
  const ctx = useContext(CompletionPromptContext)
  if (!ctx) throw new Error('useEpisodeIncrement must be used within a CompletionPromptProvider')
  return ctx.increment
}

export function useSetEpisodesWatched(): (target: IncrementTarget, value: number) => Promise<void> {
  const ctx = useContext(CompletionPromptContext)
  if (!ctx) throw new Error('useSetEpisodesWatched must be used within a CompletionPromptProvider')
  return ctx.setEpisodesWatched
}
