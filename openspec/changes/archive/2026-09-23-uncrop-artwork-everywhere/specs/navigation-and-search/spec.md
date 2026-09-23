## MODIFIED Requirements

### Requirement: A series result is marked as a series
The system SHALL mark every series result so it is never mistaken for a single anime. On the line BELOW the title, a series result SHALL show a "Series" badge together with the number of entries in the series, counting every member — main line and extras alike.

On the full results page this badge line SHALL occupy the same slot an anime card uses for its media-type and episode-count line, so a series card keeps exactly the geometry an anime card holding a picture of the same shape has within the grid. The badge SHALL be scaled so that this line takes the same vertical space as an anime card's `TYPE · N ep` line: it SHALL NOT make a series card taller than the anime cards beside it, and SHALL NOT push the row of cards it sits in taller than a row of anime cards alone. The same scaled badge SHALL be used in the type-ahead dropdown row.

In the type-ahead dropdown, every row SHALL be the same height whether it is a series row or an anime row. A series row's title and badge line together SHALL fit within that shared height rather than making the row taller, so the dropdown's overall height depends only on how many rows it holds — a dropdown of five rows SHALL be the same height whether none, some, or two of those rows are series, and rows SHALL NOT shift as the matches change while typing.

The badge SHALL stay legible and keep reading as a badge at that size — a tinted, bordered pill naming "Series", with the entry count beside it — rather than being reduced to plain text.

#### Scenario: Badge on a dropdown result
- **WHEN** a series appears in the type-ahead dropdown
- **THEN** its row shows the series title with a "Series" badge and the entry count on the line beneath it

#### Scenario: A series row does not make the dropdown taller
- **WHEN** the dropdown lists both series rows and anime rows
- **THEN** every row is the same height, and the dropdown is no taller than one holding the same number of anime rows alone

#### Scenario: The dropdown does not jump while typing
- **WHEN** typing another character changes which of the five dropdown rows are series
- **THEN** the dropdown keeps the same height and its rows stay where they are

#### Scenario: Badge on a results-page card
- **WHEN** a series appears on the full search results page
- **THEN** its card shows the "Series" badge and entry count where an anime card shows its type and episode count, and the card is the same size and shape as an anime card whose picture has the same shape

#### Scenario: A series card does not add height to its row
- **WHEN** the results grid renders a row containing both a series card and anime cards
- **THEN** the series card's badge line is the same height as the anime cards' meta line, and the row is no taller than a row of anime cards alone

#### Scenario: The badge is still a badge
- **WHEN** I look at a series result at its reduced size, in the dropdown or on the results page
- **THEN** the "Series" pill is still legible and still visually distinct from surrounding text

#### Scenario: Entry count covers extras too
- **WHEN** a series has four main-line entries and three extras
- **THEN** its badge line reports seven entries
