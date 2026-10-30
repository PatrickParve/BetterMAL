import { PosterPicture } from '../PosterPicture.tsx'
import { VsBadge } from './VsBadge.tsx'
import './PosterPair.css'

type PosterPairProps = {
  /** Up to two posters. Fewer leave their slot as the shared placeholder, and the badge is drawn regardless. */
  pictures: string[]
  size: 'tile' | 'lobby'
}

// The two tilted posters around the VS medallion that stand for Higher or Lower
// on its Games tile and on its setup screen (add-games-higher-or-lower design.md
// D11). They are decoration: they carry no score and say nothing about either
// anime's, so assistive technology skips them. A nearly empty database may give
// fewer than two posters, and the pair is still drawn, so the tile and the setup
// screen keep their shape.
export function PosterPair({ pictures, size }: PosterPairProps) {
  return (
    <div className={`poster-pair poster-pair--${size}`} aria-hidden="true">
      {(['a', 'b'] as const).map((slot, index) => (
        <PosterPicture
          key={slot}
          src={pictures[index]}
          className={`poster-pair__poster poster-pair__poster--${slot}`}
          placeholderClassName={`poster-pair__poster poster-pair__poster--${slot} poster-pair__poster--placeholder`}
          draggable={false}
        />
      ))}
      <span className="poster-pair__vs">
        <VsBadge size={size} />
      </span>
    </div>
  )
}
