import { useEffect, useRef, useState, type Ref } from 'react'
import { Link } from 'react-router-dom'
import type { GuessTheThemeRevealDto } from '../../api/types.ts'
import { pickDisplayTitle } from '../../utils/anime.ts'
import { PosterPicture } from '../PosterPicture.tsx'
import './GameButton.css'
import './ThemeReveal.css'

type ThemeRevealProps = {
  reveal: GuessTheThemeRevealDto
  /** A loss or a give-up: the song is the answer, set apart in a dashed frame and marked in words, with none of a win's green. */
  setApart: boolean
  /** The game was stopped on this reveal with End game: it keeps neither a win's green nor an answer's dashes, since it says only that the game has ended. */
  plain?: boolean
  /** The video starts by itself. A reveal returned to by reload or Back keeps it stopped. */
  autoplay: boolean
  inMyList: boolean
  /** False under Only my list, where every answer is on my list and the mark would always say so. */
  showListMark: boolean
  adding: boolean
  onAdd: () => void
  /** Present where the game goes on: the control that moves to the next song. */
  onNext?: () => void
  nexting?: boolean
  nextRef?: Ref<HTMLButtonElement>
}

// The reveal's video, with its sound. It comes from the app's own address, where its start
// has been read ahead so it begins at once; if that can't give it, the file itself is
// tried. Chrome lets it start after a press; Safari may refuse because the answer arrived
// after the press had ended, and then the video shows a play control of its own over it.
// A video that can't be played is a quiet note in its place, and the reveal around it is
// as usual.
function RevealVideo({ src, file, autoplay }: { src: string; file: string; autoplay: boolean }) {
  const ref = useRef<HTMLVideoElement>(null)
  const [source, setSource] = useState(src)
  const [failed, setFailed] = useState(false)
  const [needsPress, setNeedsPress] = useState(false)

  useEffect(() => {
    const video = ref.current
    if (!autoplay || !video) return
    video.play().catch((error: unknown) => {
      // A refusal asks for a press. An interruption (the video left, or was replaced) is not one.
      if (error instanceof DOMException && error.name === 'NotAllowedError') setNeedsPress(true)
    })
  }, [autoplay, source])

  // A video that leaves the page goes on downloading until it's told to let go of its file.
  useEffect(() => {
    const video = ref.current
    return () => {
      if (!video || video.isConnected) return
      video.removeAttribute('src')
      video.load()
    }
  }, [])

  if (failed) {
    return <p className="theme-reveal__video-note">The video can't be played right now.</p>
  }

  return (
    <div className="theme-reveal__video-box">
      <video
        ref={ref}
        src={source}
        className="theme-reveal__video"
        controls
        playsInline
        preload="auto"
        onError={() => {
          if (source === file) setFailed(true)
          else setSource(file)
        }}
        onPlay={() => setNeedsPress(false)}
      />
      {needsPress && (
        <button
          type="button"
          className="theme-reveal__video-play"
          aria-label="Play the video"
          onClick={() => {
            setNeedsPress(false)
            void ref.current?.play().catch(() => setNeedsPress(true))
          }}
        >
          <svg viewBox="0 0 24 24" width="28" height="28" aria-hidden="true" focusable="false">
            <path d="M8 5.5v13a1 1 0 0 0 1.5.86l10.5-6.5a1 1 0 0 0 0-1.72L9.5 4.64A1 1 0 0 0 8 5.5Z" fill="currentColor" />
          </svg>
        </button>
      )}
    </div>
  )
}

// What the song was (guess-the-theme "The reveal shows what the song was"): its video
// with sound, the anime's picture and title, whether it is an opening or an ending with
// its label, and the song title and artists. The picture and title link to the anime's
// page, which leaves the game as it is. A song with no artist shows none. After a loss or a give-up it is the answer: set apart in a dashed
// neutral frame and tagged "The answer". It offers Add to list for an anime not on
// my list and Next song where the game goes on.
export function ThemeReveal({
  reveal,
  setApart,
  plain = false,
  autoplay,
  inMyList,
  showListMark,
  adding,
  onAdd,
  onNext,
  nexting = false,
  nextRef,
}: ThemeRevealProps) {
  const title = pickDisplayTitle(reveal.title, reveal.englishTitle)
  const kind = reveal.kind === 'Opening' ? 'Opening' : 'Ending'
  const artists = reveal.artists.join(', ')

  return (
    <article className={'theme-reveal' + (setApart ? ' theme-reveal--answer' : '') + (plain ? ' theme-reveal--plain' : '')} aria-label={setApart ? 'The answer' : 'The song'}>
      {setApart && <span className="theme-reveal__tag">The answer</span>}
      <div className="theme-reveal__body">
        <RevealVideo key={reveal.videoUrl} src={reveal.playUrl} file={reveal.videoUrl} autoplay={autoplay} />

        <div className="theme-reveal__anime">
          <Link to={`/anime/${reveal.animeId}`} className="theme-reveal__poster-link" tabIndex={-1} aria-hidden="true">
            <PosterPicture src={reveal.pictureUrl} className="theme-reveal__poster" tier="tile" draggable={false} />
          </Link>
          <div className="theme-reveal__text">
            <h2 className="theme-reveal__title">
              <Link to={`/anime/${reveal.animeId}`}>{title}</Link>
            </h2>
            <p className="theme-reveal__label">
              {kind} <b>{reveal.label}</b>
            </p>
            <p className="theme-reveal__song">
              {reveal.songTitle ? reveal.songTitle : <span className="theme-reveal__unknown">Song title not known</span>}
            </p>
            {artists && <p className="theme-reveal__artists">{artists}</p>}
          </div>
        </div>

        <div className="theme-reveal__actions">
          {inMyList
            ? showListMark && <span className="theme-reveal__in-list">✓ In my list</span>
            : (
              <button type="button" className="game-outline-button" aria-disabled={adding} onClick={onAdd}>
                + Add to list
              </button>
            )}
          {onNext && (
            <button
              ref={nextRef}
              type="button"
              className="game-button game-button--small theme-reveal__next"
              disabled={nexting}
              onClick={onNext}
            >
              Next song
            </button>
          )}
        </div>
      </div>
    </article>
  )
}
