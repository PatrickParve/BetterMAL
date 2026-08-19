## MODIFIED Requirements

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
A hidden MAL score SHALL occupy the same horizontal space its value would occupy if shown, with its reveal control centred in that space. Hiding or revealing a score SHALL NOT change the width of the score's slot, and SHALL therefore not reflow, shift, or re-align the text, controls, or tiles around it.

The reserved slot SHALL be sized to the score format the app renders (a two-decimal MAL average) rather than to each individual value, so that a column of hidden and shown scores stays aligned with itself.

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

#### Scenario: The control is centred in a chip
- **WHEN** the hide toggle is on and I look at a compact MAL score chip, such as a profile top-series tile's chip pair
- **THEN** the reveal control sits centred within the chip, in the position the value would have occupied
