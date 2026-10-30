import { Link } from 'react-router-dom'
import type { GameListing } from './games.ts'
import { PosterPair } from './PosterPair.tsx'
import './GameButton.css'
import './GameTile.css'

type GameTileProps = {
  game: GameListing
  pictures: string[]
  /** The streak of the game in progress, or null when there is none. */
  streak: number | null
}

// One game on the Games page, drawn as a single link so the whole tile opens it
// (games-page "The Games page shows the games available"). Its button reads
// Continue with the streak beside it while a game is in progress, and Play
// otherwise, with no score or streak of any kind, since none is kept once a game
// ends. The button is a span, not a button, because it sits inside the link.
export function GameTile({ game, pictures, streak }: GameTileProps) {
  return (
    <Link to={game.to} className="game-tile">
      <PosterPair pictures={pictures} size="tile" />
      <div className="game-tile__body">
        <h2 className="game-tile__name">{game.name}</h2>
        <p className="game-tile__text">{game.text}</p>
        <div className="game-tile__foot">
          <span className="game-button game-button--small">{streak === null ? 'Play' : 'Continue'}</span>
          {streak !== null && (
            <span className="game-tile__streak">
              Streak <b>{streak.toLocaleString('en-US')}</b>
            </span>
          )}
        </div>
      </div>
    </Link>
  )
}
