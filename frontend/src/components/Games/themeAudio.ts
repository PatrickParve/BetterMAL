// The audio of Guess the Opening/Ending (add-anime-themes-and-guess-the-theme design.md
// D9). A round's song is read once as the first part of an Ogg file, decoded to
// samples, and played by cutting those samples, so each length is exact to the
// sample, counted from where the sound starts, and no media element is involved: no
// browser shows a title in its media controls, and the file's address, which names the
// show, never reaches the page.
//
// One AudioContext serves the page. Browsers only let one make sound after a press, and
// Safari is particular about it, in two ways measured in the real browser (design.md D9):
//
// - A context made inside a press can report that it is running while its clock stands at
//   zero for good, so nothing scheduled on it is ever heard and nothing says so. So the
//   context is made when the page opens, and a press only resumes it. `state` is not
//   believed either: a length plays once the clock has been seen to move.
// - A press can stop counting as one between mousedown and click, seen with the guess
//   field holding the focus, so a context resumed from a click handler stays suspended.
//   So every press resumes it at its first event, ahead of anything else.

/** The sound "starts" at the first sample louder than this (dBFS); one constant, so it can be tuned once the game has been played. */
export const ONSET_THRESHOLD_DBFS = -30

/** The ramp at each end of a length, so a 0.1s cut doesn't click. It sits inside the length: the length is exact. */
const FADE_SECONDS = 0.005
/** How long a clip being stopped takes to fade out. */
const STOP_FADE_SECONDS = 0.01
/** The longest a play waits for a context to resume before saying it did not start. */
const RESUME_WAIT_MS = 1000
/** How long a running context's clock is watched before it is taken to have stopped. A working clock is seen moving in milliseconds, so only a stopped one waits this out, and an output that is slow to wake, such as a Bluetooth one, isn't mistaken for it. */
const CLOCK_WAIT_MS = 1000
const CLOCK_LOOK_MS = 5
/** The first event of a press, by mouse, pen, touch or key, and `click` for what only sends that. */
const PRESS_EVENTS = ['pointerdown', 'mousedown', 'touchend', 'keydown', 'click'] as const

/** A decoded song and where its sound starts, in seconds from the start of the file. */
export type Clip = {
  buffer: AudioBuffer
  onset: number
}

/**
 * What became of a play: it `started`; it was `replaced` by another play or a stop
 * while it waited; or it `needsPress`, because the browser won't make sound until
 * another press, which pressing Play gives it.
 */
export type PlayResult = 'started' | 'replaced' | 'needsPress'

type Playing = {
  context: BaseAudioContext
  source: AudioBufferSourceNode
  gain: GainNode
}

let sharedContext: AudioContext | null = null
let playing: Playing | null = null
// Counts the plays and stops asked for, so a play that was waiting for the context to resume can tell it was replaced or stopped meanwhile.
let request = 0

const wait = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms))

function context(): AudioContext {
  if (sharedContext === null || sharedContext.state === 'closed') {
    claimPlaybackSession()
    sharedContext = new AudioContext({ latencyHint: 'interactive' })
  }
  return sharedContext
}

// A phone mutes Web Audio with the ring switch off unless the page says it is playing
// media. Where the property doesn't exist nothing is needed, and on a computer there is
// no switch, so it is left alone there.
function claimPlaybackSession(): void {
  try {
    const nav = navigator as Navigator & { audioSession?: { type: string } }
    if (nav.audioSession && nav.maxTouchPoints > 0) nav.audioSession.type = 'playback'
  } catch {
    // A browser that refuses simply keeps its default.
  }
}

/** Makes the context, ahead of any press. Call it when the page opens. */
export function prepare(): void {
  try {
    context()
  } catch {
    // No Web Audio: loadClip fails, and the board says the song couldn't be loaded.
  }
}

/**
 * Has every press resume the context, at the press's first event and ahead of whatever
 * else it does. Call it when the page opens; the function it returns stops listening.
 */
export function listenForPresses(): () => void {
  const wake = () => {
    if (sharedContext !== null && sharedContext.state !== 'running') void sharedContext.resume().catch(() => {})
  }
  for (const type of PRESS_EVENTS) window.addEventListener(type, wake, { capture: true, passive: true })
  return () => {
    for (const type of PRESS_EVENTS) window.removeEventListener(type, wake, { capture: true })
  }
}

/**
 * The first sample whose level, in any channel, is above the threshold, in seconds.
 * A file with no sound that loud in what the browser holds starts at 0.
 */
