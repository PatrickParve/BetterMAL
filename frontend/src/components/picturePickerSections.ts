import type {
  AnimeTmdbPicturesDto,
  SeriesTmdbPicturesDto,
  TmdbLanguageGroupDto,
} from '../api/types.ts'
import { TMDB_LANGUAGE_LABELS } from '../utils/anime.ts'

// The picture picker's data model (artwork-selection "The picture picker",
// design D12), kept beside PicturePickerOverlay and out of its .tsx so that
// file exports only the component. Each page builds its own sections from the
// DTOs it holds and hands them to the overlay: an anime's picker divides TMDB
// by scope and then language, a series' by language alone.

// width/height are TMDB's own, when known — the overlay sets them on the <img>
// so a backdrop reserves its landscape footprint before it decodes. A MAL
// option carries neither.
export type PickerOption = { url: string; width?: number; height?: number }

// A group is one openable run of options. `key` is stable across renders (the
// page's open/closed state is keyed by it). `heading` is left out only for a
// section's single group, whose toggle the section's own heading is.
export type PickerGroup = { key: string; heading?: string; options: PickerOption[] }

export type PickerSection = { key: string; heading: string; groups: PickerGroup[] }

// The two group keys the page never derives from data: the MyAnimeList group,
// and the group the overlay adds for a current picture no section lists.
export const MAL_GROUP_KEY = 'mal'
export const CURRENT_GROUP_KEY = 'current'

// The MyAnimeList section — nothing at all when MAL holds no picture, since a
// section with no options is never shown.
export function malSections(urls: string[]): PickerSection[] {
  if (urls.length === 0) return []
  return [
    {
      key: MAL_GROUP_KEY,
      heading: 'MyAnimeList',
      groups: [{ key: MAL_GROUP_KEY, options: urls.map((url) => ({ url })) }],
    },
  ]
}

function tmdbLanguageGroups(keyPrefix: string, groups: TmdbLanguageGroupDto[]): PickerGroup[] {
  return groups.map((group) => ({
    key: `${keyPrefix}:${group.language}`,
    heading: TMDB_LANGUAGE_LABELS[group.language],
    options: group.pictures.map(({ url, width, height }) => ({ url, width, height })),
  }))
}

// An anime's TMDB sections: one per scope (Series, Season naming TMDB's own
// season number, Movie), each divided by language. Scopes and language groups
// arrive already ordered and never empty. Group keys are `tmdb:series:{lang}`,
// `tmdb:season:{n}:{lang}` and `tmdb:movie:{lang}`.
export function animeTmdbSections(tmdb: AnimeTmdbPicturesDto | null): PickerSection[] {
  return (tmdb?.scopes ?? []).map(({ scope, seasonNumber, languages }) => {
    const key = scope === 'Season' ? `tmdb:season:${seasonNumber}` : `tmdb:${scope.toLowerCase()}`
    const heading = scope === 'Season' ? `TMDB · Season ${seasonNumber}` : `TMDB · ${scope}`
    return { key, heading, groups: tmdbLanguageGroups(key, languages) }
  })
}

// A series' TMDB section: one section divided by language only, the
// franchise's series, season and movie images mixed within each group. Group
// keys are `tmdb:{lang}`.
export function seriesTmdbSections(tmdb: SeriesTmdbPicturesDto): PickerSection[] {
  if (tmdb.languages.length === 0) return []
  return [{ key: 'tmdb', heading: 'TMDB', groups: tmdbLanguageGroups('tmdb', tmdb.languages) }]
}

// Which groups a picker opens with (design D12): the MyAnimeList group, and
// the group holding the current picture so the selection is visible and
// marked — or the overlay's own "Current picture" group when no section lists
// it. Every other group starts closed, and a closed group downloads nothing.
export function defaultOpenGroups(sections: PickerSection[], current: string | null): Set<string> {
  const open = new Set<string>([MAL_GROUP_KEY])
  if (current === null) return open

  for (const section of sections) {
    for (const group of section.groups) {
      if (group.options.some((option) => option.url === current)) {
        open.add(group.key)
        return open
      }
    }
  }

  open.add(CURRENT_GROUP_KEY)
  return open
}

// What "more than one picture to choose between" counts (anime-detail "The
// detail page offers a picture choice", series-page "picture and title
// controls"): distinct URLs across every section together with the current
// picture, so a lone MAL picture plus TMDB images still gets its control, and
// a lone picture that is also the current one still does not.
export function pickerOptionCount(sections: PickerSection[], current: string | null): number {
  const urls = new Set<string>()
  for (const section of sections) {
    for (const group of section.groups) {
      for (const option of group.options) urls.add(option.url)
    }
  }
  if (current !== null) urls.add(current)
  return urls.size
}
