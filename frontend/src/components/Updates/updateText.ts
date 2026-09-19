import type { AnimeUpdateDto, AnimeUpdateKind } from '../../api/types.ts'

// The one place either surface (the navbar dropdown, the history overlay)
// derives an update's text — the role `buildHeadline`/`formatShortDate`
// played on `UpdatesSection` before the feed moved off the Home page. Both
// surfaces read from here so they can never describe the same update
// differently.

// Day, short month and year — schedule news is routinely about next year,
// and `5 Oct` alone doesn't say which October (design.md D7).
export function formatUpdateDate(value: string): string {
  return new Date(value).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })
}

function formatTime(value: string): string {
  return value.slice(0, 5)
}

function formatSlot(day: string, time: string): string {
  return `${day}s ${formatTime(time)}`
}

function buildNewsLine(kind: AnimeUpdateKind, item: AnimeUpdateDto): string | null {
  switch (kind) {
    case 'Announced':
      return 'Announced'
    case 'EpisodeCountReleased':
      return item.totalEpisodes !== null ? `Total episodes: ${item.totalEpisodes}` : null
    case 'StartDateReleased':
      return item.airedFrom !== null ? `Premiere: ${formatUpdateDate(item.airedFrom)}` : null
    case 'StartDateChanged': {
      if (!item.airedFrom || !item.previousStartDate) return null
      const to = new Date(item.airedFrom)
      const was = new Date(item.previousStartDate)
      // "Moved up"/"Moved down" rest on an up/down metaphor that reads both
      // ways depending on whether the reader pictures a calendar running up
      // or down (design.md D1) — "Moved earlier"/"Delayed" name the thing
      // that moved instead, so neither can be read in reverse. Where the two
      // dates coincide (the "to" value is live, the "was" value is
      // recorded, so a move that later reverses can land back on itself)
      // neither verb applies — the guard below reports the move without a
      // direction rather than claiming one between a date and itself.
      if (to.getTime() === was.getTime()) {
        return `Premiere moved to ${formatUpdateDate(item.airedFrom)}`
      }
      const verb = to > was ? 'Delayed' : 'Moved earlier'
      return `${verb} to ${formatUpdateDate(item.airedFrom)} · was ${formatUpdateDate(item.previousStartDate)}`
    }
    case 'BroadcastSlotChanged': {
      if (!item.currentBroadcastDayOfWeek || !item.currentBroadcastTime || !item.previousBroadcastDayOfWeek || !item.previousBroadcastTime)
        return null
      return `Now ${formatSlot(item.currentBroadcastDayOfWeek, item.currentBroadcastTime)} · was ${formatSlot(item.previousBroadcastDayOfWeek, item.previousBroadcastTime)}`
    }
    case 'EpisodesMoved':
      return item.movedEpisode !== null && item.newEpisodeDate && item.previousEpisodeDate
        ? `Episode ${item.movedEpisode} moved to ${formatUpdateDate(item.newEpisodeDate)} · was ${formatUpdateDate(item.previousEpisodeDate)}`
        : null
    default:
      return null
  }
}

// One line per kind the update covers, in the DTO's own kind order.
export function buildNewsLines(item: AnimeUpdateDto): string[] {
  return item.kinds
    .map((kind) => buildNewsLine(kind, item))
    .filter((line): line is string => line !== null)
}

// The current-value lines (episode count, premiere date), suppressed where a
// news line above already stated that fact. Suppression keys off the kinds
// rather than the rendered strings: the two are read from different places —
// the news lines from what was recorded, these from the anime's current
// cached record — and are specified to differ after a correction, so
// comparing strings would print both in exactly the case this suppression
// exists for.
export function buildFactLines(item: AnimeUpdateDto): string[] {
  const lines: string[] = []

  if (!item.kinds.includes('EpisodeCountReleased') && item.totalEpisodes !== null) {
    lines.push(`Total episodes: ${item.totalEpisodes}`)
  }

  if (!item.kinds.includes('StartDateReleased') && !item.kinds.includes('StartDateChanged') && item.airedFrom !== null) {
    lines.push(`Premiere: ${formatUpdateDate(item.airedFrom)}`)
  }

  return lines
}
