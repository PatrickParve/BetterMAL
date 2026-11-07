import { useCallback, useRef, useState, type SyntheticEvent } from 'react'
import type { AnimeThemeDto } from '../../api/types.ts'
import { heldNote, heldReasons, videoRule } from './themeFlags.ts'
import './ThemePlayer.css'

type ThemePlayerProps = {
  theme: AnimeThemeDto
  /** The app-wide Hide NSFW setting: with it on, an NSFW theme's video is held back, as a spoiler's is. */
  hideNsfw: boolean
}

// The player under a row (anime-themes "Playing a theme"): the video with its
// sound and the browser's own controls, or the audio alone, straight from
// AnimeThemes' files. It mounts when a row's play control is pressed, so that press
// is the gesture that lets it start by itself, and nothing on a page plays before
// it. Leaving the page, or another row's player opening, unmounts it, and a media
// element that leaves the document pauses. A flagged theme holds its video back
// (themeFlags.ts): a spoiler, or an NSFW theme under Hide NSFW, plays its audio
// first with a note saying why, and shows the video only when I ask. The same
// player serves every page, so the rule is the same on all of them.
// A file that won't play is a quiet note in place of the player and nothing else
// on the page notices; the switch stays, so the other kind can be tried.
export function ThemePlayer({ theme, hideNsfw }: ThemePlayerProps) {
  const held = videoRule(theme, hideNsfw) === 'held'
  const canVideo = theme.videoUrl !== null
  const hasAudio = theme.audioUrl !== null

  const [videoOn, setVideoOn] = useState(!held && theme.videoUrl !== null)
  const [failed, setFailed] = useState(false)
  const mediaRef = useRef<HTMLMediaElement | null>(null)
  const setMedia = useCallback((element: HTMLMediaElement | null) => {
    mediaRef.current = element
  }, [])
  // Where the other kind picks up from, so switching keeps my place in the song.
  const resumeAtRef = useRef(0)

  // The video, if it is wanted and there is one; otherwise the audio, if there is any.
  const showVideo = videoOn && canVideo
  const showAudio = !showVideo && hasAudio
  const canSwitch = canVideo && (hasAudio || !showVideo)
  // Why the video waits, whenever it is there to be asked for and isn't showing: under
  // the audio, or alone when there is no audio. Null for a theme that isn't held.
  const holdNote = !showVideo && canVideo ? heldNote(heldReasons(theme, hideNsfw)) : null

  function switchKind() {
    resumeAtRef.current = failed ? 0 : (mediaRef.current?.currentTime ?? 0)
    setFailed(false)
    setVideoOn(!showVideo)
  }

  function handleLoadedMetadata(event: SyntheticEvent<HTMLMediaElement>) {
    const element = event.currentTarget
    const at = resumeAtRef.current
    resumeAtRef.current = 0
    if (at > 0 && Number.isFinite(element.duration)) element.currentTime = Math.min(at, element.duration)
  }

  const common = {
    controls: true,
    autoPlay: true,
    preload: 'auto' as const,
    onError: () => setFailed(true),
    onLoadedMetadata: handleLoadedMetadata,
  }

  return (
    <div className="theme-player">
      {showVideo && !failed && (
        <video
          key="video"
          ref={setMedia}
          src={theme.videoUrl ?? undefined}
          className="theme-player__video"
          playsInline
          {...common}
        />
      )}
      {showAudio && !failed && (
        <audio key="audio" ref={setMedia} src={theme.audioUrl ?? undefined} className="theme-player__audio" {...common} />
      )}
      {failed && (
        <p className="theme-player__note" role="status">
          This can't be played right now.
        </p>
      )}
      {holdNote !== null && !failed && <p className="theme-player__note">{holdNote}</p>}
      {canSwitch && (
        <button type="button" className="theme-player__switch" onClick={switchKind}>
          {showVideo ? 'Audio only' : 'Show video'}
        </button>
      )}
    </div>
  )
}
