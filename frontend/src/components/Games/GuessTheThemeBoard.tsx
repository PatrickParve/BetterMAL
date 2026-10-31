import { useEffect, useMemo, useRef, useState } from 'react'
import {
  ApiError,
  endGuessTheTheme,
  guessTheTheme,
  nextGuessTheTheme,
  replaceGuessTheThemeSong,
  skipGuessTheTheme,
  updateEntry,
} from '../../api/client.ts'
import type { GuessTheThemeGameDto, GuessTheThemeMoveDto, GuessTheThemeRevealDto } from '../../api/types.ts'
import { useActionFailure } from '../../context/ActionFailureContext.tsx'
import { pickDisplayTitle } from '../../utils/anime.ts'
import { preloadPictures } from '../../utils/preloadPictures.ts'
import { RowPicture } from '../RowPicture.tsx'
import { Confetti } from './Confetti.tsx'
import { GuessSearch, type GuessSearchHandle, type GuessSearchPick } from './GuessSearch.tsx'
import * as themeAudio from './themeAudio.ts'
import { clipAge, forgetClips, keepClips, requestClip } from './themeClips.ts'
import { formatLength, spokenLength, type FinishedThemeGame, type ThemeOver } from './themeGame.ts'
import { ThemeLadder, type LadderMark } from './ThemeLadder.tsx'
import { ThemeReveal } from './ThemeReveal.tsx'
import './GameButton.css'
import './GuessBoard.css'
import './GuessTheThemeBoard.css'

type GuessTheThemeBoardProps = {
  game: GuessTheThemeGameDto
  /** A game that has ended and is being returned to by Back or Forward: how it ended. `game` is the game as it last stood. */
  finished: FinishedThemeGame | null
  /** The page started this game itself, in a press, so its first length may play by itself. */
  startedHere: boolean
  /** Under Only my list every song's anime is on my list, so the reveal says nothing of it. */
  onlyMyList: boolean
  /** A move has landed: the page keeps its read current, so Back restores the game where it is. */
  onGame: (game: GuessTheThemeGameDto) => void
  /** The game has ended: the page keeps it in this history entry. */
  onFinished: (over: ThemeOver) => void
  /** The song's anime was added to my list from the reveal. */
  onAdded: () => void
  /** Back to the setup screen, from a game that has ended. */
  onLeave: () => void
  /** Starts a new game with the settings this one was played with. The page then swaps the new game in, which replaces this board. */
  onPlayAgain: () => Promise<void>
  /** A move found the game moved on elsewhere: re-read it, with no notice. */
  onStale: () => void
  /** The first length has been played by itself, which it only does once. */
  onAutoplayed: () => void
}

// Long enough for a picture that is on its way; a slower one is drawn when it arrives,
// and never holds a reveal back.
const REVEAL_PICTURE_WAIT_MS = 1000
// A song still loading this long after it was asked for is said to be slow, so the screen doesn't look stuck: AnimeThemes' media host can take most of a minute.
const SLOW_LOAD_MS = 6000

// One burst at the first level (60 pieces), as Guess the Anime's win has.
const WIN_CONFETTI_LEVEL = 1

const prefersReducedMotion = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches
const forcedColors = () => window.matchMedia('(forced-colors: active)').matches

