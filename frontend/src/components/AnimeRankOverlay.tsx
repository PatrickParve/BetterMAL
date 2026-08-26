import { useEffect, useRef, useState, type PointerEvent } from 'react'
import { Modal } from './Modal.tsx'
import { getRanking, putTopAnimeOrder } from '../api/client.ts'
import type { AnimeRankingScoreDto, TopAnimeMediaType, TopAnimeSectionDto } from '../api/types.ts'
import { pickDisplayTitle } from '../utils/anime.ts'
import './AnimeRankOverlay.css'

// One row's worth of what both editing surfaces need: TopAnimeEntryDto (mode
// 'top') and AnimeRankingMemberDto (mode 'all') already share this exact
// shape, so a tier built from either can be rendered and dragged by the same
// code (design.md D9).
type Member = { animeId: number; title: string; englishTitle: string | null; pictureUrl: string | null; rank: number }
type EditableTier = { score: number; members: Member[]; includedCount: number }

type AnimeRankOverlayProps =
  | {
      mode: 'top'
      section: TopAnimeSectionDto
      mediaType: TopAnimeMediaType
      mediaTypeLabel: string
      // Every reorder saves itself (see moveMember/scheduleSave below), so
      // there's only one way to leave this editor — it always flushes any
      // not-yet-saved edit first, then calls this.
      onSaved: () => void
      // "leaves the top-anime arrangement and opens the whole-library ranking
      // editor" (profile-stats spec) — the caller closes this editor and
      // opens the context-mounted 'all'-mode instance in its place.
      onRankWholeLibrary: () => void
    }
  | {
      mode: 'all'
      mediaType: TopAnimeMediaType
      mediaTypeLabel: string
      // anime-ranking "The ranking editor can open on one anime": both given
      // together or neither — the caller already knows the anime's current
      // score at the moment it opens this (the entry editor's Rank action,
      // the completion prompt's save-and-rank).
      focusAnimeId?: number
      focusScore?: number
      onSaved: () => void
    }

const TOP_LIST_SIZE = 10
const DRAG_THRESHOLD = 4
const EDGE_ZONE = 48
const MIN_SCROLL_SPEED = 4
const MAX_SCROLL_SPEED = 18

function promoteBoundary(tier: EditableTier): number {
  return Math.min(tier.includedCount, TOP_LIST_SIZE)
}

// Captured on pointerdown; promoted to an ActiveDrag once the pointer clears
// DRAG_THRESHOLD, so a plain click on a row still behaves like a click.
type ArmedDrag = {
  tierIndex: number
  fromIndex: number
  startX: number
  startY: number
  offsetX: number
  offsetY: number
  width: number
}

type ActiveDrag = {
  tierIndex: number
  fromIndex: number
  targetIndex: number
  x: number
  y: number
  offsetX: number
  offsetY: number
  width: number
}

function resolveTargetIndex(clientX: number, clientY: number, tierIndex: number, fallback: number): number {
  const hit = document.elementFromPoint(clientX, clientY)
  const rowEl = hit instanceof Element ? hit.closest('[data-row-index]') : null
  if (!(rowEl instanceof HTMLElement)) return fallback
  if (Number(rowEl.dataset.tierIndex) !== tierIndex) return fallback
  return Number(rowEl.dataset.rowIndex)
}

function MemberPicture({ member }: { member: Member }) {
  return member.pictureUrl ? (
    <img src={member.pictureUrl} alt="" className="anime-rank__picture" />
  ) : (
    <div className="anime-rank__picture anime-rank__picture--placeholder" aria-hidden="true" />
  )
}

function toEditableTiers(section: TopAnimeSectionDto): EditableTier[] {
  return section.tiers.map((tier) => ({
    score: tier.score,
    members: tier.members.map((m) => ({ animeId: m.animeId, title: m.title, englishTitle: m.englishTitle, pictureUrl: m.pictureUrl, rank: m.myRank })),
    includedCount: tier.includedCount,
  }))
}

