import { useRef, type ReactNode } from 'react'
import { useClickOutside } from '../hooks/useClickOutside.ts'
import './SearchBar.css'

type SearchFieldProps = {
  value: string
  onValueChange: (value: string) => void
  placeholder: string
  /** The input's name for a screen reader. */
  label: string
  /** Enter, or the magnifier, with text in the field. The dropdown is already dismissed and the field already unfocused. Gets the trimmed text. */
  onSubmit: (query: string) => void
  /** The field was typed in or focused: the only two things that open its dropdown. */
  onOpen: () => void
  /** Escape, a press outside the field, or a submit. */
  onDismiss: () => void
  /** Whether the dropdown shows: the field is open and has rows to show. The rows are the children. */
  dropdownOpen: boolean
  /** Added to the field's own class, e.g. `search-bar--page`. */
  className?: string
  children?: ReactNode
}

// The one search field shared by the navbar's SearchBar and the search page's own bar
// (improve-search-page design.md D3): a bordered input with the magnifier inside its
// right edge and a dropdown anchored beneath it. It owns what both fields do alike
// (navigation-and-search "The search field is one control with a self-contained focus
// ring" and "Search submission keeps the query text"): Enter or the magnifier submits
// the trimmed text, dismissing the dropdown and unfocusing the field first so the
// results page shows with nothing over it and no cursor left in the field; Escape and a
// press outside dismiss; typing or focusing opens. Empty text submits nothing. What the
// text is, which rows the dropdown holds, where a submit goes and what a pick does are
// the wrapper's: the field holds no search state.
export function SearchField({
  value,
  onValueChange,
  placeholder,
  label,
  onSubmit,
  onOpen,
  onDismiss,
  dropdownOpen,
  className,
  children,
}: SearchFieldProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  useClickOutside(containerRef, onDismiss)

  function submit() {
    const query = value.trim()
    if (query.length === 0) return
    onDismiss()
    inputRef.current?.blur()
    onSubmit(query)
  }

  return (
    <div className={className ? `search-bar ${className}` : 'search-bar'} ref={containerRef}>
      <input
        ref={inputRef}
        type="search"
        className="search-bar__input"
        placeholder={placeholder}
        value={value}
        onChange={(event) => {
          onValueChange(event.target.value)
          onOpen()
        }}
        onFocus={onOpen}
        onKeyDown={(event) => {
          if (event.key === 'Enter') submit()
          if (event.key === 'Escape') onDismiss()
        }}
        aria-label={label}
      />
      <button type="button" className="search-bar__submit" aria-label="Search" onClick={submit}>
        <SearchIcon />
      </button>
      {dropdownOpen && <ul className="search-bar__dropdown">{children}</ul>}
    </div>
  )
}

function SearchIcon() {
  return (
    <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <circle cx="10.5" cy="10.5" r="6.5" />
      <line x1="15.5" y1="15.5" x2="21" y2="21" />
    </svg>
  )
}