export function findOnset(buffer: AudioBuffer, thresholdDbfs: number = ONSET_THRESHOLD_DBFS): number {
  const amplitude = 10 ** (thresholdDbfs / 20)
  let first = buffer.length
  for (let channel = 0; channel < buffer.numberOfChannels; channel++) {
    const samples = buffer.getChannelData(channel)
    const limit = Math.min(first, samples.length)
    for (let i = 0; i < limit; i++) {
      if (Math.abs(samples[i]) > amplitude) {
        first = i
        break
      }
    }
  }
  return first >= buffer.length ? 0 : first / buffer.sampleRate
}

/** Decodes the bytes of a clip. Rejects when the browser can't decode them, which the board treats as a song that couldn't be loaded. */
export async function loadClip(bytes: ArrayBuffer): Promise<Clip> {
  const buffer = await context().decodeAudioData(bytes)
  return { buffer, onset: findOnset(buffer) }
}

/**
 * Schedules `seconds` of the clip, from its onset, to start at `when` on `ctx`. The
 * length is exact: the source is cut by `start(when, offset, duration)`, and a ramp
 * at each end, inside it, takes the click away. A length the song is too short for
 * is cut at the song's end. Exported so a probe can run the very same scheduling
 * through an OfflineAudioContext and measure what comes out.
 */
export function scheduleClip(ctx: BaseAudioContext, clip: Clip, seconds: number, when: number): Playing {
  const length = Math.min(seconds, Math.max(0, clip.buffer.duration - clip.onset))
  const fade = Math.min(FADE_SECONDS, length / 4)

  const gain = ctx.createGain()
  gain.gain.setValueAtTime(0, when)
  gain.gain.linearRampToValueAtTime(1, when + fade)
  gain.gain.setValueAtTime(1, when + length - fade)
  gain.gain.linearRampToValueAtTime(0, when + length)

  const source = ctx.createBufferSource()
  source.buffer = clip.buffer
  source.connect(gain)
  gain.connect(ctx.destination)
  source.start(when, clip.onset, length)
  return { context: ctx, source, gain }
}

// Whether the context's clock moves: the one sign that what is scheduled on it will be heard.
async function clockMoves(ctx: AudioContext): Promise<boolean> {
  const from = ctx.currentTime
  const until = performance.now() + CLOCK_WAIT_MS
  while (performance.now() < until) {
    await wait(CLOCK_LOOK_MS)
    if (ctx.state === 'running' && ctx.currentTime > from) return true
  }
  return false
}

// The context, once it is really running, or null if that takes another press.
async function soundingContext(): Promise<AudioContext | null> {
  const ctx = context()
  if (ctx.state !== 'running') {
    await Promise.race([ctx.resume().catch(() => {}), wait(RESUME_WAIT_MS)])
    // Not resumed in time, or left the press that resumed it: the next press does it.
    if ((ctx.state as AudioContextState) !== 'running') return null
  }
  if (await clockMoves(ctx)) return ctx

  // Running in name only. Stopping and starting it starts its output afresh, and a context
  // a press has already let run needs no press for this.
  await Promise.race([ctx.suspend().then(() => ctx.resume()).catch(() => {}), wait(RESUME_WAIT_MS)])
  if (await clockMoves(ctx)) return ctx

  // Past saving. A new one now, so that all the next press has to do is resume it; a
  // decoded clip plays on any context. A second play that got here first has done this.
  if (sharedContext === ctx) {
    void ctx.close().catch(() => {})
    sharedContext = null
    prepare()
  }
  return null
}

/**
 * Plays `seconds` of the clip from where its sound starts, replacing anything
 * playing. `onEnded` runs when it plays out, not when it is stopped.
 */
export async function play(clip: Clip, seconds: number, onEnded?: () => void): Promise<PlayResult> {
  stop()
  const mine = request
  const ctx = await soundingContext()
  if (mine !== request) return 'replaced'
  if (ctx === null) return 'needsPress'

  const run = scheduleClip(ctx, clip, seconds, ctx.currentTime)
  playing = run
  run.source.onended = () => {
    if (playing !== run) return
    playing = null
    onEnded?.()
  }
  return 'started'
}

/** Stops what is playing, with a very short fade so it doesn't click. Does nothing when nothing is. */
export function stop(): void {
  request++
  const run = playing
  playing = null
  if (!run) return
  const now = run.context.currentTime
  const level = run.gain.gain.value
  run.gain.gain.cancelScheduledValues(now)
  run.gain.gain.setValueAtTime(level, now)
  run.gain.gain.linearRampToValueAtTime(0, now + STOP_FADE_SECONDS)
  try {
    run.source.stop(now + STOP_FADE_SECONDS)
  } catch {
    // Already ended.
  }
}
