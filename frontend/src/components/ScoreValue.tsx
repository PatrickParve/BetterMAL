import { useScoreVisibility } from '../context/ScoreVisibilityContext.tsx'
import { useScoreReveal } from '../hooks/useScoreReveal.ts'
import { RevealControl } from './RevealControl.tsx'
import './ScoreValue.css'

type ScoreValueProps = {
  value: number | null | undefined
  placeholder?: string
  completed?: boolean
}

// Renders a MAL score respecting the global hide toggle. While hidden and not
// individually revealed, the numeric value is never placed in the DOM at all —
// only the reveal control is rendered, alone — so it can't leak via
// devtools/text-selection. `completed` opts a score into the "always show for
// completed shows" setting: when that setting is on, a completed entry's
// score is shown in full with no reveal control, regardless of the global
// hide state.
export function ScoreValue({ value, placeholder = '—', completed = false }: ScoreValueProps) {
  const { hidden, alwaysShowCompletedScores } = useScoreVisibility()
  const [revealed, reveal] = useScoreReveal()

  if (value == null) return <span className="score-value">{placeholder}</span>

  if (!hidden || revealed || (completed && alwaysShowCompletedScores)) {
    return <span className="score-value">{value.toFixed(2)}</span>
  }

  return (
    <span className="score-value">
      <RevealControl onReveal={reveal} label="Reveal score" />
    </span>
  )
}
