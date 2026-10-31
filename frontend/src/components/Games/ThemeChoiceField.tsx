import { useId } from 'react'
import type { GameThemeChoice } from '../../api/types.ts'
import { THEME_CHOICES } from './themeGame.ts'

type ThemeChoiceFieldProps = {
  value: GameThemeChoice
  onChange: (choice: GameThemeChoice) => void
}

// Guess the Opening/Ending's own setting on the setup screen: which themes are
// played. Drawn as the Anime setting is, in the setup screen's own segments, so it
// reads as one of the same settings.
export function ThemeChoiceField({ value, onChange }: ThemeChoiceFieldProps) {
  const labelId = useId()
  return (
    <div className="game-lobby__field">
      <span className="game-lobby__label" id={labelId}>
        Theme
      </span>
      <div className="game-lobby__segments" role="group" aria-labelledby={labelId}>
        {THEME_CHOICES.map((choice) => (
          <button
            key={choice.value}
            type="button"
            className="game-lobby__segment"
            aria-pressed={value === choice.value}
            onClick={() => onChange(choice.value)}
          >
            {choice.label}
          </button>
        ))}
      </div>
    </div>
  )
}
