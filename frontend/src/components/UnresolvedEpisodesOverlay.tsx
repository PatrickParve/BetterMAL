import { Link } from 'react-router-dom'
import { Modal } from './Modal.tsx'
import { RowPicture } from './RowPicture.tsx'
import { TruncatedTitle } from './TruncatedTitle.tsx'
import type { UnresolvedEpisodeEntryDto } from '../api/types.ts'
import { pickDisplayTitle } from '../utils/anime.ts'
import './UnresolvedEpisodesOverlay.css'

type UnresolvedEpisodesOverlayProps = {
  entries: UnresolvedEpisodeEntryDto[]
  onClose: () => void
}

// The episode-progress bar's unresolved-entries note opens this to name
// exactly which anime it's counting (profile-stats "All-list episode
// progress") — entries with neither a published episode count nor a known
// aired count, so the bar has nothing to measure them against. The list
// comes straight from the already-loaded profile response; no fetch of its
// own.
export function UnresolvedEpisodesOverlay({ entries, onClose }: UnresolvedEpisodesOverlayProps) {
  return (
    <Modal onClose={onClose} labelledBy="unresolved-episodes-title" className="modal--wide">
      <div className="unresolved-episodes">
        <div className="unresolved-episodes__header">
          <h2 id="unresolved-episodes-title" className="unresolved-episodes__title">
            Episode count not known yet
          </h2>
          <button type="button" className="unresolved-episodes__close" aria-label="Close" onClick={onClose}>
            <CloseIcon />
          </button>
        </div>

        <div className="unresolved-episodes__list-frame">
          <ul className="unresolved-episodes__list scroll-y">
            {entries.map((entry) => (
              <li key={entry.animeId} className="unresolved-episodes__row">
                <Link to={`/anime/${entry.animeId}`} className="unresolved-episodes__link" onClick={onClose}>
                  <RowPicture src={entry.pictureUrl} className="unresolved-episodes__picture" />
                  <span className="unresolved-episodes__info">
                    <TruncatedTitle
                      title={pickDisplayTitle(entry.title, entry.englishTitle)}
                      lines={2}
                      className="unresolved-episodes__row-title"
                    />
                    <span className="unresolved-episodes__detail">{entry.episodesWatched} episodes watched</span>
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </Modal>
  )
}

function CloseIcon() {
  return (
    <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <line x1="6" y1="6" x2="18" y2="18" />
      <line x1="18" y1="6" x2="6" y2="18" />
    </svg>
  )
}
