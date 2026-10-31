import type { GuessNumberDto, GuessTagDto, GuessTextDto, GuessTheAnimeRowDto } from '../../api/types.ts'
import { mediaTypeLabel, pickDisplayTitle } from '../../utils/anime.ts'

// The row with nothing in it matching: no green, no arrow, and nothing said of a match.
// For the answer revealed after a loss, where every stat would otherwise match itself.
export function withoutMatches(row: GuessTheAnimeRowDto): GuessTheAnimeRowDto {
  const plainNumber = (stat: GuessNumberDto): GuessNumberDto => ({ ...stat, compared: null })
  const plainText = (stat: GuessTextDto): GuessTextDto => ({ ...stat, same: false })
  const plainTags = (tags: GuessTagDto[]): GuessTagDto[] => tags.map((tag) => ({ ...tag, same: false }))
  return {
    ...row,
    year: plainNumber(row.year),
    season: plainText(row.season),
    genres: plainTags(row.genres),
    episodes: plainNumber(row.episodes),
    studios: plainTags(row.studios),
    mediaType: plainText(row.mediaType),
    score: plainNumber(row.score),
  }
}

export const formatScore = (score: number) => score.toFixed(2)
export const capitalise = (text: string) => text.charAt(0).toUpperCase() + text.slice(1)

function numberSpoken(stat: GuessNumberDto, format: (value: number) => string): string {
  if (stat.value === null) return 'unknown'
  const shown = format(stat.value)
  if (stat.compared === 'Same') return `${shown}, matches`
  if (stat.compared === 'AnswerHigher') return `${shown}, the answer is higher`
  if (stat.compared === 'AnswerLower') return `${shown}, the answer is lower`
  return shown
}

function textSpoken(stat: GuessTextDto, label: (value: string) => string): string {
  if (stat.value === null) return 'unknown'
  return stat.same ? `${label(stat.value)}, matches` : label(stat.value)
}

function tagsSpoken(tags: GuessTagDto[]): string {
  if (tags.length === 0) return 'none'
  return tags.map((tag) => (tag.same ? `${tag.name} (matches)` : tag.name)).join(', ')
}

// What a screen reader hears when a row arrives: the anime, each stat, which match
// and which way the answer lies.
export function describeGuessRow(row: GuessTheAnimeRowDto): string {
  return [
    `${pickDisplayTitle(row.title, row.englishTitle)}.`,
    `Year ${numberSpoken(row.year, String)}.`,
    `Season ${textSpoken(row.season, capitalise)}.`,
    `Genres ${tagsSpoken(row.genres)}.`,
    `Episodes ${numberSpoken(row.episodes, String)}.`,
    `Studio ${tagsSpoken(row.studios)}.`,
    `Type ${textSpoken(row.mediaType, mediaTypeLabel)}.`,
    `MAL score ${numberSpoken(row.score, formatScore)}.`,
  ].join(' ')
}
