import './RevealControl.css'

type RevealControlProps = {
  onReveal: () => void
  label: string
}

export function RevealControl({ onReveal, label }: RevealControlProps) {
  return (
    <button
      type="button"
      className="reveal-control"
      onClick={(event) => {
        // RevealControl often sits inside a clickable AnimeCard link — stop
        // the click from bubbling into a navigation/card action.
        event.preventDefault()
        event.stopPropagation()
        onReveal()
      }}
      aria-label={label}
    >
      <EyeIcon />
    </button>
  )
}

function EyeIcon() {
  return (
    <svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path d="M1,12 C1,12 5,5 12,5 C19,5 23,12 23,12 C23,12 19,19 12,19 C5,19 1,12 1,12 Z" />
      <circle cx="12" cy="12" r="3" />
    </svg>
  )
}
