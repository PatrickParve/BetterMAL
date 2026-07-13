import './IncrementButton.css'

type IncrementButtonProps = {
  onIncrement: () => void
  disabled?: boolean
  label: string
}

// The one "+" episode-increment button used everywhere an episode count is
// shown (progress bar, carousel, detail page) — same size, color, and hover
// pop everywhere so it reads as a single consistent control. The "+" is
// drawn with plain CSS pseudo-elements (see IncrementButton.css); the whole
// button scales up as one unit on hover.
export function IncrementButton({ onIncrement, disabled, label }: IncrementButtonProps) {
  return (
    <button
      type="button"
      className="increment-button"
      disabled={disabled}
      onClick={(event) => {
        event.preventDefault()
        event.stopPropagation()
        onIncrement()
      }}
      aria-label={label}
    />
  )
}
