## MODIFIED Requirements

### Requirement: Global MAL-score hide toggle
The system SHALL provide a global toggle that, when hidden, replaces every MAL score everywhere with a placeholder that does not leak the underlying value. The placeholder SHALL consist of the score's reveal control alone — no stand-in digits, dots, or other characters representing the value. The toggle's on/off state SHALL persist across page reloads and new tabs (client-side, e.g. `localStorage`), so a user who hides scores does not see them reappear on refresh.

#### Scenario: Hiding all scores
- **WHEN** I turn the hide toggle on
- **THEN** every MAL score across the app is replaced by its reveal control alone and the actual value is not exposed in the rendered output

#### Scenario: No stand-in characters for the value
- **WHEN** the hide toggle is on and I look at any hidden MAL score
- **THEN** nothing stands in for the digits beside the reveal control — no dots, no masked characters, no placeholder text

#### Scenario: Hidden state survives a reload
- **WHEN** I turn the hide toggle on and then reload the page or open the app in a new tab
- **THEN** scores are still hidden without my having to toggle again

### Requirement: Per-score in-place reveal
The system SHALL give each hidden score its own small unhide control that reveals just that one score in place. While the score is hidden, that control is the entirety of what the score renders.

#### Scenario: Revealing a single score
- **WHEN** I use a hidden score's unhide control
- **THEN** only that one score is revealed in place while the others remain hidden

### Requirement: Reveal is not persisted
The system SHALL re-hide any individually revealed score when navigating away, keeping the unhidden state non-persistent.

#### Scenario: Navigating away re-hides
- **WHEN** I reveal a score and then navigate away and back
- **THEN** that score is hidden again

### Requirement: Always-show-completed-scores setting
The system SHALL provide a setting on the Settings page, "Always show MAL scores for completed shows", that when enabled reveals the MAL score of any anime the user has marked **Completed** in full — no placeholder and no per-score reveal control — even while the global hide-scores toggle is on. The setting SHALL default to off, SHALL be independent of the global hide toggle (it never changes the global toggle's state), and SHALL apply only to scores belonging to completed entries; every other MAL score continues to follow the global hide/unhide behavior. The setting SHALL apply to every MAL score the app renders, wherever it appears — including the profile page's "They liked it, I didn't" and "I liked it, they didn't" lists — so any view rendering a MAL score SHALL know whether that score's entry is completed. The setting's on/off state SHALL persist across page reloads and new tabs (client-side, e.g. `localStorage`).

#### Scenario: Completed scores shown while hiding is on
- **WHEN** the global hide toggle is on and I enable "Always show MAL scores for completed shows"
- **THEN** the MAL score of every anime I have completed is shown in full with no placeholder and no reveal control, while all non-completed scores remain hidden

#### Scenario: Completed scores shown in the opinion-divergence lists
- **WHEN** the global hide toggle is on, "Always show MAL scores for completed shows" is enabled, and a completed anime appears in "They liked it, I didn't" or "I liked it, they didn't"
- **THEN** that row's MAL score is shown in full with no placeholder and no reveal control, while rows for entries I have not completed stay hidden

#### Scenario: Setting off keeps completed scores hidden
- **WHEN** the global hide toggle is on and "Always show MAL scores for completed shows" is off
- **THEN** completed anime's MAL scores show their reveal control, exactly like every other score

#### Scenario: Setting has no effect while scores are globally visible
- **WHEN** the global hide toggle is off
- **THEN** all MAL scores are shown regardless of the "Always show MAL scores for completed shows" setting

#### Scenario: Setting survives a reload
- **WHEN** I enable "Always show MAL scores for completed shows" and then reload the page or open the app in a new tab
- **THEN** the setting is still enabled without my having to toggle it again

## ADDED Requirements

### Requirement: A hidden score occupies the same slot as a shown score
A hidden MAL score SHALL occupy the same horizontal space its value would occupy if shown, with its reveal control centred in that space. Hiding or revealing a score SHALL NOT change the width of the score's slot, and SHALL therefore not reflow, shift, or re-align the text, controls, or tiles around it.

The reserved slot SHALL be sized to the score format the app renders (a two-decimal MAL average) rather than to each individual value, so that a column of hidden and shown scores stays aligned with itself.

This rule SHALL hold in both score densities — inside a score chip and as a bare coloured value in a dense row — and wherever a hidden score appears: My List rows, the Top anime page, the anime detail page, series-page entry rows, timeline and extra tiles, and the profile page's divergence lists and top-series chips.

#### Scenario: Revealing a score shifts nothing
- **WHEN** the hide toggle is on and I use one row's reveal control
- **THEN** the value appears in the space the control occupied, and no other text or control in that row or the rows around it moves

#### Scenario: A column of scores stays aligned
- **WHEN** the hide toggle is on, "Always show MAL scores for completed shows" is on, and a list contains both completed rows showing a value and non-completed rows showing the reveal control
- **THEN** the score slots of all those rows line up in the same column with the same width

#### Scenario: Trailing text is not pushed out
- **WHEN** the hide toggle is on and I look at a profile divergence row, which reads `Me <my score> · MAL <MAL score>`
- **THEN** the row's trailing text ends at the same place it would if the MAL score were shown, rather than being pushed further by the hidden state

#### Scenario: The control is centred in a chip
- **WHEN** the hide toggle is on and I look at a compact MAL score chip, such as a profile top-series tile's chip pair
- **THEN** the reveal control sits centred within the chip, in the position the value would have occupied
