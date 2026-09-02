## ADDED Requirements

### Requirement: A card's hover treatment clears completely when the pointer leaves
The hover and keyboard-focus treatment an anime card wears — its tinted plate, its coloured border, and its elevation — SHALL be drawn while the card is hovered or focused and SHALL leave nothing behind once it is not. This SHALL hold for the whole treatment, including the part that extends beyond the card's own box, and in every browser the app is used in.

No further interaction SHALL be required to clear it: the treatment SHALL be gone as soon as the pointer has left, rather than persisting until the row is scrolled, another card is hovered, or the page is otherwise repainted. This SHALL hold for a card at the edge of a horizontally scrolling row as much as for one in the middle of it, and for the first card hovered in a session as much as for any later one.

#### Scenario: Leaving a card leaves no trace
- **WHEN** I hover a currently-watching card and then move the pointer away from it
- **THEN** the card and the space around it look exactly as they did before I hovered, with no line, tint, or edge left behind

#### Scenario: The first card hovered is no different
- **WHEN** the first card I hover after opening the home page is the row's first card, and I then move the pointer away
- **THEN** nothing is left under or around it

#### Scenario: Nothing else is needed to clear it
- **WHEN** I move the pointer off a card and neither scroll the row nor hover anything else
- **THEN** the treatment is already gone rather than clearing later
