import type { AnimeSearchResult } from '../api/types.ts'
import { pickDisplayTitle } from '../utils/anime.ts'
import { RowPicture } from './RowPicture.tsx'
import { SeriesBadge } from './SeriesBadge.tsx'

// The anime and series rows of an anime type-ahead dropdown, shared by the navbar's
// SearchBar and the search page's bar in Anime mode, so the two show the same rows
// for the same text (navigation-and-search "Anime suggestions match the navbar's").
// They go inside a SearchField, which draws the list around them. A series row is the
// title with its badge beneath it and an anime row the title alone, both in the fixed
// row height SearchBar.css gives them.
export function AnimeSuggestionRows({
  results,
  onPickAnime,
  onPickSeries,
}: {
  results: AnimeSearchResult[]
  onPickAnime: (animeId: number) => void
  onPickSeries: (seriesId: number) => void
}) {
  return (
    <>
      {results.map((result) =>
        result.kind === 'series' ? (
          <li key={`series-${result.id}`}>
            <button
              type="button"
              className="search-bar__result search-bar__result--series"
              onClick={() => onPickSeries(result.id)}
            >
              {result.pictureUrl && <RowPicture src={result.pictureUrl} className="search-bar__thumb" />}
              <span className="search-bar__result-text">
                <span className="search-bar__result-title">{pickDisplayTitle(result.title, result.englishTitle)}</span>
                <SeriesBadge entryCount={result.entryCount} />
              </span>
            </button>
          </li>
        ) : (
          <li key={`anime-${result.id}`}>
            <button type="button" className="search-bar__result" onClick={() => onPickAnime(result.id)}>
              {result.pictureUrl && <RowPicture src={result.pictureUrl} className="search-bar__thumb" />}
              <span className="search-bar__result-title search-bar__result-title--multiline">
                {pickDisplayTitle(result.title, result.englishTitle)}
              </span>
            </button>
          </li>
        ),
      )}
    </>
  )
}
