## ADDED Requirements

### Requirement: The search field is one control with a self-contained focus ring
The system SHALL present the navbar search input and its magnifier button as a single control: one bordered field, with the magnifier sitting inside that field's right edge rather than as a separate box joined to it by a seam.

Focus SHALL be indicated on the field as a whole, and the indication SHALL stay within the field's own bounds — it SHALL NOT be drawn outside the input and SHALL NOT overlap, cross, or sit on top of the magnifier. This SHALL hold whichever part of the field has focus: typing in the input and tabbing to the magnifier SHALL each show focus without any ring landing on the other part.

The field SHALL keep its current behaviour and affordances: the placeholder, the type-ahead dropdown anchored beneath it, submission by Enter or by clicking the magnifier, and a visible hover state on the magnifier. At every font size the app renders (the root size is fluid), the ring SHALL remain inside the field.

#### Scenario: Focusing the input
- **WHEN** I click or tab into the search input
- **THEN** the whole field shows it has focus and no part of the focus indication touches or covers the magnifier

#### Scenario: Focusing the magnifier
- **WHEN** I tab from the input to the magnifier button
- **THEN** the magnifier shows it has focus without a ring spilling over the input beside it

#### Scenario: The field reads as one control
- **WHEN** the navbar renders
- **THEN** the input and the magnifier appear as a single bordered field rather than two boxes with a seam between them

#### Scenario: Submitting still works from the field
- **WHEN** I press Enter in the input, or click the magnifier
- **THEN** I am taken to the search results page for the current query, exactly as before

### Requirement: Pages do not scroll or drift horizontally
The system SHALL keep every page fixed horizontally. A sideways trackpad gesture, a horizontal wheel, or a swipe SHALL NOT slide the page, rubber-band it, or reveal anything beside it — including when the gesture starts inside a horizontally scrolling region (the currently-watching carousel, the series timeline, the profile page's poster strips) and that region is already at either end of its scroll.

Horizontal scrolling SHALL remain available inside those regions themselves: a sideways gesture over a strip SHALL still scroll the strip, and SHALL simply stop at the strip's ends rather than passing the remaining movement on to the page.

No page SHALL present a document-level horizontal scrollbar at any window width the app supports.

#### Scenario: Sideways gesture on an ordinary page
- **WHEN** I swipe left or right with two fingers on a page with no horizontal region under the pointer
- **THEN** the page does not move sideways at all

#### Scenario: Sideways gesture at the end of a strip
- **WHEN** I keep swiping sideways over a poster strip that has already reached its last tile
- **THEN** the strip stays at its end and the page behind it does not move

#### Scenario: Strips still scroll
- **WHEN** I swipe sideways over a poster strip, the carousel, or the series timeline that has more content
- **THEN** that region scrolls as it does today, including its drag-to-scroll behaviour

#### Scenario: No horizontal scrollbar
- **WHEN** any page is loaded at any supported window width
- **THEN** the document shows no horizontal scrollbar

## MODIFIED Requirements

### Requirement: A series result is marked as a series
The system SHALL mark every series result so it is never mistaken for a single anime. On the line BELOW the title, a series result SHALL show a "Series" badge together with the number of entries in the series, counting every member — main line and extras alike.

On the full results page this badge line SHALL occupy the same slot an anime card uses for its media-type and episode-count line, so series cards and anime cards keep identical geometry within the grid. The badge SHALL be scaled so that this line takes the same vertical space as an anime card's `TYPE · N ep` line: it SHALL NOT make a series card taller than the anime cards beside it, and SHALL NOT push the row of cards it sits in taller than a row of anime cards alone. The same scaled badge SHALL be used in the type-ahead dropdown row.

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
- **THEN** its card shows the "Series" badge and entry count where an anime card shows its type and episode count, and the card is the same size and shape as the anime cards around it

#### Scenario: A series card does not add height to its row
- **WHEN** the results grid renders a row containing both a series card and anime cards
- **THEN** the series card's badge line is the same height as the anime cards' meta line, and the row is no taller than a row of anime cards alone

#### Scenario: The badge is still a badge
- **WHEN** I look at a series result at its reduced size, in the dropdown or on the results page
- **THEN** the "Series" pill is still legible and still visually distinct from surrounding text

#### Scenario: Entry count covers extras too
- **WHEN** a series has four main-line entries and three extras
- **THEN** its badge line reports seven entries
