import { useEffect, useRef, useState } from 'react'
import {
  ApiError,
  discardThisOrThat,
  pickThisOrThat,
  saveThisOrThat,
  startThisOrThat,
  undoThisOrThat,
} from '../../api/client.ts'
import type { ThisOrThatScoreStateDto } from '../../api/types.ts'
import { preloadPictures } from '../../utils/preloadPictures.ts'
import '../Games/GameButton.css'
import { ThisOrThatPair } from './ThisOrThatPair.tsx'
import { ThisOrThatResult } from './ThisOrThatResult.tsx'
import { PICK_MARK_MS } from './thisOrThatTiming.ts'
import './ThisOrThatScore.css'

type ThisOrThatScoreProps = {
  state: ThisOrThatScoreStateDto
  /** The state is held from another score while this one loads: nothing on it acts. */
  stale: boolean
  /** The score's state as the server now has it, after an action. The page keeps its read current. */
  onState: (next: ThisOrThatScoreStateDto) => void
  /** An action found the ranking moved on elsewhere (a second tab, a change to my list): read it again. */
  onReread: () => Promise<void>
  /** A ranking began or ended: the tabs' counts and marks need reading again. */
  onTabsChanged: () => void
}

// The longest the page waits for the first pair's pictures; a slow one is drawn when it comes.
const PRELOAD_WAIT_MS = 1200
// Long past a pick's mark: a picture that isn't in by then isn't going to be.
const UPCOMING_PRELOAD_LIMIT_MS = 10000

const prefersReducedMotion = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches

const picksLabel = (n: number) => `${n.toLocaleString('en-US')} ${n === 1 ? 'pick' : 'picks'}`

