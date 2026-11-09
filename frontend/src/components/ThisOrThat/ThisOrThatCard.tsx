import type { ThisOrThatCardDto } from '../../api/types.ts'
import { mediaTypeLabel, pickDisplayTitle } from '../../utils/anime.ts'
import { PosterPicture } from '../PosterPicture.tsx'
import { TruncatedTitle } from '../TruncatedTitle.tsx'
import './ThisOrThatCard.css'

type ThisOrThatCardProps = {
  card: ThisOrThatCardDto
  /** A pick is on its way, or the mark is up: the card takes no further pick and shows no hover. */
  locked: boolean
  /** This is the card that was picked. */
  marked: boolean
  /** This side's anime has just changed: its picture and text fade in. */
  entering: boolean
  onPick: () => void
}

// One anime of a This or That pair (this-or-that "A pair shows two anime side by
// side"): Higher or Lower's frame, picture, title and meta line without the score
// pill or the actions row. A card shows no score of any kind. The whole card is
// one button, the pick, named after its anime. It is aria-disabled while locked
// rather than disabled so focus isn't lost (design D11), and the button stays
// put when the anime on its side changes: only what is inside it is keyed on
// the anime, so the new picture mounts already drawn.
export function ThisOrThatCard({ card, locked, marked, entering, onPick }: ThisOrThatCardProps) {
  const title = pickDisplayTitle(card.title, card.englishTitle)
  const meta = [card.mediaType ? mediaTypeLabel(card.mediaType) : null, card.year].filter((part) => part !== null).join(' · ')

  return (
    <article className={marked ? 'this-or-that-card this-or-that-card--marked' : 'this-or-that-card'}>
      <button
        type="button"
        className="this-or-that-card__pick"
        aria-label={`Pick ${title}`}
        aria-disabled={locked}
        onClick={locked ? undefined : onPick}
      >
        <span
          key={card.animeId}
          className={entering ? 'this-or-that-card__content this-or-that-card__content--entering' : 'this-or-that-card__content'}
        >
          <span className="this-or-that-card__frame">
            <PosterPicture src={card.pictureUrl} className="this-or-that-card__picture" draggable={false} />
          </span>
          <TruncatedTitle title={title} lines={2} className="this-or-that-card__title" />
          <span className="this-or-that-card__meta">{meta}</span>
        </span>
      </button>
    </article>
  )
}
