## ADDED Requirements

### Requirement: Landscape artwork is shown whole on the detail page
An anime whose picture is landscape — its intrinsic width greater than its intrinsic height — SHALL have that picture rendered at its own proportions on the detail page rather than cropped to the page's portrait poster box. The whole image SHALL be visible: no part of it SHALL be cut off to make it fill a taller box.

The picture SHALL keep the width the poster box already has, taking whatever height its own proportions give it at that width, so the page's two-column body, the controls beneath the picture, and every box beside it keep their existing positions and widths. Only the picture's own height SHALL differ from the portrait case.

This treatment SHALL apply only to landscape artwork. A portrait picture, a square picture, and the placeholder shown when an anime has no picture at all SHALL keep the existing poster box unchanged, since that box already matches portrait artwork's proportions.

Because a picture's proportions are not known until the image itself has loaded, the page SHALL render the existing portrait box until then and adopt the landscape treatment once the artwork is known to be landscape. The page SHALL NOT request, store, or wait on any additional data to make this decision.

#### Scenario: A landscape picture is not cropped
- **WHEN** I open the detail page of an anime whose picture is wider than it is tall
- **THEN** the whole picture is shown at its own proportions — a wide, shorter image — rather than a centre-cropped portrait slice of it

#### Scenario: The page around a landscape picture is unchanged
- **WHEN** I open that same detail page
- **THEN** the picture is as wide as the poster box has always been, and the progress controls beneath it and the score, info, and synopsis boxes beside it sit exactly where they do for any other anime

#### Scenario: Portrait artwork is untouched
- **WHEN** I open the detail page of an anime whose picture is taller than it is wide
- **THEN** its picture fills the same poster box it does today, unchanged

#### Scenario: A missing picture keeps the portrait placeholder
- **WHEN** I open the detail page of an anime that has no picture
- **THEN** the placeholder occupies the portrait poster box as it does today
