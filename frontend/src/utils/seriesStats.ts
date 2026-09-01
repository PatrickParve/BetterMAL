import type { SeriesEntryDto, SeriesStatsDto } from '../api/types.ts'
import { isScoreRevealableStatus } from './anime.ts'

const ASSUMED_SECONDS_PER_EPISODE = 24 * 60

// Mirrors WatchMath.EpisodeSeconds exactly: the entry's own average episode
// duration when known, else the app's assumed 24-minutes-per-episode figure.
export function episodeSeconds(entry: SeriesEntryDto): number {
  return entry.averageEpisodeDurationSeconds ?? ASSUMED_SECONDS_PER_EPISODE
}

// An entry's effective watched-episode figure (polish-rewatch design.md D2):
// a Rewatching entry counts as fully watched — the greater of its own
// episodesWatched and its aired-so-far figure — since entering Rewatching
// resets episodesWatched to 0. Every other status reads episodesWatched as
// recorded. Mirrors the backend's WatchMath.EffectiveWatchedEpisodes exactly,
// using this entry's own airedEpisodes (null-means-unknown, treated as "fall
// back to episodesWatched") rather than restating that fallback here.
export function effectiveWatchedEpisodes(entry: SeriesEntryDto): number {
  const watched = entry.entry?.episodesWatched ?? 0
  if (entry.entry?.status !== 'Rewatching') return watched
  return Math.max(watched, entry.airedEpisodes ?? watched)
}

// Mirrors WatchMath.RewatchEpisodesIncludingCurrentRun exactly: completed
// rewatch runs — the rewatch count times the entry's published total,
// falling back to episodes watched as the per-run baseline when no total is
// published — plus, while the entry is currently Rewatching, the episodes
// watched so far in the run still in progress. The two terms can't
// double-count: entering Rewatching resets episodesWatched to 0, and the
// rewatch count only increments once a run finishes.
export function rewatchEpisodesIncludingCurrentRun(entry: SeriesEntryDto): number {
  const rewatchCount = entry.entry?.rewatchCount ?? 0
  const episodesWatched = entry.entry?.episodesWatched ?? 0
  const rewatchOnly = rewatchCount * (entry.totalEpisodes ?? episodesWatched)
  return rewatchOnly + (entry.entry?.status === 'Rewatching' ? episodesWatched : 0)
}

// The member basis every server figure uses (design.md decision 6): real
// extra members, excluding related entries — anime shown in More because a
// main-line member relates to them, without being members of the series —
// which the server excludes from every average and stat.
export function realExtras(extras: SeriesEntryDto[]): SeriesEntryDto[] {
  return extras.filter((e) => !e.isRelatedEntry)
}

// Replaces base's edit-sensitive fields with client computations over the
// page's own entry arrays (design.md decision 5), so an in-place edit shows
// immediately and on every route rather than only in the frozen per-pick
// snapshot the server delivered. mainLineVisible is the picked route's
// visible main line; extras must already be the real extras (see realExtras
// above) — passing the raw display list would re-base these figures onto a
// member set the server never used (design.md decision 6). Everything else
// on base passes through untouched: it either can't change from an edit
// (episode/runtime totals, aired figures, studios, genres, longest gap,
// member counts) or is pick-independent and read separately from
// series.stats (the three tie lists — design.md decision 4).
export function deriveSeriesStats(base: SeriesStatsDto, mainLineVisible: SeriesEntryDto[], extras: SeriesEntryDto[]): SeriesStatsDto {
  const myWatchedEpisodes = mainLineVisible.reduce((sum, e) => sum + effectiveWatchedEpisodes(e), 0)
  const myWatchedSeconds = mainLineVisible.reduce((sum, e) => sum + effectiveWatchedEpisodes(e) * episodeSeconds(e), 0)
  const myRewatchedSeconds = mainLineVisible.reduce((sum, e) => sum + rewatchEpisodesIncludingCurrentRun(e) * episodeSeconds(e), 0)
  // A rewatch can only follow a completed run, so a Rewatching entry counts
  // as completed here too (polish-rewatch design.md D2).
  const entriesCompleted = mainLineVisible.filter((e) => e.entry?.status === 'Completed' || e.entry?.status === 'Rewatching').length
  const extrasCompleted = extras.filter((e) => e.entry?.status === 'Completed').length
  // Mirrors SeriesAverages.MainLineSettledByMe exactly.
  const finishedAiring = mainLineVisible.filter((e) => e.airingStatus === 'finished_airing')
  const mainLineCompletedByMe = finishedAiring.length > 0 && finishedAiring.every((e) => isScoreRevealableStatus(e.entry?.status))

  return {
    ...base,
    myWatchedEpisodes,
    myWatchedSeconds,
    myRewatchedSeconds,
    entriesCompleted,
    extrasCompleted,
    mainLineCompletedByMe,
  }
}
