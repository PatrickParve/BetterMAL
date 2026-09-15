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
      saved = await updateEntry(target.animeId, { ...target.extraEdit, episodesWatched: value })
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
    // scored rewatch already carries a score from its first viewing, so it's
    // the only rewatch left out here (an unscored rewatch prompts exactly
    // like a first completion). Every completion reachable from this
    // progress-row path now concerns an anime that has aired in full (the
    // backend refuses any other completion), so `completedAt` no longer
    // distinguishes a real finish from anything else — dropped.
    // "Triggering view refreshes after the completion prompt closes" — only a
    // saved score refreshes the triggering view, so a completion that never
    // opens the prompt, or whose prompt closes without saving, reports itself
    // through onPhantomCompleted instead of onCompleted.
    const becameCompleted = target.previousStatus !== 'Completed' && saved.status === 'Completed'
    if (!becameCompleted) return

    const isRewatch = target.previousStatus === 'Rewatching'
    if (isRewatch && target.currentScore != null) {
      target.onPhantomCompleted?.({ kind: 'RewatchCompletion', rewatchCountAfter: saved.rewatchCount })
      return
    }

    setPrompt({
      animeId: target.animeId,
      animeTitle: target.animeTitle,
      pictureUrl: target.pictureUrl,
      currentScore: target.currentScore,
      mediaType: target.mediaType,
      onClosed: (savedResult) => {
        if (savedResult !== null) {
          target.onCompleted?.(savedResult)
        } else {
          target.onPhantomCompleted?.(
            isRewatch ? { kind: 'RewatchCompletion', rewatchCountAfter: saved.rewatchCount } : { kind: 'FirstCompletion' },
          )
        }
      },
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
