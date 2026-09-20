## ADDED Requirements

### Requirement: A Currently watching arrow is inert at its end of the row

Each of the Currently watching row's two arrows SHALL be disabled whenever the row
cannot move further in that arrow's direction: the left arrow while the row is
scrolled fully left, the right arrow while it is scrolled fully right. Both arrows
SHALL be disabled at once only when the row cannot scroll at all.

This SHALL govern only each arrow's own enabled state. Whether the arrows are shown
at all SHALL continue to follow the existing rule — both are shown exactly while the
row has more entries than fit, and neither is shown when every card already fits —
so an arrow reaching its end SHALL be disabled in place rather than removed, and the
row SHALL NOT shift horizontally as a result of an arrow changing state.

A disabled arrow SHALL take no hover treatment: moving the pointer over it SHALL
leave its colour, background and border exactly as they are at rest, so it does not
present itself as something that can be clicked. It SHALL additionally be visibly
distinguishable from an enabled arrow without hovering it.

The enabled state SHALL follow the row's actual scroll position however that position
was reached — by an arrow click, by a trackpad or touch scroll, or by the row being
resized — and SHALL settle along with the scroll rather than lagging a gesture
behind. A resting position that sits a fraction of a pixel short of either end SHALL
count as being at that end.

#### Scenario: The left arrow at the start of the row

- **WHEN** the Currently watching row holds more entries than fit and is scrolled fully left
- **THEN** the left arrow is disabled and the right arrow is enabled

#### Scenario: The right arrow at the end of the row

- **WHEN** the row holds more entries than fit and is scrolled fully right
- **THEN** the right arrow is disabled and the left arrow is enabled

#### Scenario: Both arrows live in the middle of the row

- **WHEN** the row holds more entries than fit and is scrolled to neither end
- **THEN** both arrows are enabled

#### Scenario: A disabled arrow does not light up

- **WHEN** I hover a disabled arrow
- **THEN** it keeps its resting colour, background and border, and shows no hover treatment

#### Scenario: An arrow click settles the other arrow

- **WHEN** the row is scrolled fully left and I click the right arrow until the row stops
- **THEN** the right arrow becomes disabled and the left arrow becomes enabled

#### Scenario: A trackpad scroll settles the arrows too

- **WHEN** I scroll the row to its right-hand end with a trackpad or touch gesture rather than the arrows
- **THEN** the right arrow is disabled just as it is after an arrow click

#### Scenario: The arrows are still hidden when everything fits

- **WHEN** there are 5 or fewer currently-watching cards and they all fit within the visible width
- **THEN** no arrows are shown at all, disabled or otherwise
