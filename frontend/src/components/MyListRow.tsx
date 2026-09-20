import { memo } from 'react'
import { Link } from 'react-router-dom'
import type { MyListItemDto } from '../api/types.ts'
import { airingStatusShortLabel, hasAiredEpisodes, isScoreRevealableStatus, mediaTypeLabel, pickDisplayTitle, STATUS_CLASS } from '../utils/anime.ts'
import { ProgressBar } from './ProgressBar.tsx'
import { RowPicture } from './RowPicture.tsx'
import { ScoreValue } from './ScoreValue.tsx'

const SCORE_OPTIONS = Array.from({ length: 10 }, (_, i) => i + 1)

type MyListRowProps = {
  item: MyListItemDto
  rank?: number
  showAiringBadge: boolean
  incrementPending: boolean
  scorePending: boolean
  onEdit: (item: MyListItemDto) => void
  onIncrement: (item: MyListItemDto) => void
  onSetWatched: (item: MyListItemDto, value: number) => void
  onScoreChange: (item: MyListItemDto, score: number) => void
}

// One my-list row, memoised so a page-level state change (a filter keystroke,
// another row's pending flag) doesn't redraw every row — only the row whose
// own props actually changed re-renders. For the memo to bite, every
// callback prop must be a stable (useCallback) reference from the page; see
// MyListPage and D2.
export const MyListRow = memo(function MyListRow({
  item,
  rank,
  showAiringBadge,
  incrementPending,
  scorePending,
  onEdit,
  onIncrement,
  onSetWatched,
  onScoreChange,
}: MyListRowProps) {
  const displayTitle = pickDisplayTitle(item.title, item.englishTitle)
  const airingLabel = showAiringBadge ? airingStatusShortLabel(item.airingStatus) : null
  // list-editing: no progress or score control for an anime that has aired
  // no episode — the cells stay in the row (below) so columns stay aligned.
  const aired = hasAiredEpisodes(item.airingStatus, item.episodesAired)

  return (
    <li className={`my-list-row my-list-row--${STATUS_CLASS[item.entry.status]}`}>
      {rank !== undefined && <span className="my-list-row__rank">#{rank}</span>}
      <Link to={`/anime/${item.animeId}`} className="my-list-row__link">
        <RowPicture src={item.pictureUrl} className="my-list-row__picture" />
        <span className="my-list-row__info">
          <span className="my-list-row__title" title={displayTitle}>
            {displayTitle}
          </span>
          <span className="my-list-row__type">
            {mediaTypeLabel(item.mediaType)}
            {airingLabel && <span className="my-list-row__airing-badge"> · {airingLabel}</span>}
          </span>
        </span>
      </Link>
      <span className="my-list-row__progress">
        {aired && (
          <ProgressBar
            watched={item.entry.episodesWatched}
            total={item.totalEpisodes}
            aired={item.airingStatus === 'currently_airing' ? item.episodesAired : null}
            onIncrement={() => onIncrement(item)}
            onSetWatched={(value) => onSetWatched(item, value)}
            max={item.episodesAired ?? item.totalEpisodes}
            incrementPending={incrementPending}
            incrementLabel={`Increment episodes watched for ${displayTitle}`}
          />
        )}
      </span>
      <span className="my-list-row__mal-score score--mal">
        <ScoreValue value={item.malScore} completed={isScoreRevealableStatus(item.entry.status)} />
      </span>
      <span className="my-list-row__my-score">
        {aired && (
          <select
            className={`my-list-row__score-select${
              item.entry.myScore ? ' my-list-row__score-select--mine' : ''
            }`}
            value={item.entry.myScore ?? 0}
            disabled={scorePending}
            onChange={(event) => onScoreChange(item, Number(event.target.value))}
            aria-label={`Set your score for ${displayTitle}`}
          >
            <option value={0}>—</option>
            {SCORE_OPTIONS.map((score) => (
              <option key={score} value={score}>
                {score}
              </option>
            ))}
          </select>
        )}
      </span>
      <button type="button" className="my-list-row__edit" onClick={() => onEdit(item)}>
        Edit
      </button>
    </li>
  )
})
