// Which games exist is a fact about the code, so the list is a constant here
// rather than something the server sends (add-games-higher-or-lower design.md
// D10). Each entry has a name, a line of text and a route. The Games page draws
// one tile per entry, and a game's own setup screen reads its name and text
// from the same entry so the two can't disagree.
export type GameListing = {
  name: string
  text: string
  to: string
}

export const HIGHER_OR_LOWER: GameListing = {
  name: 'Higher or Lower',
  text: 'Which anime has the higher MAL score? See how many you can get in a row.',
  to: '/games/higher-or-lower',
}

export const GAMES: GameListing[] = [HIGHER_OR_LOWER]
