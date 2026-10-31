import { useCallback, useRef, useState, type SyntheticEvent } from 'react'
import type { AnimeThemeDto } from '../../api/types.ts'
import { videoRule } from './themeFlags.ts'
import './ThemePlayer.css'

type ThemePlayerProps = {
  theme: AnimeThemeDto
  /** The app-wide Hide NSFW setting, read live so flipping it closes a video that is playing. */
  hideNsfw: boolean
}

// The player under a row (anime-themes "Playing a theme"): the video with its
// sound and the browser's own controls, or the audio alone, straight from
// AnimeThemes' files. It mounts when a row's play control is pressed, so that press
// is the gesture that lets it start by itself, and nothing on a page plays before
// it. Leaving the page, or another row's player opening, unmounts it, and a media
// element that leaves the document pauses. A flagged theme holds its video back
// (themeFlags.ts): a spoiler plays its audio first and shows the video only when I
// ask, and an NSFW one under Hide NSFW has no way to the video at all.
// A file that won't play is a quiet note in place of the player and nothing else
// on the page notices; the switch stays, so the other kind can be tried.
export function ThemePlayer({ theme, hideNsfw }: ThemePlayerProps) {
  const rule = videoRule(theme, hideNsfw)
  const canVideo = theme.videoUrl !== null && rule !== 'locked'
  const hasAudio = theme.audioUrl !== null

  const [videoOn, setVideoOn] = useState(rule === 'free' && theme.videoUrl !== null)
  const [failed, setFailed] = useState(false)
  const mediaRef = useRef<HTMLMediaElement | null>(null)
  const setMedia = useCallback((element: HTMLMediaElement | null) => {
    mediaRef.current = element
  }, [])
  // Where the other kind picks up from, so switching keeps my place in the song.
  const resumeAtRef = useRef(0)

  // The video, if it is wanted and allowed; otherwise the audio, if there is any.
  // Hide NSFW turning on mid-play can make a wanted video not allowed.
  const showVideo = videoOn && canVideo
  const showAudio = !showVideo && hasAudio
  const nothingToPlay = !showVideo && !showAudio
  const canSwitch = canVideo && (hasAudio || !showVideo)

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
      {nothingToPlay && !failed && (
        <p className="theme-player__note">
          {rule === 'locked' ? "This one can't be played while Hide NSFW is on." : 'Its video may spoil the show.'}
        </p>
      )}
      {showAudio && rule === 'held' && canVideo && !failed && (
        <p className="theme-player__note">Its video may spoil the show.</p>
      )}
      {canSwitch && (
        <button type="button" className="theme-player__switch" onClick={switchKind}>
          {showVideo ? 'Audio only' : 'Show video'}
        </button>
      )}
    </div>
  )
}
