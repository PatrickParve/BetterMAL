import type { ReactNode, Ref } from 'react'
import { Link } from 'react-router-dom'
import type { GuessNumberDto, GuessTagDto, GuessTextDto, GuessTheAnimeRowDto } from '../../api/types.ts'
import { mediaTypeLabel, pickDisplayTitle } from '../../utils/anime.ts'
import { RowPicture } from '../RowPicture.tsx'
import { TruncatedTitle } from '../TruncatedTitle.tsx'
import { capitalise, formatScore, withoutMatches } from './guessRowText.ts'
import './GuessRow.css'

/** What a row shows before its stats arrive: the anime's picture and title. */
export type GuessRowAnime = Pick<GuessTheAnimeRowDto, 'animeId' | 'title' | 'englishTitle' | 'pictureUrl'>

/** The answer's row once a game has ended: what it offers beside its title. */
export type GuessRowAnswer = {
  inMyList: boolean
  /** False under Only my list, where every answer is on my list and the mark would always say so. */
  showListMark: boolean
  adding: boolean
  onAdd: () => void
  /** The game was lost or given up: the row is the answer revealed, not a guess that found it, so it is set apart and has none of the win's green. */
  lost: boolean
}

type GuessRowProps = {
  ref?: Ref<HTMLLIElement>
  anime: GuessRowAnime
  /** The row's stats, or null while they are on their way (the pending row). */
  stats: GuessTheAnimeRowDto | null
  answer: GuessRowAnswer | null
  /** A row that arrived in this visit comes in with a short reveal; one a resumed game opens on doesn't. */
  reveal: boolean
}

// A single stat that isn't known, and a list that is empty: both dim, since
// neither is a miss.
function Unknown() {
  return (
    <span className="guess-value guess-value--unknown">
      <span aria-hidden="true">?</span>
      <span className="visually-hidden">Unknown</span>
    </span>
  )
}

function None() {
  return <span className="guess-value guess-value--unknown">None</span>
}

// The match is said in words as well as drawn, so the green is never the only
// signal, and an arrow is named by what it means (guess-the-anime "The game can be
// played from the keyboard and with a screen reader").
const MATCH = <span className="visually-hidden"> (matches)</span>

function NumberValue({ stat, format, score = false }: { stat: GuessNumberDto; format: (value: number) => string; score?: boolean }) {
  if (stat.value === null) return <Unknown />
  const same = stat.compared === 'Same'
  return (
    <span className={'guess-value' + (same ? ' guess-value--same' : '') + (score ? ' guess-value--score' : '')}>
      {format(stat.value)}
      {same && MATCH}
      {stat.compared === 'AnswerHigher' && (
        <>
          <span className="guess-arrow" aria-hidden="true">
            ↑
          </span>
          <span className="visually-hidden"> (the answer is higher)</span>
        </>
      )}
      {stat.compared === 'AnswerLower' && (
        <>
          <span className="guess-arrow" aria-hidden="true">
            ↓
          </span>
          <span className="visually-hidden"> (the answer is lower)</span>
        </>
      )}
    </span>
  )
}

function TextValue({ stat, label }: { stat: GuessTextDto; label: (value: string) => string }) {
  if (stat.value === null) return <Unknown />
  return (
    <span className={'guess-value' + (stat.same ? ' guess-value--same' : '')}>
      {label(stat.value)}
      {stat.same && MATCH}
    </span>
  )
}

function Tags({ tags }: { tags: GuessTagDto[] }) {
  if (tags.length === 0) return <None />
  return (
    <span className="guess-tags">
      {tags.map((tag, index) => (
        <span key={`${index}-${tag.name}`} className={'guess-value' + (tag.same ? ' guess-value--same' : '')}>
          {tag.name}
          {tag.same && MATCH}
        </span>
      ))}
    </span>
  )
}

const PENDING = (
  <span className="guess-value guess-value--pending" aria-hidden="true">
    …
  </span>
)

// One stat of a row. The `dt` is its label: drawn above the value on a card, and
// kept for assistive technology where the columns' own heading row says it.
function Stat({ label, spoken, wide = false, children }: { label: string; spoken?: string; wide?: boolean; children: ReactNode }) {
  return (
    <div className={'guess-stat' + (wide ? ' guess-stat--wide' : '')}>
      <dt>
        {spoken ? (
          <>
            <span aria-hidden="true">{label}</span>
            <span className="visually-hidden">{spoken}</span>
          </>
        ) : (
          label
        )}
      </dt>
      <dd>{children}</dd>
    </div>
  )
}

// A guess or the answer (guess-the-anime "A guess adds a row of stats under the
// last one"): the picture and title, which link to the anime's page, then one
// `dl` of stats, drawn as columns under the board's heading row where there is
// room and as a card where there isn't. A matching stat is the same text in a
// green box, a number that misses has an arrow towards the answer, and what isn't
// known is a `?`. Until its stats arrive the row shows its picture and title with
// a dim `…` in each stat, so a slow fetch never feels like a dropped click.
export function GuessRow({ ref, anime, stats, answer, reveal }: GuessRowProps) {
  const title = pickDisplayTitle(anime.title, anime.englishTitle)
  const pending = stats === null
  // Revealed after a loss, every stat would match itself and the row would read as a
  // win, so its stats are drawn plain.
  const lost = answer?.lost === true
  const shown = lost && stats ? withoutMatches(stats) : stats
  const className = [
    'guess-row',
    answer ? 'guess-row--answer' : null,
    lost ? 'guess-row--answer-lost' : null,
    reveal ? 'guess-row--reveal' : null,
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <li ref={ref} className={className} aria-busy={pending || undefined}>
      <div className="guess-row__anime">
        {/* The title is the link a keyboard and a screen reader find; the picture's
            is the same one, for a pointer. */}
        <Link to={`/anime/${anime.animeId}`} className="guess-row__picture-link" tabIndex={-1} aria-hidden="true">
          <RowPicture src={anime.pictureUrl} className="guess-row__picture" />
        </Link>
        <div className="guess-row__text">
          {lost && <span className="guess-row__tag">The answer</span>}
          {answer && !lost && <span className="visually-hidden">The answer: </span>}
          <Link to={`/anime/${anime.animeId}`} className="guess-row__link">
            <TruncatedTitle title={title} lines={2} className="guess-row__title" />
          </Link>
          {answer &&
            (answer.inMyList ? (
              answer.showListMark && <span className="guess-row__in-list">✓ In my list</span>
            ) : (
              <button type="button" className="guess-row__add" disabled={answer.adding} onClick={answer.onAdd}>
                + Add to list
              </button>
            ))}
        </div>
      </div>
      <dl className="guess-stats">
        <Stat label="Year">{shown ? <NumberValue stat={shown.year} format={String} /> : PENDING}</Stat>
        <Stat label="Season">{shown ? <TextValue stat={shown.season} label={capitalise} /> : PENDING}</Stat>
        <Stat label="Genres" wide>
          {shown ? <Tags tags={shown.genres} /> : PENDING}
        </Stat>
        <Stat label="Eps" spoken="Episodes">
          {shown ? <NumberValue stat={shown.episodes} format={String} /> : PENDING}
        </Stat>
        <Stat label="Studio" wide>
          {shown ? <Tags tags={shown.studios} /> : PENDING}
        </Stat>
        <Stat label="Type">{shown ? <TextValue stat={shown.mediaType} label={mediaTypeLabel} /> : PENDING}</Stat>
        <Stat label="MAL" spoken="MAL score">
          {shown ? <NumberValue stat={shown.score} format={formatScore} score /> : PENDING}
        </Stat>
      </dl>
    </li>
  )
}
