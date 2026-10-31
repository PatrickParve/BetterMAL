import { PosterPicture } from '../PosterPicture.tsx'
import { VsBadge } from './VsBadge.tsx'
import './PosterPair.css'

type PosterPairProps = {
  /** Up to two posters. Fewer leave their slot as the shared placeholder, and the badge is drawn regardless. */
  pictures: string[]
  size: 'tile' | 'lobby'
  /** `vs` is Higher or Lower's medallion. `question` draws a question mark and blurs the posters, for a game about naming an anime. */
  badge: 'vs' | 'question'
}

// The two tilted posters around a medallion that stand for a game on its Games
// tile and on its setup screen (add-games-higher-or-lower design.md D11). Higher
// or Lower's wears VS; Guess the Anime's wears a question mark over blurred
// posters, so it reads as "what is this?" without a word (add-guess-the-anime
// design.md D11). They are decoration: they carry no score and say nothing about
// either anime's, so assistive technology skips them. A nearly empty database
// may give fewer than two posters, and the pair is still drawn, so the tile and
// the setup screen keep their shape.
export function PosterPair({ pictures, size, badge }: PosterPairProps) {
  return (
    <div
      className={`poster-pair poster-pair--${size}${badge === 'question' ? ' poster-pair--question' : ''}`}
      aria-hidden="true"
    >
      {(['a', 'b'] as const).map((slot, index) => (
        <PosterPicture
          key={slot}
          src={pictures[index]}
          className={`poster-pair__poster poster-pair__poster--${slot}`}
          placeholderClassName={`poster-pair__poster poster-pair__poster--${slot} poster-pair__poster--placeholder`}
          draggable={false}
          tier="tile"
        />
      ))}
      <span className="poster-pair__vs">
        <VsBadge size={size} kind={badge} />
      </span>
    </div>
  )
}
