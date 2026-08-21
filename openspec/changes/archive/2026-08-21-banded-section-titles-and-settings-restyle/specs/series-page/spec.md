## ADDED Requirements

### Requirement: A timeline card's footer always shows its rewatch indicator
A main-line timeline card whose entry has been rewatched SHALL show its rewatch indicator in full, whatever the length of the rest of its footer. The card's fixed width SHALL be wide enough that a card carrying a list status, a watched-episode figure, and a rewatch indicator shows all three at once for an entry with an ordinary episode count, rather than sizing to the shortest case and cutting the indicator off in the long one.

Where the footer's content still cannot fit — an entry with a four-digit episode count on both sides of its watched figure, for instance — the **status text SHALL be what shortens**, by truncation, and the rewatch indicator SHALL remain whole and visible. The indicator SHALL NOT be shrunk, wrapped, faded, or pushed outside the card's visible area by a longer status line.

Widening SHALL apply equally to every portrait main-line card, so cards remain the same size as one another and the timeline's per-duration-neutrality holds: a wider card SHALL still not be readable as a longer or more important entry. A landscape card SHALL remain the one alternative width and SHALL stay wider than a portrait card by the same relationship it has today. A card's picture SHALL keep its existing proportions at the new width, and the picture, title, chips, and footer of every card in a row SHALL still line up with their neighbours'.

#### Scenario: A four-digit episode count keeps the rewatch indicator
- **WHEN** a main-line card shows an entry I have rewatched whose watched figure is `1141/1141`
- **THEN** the card still shows its rewatch indicator in full, with the status text truncated ahead of it if room runs out

#### Scenario: An ordinary entry fits without truncation
- **WHEN** a main-line card shows a rewatched entry with a two-digit episode count
- **THEN** its status, watched figure, and rewatch indicator all render in full with nothing truncated

#### Scenario: Every portrait card is still the same size
- **WHEN** a series' main line holds entries of very different lengths
- **THEN** every portrait card renders at the same widened width as every other, and the spacing between cards is unchanged

#### Scenario: Landscape cards stay the wider exception
- **WHEN** a main line holds both portrait and landscape artwork
- **THEN** the landscape cards are still wider than the portrait ones, all landscape cards match one another, and every card's picture, title, chips, and footer still line up across the row

#### Scenario: The row still scrolls rather than the page
- **WHEN** the widened cards no longer fit the container
- **THEN** the timeline row scrolls within its own container without a visible scrollbar, exactly as it does today
