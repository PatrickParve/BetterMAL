import { useLayoutEffect, useRef, useState, type CSSProperties, type KeyboardEvent } from 'react'
import { useClickOutside } from '../hooks/useClickOutside.ts'
import { StableLabel } from './StableLabel.tsx'
import './FilterMultiSelect.css'

// `disabled` greys an option out in place (the my-list genre board's dead
// ends): it keeps its row, stays readable, and can't be ticked. Only an
// unticked option is ever drawn that way, so a restriction in force can always
// be lifted (design D3 of add-genre-filter-and-polish-lists).
export type FilterMultiSelectOption = { value: string; label: string; disabled?: boolean }

// `selected` is the ticked values, and `[]` is nothing ticked, which is no
// restriction: every value passes. There is no state in which nothing passes,
// so a filter can never by itself empty the view it narrows, and "no
// restriction" has this one representation wherever a page keeps it, in its
// URL or in restorable state (design D3).
type FilterMultiSelectProps = {
  label: string
  options: FilterMultiSelectOption[]
  // The full universe of values this control could ever offer, independent
  // of what's currently on offer — width reservation only (D7 of
  // fit-my-list-filters-to-the-list). It never appears in the panel and never
  // affects which values pass the filter. Defaults to `options`, so a caller
  // with no universe to give keeps today's behaviour of sizing to whatever
  // it currently offers.
  widthOptions?: FilterMultiSelectOption[]
  selected: string[]
  onChange: (next: string[]) => void
  // One option normally means every selection the control could express is
  // the view already on screen, so the control is unavailable. A filter whose
  // every option narrows the view when ticked on its own (the genre board
  // holds only genres some but not all of the listed entries carry) sets this
  // instead: one option is still a choice, and only none is unavailable.
  singleOptionIsChoice?: boolean
  // 'list' is one column beneath the trigger. 'board' lays many options out
  // in columns inside a panel as wide as the viewport allows, scrolling
  // inside itself when it is too tall (design D3).
  layout?: 'list' | 'board'
  // For a filter over names with no closed set (my list's studios): the longest
  // label the trigger draws in full. A longer one is cut with an ellipsis there,
  // and the whole name is the trigger's tooltip, so the trigger holds one width
  // however long a name is on offer. Unset, every label is drawn whole.
  maxLabelChars?: number
}

// The most characters a board's columns are sized for. A longer label is cut
// with an ellipsis inside its column, and shows whole as its tooltip, so one
// very long name cannot widen every column.
const BOARD_MAX_CHARS = 28

// A label cut to `max` characters, ending in an ellipsis; whole when it fits or
// when no cap is set.
function clipLabel(text: string, max: number | undefined): string {
  return max !== undefined && text.length > max ? `${text.slice(0, max - 1).trimEnd()}…` : text
}

// Where the open panel sits. All of it is measured from the page after the
// panel renders (the layout effect below), so none of it is known on a first
// render.
type Placement = {
  // Lined up with the trigger's trailing edge instead of its leading edge.
  alignEnd: boolean
  // Set only when neither edge alignment fits: the panel's offset from the
  // trigger's leading edge, in px, that keeps it inside the viewport.
  left: number | null
  // Board only: the viewport's width less its margins, so the panel is never
  // wider than what the viewport can show (excludes a scrollbar, which the
  // stylesheet's 100vw does not).
  maxWidth: number | null
  // Board only: open above the trigger instead of beneath it.
  above: boolean
  // Board only: the option area's height cap, null while it fits whole.
  scrollMaxHeight: number | null
}

const DEFAULT_PLACEMENT: Placement = { alignEnd: false, left: null, maxWidth: null, above: false, scrollMaxHeight: null }

function samePlacement(a: Placement, b: Placement): boolean {
  return (
    a.alignEnd === b.alignEnd &&
    a.left === b.left &&
    a.maxWidth === b.maxWidth &&
    a.above === b.above &&
    a.scrollMaxHeight === b.scrollMaxHeight
  )
}

// Kept clear of the viewport's edge on every side the panel can reach.
const VIEWPORT_MARGIN = 8
// Between the trigger and the panel — the 4px in the stylesheet's
// `top: calc(100% + 4px)` and its `--above` twin.
const TRIGGER_GAP = 4
// The least a board's option area keeps when the room is smaller still (a
// phone held sideways), so it stays usable; the page scrolls for the rest.
const MIN_BOARD_SCROLL_HEIGHT = 96

