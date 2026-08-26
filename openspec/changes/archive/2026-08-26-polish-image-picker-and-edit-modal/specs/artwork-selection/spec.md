## MODIFIED Requirements

### Requirement: The picture picker
The system SHALL offer a picture picker as an overlay showing every option as an image, with the current selection marked. Clicking an option SHALL set it and close the overlay.

Every option SHALL be shown **whole, at its own proportions**. No part of any option SHALL be cropped away, and no option SHALL be letterboxed inside a box of a different shape: a portrait picture SHALL be drawn portrait, a landscape picture SHALL be drawn landscape and correspondingly wider than a portrait option beside it, and a square picture SHALL be drawn square. Two options that differ only near their edges SHALL therefore be distinguishable in the picker, which is the whole point of showing them side by side.

Options SHALL be laid out to a **common height**, each option's width following from its own proportions at that height, so that a set mixing orientations still forms tidy rows rather than a ragged field. That height SHALL be smaller than the picture the page behind the overlay displays for the same anime or series, so that a large option set shows more of itself at once and a picker of a franchise's whole pool is not mostly below the fold.

An option whose proportions are so wide that it cannot fit the width available to a row SHALL be reduced to fit whole within that width rather than being cropped or forced to overflow the overlay.

The whole of an option — its full drawn area, whatever its shape — SHALL be the control that chooses it, and the current selection's marking SHALL be legible on options of every shape.

The overlay SHALL behave as the app's other overlays do — dismissable with Escape and by clicking outside it, locking the page behind it from scrolling, and closing when the page it was opened over is navigated away from.

The control that opens the picker SHALL be rendered **only when there is more than one option**, so a picker never opens onto a single image and an anime with several pictures never lacks the control.

These presentation rules SHALL hold for the anime picker and the series picker alike.

#### Scenario: Opening and choosing
- **WHEN** I open the picker and click a picture
- **THEN** that picture becomes the anime's picture and the overlay closes

#### Scenario: The current selection is marked
- **WHEN** I open the picker for an anime that has a chosen picture
- **THEN** that picture is marked as the current selection

#### Scenario: One option, no control
- **WHEN** an anime's option set holds exactly one picture
- **THEN** no control to open the picker is rendered

#### Scenario: Dismissing without choosing
- **WHEN** I press Escape or click outside the overlay
- **THEN** it closes and the anime's picture is unchanged

#### Scenario: A landscape option is shown landscape
- **WHEN** I open the picker on an option set containing a picture wider than it is tall
- **THEN** that option is drawn as a wide, short image at its own proportions — wider than the portrait options beside it — rather than as a centre-cropped portrait slice of it

#### Scenario: Nothing is cut off
- **WHEN** I open the picker on an option set containing two pictures that differ only near their left and right edges
- **THEN** both are shown whole and the difference between them is visible in the picker

#### Scenario: Mixed orientations line up
- **WHEN** an option set holds both portrait and landscape pictures
- **THEN** every option is drawn to the same height, differing only in width, and the options sit in even rows

#### Scenario: Options are smaller than the picture on the page
- **WHEN** I open the picker from a page showing that anime's or series' picture
- **THEN** each option is drawn shorter than that picture, so more options fit in the overlay at once

#### Scenario: An extremely wide option still fits
- **WHEN** an option is so wide that at the common height it would be wider than the overlay's row
- **THEN** it is reduced to fit whole within that width, and the overlay does not scroll sideways

#### Scenario: The series picker looks the same
- **WHEN** I open the series picture picker on a pool drawn from several main-line members
- **THEN** its options follow the same rules — shown whole, at their own proportions, to a common height
