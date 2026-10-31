import type { GuessTheAnimeAnswerDto, GuessTheAnimeEnd, GuessTheAnimeRowDto } from '../../api/types.ts'

// How a game ended: the tenth wrong guess, the right one, or End game.
export type GuessOverKind = GuessTheAnimeEnd | 'Ended'

export type GuessOver = {
  kind: GuessOverKind
  answer: GuessTheAnimeAnswerDto
  /** The guess that found it, for the verdict of a win. */
  guesses: number
}

// A game that has ended, as the page keeps it in the history entry it ended on, so
// Back and Forward return to it as I left it. The server holds nothing of a
// finished game, so this is all there is of it; it lasts as long as the entry does,
// and is dropped by Back to start, Escape and Play again (add-guess-the-anime
// design.md D15).
export type FinishedGuessGame = {
  game: { id: number; maxGuesses: number }
  rows: GuessTheAnimeRowDto[]
  over: GuessOver
  /** The answer has been added to my list from its row. */
  added: boolean
}