// Checkbox popover for "any combination of a handful of values" filters (my
// list's type, airing-status and genre filters, and the Type filter on the
// Search, Season and Year pages). A native <select multiple> was rejected: it
// renders as a fixed-height scrolling box, has no room for a summary label,
// and behaves badly for multi-selection on macOS (D7).
export function FilterMultiSelect({
  label,
  options,
  widthOptions = options,
  selected,
  onChange,
  singleOptionIsChoice = false,
  layout = 'list',
  maxLabelChars,
}: FilterMultiSelectProps) {
  const [open, setOpen] = useState(false)
  const [placement, setPlacement] = useState(DEFAULT_PLACEMENT)
  const containerRef = useRef<HTMLDivElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const panelRef = useRef<HTMLDivElement>(null)
  const scrollRef = useRef<HTMLDivElement>(null)

  useClickOutside(containerRef, () => setOpen(false))

  // A layout effect, not a plain effect: the first painted frame must
  // already be placed, or the panel would visibly jump once its rect is
  // measured. Everything it reads is independent of where the panel
  // currently sits (the trigger's rect, the panel's width, the option area's
  // full scroll height), so re-running it on resize needs no reset to a
  // baseline first. Resets on close too, so a stale placement is never what
  // the next open starts from.
  useLayoutEffect(() => {
    if (!open) {
      setPlacement(DEFAULT_PLACEMENT)
      return
    }
    function evaluate() {
      const container = containerRef.current
      const panel = panelRef.current
      if (!container || !panel) return

      // The container holds only the trigger (the panel is absolute), so its
      // rect is the trigger's.
      const trigger = container.getBoundingClientRect()
      const viewportWidth = document.documentElement.clientWidth
      const room = viewportWidth - 2 * VIEWPORT_MARGIN
      // A panel is never wider than the room (the board's `maxWidth` below;
      // a list panel is a few hundred px at most), and its width doesn't
      // depend on which edge it is lined up with.
      const width = Math.min(panel.getBoundingClientRect().width, room)

      // Leading edge if it fits, else trailing edge if that fits, else as
      // close to the leading edge as keeps the whole panel inside the
      // viewport: right edge pinned to the margin.
      let alignEnd = false
      let left: number | null = null
      if (trigger.left + width > viewportWidth - VIEWPORT_MARGIN) {
        if (trigger.right - width >= VIEWPORT_MARGIN) alignEnd = true
        else left = viewportWidth - VIEWPORT_MARGIN - width - trigger.left
      }

      // The board stays inside the viewport vertically too: its option area
      // takes whatever height the panel's other parts (the Clear row, padding,
      // border) leave, below the trigger, or above it when that is the
      // larger room and the board doesn't fit whole beneath.
      let above = false
      let scrollMaxHeight: number | null = null
      const scroller = scrollRef.current
      if (scroller) {
        const chrome = panel.offsetHeight - scroller.offsetHeight
        const natural = chrome + scroller.scrollHeight
        const below = window.innerHeight - trigger.bottom - TRIGGER_GAP - VIEWPORT_MARGIN
        const over = trigger.top - TRIGGER_GAP - VIEWPORT_MARGIN
        above = natural > below && over > below
        const space = above ? over : below
        if (natural > space) scrollMaxHeight = Math.max(MIN_BOARD_SCROLL_HEIGHT, space - chrome)
      }

      const next: Placement = { alignEnd, left, maxWidth: scroller ? room : null, above, scrollMaxHeight }
      setPlacement((prev) => (samePlacement(prev, next) ? prev : next))
    }
    evaluate()
    window.addEventListener('resize', evaluate)
    return () => window.removeEventListener('resize', evaluate)
  }, [open, layout, options.length])

  function toggle(value: string) {
    onChange(selected.includes(value) ? selected.filter((v) => v !== value) : [...selected, value])
  }

  // Focus goes back to the trigger rather than staying on a button that has
  // just become disabled, which would drop it to the page and take the
  // panel's Escape handling with it.
  function clear() {
    onChange([])
    triggerRef.current?.focus()
  }

  // The bare state name, with no `${label}: ` prefix — shared by the live
  // summary and (via summaryCandidates) every other state the button could
  // be showing, so both are built from the same three branches.
  function bareSummary(value: string[]): string {
    if (value.length === 0) return 'All'
    if (value.length === 1) {
      const match = options.find((option) => option.value === value[0])
      return clipLabel(match ? match.label : value[0], maxLabelChars)
    }
    return `${value.length} selected`
  }

  // One option means every selection this control could express is the view
  // already on screen (see `unavailable` below), so its label states that
  // one value rather than the ordinary All/label/count states; zero options
  // falls through to bareSummary([]), which reads All since `unavailable`
  // only holds with nothing ticked (D5).
  function summary(): string {
    if (unavailable && options.length === 1) return `${label}: ${clipLabel(options[0].label, maxLabelChars)}`
    return `${label}: ${bareSummary(selected)}`
  }

  // Every bare state the control could ever show, for either the options it
  // currently offers or the wider universe `widthOptions` reserves against —
  // unioned rather than built from the universe alone, because
  // `mediaTypeLabel` prettifies any raw value MAL adds that the app hasn't
  // mapped yet, so an offered label can sit outside the universe and would
  // otherwise be clipped (design D7). Ticking every option leaves them all
  // ticked, so the count runs up to N, not N - 1. The label in force is added
  // too: a tick that is on neither list (an address naming a type the page has
  // none of) would otherwise match no candidate and draw a blank trigger. It
  // costs a few pixels of width only in that state. Deduped since the lists
  // usually overlap heavily.
  function summaryCandidates(): string[] {
    const n = Math.max(widthOptions.length, options.length)
    const counts: string[] = []
    for (let i = 2; i <= n; i++) counts.push(`${i} selected`)
    const candidates = [
      'All',
      ...widthOptions.map((option) => clipLabel(option.label, maxLabelChars)),
      ...options.map((option) => clipLabel(option.label, maxLabelChars)),
      ...counts,
      bareSummary(selected),
    ]
    return [...new Set(candidates)]
  }

  // One option left means every selection this control could express is the
  // view already on screen (unless the filter says a single option is still a
  // choice); zero options means there's nothing to express either way. Gated
  // on nothing being ticked so a control that is itself narrowing the view —
  // a stale selection over a since-narrowed base — stays available and its
  // restriction can still be lifted (D5).
  const unavailable = selected.length === 0 && options.length <= (singleOptionIsChoice ? 0 : 1)

  // Handled at the container so it fires regardless of which element inside
  // the panel has focus (button, a checkbox, a shortcut) — stopPropagation
  // keeps this Escape from also reaching e.g. a Modal's own document-level
  // Escape listener when the bar happens to sit under one.
  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Escape' && open) {
      event.stopPropagation()
      setOpen(false)
    }
  }

  function renderOption(option: FilterMultiSelectOption) {
    const checked = selected.includes(option.value)
    // Never disabled while ticked, whatever the caller says: a restriction in
    // force can always be unticked. A disabled input takes no click and no
    // focus, so the keyboard passes it by as well.
    const disabled = option.disabled === true && !checked
    return (
      <label
        key={option.value}
        className={`filter-multi-select__option${disabled ? ' filter-multi-select__option--disabled' : ''}`}
        title={layout === 'board' && option.label.length > BOARD_MAX_CHARS ? option.label : undefined}
      >
        <input type="checkbox" checked={checked} disabled={disabled} onChange={() => toggle(option.value)} />
        <span className="filter-multi-select__option-text">{option.label}</span>
      </label>
    )
  }

  // The whole name behind a trigger that had to cut it short.
  const singleLabel =
    selected.length === 1 ? (options.find((option) => option.value === selected[0])?.label ?? selected[0]) : null
  const fullSingleLabel =
    singleLabel !== null && clipLabel(singleLabel, maxLabelChars) !== singleLabel ? `${label}: ${singleLabel}` : undefined

  const panelClass = [
    'filter-multi-select__panel',
    layout === 'board' ? 'filter-multi-select__panel--board' : '',
    placement.alignEnd ? 'filter-multi-select__panel--end' : '',
    placement.above ? 'filter-multi-select__panel--above' : '',
  ]
    .filter(Boolean)
    .join(' ')
  const panelStyle: CSSProperties = {}
  if (placement.left !== null) panelStyle.left = placement.left
  if (placement.maxWidth !== null) panelStyle.maxWidth = placement.maxWidth

  // A board's columns are as wide as its longest label needs; the stylesheet
  // turns the character count into a width (it can't measure the labels
  // itself). Taken over the universe as well as the offered options, so the
  // columns are the same width whichever genres are on the board right now.
  let longestLabel = 0
  if (layout === 'board') {
    for (const option of widthOptions) longestLabel = Math.max(longestLabel, Math.min(option.label.length, BOARD_MAX_CHARS))
    for (const option of options) longestLabel = Math.max(longestLabel, Math.min(option.label.length, BOARD_MAX_CHARS))
  }

  return (
    <div className="filter-multi-select" ref={containerRef} onKeyDown={handleKeyDown}>
      <button
        ref={triggerRef}
        type="button"
        // No `&& !unavailable` guard needed here: `unavailable` only ever
        // holds with nothing ticked, which is already the branch that leaves
        // the accent off.
        className={`filter-multi-select__button${selected.length > 0 ? ' filter-multi-select--active' : ''}`}
        title={fullSingleLabel}
        aria-haspopup="true"
        aria-expanded={open}
        disabled={unavailable}
        onClick={() => setOpen((prev) => !prev)}
      >
        <StableLabel current={summary()} candidates={summaryCandidates().map((candidate) => `${label}: ${candidate}`)} />
      </button>
      {open && (
        <div ref={panelRef} className={panelClass} style={panelStyle} role="group" aria-label={label}>
          {/* One shortcut, always drawn: ticking the first option or clearing
              the last must not move the options beneath it. It stays outside
              the board's scroll area so it never scrolls out of reach. */}
          <div className="filter-multi-select__shortcuts">
            <button type="button" className="filter-multi-select__shortcut" disabled={selected.length === 0} onClick={clear}>
              Clear
            </button>
          </div>
          {layout === 'board' ? (
            // The scroll area wraps the columns rather than being them: a
            // height-limited multi-column box spills sideways into extra
            // columns instead of scrolling.
            <div
              ref={scrollRef}
              className="filter-multi-select__scroll"
              style={{ maxHeight: placement.scrollMaxHeight ?? undefined }}
            >
              <div className="filter-multi-select__columns" style={{ '--filter-board-chars': longestLabel } as CSSProperties}>
                {options.map(renderOption)}
              </div>
            </div>
          ) : (
            options.map(renderOption)
          )}
        </div>
      )}
    </div>
  )
}
