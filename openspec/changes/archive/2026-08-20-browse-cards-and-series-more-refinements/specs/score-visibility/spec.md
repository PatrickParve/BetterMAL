## MODIFIED Requirements

### Requirement: Always-show-completed-scores setting
The system SHALL provide a setting on the Settings page, "Always show MAL scores for completed and dropped shows", that when enabled reveals the MAL score of any anime the user has marked **Completed** or **Dropped** in full — no placeholder and no per-score reveal control — even while the global hide-scores toggle is on. Completed and Dropped SHALL be treated identically by this setting: both are statuses in which the user has settled their relationship with the anime, so a community average can no longer bias or spoil a viewing that is still ahead of them.

The setting SHALL default to off, SHALL be independent of the global hide toggle (it never changes the global toggle's state), and SHALL apply only to scores belonging to completed or dropped entries; every other MAL score — Watching, On-hold, Plan to watch, and anime not in my list at all — continues to follow the global hide/unhide behavior. The setting SHALL apply to every MAL score the app renders in a view scoped to the user's own entries, wherever it appears — including the profile page's "They liked it, I didn't" and "I liked it, they didn't" lists — so any such view rendering a MAL score SHALL know whether that score's entry is completed or dropped. The setting's on/off state SHALL persist across page reloads and new tabs (client-side, e.g. `localStorage`), and an already-enabled setting SHALL stay enabled across this change rather than resetting to off.

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

#### Scenario: An enabled setting is not reset by the wider scope
- **WHEN** I had "Always show MAL scores for completed shows" enabled before the setting was widened to cover dropped shows
- **THEN** it is still enabled afterwards, now revealing dropped shows' scores as well

### Requirement: A hidden score occupies the same slot as a shown score
A hidden MAL score SHALL occupy the same horizontal space its value would occupy if shown, with its reveal control placed inside that space where the value itself would sit. Hiding or revealing a score SHALL NOT change the width of the score's slot, and SHALL therefore not reflow, shift, or re-align the text, controls, or tiles around it.

The reserved slot SHALL be sized to the score format the app renders (a two-decimal MAL average) rather than to each individual value, so that a column of hidden and shown scores stays aligned with itself.

Where the value is centred within its slot, the control SHALL be centred; where the value is aligned to the start of its slot — as in a labelled chip whose label and value both begin at the chip's leading edge — the control SHALL sit at the start; and where the value is aligned to the end of its slot — as in a right-aligned score column or a score pinned to the trailing side of a card — the control SHALL sit at the end. The control SHALL therefore begin, or end, where the number it replaces begins or ends, and SHALL line up with any unscored `—` placeholder shown for my score in the same layout.

This rule SHALL hold in both score densities — inside a score chip and as a bare coloured value in a dense row — and wherever a hidden score appears: My List rows, the Top anime page, the anime detail page, series-page entry rows, timeline and extra tiles, and the profile page's divergence lists and top-series chips.

The season and search pages' browse cards are the one exception: there a hidden MAL score SHALL be omitted from the card entirely — no value, no placeholder, and no reveal control — rather than keeping a slot. Those grids show many cards at once, each carrying a single score at the end of its meta line with nothing after it, so an omitted score cannot pull any other text out of position, while a grid of reveal controls would read as a wall of eye icons on pages the user is browsing rather than reading their own scores from. The type and episode count at the start of that meta line SHALL stay where they are whether or not a score is drawn beside them.

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

#### Scenario: A browse card drops its score rather than reserving a slot
- **WHEN** the hide toggle is on and I look at the season page or the search results page
- **THEN** no card shows a score, a placeholder, or a reveal control, and each card's type and episode count sit exactly where they do when scores are shown
