// Which games exist is a fact about the code, so the games are constants here
// rather than something the server sends (add-games-higher-or-lower design.md
// D10). Each entry has a name, a line of text, a route and the medallion its
// posters wear. The Games page draws a tile for each, with that game's own
// progress and posters, and a game's own setup screen reads its name and text
// from the same entry so the two can't disagree.
export type GameListing = {
  name: string
  text: string
  to: string
  /** The medallion on the game's poster pair: `vs`, or `question` over blurred posters. */
  badge: 'vs' | 'question'
}

export const HIGHER_OR_LOWER: GameListing = {
  name: 'Higher or Lower',
  text: 'Which anime has the higher MAL score? See how many you can get in a row.',
  to: '/games/higher-or-lower',
  badge: 'vs',
}

export const GUESS_THE_ANIME: GameListing = {
  name: 'Guess the Anime',
  text: 'Find the hidden anime in 10 guesses. Every guess shows how close you were.',
  to: '/games/guess-the-anime',
  badge: 'question',
}
