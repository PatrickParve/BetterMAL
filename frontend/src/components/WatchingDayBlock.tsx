import { Link } from 'react-router-dom'
import type { RecapRowDto, RecapWatchingDayDto } from '../api/types.ts'
import { episodeRunsLabel, formatIsoDayWithWeekday, pickDisplayTitle } from '../utils/anime.ts'
import { RowPicture } from './RowPicture.tsx'
import { TruncatedTitle } from './TruncatedTitle.tsx'
import './WatchingDayBlock.css'

type WatchingDayBlockProps = {
  day: RecapWatchingDayDto
  // The day's place among the top days (1 to 10); omitted under the calendar.
  place?: number
  // The recap's counted set by anime id: a day names its anime by id alone, so the
  // title and picture are looked up here.
  itemsById: Map<number, RecapRowDto>
}

function episodesLabel(count: number): string {
  return `${count} ${count === 1 ? 'episode' : 'episodes'}`
}

// One watching day (list-recaps "The watching days overlay"): a heading with the
// day's place when it has one, the day with its weekday and its total, then one
// link row per anime in the day's order: picture, one-line title, which episodes,
// how many, and a Finished mark when the day reached the anime's last episode.
// Top days stacks up to ten of these, all expanded, and the calendar shows the one
// chosen day's beneath it, so both tabs draw a day the same way.
export function WatchingDayBlock({ day, place, itemsById }: WatchingDayBlockProps) {
  return (
    <section className="watching-day">
      <h3 className="watching-day__heading">
        {place !== undefined && <span className="watching-day__place">{place}</span>}
        <span className="watching-day__date">{formatIsoDayWithWeekday(day.date)}</span>
        <span className="watching-day__total">{episodesLabel(day.episodes)}</span>
      </h3>
      <ul className="watching-day__list">
        {day.anime.map((anime) => {
          const item = itemsById.get(anime.animeId)
          const title = item ? pickDisplayTitle(item.title, item.englishTitle) : `Anime ${anime.animeId}`
          return (
            <li key={anime.animeId}>
              <Link to={`/anime/${anime.animeId}`} className="watching-day__row" aria-label={title}>
                <RowPicture
                  src={item?.pictureUrl}
                  className="watching-day__picture"
                  placeholderClassName="watching-day__picture-placeholder"
                />
                <span className="watching-day__main">
                  <span className="watching-day__title">
                    <TruncatedTitle title={title} lines={1} className="watching-day__title-text" />
                  </span>
                  <span className="watching-day__runs">{episodeRunsLabel(anime.runs)}</span>
                  <span className="watching-day__tail">
                    <span className="watching-day__count">{anime.episodes}</span>
                    <span className="watching-day__finished">{anime.finished ? 'Finished' : ''}</span>
                  </span>
                </span>
              </Link>
            </li>
          )
        })}
      </ul>
    </section>
  )
}
