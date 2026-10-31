import { ApiError, getGuessTheThemeClip } from '../../api/client.ts'
import { loadClip, type Clip } from './themeAudio.ts'

// The songs of a game that are loaded or on their way, by game and round
// (add-anime-themes-and-guess-the-theme design.md D9, D10). A round's clip is read
// from the backend once and kept decoded in memory, so a wrong guess replays the next
// length with no wait. It is asked for by whoever wants it first: the setup screen's
// Play, to have the first clip before the board opens, and the board, for the song
// being played and for the one already drawn for after a reveal. Asking again for a
// clip already requested is the same promise, not a second read. A clip that fails is
// forgotten, so asking again (Try again) reads it afresh. Nothing is stored beyond
// the page: these are decoded samples in memory, a clip let go has its read stopped,
// and `forgetClips` lets go of a game's. It takes the game, because Play again has the
// new game's first song on its way while the board of the old one is still closing.
type Held = {
  clip: Promise<Clip>
  // When it was first asked for, so a screen can tell how long a song has been on its way.
  askedAt: number
  reading: AbortController
}

const clips = new Map<string, Held>()

const keyOf = (runId: number, round: number) => `${runId}:${round}`

/** How long after a failed read the one more try waits. */
const RETRY_AFTER_MS = 1500

// What may pass if it is asked for again: AnimeThemes was busy or didn't answer (a 502),
// or the connection dropped or the body was cut off (a fetch that rejects). A file that
// has gone (410), a song the game doesn't hold (404) and a clip the browser can't decode
// are the same next time, so they are not tried again.
function mayPass(error: unknown): boolean {
  return error instanceof ApiError ? error.status === 502 : error instanceof TypeError
}

// AnimeThemes' media host answers a clip in anywhere from under a second to most of a
// minute, and sometimes not at all, so a read that failed is asked for once more before
// the board says the song couldn't be loaded. The backend waits for the host and asks it
// again itself where it can; this covers what it had to give up on.
async function fetchClip(runId: number, round: number, signal: AbortSignal): Promise<Clip> {
  let bytes: ArrayBuffer
  try {
    bytes = await getGuessTheThemeClip(runId, round, signal)
  } catch (error) {
    if (!mayPass(error)) throw error
    await new Promise((resolve) => window.setTimeout(resolve, RETRY_AFTER_MS))
    bytes = await getGuessTheThemeClip(runId, round, signal)
  }
  return loadClip(bytes)
}

export function requestClip(runId: number, round: number): Promise<Clip> {
  const key = keyOf(runId, round)
  const held = clips.get(key)
  if (held) return held.clip

  const reading = new AbortController()
  const pending = fetchClip(runId, round, reading.signal).catch((error: unknown) => {
    if (clips.get(key)?.clip === pending) clips.delete(key)
    throw error
  })
  clips.set(key, { clip: pending, askedAt: Date.now(), reading })
  return pending
}

/** How long ago a song was first asked for, in milliseconds; 0 for one that never was. */
export function clipAge(runId: number, round: number): number {
  const at = clips.get(keyOf(runId, round))?.askedAt
  return at === undefined ? 0 : Date.now() - at
}

function letGo(key: string): void {
  clips.get(key)?.reading.abort()
  clips.delete(key)
}

/** Lets go of every clip but the ones named: the song being played and the one drawn for after it. */
export function keepClips(runId: number, rounds: (number | null)[]): void {
  const keep = new Set(rounds.filter((round): round is number => round !== null).map((round) => keyOf(runId, round)))
  for (const key of [...clips.keys()]) {
    if (!keep.has(key)) letGo(key)
  }
}

/** Lets go of everything of one game, and leaves any other game's alone. */
export function forgetClips(runId: number): void {
  for (const key of [...clips.keys()]) {
    if (key.startsWith(`${runId}:`)) letGo(key)
  }
}
