## MODIFIED Requirements

### Requirement: Global MAL-score hide toggle
The system SHALL provide a global toggle that, when hidden, replaces every MAL score everywhere with a placeholder that does not leak the underlying value. The placeholder SHALL consist of the score's reveal control alone — no stand-in digits, dots, or other characters representing the value. The toggle's on/off state SHALL persist across page reloads and new tabs (client-side, e.g. `localStorage`), so a user who hides scores does not see them reappear on refresh.

The toggle SHALL default to **on** — scores hidden — whenever no choice has been stored, as on the first run of the app in a browser. Once a choice is stored, by toggling or otherwise, the stored choice SHALL govern from then on, whether it is on or off; the default SHALL NOT override it.

#### Scenario: Hiding all scores
- **WHEN** I turn the hide toggle on
- **THEN** every MAL score across the app is replaced by its reveal control alone and the actual value is not exposed in the rendered output

#### Scenario: No stand-in characters for the value
- **WHEN** the hide toggle is on and I look at any hidden MAL score
- **THEN** nothing stands in for the digits beside the reveal control — no dots, no masked characters, no placeholder text

#### Scenario: Hidden state survives a reload
- **WHEN** I turn the hide toggle on and then reload the page or open the app in a new tab
- **THEN** scores are still hidden without my having to toggle again

#### Scenario: Scores start hidden on a first run
- **WHEN** I open the app in a browser that has no stored hide-toggle choice
- **THEN** the navbar's score switch is in its hidden state and every MAL score is replaced by its reveal control

#### Scenario: Showing scores is remembered
- **WHEN** I turn the hide toggle off and then reload the page or open the app in a new tab
- **THEN** scores are still shown, rather than the first-run default hiding them again

#### Scenario: A stored choice is not overridden by the default
- **WHEN** a browser already has a stored choice of scores shown from before this default was introduced
- **THEN** scores are still shown there

### Requirement: Always-show-completed-scores setting
The system SHALL provide a setting on the Settings page, "Always show MAL scores for completed and dropped shows", that when enabled reveals the MAL score of any anime the user has marked **Completed** or **Dropped** in full — no placeholder and no per-score reveal control — even while the global hide-scores toggle is on. Completed and Dropped SHALL be treated identically by this setting: both are statuses in which the user has settled their relationship with the anime, so a community average can no longer bias or spoil a viewing that is still ahead of them.

The setting SHALL default to **on** whenever no choice has been stored, as on the first run of the app in a browser. It SHALL be independent of the global hide toggle (it never changes the global toggle's state), and SHALL apply only to scores belonging to completed or dropped entries; every other MAL score — Watching, On-hold, Plan to watch, and anime not in my list at all — continues to follow the global hide/unhide behavior. The setting SHALL apply to every MAL score the app renders in a view scoped to the user's own entries, wherever it appears — including the profile page's "They liked it, I didn't" and "I liked it, they didn't" lists — so any such view rendering a MAL score SHALL know whether that score's entry is completed or dropped. The setting's on/off state SHALL persist across page reloads and new tabs (client-side, e.g. `localStorage`). A stored choice — enabled or disabled — SHALL be kept as it is; the default applies only where nothing is stored, and an already-enabled setting SHALL stay enabled rather than resetting.

The setting SHALL NOT apply to the season and search browse cards, which are governed instead by their own rule that a hidden score is omitted from the card entirely (see "A hidden score occupies the same slot as a shown score"). Those two pages list arbitrary MAL anime rather than the user's own entries, and while hiding is on they show no score furniture at all — a rule the setting cannot partially reopen without putting a stray figure back on some cards in the grid.

#### Scenario: Completed scores shown while hiding is on
- **WHEN** the global hide toggle is on and I enable "Always show MAL scores for completed and dropped shows"
- **THEN** the MAL score of every anime I have completed is shown in full with no placeholder and no reveal control, while all scores belonging to neither a completed nor a dropped entry remain hidden

#### Scenario: Dropped scores shown while hiding is on
- **WHEN** the global hide toggle is on, the setting is enabled, and I look at an anime I have marked Dropped
- **THEN** its MAL score is shown in full with no placeholder and no reveal control, exactly as a completed anime's is

#### Scenario: Dropped scores shown everywhere a score is rendered
- **WHEN** the global hide toggle is on, the setting is enabled, and a dropped anime appears in my list, on its detail page, on the Top anime page, in a series' entry rows or extras tiles, on the series timeline, in a recap's top ten or hot takes, or in either profile divergence list
- **THEN** its MAL score is shown in full in every one of those places

#### Scenario: The setting does not reopen browse-card scores
- **WHEN** the global hide toggle is on, the setting is enabled, and an anime I have completed appears on the season page or the search results page
- **THEN** its card still shows no MAL score at all, like every other card in that grid

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

#### Scenario: Default is on for a first run
- **WHEN** I open the Settings page in a browser that has no stored choice for this setting
- **THEN** "Always show MAL scores for completed and dropped shows" is checked, and — with the hide toggle at its own first-run default of on — completed and dropped anime's MAL scores are shown in full while every other MAL score is hidden

#### Scenario: Disabling the setting is remembered
- **WHEN** I disable the setting and then reload the page or open the app in a new tab
- **THEN** the setting is still disabled, rather than the first-run default re-enabling it

#### Scenario: An enabled setting is not reset by the wider scope
- **WHEN** I had "Always show MAL scores for completed shows" enabled before the setting was widened to cover dropped shows
- **THEN** it is still enabled afterwards, now revealing dropped shows' scores as well
