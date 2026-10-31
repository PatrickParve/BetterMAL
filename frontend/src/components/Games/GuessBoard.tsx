import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import { ApiError, endGuessTheAnime, guessTheAnime, updateEntry } from '../../api/client.ts'
import type {
  GuessTheAnimeAnswerDto,
  GuessTheAnimeGameDto,
  GuessTheAnimeGuessDto,
  GuessTheAnimeRowDto,
} from '../../api/types.ts'
import { useActionFailure } from '../../context/ActionFailureContext.tsx'
import { useRestorableScroll } from '../../hooks/useRestorableScroll.ts'
import { usePageState } from '../../state/PageStateContext.tsx'
import { pickDisplayTitle } from '../../utils/anime.ts'
import { preloadPictures } from '../../utils/preloadPictures.ts'
import { Confetti } from './Confetti.tsx'
import { GuessRow, type GuessRowAnime } from './GuessRow.tsx'
import { GuessSearch, type GuessSearchHandle, type GuessSearchPick } from './GuessSearch.tsx'
import type { FinishedGuessGame, GuessOver } from './guessGame.ts'
import { describeGuessRow } from './guessRowText.ts'
import './GameButton.css'
import './GuessBoard.css'

// guessing: the field is open for a guess. sending: a guess is on its way, its row
// already on the board. over: the game has ended, and its answer stays on screen
// until I leave.
type Phase = 'guessing' | 'sending' | 'over'

type GuessBoardProps = {
  game: GuessTheAnimeGameDto
  /** A game that has ended and is being returned to by Back or Forward: how it ended, with its rows in `game`. */
  finished: FinishedGuessGame | null
  /** Under Only my list every answer is on my list, so its row says nothing of it. */
  onlyMyList: boolean
  /** A guess landed: the page keeps its read current, so Back restores the rows. */
  onGuessed: (row: GuessTheAnimeRowDto) => void
  /** The game has ended: the page keeps it in this history entry. `rows` are every guess, the last one included. */
  onFinished: (rows: GuessTheAnimeRowDto[], over: GuessOver) => void
  /** The answer was added to my list from its row. */
  onAdded: () => void
  /** Back to the setup screen, from a game that has ended. */
  onLeave: () => void
  /** Starts a new game with the settings this one was played with. The page then swaps the new game in, which replaces this board. */
  onPlayAgain: () => Promise<void>
  /** A guess or End game found the game moved on elsewhere: re-read it, with no notice. */
  onStale: () => void
}

// Long enough for a picture that is on its way; a slower one is drawn when it
// arrives, and never holds the answer back.
const ANSWER_PICTURE_WAIT_MS = 1000

// How far above the bottom of the list a new row is brought to rest, and the
// confetti's size: one burst at level 1 (60 pieces), as design.md D9 sets out.
const ROW_BREATHING_ROOM_PX = 16
const WIN_CONFETTI_LEVEL = 1

const prefersReducedMotion = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches
const forcedColors = () => window.matchMedia('(forced-colors: active)').matches

