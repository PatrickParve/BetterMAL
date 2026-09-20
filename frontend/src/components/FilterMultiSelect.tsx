import { useLayoutEffect, useRef, useState, type KeyboardEvent } from 'react'
import { useClickOutside } from '../hooks/useClickOutside.ts'
import { StableLabel } from './StableLabel.tsx'
import './FilterMultiSelect.css'

export type FilterMultiSelectOption = { value: string; label: string }

// `selected`/`onChange`'s value is `string[] | null`: `null` is All (no
// restriction, every option passes), an array is exactly those values, and
// `[]` is None (nothing passes). The empty array can't keep doubling as
// "unfiltered" — All and None have to render as, and be, two different
// states, and an empty selection is the only shape left for the control to
// say "the user chose to select nothing" (design D1).
type FilterMultiSelectProps = {
  label: string
  options: FilterMultiSelectOption[]
  // The full universe of values this control could ever offer, independent
  // of what's currently on offer — width reservation only (D7 of
  // fit-my-list-filters-to-the-list). It never appears in the panel, never
  // affects emit()'s All-normalisation, and never affects which values pass
  // the filter. Defaults to `options`, so a caller with no universe to give
  // keeps today's behaviour of sizing to whatever it currently offers.
  widthOptions?: FilterMultiSelectOption[]
  selected: string[] | null
  onChange: (next: string[] | null) => void
}

// Checkbox popover for "any combination of a handful of values" filters (my
// list's type and airing-status filters). A native <select multiple> was
// rejected: it renders as a fixed-height scrolling box, has no room for a
// summary label, and behaves badly for multi-selection on macOS (D7).
export function FilterMultiSelect({ label, options, widthOptions = options, selected, onChange }: FilterMultiSelectProps) {
  const [open, setOpen] = useState(false)
  const [alignEnd, setAlignEnd] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)
  const panelRef = useRef<HTMLDivElement>(null)

  useClickOutside(containerRef, () => setOpen(false))

  // A layout effect, not a plain effect: the first painted frame must
  // already be aligned, or the panel would visibly jump once its rect is
  // measured. Resets alignEnd on close too, so the next open always starts
  // from the left-aligned baseline it measures against, rather than
  // inheriting whichever edge the previous open settled on.
  useLayoutEffect(() => {
    if (!open) {
      setAlignEnd(false)
      return
    }
    function evaluate() {
      const panel = panelRef.current
      if (!panel) return
      setAlignEnd(panel.getBoundingClientRect().right > window.innerWidth - 8)
    }
    evaluate()
    window.addEventListener('resize', evaluate)
    return () => window.removeEventListener('resize', evaluate)
  }, [open])

  // Every emission goes through here so All has exactly one representation
  // however it's reached — the shortcut, a fresh visit, or ticking the last
  // unticked box by hand — rather than a `next.length === options.length`
  // array that renders the same as `null` but isn't it (design D2).
  function emit(next: string[]) {
    onChange(options.every((option) => next.includes(option.value)) ? null : next)
  }

  function toggle(value: string) {
    if (selected === null) {
      emit(options.map((option) => option.value).filter((v) => v !== value))
      return
    }
    emit(selected.includes(value) ? selected.filter((v) => v !== value) : [...selected, value])
  }

  // The bare state name, with no `${label}: ` prefix — shared by the live
  // summary and (via summaryCandidates) every other state the button could
  // be showing, so both are built from the same four branches.
  function bareSummary(value: string[] | null): string {
    if (value === null) return 'All'
    if (value.length === 0) return 'None'
    if (value.length === 1) {
      const match = options.find((option) => option.value === value[0])
      return match ? match.label : value[0]
    }
    return `${value.length} selected`
  }

  // One option means every selection this control could express is the view
  // already on screen (see `unavailable` below), so its label states that
  // one value rather than the ordinary All/None/label/count states; zero
  // options falls through to bareSummary(null), which reads All since
  // `unavailable` only holds with `selected === null` (D5).
  function summary(): string {
    if (unavailable && options.length === 1) return `${label}: ${options[0].label}`
    return `${label}: ${bareSummary(selected)}`
  }

  // Every bare state the control could ever show, for either the options it
  // currently offers or the wider universe `widthOptions` reserves against —
  // unioned rather than built from the universe alone, because
  // `mediaTypeLabel` prettifies any raw value MAL adds that the app hasn't
  // mapped yet, so an offered label can sit outside the universe and would
  // otherwise be clipped (design D7). Deduped since the two lists usually
  // overlap heavily.
  function summaryCandidates(): string[] {
    const n = Math.max(widthOptions.length, options.length)
    const counts: string[] = []
    for (let i = 2; i <= n - 1; i++) counts.push(`${i} selected`)
    const candidates = [
      'All',
      'None',
      ...widthOptions.map((option) => option.label),
      ...options.map((option) => option.label),
      ...counts,
    ]
    return [...new Set(candidates)]
  }

  // One option left means every selection this control could express is the
  // view already on screen; zero options means there's nothing to express
  // either way. Gated on `selected === null` so a control that is itself
  // narrowing the view — a stale selection over a since-narrowed base — stays
  // available and its restriction can still be lifted (D5).
  const unavailable = selected === null && options.length <= 1

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

  return (
    <div className="filter-multi-select" ref={containerRef} onKeyDown={handleKeyDown}>
      <button
        type="button"
        // No `&& !unavailable` guard needed here: `unavailable` only ever
        // holds when `selected === null`, which is already the branch that
        // leaves the accent off.
        className={`filter-multi-select__button${selected !== null ? ' filter-multi-select--active' : ''}`}
        aria-haspopup="true"
        aria-expanded={open}
        disabled={unavailable}
        onClick={() => setOpen((prev) => !prev)}
      >
        <StableLabel current={summary()} candidates={summaryCandidates().map((candidate) => `${label}: ${candidate}`)} />
      </button>
      {open && (
        <div
          ref={panelRef}
          className={`filter-multi-select__panel${alignEnd ? ' filter-multi-select__panel--end' : ''}`}
          role="group"
          aria-label={label}
        >
          <div className="filter-multi-select__shortcuts">
            {/* All deliberately does not write out every value: two states
                that render identically (every box ticked vs. no restriction
                at all) must not be separately expressible in a URL or a
                page snapshot. */}
            <button type="button" className="filter-multi-select__shortcut" onClick={() => onChange(null)}>
              All
            </button>
            <button type="button" className="filter-multi-select__shortcut" onClick={() => onChange([])}>
              None
            </button>
          </div>
          {options.map((option) => (
            <label key={option.value} className="filter-multi-select__option">
              <input
                type="checkbox"
                checked={selected === null || selected.includes(option.value)}
                onChange={() => toggle(option.value)}
              />
              {option.label}
            </label>
          ))}
        </div>
      )}
    </div>
  )
}
