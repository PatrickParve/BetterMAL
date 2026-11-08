import { Fragment } from 'react'
import { Link } from 'react-router-dom'
import type { TasteComboDto } from '../../api/types.ts'
import './FavouriteCombos.css'

// A combination's link into my list: every genre of it ticked in the Genre filter
// (which keeps entries carrying every ticked genre) and every status but Plan to
// watch selected, so the list holds exactly the anime the row counted
// (profile-stats "Favourite combos", add-genre-and-studio-stats design.md D14).
function comboLink(genres: string[]): string {
  const params = new URLSearchParams({ focus: 'watched' })
  for (const genre of genres) params.append('genre', genre)
  return `/my-list?${params.toString()}`
}

function ComboList({ combos }: { combos: TasteComboDto[] }) {
  return (
    <ol className="favourite-combos__list">
      {combos.map((combo) => (
        <li key={combo.genres.join('+')}>
          <Link
            to={comboLink(combo.genres)}
            className="favourite-combos__link"
            aria-label={`${combo.genres.join(', ')}: ${combo.animeCount} anime`}
          >
            <span className="favourite-combos__chips">
              {combo.genres.map((genre, index) => (
                <Fragment key={genre}>
                  {index > 0 && (
                    <span className="favourite-combos__plus" aria-hidden="true">
                      +
                    </span>
                  )}
                  <span className="favourite-combos__chip">{genre}</span>
                </Fragment>
              ))}
            </span>
            <span className="favourite-combos__count">{combo.animeCount}</span>
          </Link>
        </li>
      ))}
    </ol>
  )
}

// The profile's Favourite combos box (profile-stats): the genre pairs and trios I
// watch most, side by side, each row the genres as chips joined by "+" and how
// many anime carry all of them. The two lists carry no headings of their own: the
// chips say which is which. A list with nothing to show is not drawn and the other
// takes the box; with neither, there is no box. The genre family bands the title
// and tints a row on hover, as it does on Top genres.
export function FavouriteCombos({ pairs, trios }: { pairs: TasteComboDto[]; trios: TasteComboDto[] }) {
  if (pairs.length === 0 && trios.length === 0) return null

  return (
    <section className="profile-box family--genre">
      <h2 className="section-band">Favourite combos</h2>
      <div className="favourite-combos">
        {pairs.length > 0 && <ComboList combos={pairs} />}
        {trios.length > 0 && <ComboList combos={trios} />}
      </div>
    </section>
  )
}
