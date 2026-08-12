import { useRef, useState, type KeyboardEvent } from 'react'
import { useClickOutside } from '../hooks/useClickOutside.ts'
import './FilterMultiSelect.css'

export type FilterMultiSelectOption = { value: string; label: string }

type FilterMultiSelectProps = {
  label: string
  options: FilterMultiSelectOption[]
  selected: string[]
  onChange: (next: string[]) => void
}

// Checkbox popover for "any combination of a handful of values" filters (my
// list's type and airing-status filters). A native <select multiple> was
// rejected: it renders as a fixed-height scrolling box, has no room for a
// summary label, and behaves badly for multi-selection on macOS (D7).
export function FilterMultiSelect({ label, options, selected, onChange }: FilterMultiSelectProps) {
  const [open, setOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)

  useClickOutside(containerRef, () => setOpen(false))

  function toggle(value: string) {
    onChange(selected.includes(value) ? selected.filter((v) => v !== value) : [...selected, value])
  }

  function summary(): string {
    if (selected.length === 0) return `${label}: All`
    if (selected.length === 1) {
      const match = options.find((option) => option.value === selected[0])
      return `${label}: ${match ? match.label : selected[0]}`
    }
    return `${label}: ${selected.length} selected`
  }

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
        className="filter-multi-select__button"
        aria-haspopup="true"
        aria-expanded={open}
        onClick={() => setOpen((prev) => !prev)}
      >
        {summary()}
      </button>
      {open && (
        <div className="filter-multi-select__panel" role="group" aria-label={label}>
          <div className="filter-multi-select__shortcuts">
            <button
              type="button"
              className="filter-multi-select__shortcut"
              onClick={() => onChange(options.map((option) => option.value))}
            >
              All
            </button>
            <button type="button" className="filter-multi-select__shortcut" onClick={() => onChange([])}>
              None
            </button>
          </div>
          {options.map((option) => (
            <label key={option.value} className="filter-multi-select__option">
              <input type="checkbox" checked={selected.includes(option.value)} onChange={() => toggle(option.value)} />
              {option.label}
            </label>
          ))}
        </div>
      )}
    </div>
  )
}
