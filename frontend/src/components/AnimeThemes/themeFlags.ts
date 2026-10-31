import type { AnimeThemeDto } from '../../api/types.ts'

// How much of a theme's video a page may show (anime-themes "Flagged themes stay
// listed and hold their video back", design.md D6):
//   free   - the video plays as usual
//   held   - the audio plays first, and the video only when I ask for it
//   locked - the audio alone, with no way to the video (NSFW while Hide NSFW is on)
// A theme flagged both follows whichever shows less: with Hide NSFW off that is
// the spoiler's rule, and with it on the NSFW one.
export type VideoRule = 'free' | 'held' | 'locked'

export function videoRule(theme: Pick<AnimeThemeDto, 'nsfw' | 'spoiler'>, hideNsfw: boolean): VideoRule {
  if (theme.nsfw && hideNsfw) return 'locked'
  if (theme.spoiler) return 'held'
  return 'free'
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