// The keys the game answers to (Escape, once the game is over) are left alone where
// they belong to something else: a modifier held, a text field or dropdown with
// focus, or an open overlay, as the other games' are.
function keyIsSpoken(event: KeyboardEvent): boolean {
  if (event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return true
  if (document.querySelector('[role="dialog"]')) return true
  const target = event.target
  return (
    target instanceof HTMLElement &&
    (target.isContentEditable || target.closest('input, textarea, select, [role="listbox"], [role="menu"]') !== null)
  )
}

function verdictText(over: ThemeOver): string {
  switch (over.kind) {
    case 'Lost':
      return 'You lost'
    case 'GaveUp':
      return 'You gave up'
    case 'Stopped':
      return 'The game has ended'
    case 'Cleared':
      return 'You cleared them all'
  }
}

function verdictTone(over: ThemeOver): 'won' | 'lost' | 'plain' {
  if (over.kind === 'Cleared') return 'won'
  return over.kind === 'Stopped' ? 'plain' : 'lost'
}

function describeSong(reveal: GuessTheThemeRevealDto): string {
  const title = pickDisplayTitle(reveal.title, reveal.englishTitle)
  const kind = reveal.kind === 'Opening' ? 'Opening' : 'Ending'
  return `${title}, ${kind} ${reveal.label}${reveal.songTitle ? `, ${reveal.songTitle}` : ''}`
}

// What a screen reader hears when a game ends: how it ended, the answer where there is
// one, and the way back.
function endAnnouncement(over: ThemeOver): string {
  const answer = over.reveal ? ` The answer is ${describeSong(over.reveal)}.` : ''
  return `${verdictText(over)}.${answer} Press Escape to go back to the start.`
}

type Busy = 'guess' | 'skip' | 'next' | 'end'

// What became of the lengths that have gone, remembered for the song they belong to.
type RoundMarks = { round: number; marks: LadderMark[] }

// What became of each length that has gone. This board remembers it as it happens. A
// game found again after a reload has only the server's count of wrong guesses to go
// on: when every length was one kind that settles it, and when both kinds are
// mixed the board cannot tell which was which, so they are all shown as simply gone.
function ladderMarks(game: GuessTheThemeGameDto, remembered: RoundMarks): (LadderMark | undefined)[] {
  if (remembered.round === game.round && remembered.marks.length >= game.step) return remembered.marks
  const wrong = game.guessedIds.length
  const skipped = game.step - wrong
  const kind: LadderMark = skipped <= 0 ? 'wrong' : wrong === 0 ? 'skipped' : 'gone'
  return Array.from({ length: game.step }, () => kind)
}

// The game in play (guess-the-theme, design.md D11). The server holds the game and
// judges every move, so this is a small state machine that sends what I pick and
// draws what comes back: listening (a length plays; guess or skip), sending (a move
// is on its way), revealed (the song was got: its reveal waits for Next song) and
// over (the game has ended, and stays on screen until I leave it). The audio is the
// browser's: each song's clip is loaded once, decoded, and cut to the length being
// listened to (themeAudio.ts), and the page sees to letting it make sound. Nothing on
// this board moves by itself except the
// first length of a game started here and the next length after a miss, which follow
// from a press, and a finished game sits on screen with no timers.
export function GuessTheThemeBoard({
  game,
  finished,
  startedHere,
  onlyMyList,
  onGame,
  onFinished,
  onAdded,
  onLeave,
  onPlayAgain,
  onStale,
  onAutoplayed,
}: GuessTheThemeBoardProps) {
  const reportFailure = useActionFailure()

  const [over, setOver] = useState<ThemeOver | null>(finished?.over ?? null)
  const [busy, setBusy] = useState<Busy | null>(null)
  const [clipState, setClipState] = useState<'loading' | 'ready' | 'failed'>('loading')
  const [slowLoad, setSlowLoad] = useState(false)
  const [attempt, setAttempt] = useState(0)
  const [playing, setPlaying] = useState(false)
  // The browser wouldn't make sound for a length, which takes another press: Play says so.
  const [heldBack, setHeldBack] = useState(false)
  const [restarting, setRestarting] = useState(false)
  const [announcement, setAnnouncement] = useState('')
  const [confetti, setConfetti] = useState(false)
  const [remembered, setRemembered] = useState<RoundMarks>({ round: game.round, marks: [] })
  // The song's anime, once I've added it from the reveal, and the add in flight.
  const [added, setAdded] = useState(finished?.added ?? false)
  const [adding, setAdding] = useState(false)
  // A reveal the board opened on, or a game returned to by Back, keeps its video stopped:
  // nothing plays on a board I have just come back to.
  const [openedRevealRound] = useState(() => (finished === null && game.phase === 'Revealed' ? game.round : null))
  const [overRestored] = useState(finished !== null)

  const searchRef = useRef<GuessSearchHandle>(null)
  const againRef = useRef<HTMLButtonElement>(null)
  const nextRef = useRef<HTMLButtonElement>(null)
  const revealBoxRef = useRef<HTMLDivElement>(null)
  const clipRef = useRef<themeAudio.Clip | null>(null)
  // The first length of a game started here plays as soon as its song is ready, and so
  // does the one after a miss or a replacement; a game found again waits for a press.
  const autoPlayRef = useRef(startedHere && finished === null && game.phase === 'Listening' && game.step === 0)
  const mountedRef = useRef(true)
  const busyRef = useRef(false)

  const gameId = game.id
  useEffect(() => {
    mountedRef.current = true
    return () => {
      mountedRef.current = false
      themeAudio.stop()
      forgetClips(gameId)
    }
  }, [gameId])

  const phase: 'listening' | 'revealed' | 'over' = over !== null ? 'over' : game.phase === 'Revealed' ? 'revealed' : 'listening'
  const length = game.lengths[game.step] ?? game.lengths[game.lengths.length - 1]

  // The latest of these, for the effects and callbacks that outlive a render.
  const latest = useRef({ game, length })
  latest.current = { game, length }

  async function playLength(clip: themeAudio.Clip, seconds: number) {
    setPlaying(true)
    const result = await themeAudio.play(clip, seconds, () => setPlaying(false))
    if (result === 'replaced' || !mountedRef.current) return
    setHeldBack(result === 'needsPress')
    if (result === 'needsPress') setPlaying(false)
  }

  function finish(next: ThemeOver) {
    themeAudio.stop()
    forgetClips(game.id)
    setPlaying(false)
    onFinished(next)
    setOver(next)
    setAnnouncement(endAnnouncement(next))
    if (next.kind === 'Cleared' && !prefersReducedMotion() && !forcedColors()) setConfetti(true)
  }

  // The song for the round being listened to: loaded and decoded once, from the cache the
  // setup screen's Play may already have filled. A file that has gone (410) is replaced by
  // another song, with no notice; any other failure leaves the round as it was, with Try
  // again. Nothing here uses a length up.
  const loadRef = useRef<() => () => void>(() => () => {})
  loadRef.current = () => {
    let cancelled = false
    clipRef.current = null
    setClipState('loading')
    const { id, round } = latest.current.game
    requestClip(id, round).then(
      (clip) => {
        if (cancelled || !mountedRef.current) return
        clipRef.current = clip
        setClipState('ready')
        if (autoPlayRef.current) {
          autoPlayRef.current = false
          onAutoplayed()
          void playLength(clip, latest.current.length)
        }
      },
      (error: unknown) => {
        if (cancelled || !mountedRef.current) return
        if (error instanceof ApiError && error.status === 410) void replaceSong()
        else setClipState('failed')
      },
    )
    return () => {
      cancelled = true
    }
  }
  useEffect(() => {
    if (over !== null || game.phase !== 'Listening') return
    return loadRef.current()
  }, [game.id, game.round, game.phase, over, attempt])

  // The song drawn for after this one is loaded behind it, so Next song can play it straight
  // away. At a reveal it is wanted soon, so it is asked for at once; while a round is being
  // listened to it waits for this round's own song, so the two don't ask the media host
  // together. Anything older than the two songs is let go.
  useEffect(() => {
    if (over !== null) return
    keepClips(game.id, [game.round, game.nextRound])
    if (game.nextRound === null) return
    if (game.phase === 'Revealed' || clipState === 'ready') requestClip(game.id, game.nextRound).catch(() => undefined)
  }, [game.id, game.round, game.nextRound, game.phase, over, clipState])

  // A song that is slow to arrive is said to be, once, so the wait doesn't look like a hang.
  const waitingForSong = over === null && game.phase === 'Listening' && clipState === 'loading'
  useEffect(() => {
    if (!waitingForSong) return
    // Counted from when the song was asked for, which may be before this board was: the setup screen asks for the first one.
    const timer = window.setTimeout(() => setSlowLoad(true), Math.max(0, SLOW_LOAD_MS - clipAge(game.id, game.round)))
    return () => {
      window.clearTimeout(timer)
      setSlowLoad(false)
    }
  }, [waitingForSong, game.id, game.round, attempt])

  // Focus: the field takes it when a song starts where there is a keyboard, and not on a
  // phone, where it would put the on-screen keyboard over the board; Next song takes it
  // at a reveal; Play again at the end.
  useEffect(() => {
    if (phase === 'revealed') nextRef.current?.focus({ preventScroll: true })
    else if (phase === 'listening' && window.matchMedia('(pointer: fine)').matches) searchRef.current?.focus()
  }, [phase])
  useEffect(() => {
    if (over !== null) againRef.current?.focus({ preventScroll: true })
  }, [over])

  const guessedIds = useMemo(() => new Set(game.guessedIds), [game.guessedIds])

  async function replaceSong() {
    const { id, round } = latest.current.game
    try {
      const res = await replaceGuessTheThemeSong(id, round)
      if (!mountedRef.current) return
      if (res.outcome === 'Cleared' || res.game === null) {
        finish({ kind: 'Cleared', reveal: null })
        return
      }
      // The round starts again on another song under the same round number, so the clip is
      // asked for again by hand: nothing in the game's numbers says the song changed.
      setAttempt((count) => count + 1)
      onGame(res.game)
    } catch (error) {
      if (!mountedRef.current) return
      if (error instanceof ApiError && error.status === 404) onStale()
      else setClipState('failed')
    }
  }

  // A reveal's picture is loaded before the reveal appears, where it can be within
  // about a second.
  async function revealWhenReady(reveal: GuessTheThemeRevealDto | null) {
    if (reveal === null) return
    await preloadPictures([reveal.pictureUrl], 'tile', REVEAL_PICTURE_WAIT_MS)
  }

  // What a guess or a skip did.
  async function applyMove(res: GuessTheThemeMoveDto, from: GuessTheThemeGameDto) {
    if (res.outcome === 'Wrong' || res.outcome === 'Skipped') {
      const next = res.game
      if (next === null) return
      const mark: LadderMark = res.outcome === 'Wrong' ? 'wrong' : 'skipped'
      setRemembered((current) => {
        const marks = current.round === from.round ? [...current.marks] : []
        marks[from.step] = mark
        return { round: from.round, marks }
      })
      onGame(next)
      setAnnouncement(`${res.outcome === 'Wrong' ? 'Not that one.' : 'Skipped.'} Next, ${spokenLength(next.lengths[next.step])}.`)
      // The next length plays from the start at once, or as soon as the song has loaded.
      const clip = clipRef.current
      if (clip) void playLength(clip, next.lengths[next.step])
      else autoPlayRef.current = true
      return
    }

    themeAudio.stop()
    setPlaying(false)

    if (res.outcome === 'Right') {
      const next = res.game
      if (next === null) return
      await revealWhenReady(next.reveal)
      if (!mountedRef.current) return
      onGame(next)
      const heard = next.reveal?.heardSeconds
      setAnnouncement(
        `${heard != null ? `You got it at ${spokenLength(heard)}. ` : 'You got it. '}${next.reveal ? describeSong(next.reveal) : ''}`,
      )
      return
    }

    await revealWhenReady(res.reveal)
    if (!mountedRef.current) return
    finish({ kind: res.outcome === 'Cleared' ? 'Cleared' : 'Lost', reveal: res.reveal })
  }

  // A move that did not go ahead. A game that moved on elsewhere (a 404, or a 409 for a
  // move it has passed) is re-read with no notice, since nothing was lost; anything
  // else says what happened, and restores what was typed.
  function moveFailed(error: unknown, notCounted: string, notSent: string, typed?: string) {
    busyRef.current = false
    setBusy(null)
    if (error instanceof ApiError && (error.status === 404 || error.status === 409)) {
      onStale()
      return
    }
    reportFailure({
      title: error instanceof ApiError ? notCounted : notSent,
      reason: error instanceof ApiError ? error.reason : null,
    })
    if (typed !== undefined) searchRef.current?.restore(typed)
  }

  async function guess(pick: GuessSearchPick, typed: string) {
    if (busyRef.current || phase !== 'listening') return
    busyRef.current = true
    setBusy('guess')
    themeAudio.stop()
    setPlaying(false)
    const from = game
    let res: GuessTheThemeMoveDto
    try {
      res = await guessTheTheme(from.id, from.move, pick.id)
    } catch (error) {
      if (mountedRef.current) moveFailed(error, "Your guess wasn't counted", "Your guess wasn't sent", typed)
      return
    }
    if (!mountedRef.current) return
    await applyMove(res, from)
    if (!mountedRef.current) return
    busyRef.current = false
    setBusy(null)
  }

  async function skip() {
    if (busyRef.current || phase !== 'listening') return
    busyRef.current = true
    setBusy('skip')
    themeAudio.stop()
    setPlaying(false)
    const from = game
    let res: GuessTheThemeMoveDto
    try {
      res = await skipGuessTheTheme(from.id, from.move)
    } catch (error) {
      if (mountedRef.current) moveFailed(error, "Couldn't skip", "Couldn't skip")
      return
    }
    if (!mountedRef.current) return
    await applyMove(res, from)
    if (!mountedRef.current) return
    busyRef.current = false
    setBusy(null)
  }

  async function nextSong() {
    if (busyRef.current || phase !== 'revealed') return
    busyRef.current = true
    setBusy('next')
    try {
      const next = await nextGuessTheTheme(game.id, game.move)
      if (!mountedRef.current) return
      autoPlayRef.current = true
      onGame(next)
    } catch (error) {
      if (mountedRef.current) moveFailed(error, "Couldn't go to the next song", "Couldn't go to the next song")
      return
    }
    busyRef.current = false
    setBusy(null)
  }

  async function endGame() {
    if (busyRef.current || phase === 'over') return
    busyRef.current = true
    setBusy('end')
    themeAudio.stop()
    setPlaying(false)
    try {
      const res = await endGuessTheTheme(game.id)
      if (res.reveal !== null) await revealWhenReady(res.reveal)
      if (!mountedRef.current) return
      // During a round the answer comes with it; during a reveal it is already on screen.
      if (res.reveal !== null) finish({ kind: 'GaveUp', reveal: res.reveal })
      else finish({ kind: 'Stopped', reveal: game.reveal })
    } catch (error) {
      if (mountedRef.current) moveFailed(error, "Couldn't end the game", "Couldn't end the game")
      return
    }
    busyRef.current = false
    setBusy(null)
  }

  async function playAgain() {
    if (restarting) return
    setRestarting(true)
    try {
      // The new game replaces this board, so there is nothing to reset here.
      await onPlayAgain()
    } catch (error) {
      if (!mountedRef.current) return
      setRestarting(false)
      // The settings no longer leave any song: the setup screen says so.
      if (error instanceof ApiError && error.status === 409) onLeave()
      else reportFailure({ title: "Couldn't start a new game", reason: error instanceof ApiError ? error.reason : null })
    }
  }

  async function addToList(reveal: GuessTheThemeRevealDto) {
    if (adding) return
    setAdding(true)
    try {
      await updateEntry(reveal.animeId, { status: 'PlanToWatch' })
      onAdded()
      if (mountedRef.current) setAdded(true)
    } catch (error) {
      // The reveal still offers Add to list, and the notice says why.
      reportFailure({
        title: `Couldn't add ${pickDisplayTitle(reveal.title, reveal.englishTitle)} to list`,
        reason: error instanceof ApiError ? error.reason : null,
      })
    } finally {
      if (mountedRef.current) setAdding(false)
    }
  }

  function pressPlay() {
    const clip = clipRef.current
    if (clip && phase === 'listening') void playLength(clip, length)
  }

  function tryAgain() {
    autoPlayRef.current = true
    setAttempt((count) => count + 1)
  }

  // Escape goes back to the setup screen, but only once the game is over: during play it
  // only closes the dropdown (which the field handles), and a stray press must not throw
  // a game away. The latest phase is read through a ref, so the listener is attached once.
  const leaveRef = useRef<() => boolean>(() => false)
  leaveRef.current = () => {
    if (over === null) return false
    onLeave()
    return true
  }
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key !== 'Escape' || event.repeat || keyIsSpoken(event)) return
      if (leaveRef.current()) event.preventDefault()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [])

  const marks = useMemo(() => ladderMarks(game, remembered), [game, remembered])
  const listening = phase === 'listening'
  const failed = clipState === 'failed'
  const loading = clipState === 'loading'

  // The reveal on screen: the one a right guess brought, or the one a game ended with.
  const shownReveal = over !== null ? over.reveal : phase === 'revealed' ? game.reveal : null
  const revealAutoplays = over !== null ? !overRestored : game.round !== openedRevealRound
  const revealInMyList = (shownReveal?.inMyList ?? false) || added

  const heard = game.reveal?.heardSeconds

  return (
    <div className="guess-page theme-board">
      <h1 className="visually-hidden">Guess the Opening/Ending</h1>

      <div className="guess-bar">
        {over === null ? (
          <>
            <button
              type="button"
              className="game-outline-button guess-bar__end"
              aria-disabled={busy !== null}
              onClick={() => void endGame()}
            >
              End game
            </button>
            {listening ? (
              <GuessSearch
                ref={searchRef}
                guessedIds={guessedIds}
                sending={busy === 'guess' || failed}
                onGuess={(pick, typed) => void guess(pick, typed)}
              />
            ) : (
              <p className="guess-bar__verdict guess-bar__verdict--won">
                {heard != null ? `You got it at ${formatLength(heard)}` : 'You got it'}
              </p>
            )}
            <span aria-hidden="true" />
          </>
        ) : (
          <>
            <button type="button" className="game-outline-button guess-bar__end" onClick={onLeave}>
              Back to start
            </button>
            <p className={`guess-bar__verdict guess-bar__verdict--${verdictTone(over)}`}>{verdictText(over)}</p>
            <button
              ref={againRef}
              type="button"
              className="game-button game-button--small guess-bar__again"
              disabled={restarting}
              onClick={() => void playAgain()}
            >
              Play again
            </button>
          </>
        )}
      </div>

      <div className="guess-scroll theme-stage">
        <div className="theme-stage__inner">
          {listening && (
            <>
              {failed ? (
                <div className="theme-failed" role="status">
                  <p>The song couldn't be loaded.</p>
                  <button type="button" className="game-button game-button--small" onClick={tryAgain}>
                    Try again
                  </button>
                </div>
              ) : (
                <button
                  type="button"
                  className={'theme-play' + (playing ? ' theme-play--playing' : '') + (loading ? ' theme-play--loading' : '')}
                  aria-label={loading ? 'Loading the song' : `Play ${spokenLength(length)}`}
                  aria-busy={loading}
                  disabled={loading}
                  onClick={pressPlay}
                >
                  <span className="theme-play__icon" aria-hidden="true">
                    {loading ? (
                      <span className="theme-play__spinner" />
                    ) : playing ? (
                      <span className="theme-play__bars">
                        <i />
                        <i />
                        <i />
                      </span>
                    ) : (
                      <svg viewBox="0 0 24 24" width="40" height="40" focusable="false">
                        <path d="M8 5.5v13a1 1 0 0 0 1.5.86l10.5-6.5a1 1 0 0 0 0-1.72L9.5 4.64A1 1 0 0 0 8 5.5Z" fill="currentColor" />
                      </svg>
                    )}
                  </span>
                  <span className="theme-play__length" aria-hidden="true">
                    {loading ? 'Loading…' : formatLength(length)}
                  </span>
                </button>
              )}

              {loading && slowLoad && (
                <p className="theme-loading-note" role="status">
                  AnimeThemes is slow to send this song. Still waiting for it…
                </p>
              )}

              {!loading && !failed && heldBack && (
                <p className="theme-loading-note" role="status">
                  Press play to hear the song.
                </p>
              )}

              <ThemeLadder lengths={game.lengths} step={game.step} marks={marks} />

              {!failed && (
                <button
                  type="button"
                  className="game-outline-button theme-skip"
                  aria-disabled={busy !== null}
                  onClick={() => void skip()}
                >
                  Skip
                </button>
              )}

              {game.guessed.length > 0 && (
                <ol className="theme-guesses" aria-label="Your guesses this round">
                  {game.guessed.map((card) => (
                    <li key={card.animeId} className="theme-guess">
                      <RowPicture src={card.pictureUrl} className="theme-guess__thumb" />
                      <span className="theme-guess__title">{pickDisplayTitle(card.title, card.englishTitle)}</span>
                    </li>
                  ))}
                </ol>
              )}
            </>
          )}

          {!listening && shownReveal !== null && (
            <div className="theme-stage__reveal" ref={revealBoxRef}>
              <ThemeReveal
                key={shownReveal.videoUrl}
                reveal={shownReveal}
                setApart={over !== null && (over.kind === 'Lost' || over.kind === 'GaveUp')}
                plain={over !== null && over.kind === 'Stopped'}
                autoplay={revealAutoplays}
                inMyList={revealInMyList}
                showListMark={!onlyMyList}
                adding={adding}
                onAdd={() => void addToList(shownReveal)}
                onNext={over === null ? () => void nextSong() : undefined}
                nexting={busy === 'next'}
                nextRef={nextRef}
              />
            </div>
          )}

          {over !== null && over.reveal === null && <p className="theme-stage__empty">There are no more songs to play.</p>}
        </div>
      </div>

      {confetti && <Confetti level={WIN_CONFETTI_LEVEL} originRef={revealBoxRef} onDone={() => setConfetti(false)} />}
      <p className="visually-hidden" aria-live="polite">
        {announcement}
      </p>
    </div>
  )
}
