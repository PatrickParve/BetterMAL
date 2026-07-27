import { Link } from 'react-router-dom'
import type { AiringTodayItemDto } from '../api/types.ts'
import { pickDisplayTitle } from '../utils/anime.ts'
import './AiringTodayList.css'

type AiringTodayListProps = {
  items: AiringTodayItemDto[]
}

// "Airing today" column: my-list anime whose broadcast day, converted to
// local time, is today. Rendered as a single-column list, not a grid. Each
// row is a larger thumbnail beside two stacked lines: `time : Ep N` (time
// alone when the episode number can't be resolved) over the title, clamped
// to two lines.
export function AiringTodayList({ items }: AiringTodayListProps) {
  return (
    <section className="dashboard-section">
      <h2>Airing today</h2>
      {items.length === 0 ? (
        <p className="airing-today__empty">Nothing airing today.</p>
      ) : (
        <ul className="airing-today__list">
          {items.map((item) => (
            <li key={item.animeId}>
              <Link to={`/anime/${item.animeId}`} className="airing-today__row">
                {item.pictureUrl ? (
                  <img src={item.pictureUrl} alt="" className="airing-today__thumb" />
                ) : (
                  <div className="airing-today__thumb airing-today__thumb--placeholder" aria-hidden="true" />
                )}
                <span className="airing-today__text">
                  <span className="airing-today__meta">
                    {item.localTime}
                    {item.episodeNumber !== null && ` : Ep ${item.episodeNumber}`}
                  </span>
                  <span className="airing-today__title">{pickDisplayTitle(item.title, item.englishTitle)}</span>
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
