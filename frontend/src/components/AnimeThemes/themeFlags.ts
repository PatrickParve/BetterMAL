import type { AnimeThemeDto } from '../../api/types.ts'

// How much of a theme's video a page may show (anime-themes "Flagged themes stay
// listed and hold their video back"):
//   free - the video plays as usual
//   held - the audio plays first, with a note saying why, and the video only when I
//          ask for it
// A video is held while its theme is a spoiler, and while it is NSFW and Hide NSFW is
// on; a theme flagged both stays held while either reason applies. There is no third
// rule that shuts the video away: a held video is always one press from playing.
export type VideoRule = 'free' | 'held'

// Why a theme's video is held, in the order a note gives them.
export type HeldReason = 'nsfw' | 'spoiler'

export function heldReasons(theme: Pick<AnimeThemeDto, 'nsfw' | 'spoiler'>, hideNsfw: boolean): HeldReason[] {
  const reasons: HeldReason[] = []
  if (theme.nsfw && hideNsfw) reasons.push('nsfw')
  if (theme.spoiler) reasons.push('spoiler')
  return reasons
}

export function videoRule(theme: Pick<AnimeThemeDto, 'nsfw' | 'spoiler'>, hideNsfw: boolean): VideoRule {
  return heldReasons(theme, hideNsfw).length > 0 ? 'held' : 'free'
}

// The note under a held theme's audio: every reason that applies, and null when the
// video isn't held.
export function heldNote(reasons: readonly HeldReason[]): string | null {
  const nsfw = reasons.includes('nsfw')
  const spoiler = reasons.includes('spoiler')
  if (nsfw && spoiler) return 'Its video is marked NSFW and may spoil the show.'
  if (nsfw) return 'Its video is marked NSFW.'
  if (spoiler) return 'Its video may spoil the show.'
  return null
}

// A theme can be played when it has a file of either kind to play.
export function isPlayable(theme: Pick<AnimeThemeDto, 'videoUrl' | 'audioUrl'>): boolean {
  return theme.videoUrl !== null || theme.audioUrl !== null
}

// AnimeThemes writes where a theme plays as a range or a list ("1-12", "1-12, 13-24").
// One episode alone reads as singular; whatever else it is, it is shown as written.
export function formatEpisodes(episodes: string | null): string | null {
  const text = episodes?.trim()
  if (!text) return null
  return /^\d+$/.test(text) ? `Episode ${text}` : `Episodes ${text}`
}

// The row's name for a screen reader and the play control's label: "OP1, Song title",
// or just the label where the title isn't known.
export function themeName(theme: Pick<AnimeThemeDto, 'label' | 'songTitle'>): string {
  return theme.songTitle ? `${theme.label}, ${theme.songTitle}` : theme.label
}
