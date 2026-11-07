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

// Which kind of file a player begins with (anime-themes "Playing a theme"). The
// anime and series pages begin with the video; the search page's OP/ED results begin
// with the audio, with the video one press away (theme-search "OP/ED results are
// songs that play in place").
export type ThemeStart = 'video' | 'audio'

// Whether a player begins on the video (true) or on the audio alone (false).
//   video  the video, unless it is held or there is none: then the audio, with the
//          note and the way to the video when it is held. This is how every player
//          began before the search results had a say, so it is the default.
//   audio  the audio, whenever the song has one. A song with no audio begins on its
//          video exactly as above: its video unless that is held, and a held one waits
//          behind the note and "Show video", as a held theme with no audio does anywhere.
// A held theme never begins on its video, whichever is asked for.
export function startsOnVideo(
  theme: Pick<AnimeThemeDto, 'videoUrl' | 'audioUrl'>,
  held: boolean,
  startWith: ThemeStart,
): boolean {
  if (theme.videoUrl === null || held) return false
  return startWith === 'video' || theme.audioUrl === null
}

// Names one theme across a page: AnimeThemes' theme id names a row only within an
// anime, and an anime listed under two MAL ids repeats its ids under each.
export function themeKey(animeId: number, themeId: number): string {
  return `${animeId}:${themeId}`
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

// The line beneath a song's title in the search page's OP/ED dropdown: its label, the
// anime's title (already chosen for display) and the artists, as one line the row cuts
// short with an ellipsis. What isn't known is left out, so a song with no artists has no
// trailing separator.
export function themeSuggestionLine(theme: Pick<AnimeThemeDto, 'label' | 'artists'>, animeTitle: string): string {
  return [theme.label, animeTitle, theme.artists.join(', ')].filter((part) => part).join(' · ')
}
