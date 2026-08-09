import { createContext, useContext, useState, type ReactNode } from 'react'
import type { IncrementTarget, UserAnimeEntryDto } from '../api/types.ts'
import { updateEntry } from '../api/client.ts'
import { CompletionScoreOverlay } from '../components/CompletionScoreOverlay.tsx'

type PromptState = {
  animeId: number
  animeTitle: string
  pictureUrl: string | null
  currentScore: number | null
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

  async function setEpisodesWatched(target: IncrementTarget, value: number) {
    let saved: UserAnimeEntryDto
    try {
      saved = await updateEntry(target.animeId, { episodesWatched: value })
    } catch {
      // Leave the count as-is; the user can retry.
      return
    }
    target.onSaved(saved)

    const justCompleted = target.previousStatus !== 'Completed' && saved.status === 'Completed'
    if (!justCompleted) return

    setPrompt({
      animeId: target.animeId,
      animeTitle: target.animeTitle,
      pictureUrl: target.pictureUrl,
      currentScore: target.currentScore,
      onClosed: (saved) => target.onCompleted?.(saved),
    })
  }

  function increment(target: IncrementTarget) {
    return setEpisodesWatched(target, target.episodesWatched + 1)
  }

  function handleClose(saved: UserAnimeEntryDto | null) {
    const onClosed = prompt?.onClosed
    setPrompt(null)
    onClosed?.(saved)
  }

  return (
    <CompletionPromptContext.Provider value={{ increment, setEpisodesWatched }}>
      {children}
      {prompt && (
        <CompletionScoreOverlay
          key={prompt.animeId}
          animeId={prompt.animeId}
          animeTitle={prompt.animeTitle}
          pictureUrl={prompt.pictureUrl}
          currentScore={prompt.currentScore}
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
