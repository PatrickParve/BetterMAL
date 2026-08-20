# score-visibility Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
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
The system SHALL provide a setting on the Settings page, "Always show MAL scores for completed and dropped shows", that when enabled reveals the MAL score of any anime the user has marked **Completed** or **Dropped** in full — no placeholder and no per-score reveal control — even while the global hide-scores toggle is on. Completed and Dropped SHALL be treated identically by this setting: both are statuses in which the user has settled their relationship with the anime, so a community average can no longer bias or spoil a viewing that is still ahead of them.

The setting SHALL default to off, SHALL be independent of the global hide toggle (it never changes the global toggle's state), and SHALL apply only to scores belonging to completed or dropped entries; every other MAL score — Watching, On-hold, Plan to watch, and anime not in my list at all — continues to follow the global hide/unhide behavior. The setting SHALL apply to every MAL score the app renders, wherever it appears — including the profile page's "They liked it, I didn't" and "I liked it, they didn't" lists — so any view rendering a MAL score SHALL know whether that score's entry is completed or dropped. The setting's on/off state SHALL persist across page reloads and new tabs (client-side, e.g. `localStorage`), and an already-enabled setting SHALL stay enabled across this change rather than resetting to off.

#### Scenario: Completed scores shown while hiding is on
- **WHEN** the global hide toggle is on and I enable "Always show MAL scores for completed and dropped shows"
- **THEN** the MAL score of every anime I have completed is shown in full with no placeholder and no reveal control, while all scores belonging to neither a completed nor a dropped entry remain hidden

#### Scenario: Dropped scores shown while hiding is on
- **WHEN** the global hide toggle is on, the setting is enabled, and I look at an anime I have marked Dropped
- **THEN** its MAL score is shown in full with no placeholder and no reveal control, exactly as a completed anime's is

#### Scenario: Dropped scores shown everywhere a score is rendered
- **WHEN** the global hide toggle is on, the setting is enabled, and a dropped anime appears in my list, on its detail page, on the Top anime page, in a series' entry rows or extras tiles, on the series timeline, in a recap's top ten or hot takes, or in either profile divergence list
- **THEN** its MAL score is shown in full in every one of those places

#### Scenario: Statuses the setting does not cover
- **WHEN** the global hide toggle is on, the setting is enabled, and I look at an anime I am Watching, have On-hold, or Plan to watch
- **THEN** its MAL score still shows the reveal control, exactly like every other hidden score

#### Scenario: Setting off keeps completed and dropped scores hidden
- **WHEN** the global hide toggle is on and the setting is off
- **THEN** completed and dropped anime's MAL scores show their reveal control, exactly like every other score

#### Scenario: Setting has no effect while scores are globally visible
- **WHEN** the global hide toggle is off
- **THEN** all MAL scores are shown regardless of the setting

#### Scenario: Setting survives a reload
- **WHEN** I enable the setting and then reload the page or open the app in a new tab
- **THEN** the setting is still enabled without my having to toggle it again

#### Scenario: An enabled setting is not reset by the wider scope
- **WHEN** I had "Always show MAL scores for completed shows" enabled before the setting was widened to cover dropped shows
- **THEN** it is still enabled afterwards, now revealing dropped shows' scores as well

### Requirement: A hidden score occupies the same slot as a shown score
A hidden MAL score SHALL occupy the same horizontal space its value would occupy if shown, with its reveal control placed inside that space where the value itself would sit. Hiding or revealing a score SHALL NOT change the width of the score's slot, and SHALL therefore not reflow, shift, or re-align the text, controls, or tiles around it.

The reserved slot SHALL be sized to the score format the app renders (a two-decimal MAL average) rather than to each individual value, so that a column of hidden and shown scores stays aligned with itself.

Where the value is centred within its slot, the control SHALL be centred; where the value is aligned to the start of its slot — as in a labelled chip whose label and value both begin at the chip's leading edge — the control SHALL sit at the start; and where the value is aligned to the end of its slot — as in a right-aligned score column or a score pinned to the trailing side of a card — the control SHALL sit at the end. The control SHALL therefore begin, or end, where the number it replaces begins or ends, and SHALL line up with any unscored `—` placeholder shown for my score in the same layout.

This rule SHALL hold in both score densities — inside a score chip and as a bare coloured value in a dense row — and wherever a hidden score appears: My List rows, the Top anime page, the anime detail page, series-page entry rows, timeline and extra tiles, and the profile page's divergence lists and top-series chips.

#### Scenario: Revealing a score shifts nothing
- **WHEN** the hide toggle is on and I use one row's reveal control
- **THEN** the value appears in the space the control occupied, and no other text or control in that row or the rows around it moves

#### Scenario: A column of scores stays aligned
- **WHEN** the hide toggle is on, "Always show MAL scores for completed and dropped shows" is on, and a list contains completed and dropped rows showing a value alongside rows showing the reveal control
- **THEN** the score slots of all those rows line up in the same column with the same width

#### Scenario: Trailing text is not pushed out
- **WHEN** the hide toggle is on and I look at a profile divergence row, which reads `Me <my score> · MAL <MAL score>`
- **THEN** the row's trailing text ends at the same place it would if the MAL score were shown, rather than being pushed further by the hidden state

#### Scenario: The control is centred where the value is centred
- **WHEN** the hide toggle is on and I look at a compact MAL score chip, such as a profile top-series tile's chip pair, whose value is centred
- **THEN** the reveal control sits centred within the chip, in the position the value would have occupied

#### Scenario: The control starts where a labelled chip's value starts
- **WHEN** the hide toggle is on and I look at a Top anime showcase card's "MAL" chip, whose label and value both begin at the chip's leading edge
- **THEN** the reveal control sits at that leading edge, starting where the score would start and lining up with the `—` shown in the "My score" chip beside it when I have not scored that anime

#### Scenario: The control ends where a trailing value ends
- **WHEN** the hide toggle is on and I look at a Top anime ranks-4-to-10 card, whose my-score sits at the card's leading edge and whose MAL score sits at its trailing edge
- **THEN** the reveal control ends at that trailing edge, where the score would end, so it is inset from its edge by the same amount the my-score value is inset from the opposite edge

### Requirement: The global hide toggle is presented as a switch
The global hide/unhide control in the navigation bar SHALL be presented as a two-state switch rather than as a text button, so that it reads as a control the app is currently on one side of, and so which side that is can be told at a glance.

The switch SHALL consist of a track carrying a fixed label naming what it governs, and a knob that sits over that label and slides from one side of the track to the other as the state changes. The label SHALL NOT change with the state — the state is carried by the knob's position and its icon — so the control's width is identical in both states and toggling it SHALL NOT reflow the navigation bar around it.

The knob SHALL sit wholly within the track, separated from the track's border by a visible gap on every side, in both states and at every point of its travel. Neither the knob nor any shadow it casts SHALL overlap, obscure, or darken the track's border. The gap at the knob's resting end and at its travelled end SHALL be equal, so the switch is symmetric in both states.

The knob SHALL carry an eye icon: **open** while scores are shown, and **closed or struck through** while scores are hidden. The icon alone SHALL be sufficient to tell the current state, without reference to the knob's position or the track's colouring. The icon SHALL be centred within the knob, and the knob SHALL be centred on the track's vertical axis.

Moving between the two states SHALL be animated — the knob travelling across the track and the eye opening or closing — rather than snapping between two static images. Where the user has asked for reduced motion, the control SHALL change state instantly instead, remaining fully usable and still showing the correct icon.

The switch SHALL expose its two-state nature to assistive technology, reporting whether scores are currently shown, and SHALL carry an accessible name describing the action taking it will perform. It SHALL be operable by keyboard, SHALL show a visible focus indicator when focused by keyboard, and SHALL give the same hover feedback the app's other navigation controls give.

The switch SHALL be the same height as the navigation bar's other controls, so adjusting the knob within it SHALL NOT change the height of the navigation bar or the alignment of the controls beside it.

Only the control's presentation changes: it SHALL drive the same global hide state as before, with the same persistence, so a user who had scores hidden finds the switch already in the hidden position when the app next loads.

#### Scenario: Scores shown
- **WHEN** scores are not hidden
- **THEN** the switch's knob rests on one side of its track showing an open eye

#### Scenario: Scores hidden
- **WHEN** scores are hidden
- **THEN** the knob rests on the other side of the track showing a closed or struck-through eye

#### Scenario: The knob does not cover the track's border
- **WHEN** the switch is shown in either state
- **THEN** the track's border is visible unbroken all the way around, with a clear gap between it and the knob, and no shadow from the knob falling across it

#### Scenario: The knob is centred and symmetric
- **WHEN** I compare the switch in its two states
- **THEN** the knob is vertically centred on the track in both, and the gap between the knob and the nearer end of the track is the same in both

#### Scenario: Toggling animates
- **WHEN** I use the switch
- **THEN** the knob slides across the track and the eye opens or closes as it travels, rather than the control redrawing instantly

#### Scenario: The navbar does not reflow
- **WHEN** I toggle the switch
- **THEN** the control occupies exactly the same width as before and nothing beside it in the navigation bar moves

#### Scenario: The switch matches the navbar's control height
- **WHEN** the navigation bar is shown
- **THEN** the switch is the same height as the controls beside it and their centre lines agree

#### Scenario: Reduced motion
- **WHEN** I have asked my system for reduced motion and I use the switch
- **THEN** it changes state without the sliding or opening animation, and still shows the icon for the new state

#### Scenario: Announced as a switch
- **WHEN** I reach the control with a screen reader
- **THEN** it is announced as a two-state control reporting whether scores are currently shown, with a name describing what using it will do

#### Scenario: Keyboard operation
- **WHEN** I focus the control with the keyboard and activate it
- **THEN** scores are hidden or shown as they would be on a click, and the control shows a visible focus indicator while focused

#### Scenario: Hover feedback
- **WHEN** I move the pointer over the switch
- **THEN** it is clearly highlighted in the same style as the app's other navigation controls

#### Scenario: The hidden state still survives a reload
- **WHEN** I hide scores with the switch and then reload the page
- **THEN** scores are still hidden and the switch is already in its hidden position

