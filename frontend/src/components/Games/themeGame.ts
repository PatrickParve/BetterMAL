import type { GameThemeChoice, GuessTheThemeGameDto, GuessTheThemeRevealDto } from '../../api/types.ts'

// How a game of Guess the Opening/Ending ended: a wrong guess or a skip at the last
// length (Lost), End game during a round (GaveUp), End game during a reveal, where the
// song was already got (Stopped), or no song left to draw (Cleared). `reveal` is the
// song to show; a Cleared game that ended on a replacement has none.
export type ThemeOverKind = 'Lost' | 'GaveUp' | 'Stopped' | 'Cleared'

export type ThemeOver = {
  kind: ThemeOverKind
  reveal: GuessTheThemeRevealDto | null
}

// A game that has ended, as the page keeps it in the history entry it ended on, so
// Back and Forward return to it as I left it. The server holds nothing of a finished
// game, so this is all there is of it; it lasts as long as the entry does, and is
// dropped by Back to start, Escape and Play again (guess-the-theme "A finished game
// can be returned to").
export type FinishedThemeGame = {
  /** The game as it last stood, for its id. */
  game: GuessTheThemeGameDto
  over: ThemeOver
  /** The song's anime has been added to my list from the reveal. */
  added: boolean
}

// A length as it is written on the screen: 0.1s, 10s.
export function formatLength(seconds: number): string {
  return `${seconds}s`
}

// A length as a screen reader should hear it.
export function spokenLength(seconds: number): string {
  return seconds === 1 ? '1 second' : `${seconds} seconds`
}

export const THEME_CHOICES: { value: GameThemeChoice; label: string }[] = [
  { value: 'All', label: 'All themes' },
  { value: 'Openings', label: 'Openings' },
  { value: 'Endings', label: 'Endings' },
]

export const DEFAULT_THEME_CHOICE: GameThemeChoice = 'All'

// Theme is kept beside the shared settings, in a key of its own, so the shared
// reader and writer stay the same for every game (guess-the-theme "Guess the
// Opening/Ending opens on its setup screen"). A value that no longer means anything
// reads as the default; storage can throw, and then there is simply nothing stored.
export const THEME_STORAGE_KEY = 'bettermal.guessTheTheme.themes'

export function readStoredThemeChoice(): GameThemeChoice {
  try {
    const raw = window.localStorage.getItem(THEME_STORAGE_KEY)
    return THEME_CHOICES.find((choice) => choice.value === raw)?.value ?? DEFAULT_THEME_CHOICE
  } catch {
    return DEFAULT_THEME_CHOICE
  }
}

export function writeStoredThemeChoice(choice: GameThemeChoice): void {
  try {
    window.localStorage.setItem(THEME_STORAGE_KEY, choice)
  } catch {
    // Nothing to remember it in: the next visit opens on the default.
  }
}