// The keys the game answers to (Escape, once the game is over) are left alone
// where they belong to something else: a modifier held, a text field or dropdown
// with focus, or an open overlay, as Higher or Lower's are (design.md D16).
function keyIsSpoken(event: KeyboardEvent): boolean {
  if (event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return true
  if (document.querySelector('[role="dialog"]')) return true
  const target = event.target
  return (
    target instanceof HTMLElement &&
    (target.isContentEditable || target.closest('input, textarea, select, [role="listbox"], [role="menu"]') !== null)
  )
}

function verdictText(over: GuessOver): string {
  if (over.kind === 'Guessed') return `You got it in ${over.guesses}`
  return over.kind === 'OutOfGuesses' ? 'You lost: out of guesses' : 'You gave up'
}

// What a screen reader hears when a game ends: how it ended, the answer and the
// way back.
function endAnnouncement(over: GuessOver): string {
  const title = pickDisplayTitle(over.answer.row.title, over.answer.row.englishTitle)
  const how =
    over.kind === 'Guessed'
      ? `You got it in ${over.guesses}.`
      : over.kind === 'OutOfGuesses'
        ? 'You lost: out of guesses.'
        : 'You gave up.'
  return `${how} The answer is ${title}. Press Escape to go back to the start.`
}

// The game in play (guess-the-anime, design.md D10). The server holds the game and
// judges every guess, so this is a small state machine that sends what I pick and
// draws what comes back: pick, send with the row already on the board, fill it in;
// or send, and end. A finished game stays here until I leave it, with no timers:
// nothing on this board moves by itself.
export function GuessBoard({
  game,
  finished,
  onlyMyList,
  onGuessed,
  onFinished,
  onAdded,
  onLeave,
  onPlayAgain,
  onStale,
}: GuessBoardProps) {
  const reportFailure = useActionFailure()
  const { isRestore } = usePageState()

  const [phase, setPhase] = useState<Phase>(finished ? 'over' : 'guessing')
  // The guess on its way: its picture and title, and the text it took out of the
  // field, which a guess that can't go ahead puts back.
  const [pending, setPending] = useState<{ anime: GuessRowAnime; typed: string } | null>(null)
  const [over, setOver] = useState<GuessOver | null>(finished?.over ?? null)
  const [ending, setEnding] = useState(false)
  const [restarting, setRestarting] = useState(false)
  const [announcement, setAnnouncement] = useState('')
  const [confetti, setConfetti] = useState(false)
  // The answer, once I've added it from its row, and the add in flight.
  const [added, setAdded] = useState(finished?.added ?? false)
  const [adding, setAdding] = useState(false)
  // The rows the game opened on come in with no reveal: a resumed board shouldn't replay.
  const [openedOn] = useState(() => new Set(game.rows.map((row) => row.animeId)))

  const searchRef = useRef<GuessSearchHandle>(null)
  const againRef = useRef<HTMLButtonElement>(null)
  const rowsRef = useRef<HTMLOListElement>(null)
  const scrollRef = useRef<HTMLDivElement | null>(null)
  const answerRowRef = useRef<HTMLLIElement>(null)
  const mountedRef = useRef(true)
  const busyRef = useRef(false)

  useEffect(() => {
    mountedRef.current = true
    return () => {
      mountedRef.current = false
    }
  }, [])

  // The field takes focus when a game starts where there is a keyboard, and not on
  // a phone, where it would put the on-screen keyboard over the board.
  useEffect(() => {
    if (finished === null && window.matchMedia('(pointer: fine)').matches) searchRef.current?.focus()
    // A game I'm resuming opens at its latest guess; one I'm returning to by Back or
    // Forward is put back where I had scrolled it (below).
    const box = scrollRef.current
    if (box && !isRestore) box.scrollTop = box.scrollHeight
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // The list of guesses is the one thing on the page that scrolls, so it is
  // remembered for Back and Forward like any other scroller.
  const restoreScroll = useRestorableScroll('guess-rows', 'vertical')
  const scrollBoxRef = useCallback(
    (el: HTMLDivElement | null) => {
      scrollRef.current = el
      restoreScroll(el)
    },
    [restoreScroll],
  )

  // The end moves focus to Play again, the thing I most likely want next.
  useEffect(() => {
    if (over !== null) againRef.current?.focus({ preventScroll: true })
  }, [over])

  const guessedIds = useMemo(() => {
    const ids = new Set(game.rows.map((row) => row.animeId))
    if (pending !== null) ids.add(pending.anime.animeId)
    return ids
  }, [game.rows, pending])

  // A new row that lands below what the list shows is brought into view; one
  // already in view moves nothing. The list is the only thing that scrolls, so it is
  // scrolled by its own offset (design.md D12). It runs for the pending row, for its
  // stats arriving (a card can grow) and for the answer's row, and not for the rows
  // the board opened on.
  const lastCheckedRef = useRef<string | null>(null)
  useLayoutEffect(() => {
    const key = `${game.rows.length}:${pending?.anime.animeId ?? ''}:${over?.answer.row.animeId ?? ''}`
    // The first run is the board opening, and an unchanged key is an effect run twice
    // in development: neither is a row arriving.
    const unchanged = lastCheckedRef.current === null || lastCheckedRef.current === key
    lastCheckedRef.current = key
    if (unchanged) return
    const box = scrollRef.current
    const last = rowsRef.current?.lastElementChild
    if (!box || !last) return
    const hidden = last.getBoundingClientRect().bottom - box.getBoundingClientRect().bottom
    if (hidden <= 0) return
    box.scrollTo({
      top: box.scrollTop + hidden + ROW_BREATHING_ROOM_PX,
      behavior: prefersReducedMotion() ? 'auto' : 'smooth',
    })
  }, [game.rows.length, pending, over])

  function finish(next: GuessOver, rows: GuessTheAnimeRowDto[]) {
    onFinished(rows, next)
    setOver(next)
    setPhase('over')
    setPending(null)
    setAnnouncement(endAnnouncement(next))
    if (next.kind === 'Guessed' && !prefersReducedMotion() && !forcedColors()) setConfetti(true)
  }

  async function guess(pick: GuessSearchPick, typed: string) {
    if (busyRef.current || phase === 'over') return
    busyRef.current = true
    const number = game.rows.length + 1
    setPhase('sending')
    setPending({
      anime: { animeId: pick.id, title: pick.title, englishTitle: pick.englishTitle, pictureUrl: pick.pictureUrl },
      typed,
    })

    let res: GuessTheAnimeGuessDto
    try {
      res = await guessTheAnime(game.id, number, pick.id)
    } catch (err) {
      if (!mountedRef.current) return
      // The row goes and the guess isn't counted. A game that moved on elsewhere is
      // re-read with no notice, since nothing was lost; anything else keeps what I
      // typed in the field and says what happened.
      busyRef.current = false
      setPending(null)
      setPhase('guessing')
      if (err instanceof ApiError && (err.status === 404 || err.status === 409)) {
        onStale()
        return
      }
      reportFailure({
        title: err instanceof ApiError ? "Your guess wasn't counted" : "Your guess wasn't sent",
        reason: err instanceof ApiError ? err.reason : null,
      })
      searchRef.current?.restore(typed)
      return
    }
    if (!mountedRef.current) return

    // Its row lands in the read, so Back restores the board with it.
    onGuessed(res.row)
    if (res.end === null || res.answer === null) {
      setPending(null)
      setPhase('guessing')
      busyRef.current = false
      setAnnouncement(describeGuessRow(res.row))
      return
    }

    // The game is over. A guess's own picture is already drawn; an answer that
    // wasn't guessed is loaded before its row appears, where it can be within about
    // a second, and the row of the guess that ran out the game stays up meanwhile.
    setPending(null)
    if (res.end === 'OutOfGuesses') {
      await preloadPictures([res.answer.row.pictureUrl], 'row', ANSWER_PICTURE_WAIT_MS)
      if (!mountedRef.current) return
    }
    finish({ kind: res.end, answer: res.answer, guesses: number }, [...game.rows, res.row])
  }

  async function endGame() {
    if (busyRef.current || ending || phase !== 'guessing') return
    busyRef.current = true
    setEnding(true)
    try {
      const res = await endGuessTheAnime(game.id)
      await preloadPictures([res.answer.row.pictureUrl], 'row', ANSWER_PICTURE_WAIT_MS)
      if (!mountedRef.current) return
      setEnding(false)
      finish({ kind: 'Ended', answer: res.answer, guesses: game.rows.length }, game.rows)
    } catch (err) {
      if (!mountedRef.current) return
      busyRef.current = false
      setEnding(false)
      if (err instanceof ApiError && (err.status === 404 || err.status === 409)) onStale()
      else reportFailure({ title: "Couldn't end the game", reason: err instanceof ApiError ? err.reason : null })
    }
  }

  async function playAgain() {
    if (restarting) return
    setRestarting(true)
    try {
      // The new game replaces this board, so there is nothing to reset here.
      await onPlayAgain()
    } catch (err) {
      if (!mountedRef.current) return
      setRestarting(false)
      // The settings no longer leave enough anime: the setup screen says so.
      if (err instanceof ApiError && err.status === 409) onLeave()
      else reportFailure({ title: "Couldn't start a new game", reason: err instanceof ApiError ? err.reason : null })
    }
  }

  async function addAnswer(answer: GuessTheAnimeAnswerDto) {
    if (adding) return
    setAdding(true)
    try {
      await updateEntry(answer.row.animeId, { status: 'PlanToWatch' })
      onAdded()
      if (mountedRef.current) setAdded(true)
    } catch (err) {
      // The row still offers Add to list, and the notice says why.
      reportFailure({
        title: `Couldn't add ${pickDisplayTitle(answer.row.title, answer.row.englishTitle)} to list`,
        reason: err instanceof ApiError ? err.reason : null,
      })
    } finally {
      if (mountedRef.current) setAdding(false)
    }
  }

  // Escape goes back to the setup screen, but only once the game is over: during
  // play it only closes the dropdown (which the field handles), and a stray press
  // must not throw a game away. The latest phase is read through a ref, so the
  // listener is attached once.
  const leaveRef = useRef<() => boolean>(() => false)
  leaveRef.current = () => {
    if (phase !== 'over') return false
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

  const answerFor = (row: GuessRowAnime) =>
    over !== null && over.answer.row.animeId === row.animeId
      ? {
          inMyList: over.answer.inMyList || added,
          showListMark: !onlyMyList,
          adding,
          onAdd: () => void addAnswer(over.answer),
          lost: over.kind !== 'Guessed',
        }
      : null

  // The answer under the guesses, unless it was a guess: a win's last row is it.
  const answerAppended = over !== null && !game.rows.some((row) => row.animeId === over.answer.row.animeId)
  const current = Math.min(game.rows.length + 1, game.maxGuesses)

  // One flat list under one set of keys, so the pending row and the row that
  // replaces it are the same element and the reveal plays once.
  const rowElements = game.rows.map((row) => {
    const answer = answerFor(row)
    return (
      <GuessRow
        key={row.animeId}
        ref={answer ? answerRowRef : undefined}
        anime={row}
        stats={row}
        answer={answer}
        reveal={!openedOn.has(row.animeId)}
      />
    )
  })
  if (pending !== null) {
    rowElements.push(<GuessRow key={pending.anime.animeId} anime={pending.anime} stats={null} answer={null} reveal />)
  }
  if (over !== null && answerAppended) {
    rowElements.push(
      <GuessRow
        key={over.answer.row.animeId}
        ref={answerRowRef}
        anime={over.answer.row}
        stats={over.answer.row}
        answer={answerFor(over.answer.row)}
        reveal
      />,
    )
  }

  return (
    <div className="guess-page">
      <h1 className="visually-hidden">Guess the Anime</h1>

      <div className="guess-bar">
        {over === null ? (
          <>
            <button
              type="button"
              className="game-outline-button guess-bar__end"
              aria-disabled={ending || phase === 'sending'}
              onClick={() => void endGame()}
            >
              End game
            </button>
            <GuessSearch ref={searchRef} guessedIds={guessedIds} sending={phase === 'sending'} onGuess={(pick, typed) => void guess(pick, typed)} />
            <div className="guess-bar__count">
              <span className="guess-bar__label">Guess</span>
              <span className="guess-bar__value">
                {current} / {game.maxGuesses}
              </span>
            </div>
          </>
        ) : (
          <>
            <button type="button" className="game-outline-button guess-bar__end" onClick={onLeave}>
              Back to start
            </button>
            <p className={`guess-bar__verdict guess-bar__verdict--${over.kind === 'Guessed' ? 'won' : 'lost'}`}>
              {verdictText(over)}
            </p>
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

      <div className="guess-scroll" ref={scrollBoxRef}>
        <div className="guess-head" aria-hidden="true">
          <span>Anime</span>
          <span>Year</span>
          <span>Season</span>
          <span>Genres</span>
          <span>Eps</span>
          <span>Studio</span>
          <span>Type</span>
          <span>MAL</span>
        </div>

        <ol className="guess-rows" ref={rowsRef} aria-label="Your guesses">
          {rowElements}
        </ol>
      </div>

      {confetti && <Confetti level={WIN_CONFETTI_LEVEL} originRef={answerRowRef} onDone={() => setConfetti(false)} />}
      <p className="visually-hidden" aria-live="polite">
        {announcement}
      </p>
    </div>
  )
}
