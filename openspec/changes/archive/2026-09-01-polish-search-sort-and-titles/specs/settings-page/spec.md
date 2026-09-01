## ADDED Requirements

### Requirement: The force-refresh picker names anime in English

The Settings page's force-refresh anime picker SHALL name every anime by the same rule the rest of the app uses: the English title where one is known, falling back to the stored MyAnimeList title where none is. No surface of this control SHALL show a romaji title while the same anime is named in English everywhere else.

This SHALL cover every place the control names an anime: each row of its search dropdown, the text it writes into the search field when a row is picked, and the messages it shows after a refresh succeeds or fails.

The control's behaviour SHALL be unchanged. It SHALL still search the same way, still offer only anime rows (never series), and still refresh the anime by its id — the title it displays is a label, never what identifies the anime being refreshed.

#### Scenario: A dropdown row is named in English
- **WHEN** I type into the force-refresh picker and a matching anime has a known English title
- **THEN** its row shows that English title rather than the romaji one

#### Scenario: Picking a row fills the field in English
- **WHEN** I pick that row
- **THEN** the search field shows the same English title the row showed

#### Scenario: The result message uses the same name
- **WHEN** the refresh completes, or fails
- **THEN** the message names the anime exactly as the row and the field did

#### Scenario: An anime with no English title
- **WHEN** a matching anime has no English title
- **THEN** it is shown under its stored MyAnimeList title, in the row, the field, and the message alike

#### Scenario: The refresh itself is unchanged
- **WHEN** I press Refresh after picking a row
- **THEN** the same anime is refreshed as before, identified by its id rather than by the displayed title
