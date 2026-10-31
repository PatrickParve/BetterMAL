import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import type { GameListing } from './games.ts'
import { PosterPair } from './PosterPair.tsx'
import './GameButton.css'
import './GameTile.css'

type GameTileProps = {
  game: GameListing
  pictures: string[]
  /** How far the game in progress has got, in the game's own words, or null when there is none. */
  progress: ReactNode | null
}

// One game on the Games page, drawn as a single link so the whole tile opens it
// (games-page "The Games page shows the games available"). Its button reads
// Continue with the game's progress beside it while a game is in progress, and
// Play otherwise, with no score or result of any kind, since none is kept once a
// game ends. Each game says what its progress is: a streak, a guess number. The
// button is a span, not a button, because it sits inside the link.
export function GameTile({ game, pictures, progress }: GameTileProps) {
  return (
    <Link to={game.to} className="game-tile">
      <PosterPair pictures={pictures} size="tile" badge={game.badge} />
      <div className="game-tile__body">
        <h2 className="game-tile__name">{game.name}</h2>
        <p className="game-tile__text">{game.text}</p>
        <div className="game-tile__foot">
          <span className="game-button game-button--small">{progress === null ? 'Play' : 'Continue'}</span>
          {progress !== null && <span className="game-tile__progress">{progress}</span>}
        </div>
      </div>
    </Link>
  )
}
