import { createContext, useContext, useMemo, useState, type ReactNode } from 'react'
import type { TopAnimeMediaType } from '../api/types.ts'
import { AnimeRankOverlay } from '../components/AnimeRankOverlay.tsx'

type OpenRankingOptions = {
  animeId?: number
  score?: number
  mediaType?: TopAnimeMediaType
  // ProfilePage's whole-library action needs to know when a save changes the
  // ranking beneath it, so it can refresh its own top-anime section
  // (add-anime-ranking tasks.md 5.6) — no other caller needs this.
  onSaved?: () => void
}

type Target = {
  animeId?: number
  score?: number
  mediaType: TopAnimeMediaType
  onSaved?: () => void
}

type AnimeRankContextValue = {
  openRanking: (options?: OpenRankingOptions) => void
}

const AnimeRankContext = createContext<AnimeRankContextValue | null>(null)

const MEDIA_TYPE_LABELS: Record<TopAnimeMediaType, string> = {
  all: 'All',
  tv: 'TV',
  movie: 'Movie',
  ova: 'OVA',
  ona: 'ONA',
  special: 'Specials',
}

// Mounts a single whole-library ranking editor instance at the app root,
// mirroring EntryEditorContext/CompletionPromptContext — any component
// anywhere can open it via useAnimeRank().openRanking(...) instead of each
// page managing its own instance (add-anime-ranking design.md D9).
export function AnimeRankProvider({ children }: { children: ReactNode }) {
  const [target, setTarget] = useState<Target | null>(null)

  const value = useMemo<AnimeRankContextValue>(
    () => ({
      openRanking: (options) =>
        setTarget({
          animeId: options?.animeId,
          score: options?.score,
          mediaType: options?.mediaType ?? 'all',
          onSaved: options?.onSaved,
        }),
    }),
    [],
  )

  return (
    <AnimeRankContext.Provider value={value}>
      {children}
      {target && (
        <AnimeRankOverlay
          key={`${target.animeId ?? 'library'}:${target.mediaType}`}
          mode="all"
          mediaType={target.mediaType}
          mediaTypeLabel={MEDIA_TYPE_LABELS[target.mediaType]}
          focusAnimeId={target.animeId}
          focusScore={target.score}
          onSaved={() => {
            setTarget(null)
            target.onSaved?.()
          }}
        />
      )}
    </AnimeRankContext.Provider>
  )
}

export function useAnimeRank(): AnimeRankContextValue {
  const ctx = useContext(AnimeRankContext)
  if (!ctx) throw new Error('useAnimeRank must be used within an AnimeRankProvider')
  return ctx
}