// The ranking editor (anime-ranking capability): arranges one score tier at
// a time, always through drag/arrows/promote — identical machinery whether
// it's rendering the profile's top-list tiers (mode 'top', every tier at
// once, with a cut line) or one score of the whole library fetched on demand
// (mode 'all', via a score selector, with no cut line since everything shown
// is already in the ranking). Generalised from TopAnimeSelectionOverlay
// (design.md D9).
export function AnimeRankOverlay(props: AnimeRankOverlayProps) {
  const { mode, mediaType, mediaTypeLabel, onSaved } = props
  // Pre-narrowed once here: TypeScript can't follow discriminated-union
  // narrowing through the separately destructured `mode` above, only
  // through direct `props.mode` checks — doing that everywhere below would
  // be noise, so it's done once.
  const onRankWholeLibrary = props.mode === 'top' ? props.onRankWholeLibrary : undefined
  const focusAnimeId = props.mode === 'all' ? props.focusAnimeId : undefined
  const focusScore = props.mode === 'all' ? props.focusScore : undefined

  const [tiers, setTiers] = useState<EditableTier[]>(props.mode === 'top' ? toEditableTiers(props.section) : [])
  const [scores, setScores] = useState<AnimeRankingScoreDto[]>([])
  const [selectedScore, setSelectedScore] = useState<number | null>(mode === 'all' ? (focusScore ?? null) : null)
  const [loadingTier, setLoadingTier] = useState(mode === 'all')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [drag, setDrag] = useState<ActiveDrag | null>(null)
  const dragging = drag !== null

  const armRef = useRef<ArmedDrag | null>(null)
  const dragStateRef = useRef<ActiveDrag | null>(null)
  const pointerYRef = useRef(0)
  const rafRef = useRef<number | null>(null)
  const tiersRef = useRef<HTMLDivElement | null>(null)
  const headerRef = useRef<HTMLDivElement | null>(null)
  const autoScrolledHeaderRef = useRef(false)
  const focusedRowRef = useRef<HTMLDivElement | null>(null)
  const hasScrolledToFocusRef = useRef(false)
  // Auto-save (no explicit Save button): latestTiersRef always mirrors the
  // current `tiers` state so the debounced save (which reads it from a
  // setTimeout, well after the render that scheduled it) never sends a
  // stale snapshot; saveChainRef serializes every actual PUT so two never
  // race; saveTimerRef is the pending debounce, if any.
  const latestTiersRef = useRef(tiers)
  latestTiersRef.current = tiers
  const saveChainRef = useRef<Promise<boolean>>(Promise.resolve(true))
  const saveTimerRef = useRef<number | null>(null)

  // mode 'all': loads the score selector plus one tier on open, and again
  // whenever the selector picks a score this session hasn't fetched yet.
  // Every tier fetched stays in `tiers` for the rest of the session (even
  // after switching away) so save can persist every score arranged, not just
  // the one currently on screen.
  useEffect(() => {
    if (mode !== 'all') return
    let cancelled = false
    setLoadingTier(true)
    setError(null)
    getRanking(mediaType, focusScore)
      .then((response) => {
        if (cancelled) return
        setScores(response.scores)
        const score = response.tier?.score ?? response.scores[0]?.score ?? null
        setSelectedScore(score)
        if (response.tier) {
          const tier: EditableTier = { score: response.tier.score, members: response.tier.members, includedCount: response.tier.members.length }
          setTiers((prev) => [...prev.filter((t) => t.score !== tier.score), tier])
        }
      })
      .catch(() => {
        if (!cancelled) setError('Could not load the ranking. Please try again.')
      })
      .finally(() => {
        if (!cancelled) setLoadingTier(false)
      })
    return () => {
      cancelled = true
    }
    // Runs once on mount only — mediaType/focusScore never change for the
    // life of this instance (a new one mounts per `openRanking` call).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  function selectScore(score: number) {
    if (score === selectedScore) return
    setSelectedScore(score)
    if (tiers.some((t) => t.score === score)) return
    setLoadingTier(true)
    setError(null)
    getRanking(mediaType, score)
      .then((response) => {
        if (!response.tier) return
        const tier: EditableTier = { score: response.tier.score, members: response.tier.members, includedCount: response.tier.members.length }
        setTiers((prev) => [...prev.filter((t) => t.score !== tier.score), tier])
      })
      .catch(() => setError('Could not load that score. Please try again.'))
      .finally(() => setLoadingTier(false))
  }

  // Scrolls the focused anime's row into view and marks it, once, the first
  // time its tier is actually on screen.
  useEffect(() => {
    if (mode !== 'all' || hasScrolledToFocusRef.current) return
    if (!focusedRowRef.current) return
    focusedRowRef.current.scrollIntoView({ block: 'center' })
    hasScrolledToFocusRef.current = true
  })

  function updateDrag(next: ActiveDrag | null) {
    dragStateRef.current = next
    setDrag(next)
  }

  // While a row is being dragged, smoothly scrolls the header (title,
  // Close, and — in whole-library mode — the score tabs) out of view,
  // exactly the way scrolling down normally would: nothing resizes or
  // reflows, so the row being dragged never jumps out from under the
  // pointer, and the topmost cards simply become visible in the space the
  // header vacates. Only engages when the header is still at least partly
  // visible at the moment the drag starts — a drag begun deep in a long
  // list leaves the scroll position exactly where it was, never yanking the
  // view back up — and a container with nothing to scroll (or already
  // scrolled past the header) is naturally a no-op, so real cards are never
  // cut off from the top to make room. Reverses only the scroll it actually
  // performed, once the drag ends.
  useEffect(() => {
    const container = tiersRef.current
    const header = headerRef.current
    if (!container || !header) return

    if (dragging) {
      if (container.scrollTop < header.offsetHeight) {
        autoScrolledHeaderRef.current = true
        container.scrollTo({ top: header.offsetHeight, behavior: 'smooth' })
      }
    } else if (autoScrolledHeaderRef.current) {
      autoScrolledHeaderRef.current = false
      container.scrollTo({ top: 0, behavior: 'smooth' })
    }
    // Reacts only to the drag/not-drag transition, not every pointer-move
    // update `drag` carries — re-running this per move would fight the
    // smooth scroll already in progress.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dragging])

  function moveMember(tierIndex: number, fromIndex: number, toIndex: number) {
    // Guarded up front (against the current render's own `tiers`, not
    // `prev` inside the updater) so a genuine no-op never schedules a save —
    // safe because moveMember is only ever called once per discrete user
    // action, never batched with another call in the same tick.
    const tier = tiers[tierIndex]
    if (!tier || toIndex < 0 || toIndex >= tier.members.length || fromIndex === toIndex) return
    setTiers((prev) => {
      const next = prev.map((t, i) => (i === tierIndex ? { ...t, members: [...t.members] } : t))
      const [moved] = next[tierIndex].members.splice(fromIndex, 1)
      next[tierIndex].members.splice(toIndex, 0, moved)
      return next
    })
    scheduleSave()
  }

  function stopAutoScroll() {
    if (rafRef.current != null) {
      cancelAnimationFrame(rafRef.current)
      rafRef.current = null
    }
  }

  function startAutoScroll() {
    if (rafRef.current != null) return
    const tick = () => {
      const container = tiersRef.current
      const current = dragStateRef.current
      if (!container || !current) {
        rafRef.current = null
        return
      }
      const rect = container.getBoundingClientRect()
      const y = pointerYRef.current
      let direction = 0
      let proximity = 0
      if (y < rect.top + EDGE_ZONE) {
        direction = -1
        proximity = (rect.top + EDGE_ZONE - y) / EDGE_ZONE
      } else if (y > rect.bottom - EDGE_ZONE) {
        direction = 1
        proximity = (y - (rect.bottom - EDGE_ZONE)) / EDGE_ZONE
      }
      if (direction !== 0) {
        const speed = MIN_SCROLL_SPEED + (MAX_SCROLL_SPEED - MIN_SCROLL_SPEED) * Math.min(proximity, 1)
        container.scrollTop += direction * speed
        const targetIndex = resolveTargetIndex(current.x, current.y, current.tierIndex, current.targetIndex)
        if (targetIndex !== current.targetIndex) updateDrag({ ...current, targetIndex })
      }
      rafRef.current = requestAnimationFrame(tick)
    }
    rafRef.current = requestAnimationFrame(tick)
  }

  // Escape is claimed here, in the capture phase, so a drag in progress
  // consumes it before Modal's bubble-phase listener can close the editor.
  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key !== 'Escape' || !dragStateRef.current) return
      event.stopPropagation()
      armRef.current = null
      stopAutoScroll()
      updateDrag(null)
    }
    document.addEventListener('keydown', handleKeyDown, { capture: true })
    return () => {
      document.removeEventListener('keydown', handleKeyDown, { capture: true })
      stopAutoScroll()
    }
  }, [])

  function handlePointerDown(event: PointerEvent<HTMLDivElement>, tierIndex: number, index: number) {
    if (event.button !== 0) return
    // Without this, holding and moving the mouse starts the browser's native
    // text-selection drag on whatever rows the pointer passes over.
    event.preventDefault()
    const rect = event.currentTarget.getBoundingClientRect()
    armRef.current = {
      tierIndex,
      fromIndex: index,
      startX: event.clientX,
      startY: event.clientY,
      offsetX: event.clientX - rect.left,
      offsetY: event.clientY - rect.top,
      width: rect.width,
    }
    event.currentTarget.setPointerCapture(event.pointerId)
  }

  function handlePointerMove(event: PointerEvent<HTMLDivElement>, tierIndex: number, index: number) {
    const current = dragStateRef.current
    if (current) {
      pointerYRef.current = event.clientY
      const targetIndex = resolveTargetIndex(event.clientX, event.clientY, current.tierIndex, current.targetIndex)
      updateDrag({ ...current, x: event.clientX, y: event.clientY, targetIndex })
      return
    }
    const armed = armRef.current
    if (!armed || armed.tierIndex !== tierIndex || armed.fromIndex !== index) return
    const dx = event.clientX - armed.startX
    const dy = event.clientY - armed.startY
    if (Math.hypot(dx, dy) < DRAG_THRESHOLD) return
    pointerYRef.current = event.clientY
    updateDrag({
      tierIndex: armed.tierIndex,
      fromIndex: armed.fromIndex,
      targetIndex: armed.fromIndex,
      x: event.clientX,
      y: event.clientY,
      offsetX: armed.offsetX,
      offsetY: armed.offsetY,
      width: armed.width,
    })
    startAutoScroll()
  }

  function handlePointerUp() {
    armRef.current = null
    const current = dragStateRef.current
    if (!current) return
    stopAutoScroll()
    if (current.targetIndex !== current.fromIndex) moveMember(current.tierIndex, current.fromIndex, current.targetIndex)
    updateDrag(null)
  }

  function handlePointerCancel() {
    armRef.current = null
    if (!dragStateRef.current) return
    stopAutoScroll()
    updateDrag(null)
  }

  // Persists the current order — chained onto whatever save is already in
  // flight (`saveChainRef`) so two PUTs are never racing each other; if an
  // older one somehow settled after a newer one, it would silently clobber
  // the newer order back to stale. Resolves to whether *this* call's save
  // succeeded, so a caller flushing before closing knows whether it's safe
  // to actually close.
  function persistNow(): Promise<boolean> {
    const run = async (): Promise<boolean> => {
      setSaving(true)
      setError(null)
      try {
        await putTopAnimeOrder(
          mediaType,
          latestTiersRef.current.map((tier) => ({ score: tier.score, animeIds: tier.members.map((m) => m.animeId) })),
        )
        return true
      } catch {
        setError('Could not save your changes. Please try again.')
        return false
      } finally {
        setSaving(false)
      }
    }
    const result = saveChainRef.current.then(run, run)
    saveChainRef.current = result
    return result
  }

  // Debounced auto-save (list-editing feel: no explicit Save button) — every
  // reorder calls this via moveMember, and a short quiet period after the
  // last one is what actually triggers the request, so a burst of arrow
  // clicks or a drag-then-immediate-second-drag collapses into one PUT
  // instead of one per click.
  function scheduleSave() {
    if (saveTimerRef.current != null) window.clearTimeout(saveTimerRef.current)
    saveTimerRef.current = window.setTimeout(() => {
      saveTimerRef.current = null
      void persistNow()
    }, 600)
  }

  // Flushes a pending debounced save immediately, or — if a save from an
  // earlier debounce is already in flight — waits for that instead, so a
  // caller closing right on the heels of an edit never fires its own
  // refresh (e.g. reloading the profile's top-anime section) before the PUT
  // carrying that edit has actually settled. Resolves false only when a
  // save was actually attempted here and failed, so the caller can keep the
  // editor open rather than close over an edit that never saved.
  async function flushPendingSave(): Promise<boolean> {
    if (saveTimerRef.current == null) return saveChainRef.current
    window.clearTimeout(saveTimerRef.current)
    saveTimerRef.current = null
    return persistNow()
  }

  async function handleDismiss() {
    if (await flushPendingSave()) onSaved()
  }

  async function handleRankWholeLibrary() {
    if ((await flushPendingSave()) && onRankWholeLibrary) onRankWholeLibrary()
  }

  const title = mode === 'top' ? 'Edit top anime order' : 'Rank my library'
  const visibleTiers = mode === 'top' ? tiers : tiers.filter((t) => t.score === selectedScore)
  const hint =
    `Editing order for: ${mediaTypeLabel}. ` +
    (mode === 'top'
      ? 'Drag a row, use the arrows, or promote a row into the top 10 to reorder within its score' +
        (tiers.some((t) => t.includedCount < t.members.length) ? '; the line marks the cutoff for the top list.' : '.')
      : 'Drag a row, or use the arrows to send it straight to the top or bottom of its score.')

  return (
    <Modal onClose={() => void handleDismiss()} labelledBy="anime-rank-title" className="modal--rank">
      <div className="anime-rank">
        {/* The header lives inside the same scrollable box as the list
            (rather than a fixed sibling above it) specifically so that
            "make room while dragging" is a real scroll, not a resize: a
            resize reflows the list underneath and makes the row being
            dragged visibly jump, where a scroll just moves everything
            together, exactly the way scrolling this box normally does. */}
        <div className="anime-rank__tiers" ref={tiersRef}>
          <div className="anime-rank__panel-header" ref={headerRef}>
            <div className="anime-rank__header">
              <div className="anime-rank__header-left">
                <h2 id="anime-rank-title" className="anime-rank__title">
                  {title}
                  <span className="anime-rank__help" tabIndex={0}>
                    <span aria-hidden="true">?</span>
                    <span className="anime-rank__help-tooltip" role="tooltip">
                      {hint}
                    </span>
                  </span>
                </h2>
              </div>
              <div className="anime-rank__header-actions">
                {/* Every reorder saves itself (moveMember -> scheduleSave) —
                    there's nothing left to confirm, so this just closes,
                    flushing first if an edit hasn't saved yet. */}
                <span className="anime-rank__save-status" aria-live="polite">
                  {saving ? 'Saving…' : ''}
                </span>
                <button type="button" onClick={() => void handleDismiss()}>
                  Close
                </button>
              </div>
            </div>

            {mode === 'top' && (
              <button type="button" className="anime-rank__whole-library" onClick={() => void handleRankWholeLibrary()}>
                Rank my whole library
              </button>
            )}

            {mode === 'all' && scores.length > 0 && (
              <div className="anime-rank__score-tabs" role="tablist" aria-label="Score">
                {scores.map((s) => (
                  <button
                    key={s.score}
                    type="button"
                    role="tab"
                    aria-selected={selectedScore === s.score}
                    className={
                      selectedScore === s.score
                        ? 'anime-rank__score-tab anime-rank__score-tab--active'
                        : 'anime-rank__score-tab'
                    }
                    onClick={() => selectScore(s.score)}
                  >
                    {s.score} <span className="anime-rank__score-tab-count">({s.count})</span>
                  </button>
                ))}
              </div>
            )}

            {error && (
              <p className="anime-rank__error">
                {error}{' '}
                <button type="button" className="anime-rank__retry" onClick={() => void persistNow()}>
                  Retry
                </button>
              </p>
            )}
          </div>

          {mode === 'all' && scores.length === 0 && !loadingTier ? (
            <p className="anime-rank__empty">Nothing hand-orderable yet for {mediaTypeLabel}.</p>
          ) : loadingTier && visibleTiers.length === 0 ? (
            <p className="anime-rank__loading">Loading…</p>
          ) : (
            visibleTiers.map((tier) => {
                const tierIndex = tiers.indexOf(tier)
                const boundary = promoteBoundary(tier)
                return (
                  <div key={tier.score} className="anime-rank__tier">
                    <h3 className="anime-rank__tier-title">Score {tier.score}</h3>
                    <ul className="anime-rank__list">
                      {tier.members.map((member, index) => {
                        const isSource = drag != null && drag.tierIndex === tierIndex && drag.fromIndex === index
                        const isTarget =
                          drag != null &&
                          drag.tierIndex === tierIndex &&
                          drag.targetIndex === index &&
                          drag.targetIndex !== drag.fromIndex
                        const insertAbove = isTarget && drag != null && drag.targetIndex < drag.fromIndex
                        const insertBelow = isTarget && drag != null && drag.targetIndex > drag.fromIndex
                        const isFocused = mode === 'all' && member.animeId === focusAnimeId
                        return (
                          <li key={member.animeId}>
                            {insertAbove && <div className="anime-rank__insertion-line" />}
                            <div
                              ref={isFocused ? focusedRowRef : undefined}
                              className={[
                                'anime-rank__row',
                                isSource ? 'anime-rank__row--dragging' : '',
                                isFocused ? 'anime-rank__row--focused' : '',
                              ]
                                .filter(Boolean)
                                .join(' ')}
                              data-tier-index={tierIndex}
                              data-row-index={index}
                              onPointerDown={(event) => handlePointerDown(event, tierIndex, index)}
                              onPointerMove={(event) => handlePointerMove(event, tierIndex, index)}
                              onPointerUp={handlePointerUp}
                              onPointerCancel={handlePointerCancel}
                            >
                              <MemberPicture member={member} />
                              <span className="anime-rank__row-rank">#{member.rank}</span>
                              <span
                                className="anime-rank__row-title"
                                title={pickDisplayTitle(member.title, member.englishTitle)}
                              >
                                {pickDisplayTitle(member.title, member.englishTitle)}
                              </span>
                              <span className="anime-rank__row-buttons" onPointerDown={(event) => event.stopPropagation()}>
                                {mode === 'top' ? (
                                  index < boundary ? (
                                    <>
                                      <button
                                        type="button"
                                        aria-label="Move up"
                                        disabled={index === 0}
                                        onClick={() => moveMember(tierIndex, index, index - 1)}
                                      >
                                        ↑
                                      </button>
                                      <button
                                        type="button"
                                        aria-label="Move down"
                                        disabled={index === tier.members.length - 1}
                                        onClick={() => moveMember(tierIndex, index, index + 1)}
                                      >
                                        ↓
                                      </button>
                                    </>
                                  ) : (
                                    <button
                                      type="button"
                                      className="anime-rank__promote"
                                      aria-label="Move into top 10"
                                      onClick={() => moveMember(tierIndex, index, boundary - 1)}
                                    >
                                      ⤒
                                    </button>
                                  )
                                ) : (
                                  // mode 'all': no top-10 concept over a whole score or the whole
                                  // library, so every row gets jump-to-top/jump-to-bottom controls
                                  // instead — a single step would take forever over a large tier.
                                  // The first row has nowhere to jump up to, the last nowhere to
                                  // jump down to, so each of those shows only the one that applies.
                                  <>
                                    {index > 0 && (
                                      <button
                                        type="button"
                                        className="anime-rank__promote"
                                        aria-label="Move to top"
                                        onClick={() => moveMember(tierIndex, index, 0)}
                                      >
                                        ⤒
                                      </button>
                                    )}
                                    {index < tier.members.length - 1 && (
                                      <button
                                        type="button"
                                        className="anime-rank__promote"
                                        aria-label="Move to bottom"
                                        onClick={() => moveMember(tierIndex, index, tier.members.length - 1)}
                                      >
                                        ⤓
                                      </button>
                                    )}
                                  </>
                                )}
                              </span>
                            </div>
                            {insertBelow && <div className="anime-rank__insertion-line" />}
                            {index === tier.includedCount - 1 && tier.includedCount < tier.members.length && (
                              <div className="anime-rank__cut-line" role="separator" aria-label="Top list cutoff" />
                            )}
                          </li>
                        )
                      })}
                    </ul>
                  </div>
                )
              })
            )}
        </div>

        {drag &&
          (() => {
            const dragMember = tiers[drag.tierIndex]?.members[drag.fromIndex]
            if (!dragMember) return null
            return (
              <div
                className="anime-rank__preview"
                style={{ left: drag.x - drag.offsetX, top: drag.y - drag.offsetY, width: drag.width }}
              >
                <MemberPicture member={dragMember} />
                <span className="anime-rank__row-title">{pickDisplayTitle(dragMember.title, dragMember.englishTitle)}</span>
              </div>
            )
          })()}
      </div>
    </Modal>
  )
}