// One score of the This or That page: the Start panel while no ranking is in
// progress, the pair while one is, the result once it is finished
// (add-this-or-that-ranking design.md D11). The server holds the ranking and
// returns the state each action leaves it in, so this paces what it is told: every
// action runs one at a time, a failed one says so in place and leaves the screen as
// it was, and one refused as out of date (404 or 409) reads the score again, with no
// notice, since nothing was lost. The page mounts it keyed on the score, so a switch
// of tab never carries a half-open confirm or a message over.
export function ThisOrThatScore({ state, stale, onState, onReread, onTabsChanged }: ThisOrThatScoreProps) {
  const { score, count, maxPicks, run } = state

  // An action is in flight, or a pick's mark is up. The ref is the guard (a second
  // click lands before React re-renders); the state is what the screen shows.
  const [busy, setBusy] = useState(false)
  const busyRef = useRef(false)
  const [pickedId, setPickedId] = useState<number | null>(null)
  // A confirm is up in place of the HUD: to start the ranking over, or to throw it away.
  const [confirming, setConfirming] = useState<'restart' | 'cancel' | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)
  const mountedRef = useRef(true)

  useEffect(() => {
    mountedRef.current = true
    return () => {
      mountedRef.current = false
    }
  }, [])

  // The pictures of whichever anime the next pair could hold, whichever way the pick
  // goes, start downloading while this pair is on screen. Nothing waits on them.
  const upcoming = run?.upcomingPictureUrls
  useEffect(() => {
    if (upcoming !== undefined && upcoming.length > 0) void preloadPictures(upcoming, 'card', UPCOMING_PRELOAD_LIMIT_MS)
  }, [upcoming])

  // Runs one action at a time. A request the server refuses as out of date reads the
  // score again; any other failure says `failure` in place.
  async function perform(action: () => Promise<void>, failure: string) {
    if (busyRef.current || stale) return
    busyRef.current = true
    setBusy(true)
    setMessage(null)
    try {
      await action()
    } catch (err) {
      if (!mountedRef.current) return
      if (err instanceof ApiError && (err.status === 404 || err.status === 409)) await onReread()
      else setMessage(failure)
    } finally {
      if (mountedRef.current) {
        busyRef.current = false
        setBusy(false)
        setPickedId(null)
      }
    }
  }

  // The first pair of a ranking is waited for, so it opens with its pictures drawn
  // rather than filling them in a beat later.
  async function startRanking(replacingVersion?: number) {
    await perform(async () => {
      const next = await startThisOrThat(score, replacingVersion)
      const pair = next.run?.pair
      if (pair) await preloadPictures([pair.left.pictureUrl, pair.right.pictureUrl], 'card', PRELOAD_WAIT_MS)
      if (!mountedRef.current) return
      setConfirming(null)
      setSaved(false)
      onState(next)
    }, "Couldn't start. Try again.")
  }

  // winnerId null is Can't decide. The pick is sent at once; the next pair shows when
  // both the mark's moment and the answer are in. Under reduced motion there is no
  // moment to wait out.
  async function pick(winnerId: number | null) {
    const pair = run?.pair
    if (!pair) return
    await perform(async () => {
      setPickedId(winnerId)
      const mark = prefersReducedMotion() ? Promise.resolve() : new Promise<void>((resolve) => setTimeout(resolve, PICK_MARK_MS))
      const next = await pickThisOrThat(score, pair.left.animeId, pair.right.animeId, winnerId)
      await mark
      if (mountedRef.current) onState(next)
    }, "That pick didn't go through.")
  }

  async function undo() {
    if (!run) return
    const version = run.version
    await perform(async () => {
      const next = await undoThisOrThat(score, version)
      if (mountedRef.current) onState(next)
    }, "Undo didn't go through.")
  }

  // The ranking is over and the score is back to its Start panel.
  function ended(note: boolean) {
    setConfirming(null)
    setSaved(note)
    onState({ score, count, maxPicks, run: null })
    onTabsChanged()
  }

  async function save() {
    if (!run) return
    const version = run.version
    await perform(async () => {
      await saveThisOrThat(score, version)
      if (mountedRef.current) ended(true)
    }, "Couldn't save. Try again.")
  }

  // Cancel: the ranking's picks are thrown away (after the confirm) and the score is back to
  // its Start panel, with the ranking as it was.
  async function cancelRanking() {
    if (!run) return
    const version = run.version
    await perform(async () => {
      await discardThisOrThat(score, version)
      if (mountedRef.current) ended(false)
    }, "Couldn't cancel. Try again.")
  }

  async function keep() {
    if (!run) return
    const version = run.version
    await perform(async () => {
      await discardThisOrThat(score, version)
      if (mountedRef.current) ended(false)
    }, "Couldn't keep it. Try again.")
  }

  const statusLine = message !== null && (
    <p className="this-or-that-score__message" role="status">
      {message}
    </p>
  )

  return (
    <div className="this-or-that-score">
      {run === null && (
        <div className="this-or-that-start">
          <p className="this-or-that-start__summary">
            <b>{count.toLocaleString('en-US')}</b> anime · up to <b>{picksLabel(maxPicks)}</b>
          </p>
          <button type="button" className="game-button" aria-disabled={busy} onClick={() => void startRanking()}>
            Start
          </button>
          {saved && (
            <p className="this-or-that-start__saved" aria-hidden="true">
              Saved.
            </p>
          )}
          {statusLine}
        </div>
      )}

      {run !== null && run.pair !== null && (
        <div className="this-or-that-ranking">
          {confirming === 'restart' ? (
            <div className="this-or-that-hud this-or-that-hud--confirm">
              <p className="this-or-that-hud__question">Throw away {picksLabel(run.picksMade)} and start again?</p>
              <div className="this-or-that-hud__buttons">
                <button
                  type="button"
                  className="game-button game-button--small"
                  aria-disabled={busy}
                  onClick={() => void startRanking(run.version)}
                >
                  Start over
                </button>
                <button type="button" className="game-outline-button" aria-disabled={busy} onClick={() => setConfirming(null)}>
                  Cancel
                </button>
              </div>
            </div>
          ) : confirming === 'cancel' ? (
            <div className="this-or-that-hud this-or-that-hud--confirm">
              <p className="this-or-that-hud__question">Throw away {picksLabel(run.picksMade)}?</p>
              <div className="this-or-that-hud__buttons">
                <button type="button" className="game-button game-button--small" aria-disabled={busy} onClick={() => void cancelRanking()}>
                  Throw away
                </button>
                <button type="button" className="game-outline-button" aria-disabled={busy} onClick={() => setConfirming(null)}>
                  Keep ranking
                </button>
              </div>
            </div>
          ) : (
            <div className="this-or-that-hud">
              <div className="this-or-that-hud__left">
                {run.canUndo && (
                  <button type="button" className="game-outline-button" aria-disabled={busy} onClick={() => void undo()}>
                    Undo
                  </button>
                )}
              </div>
              <p className="this-or-that-hud__progress">
                {run.placed.toLocaleString('en-US')} of {run.total.toLocaleString('en-US')} placed · up to {picksLabel(run.picksLeftAtMost)} left
              </p>
              <div className="this-or-that-hud__right">
                <button type="button" className="game-outline-button" aria-disabled={busy} onClick={() => setConfirming('restart')}>
                  Start over
                </button>
                <button type="button" className="game-outline-button" aria-disabled={busy} onClick={() => setConfirming('cancel')}>
                  Cancel
                </button>
              </div>
            </div>
          )}

          <ThisOrThatPair
            pair={run.pair}
            size="page"
            locked={busy}
            pickedId={pickedId}
            keysActive={!stale && confirming === null}
            canUndo={run.canUndo}
            onPick={(animeId) => void pick(animeId)}
            onCantDecide={() => void pick(null)}
            onUndo={() => void undo()}
          />

          <div className="this-or-that-ranking__under">
            <button type="button" className="game-outline-button" aria-disabled={busy} onClick={() => void pick(null)}>
              Can't decide
            </button>
          </div>
          {/* A line's room that stays, so a message in it moves nothing. */}
          <div className="this-or-that-score__note">{statusLine}</div>
        </div>
      )}

      {run !== null && run.result !== null && (
        <>
          <ThisOrThatResult
            rows={run.result}
            canUndo={run.canUndo}
            busy={busy}
            onSave={() => void save()}
            onKeep={() => void keep()}
            onUndo={() => void undo()}
          />
          {statusLine}
        </>
      )}

      {/* Always mounted, so the words are announced when they appear. */}
      <p className="visually-hidden" aria-live="polite">
        {run === null && saved ? 'Saved.' : ''}
      </p>
    </div>
  )
}
