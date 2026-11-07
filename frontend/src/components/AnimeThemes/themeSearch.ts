import type { AnimeThemeDto } from '../../api/types.ts'

// Matching for the series page's song search (series-page "The series page's
// openings and endings can be searched"). Plain TypeScript with only a type import,
// so a scratch Node script can run it with `--experimental-strip-types`.

// Case and accents folded away, so "kyo" finds "Kyō": decompose each letter from its
// accent, drop the accents (combining marks), lower-case what is left. "Kyou" is a
// different spelling of the long vowel and doesn't find "Kyō".
function fold(text: string): string {
  return text.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase()
}

/** The words of a search, folded. An empty query, or one of only spaces, has none: that is no search. */
export function searchWords(query: string): string[] {
  return fold(query)
    .split(/\s+/)
    .filter((word) => word !== '')
}

/**
 * Whether every word appears in the song title or in one of the artists' names, in
 * either, in any order. Only those two are searched: the label ("OP1") is not, since
 * "op" and "ed" sit inside many titles and would stop filtering by kind, and the
 * anime's title is the group's heading, not a song's. With no words every theme matches.
 */
export function themeMatches(theme: Pick<AnimeThemeDto, 'songTitle' | 'artists'>, words: readonly string[]): boolean {
  if (words.length === 0) return true
  const parts = theme.songTitle ? [theme.songTitle, ...theme.artists] : theme.artists
  const text = parts.map(fold).join(' ')
  return words.every((word) => text.includes(word))
}
