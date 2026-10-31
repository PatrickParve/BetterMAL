import type { ReactNode } from 'react'
import { formatLength, spokenLength } from './themeGame.ts'
import './ThemeLadder.css'

/** What became of a length that has gone: a wrong guess, a skip, or, where it isn't known which (a game found again after a reload), simply gone. */
export type LadderMark = 'wrong' | 'skipped' | 'gone'

type ThemeLadderProps = {
  lengths: number[]
  /** The length being listened to, as a position in `lengths`. */
  step: number
  /** What became of each length that has gone, by position. A missing one reads as simply gone. */
  marks: (LadderMark | undefined)[]
}

type MarkState = LadderMark | 'current' | 'upcoming'

const STATE_WORDS: Record<MarkState, string> = {
  wrong: 'wrong guess',
  skipped: 'skipped',
  gone: 'used',
  current: 'current length',
  upcoming: 'still to come',
}

function Glyph({ state }: { state: MarkState }) {
  const svg = (children: ReactNode) => (
    <svg viewBox="0 0 16 16" width="14" height="14" aria-hidden="true" focusable="false">
      {children}
    </svg>
  )
  if (state === 'wrong' || state === 'gone') {
    return svg(<path d="M4 4l8 8M12 4l-8 8" stroke="currentColor" strokeWidth="2" strokeLinecap="round" fill="none" />)
  }
  if (state === 'skipped') {
    return svg(
      <>
        <path d="M3.5 3.8v8.4a.5.5 0 0 0 .78.42l5.7-4.2a.5.5 0 0 0 0-.84l-5.7-4.2a.5.5 0 0 0-.78.42Z" fill="currentColor" />
        <path d="M12.5 3.5v9" stroke="currentColor" strokeWidth="2" strokeLinecap="round" fill="none" />
      </>,
    )
  }
  if (state === 'current') return svg(<circle cx="8" cy="8" r="4" fill="currentColor" />)
  return svg(<circle cx="8" cy="8" r="2" fill="currentColor" />)
}

// The six lengths of a song (guess-the-theme "A game is a run of songs, each with a
// ladder of six lengths"): the ones that have gone, marked by what ended them, the
// one being listened to, and the ones to come. A mark is never told by its colour
// alone: each state has its own glyph and border, and its own words for a screen
// reader, which hears "0.5 seconds, wrong guess" rather than a symbol.
export function ThemeLadder({ lengths, step, marks }: ThemeLadderProps) {
  return (
    <ol className="theme-ladder" aria-label="Lengths">
      {lengths.map((seconds, index) => {
        const state: MarkState = index < step ? (marks[index] ?? 'gone') : index === step ? 'current' : 'upcoming'
        return (
          <li
            key={seconds}
            className={`theme-ladder__mark theme-ladder__mark--${state}`}
            aria-current={state === 'current' ? 'step' : undefined}
          >
            <Glyph state={state} />
            <span className="theme-ladder__length" aria-hidden="true">
              {formatLength(seconds)}
            </span>
            <span className="visually-hidden">
              {spokenLength(seconds)}, {STATE_WORDS[state]}
            </span>
          </li>
        )
      })}
    </ol>
  )
}
