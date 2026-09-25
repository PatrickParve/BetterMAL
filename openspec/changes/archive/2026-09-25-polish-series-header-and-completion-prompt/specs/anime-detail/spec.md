## MODIFIED Requirements

### Requirement: Portrait artwork is shown at its own proportions on the detail page
An anime whose picture is portrait — its intrinsic height greater than or equal to its intrinsic width — SHALL have that picture rendered at its own proportions on the detail page. The whole image SHALL be visible: no part of it SHALL be cut off to make it fill a box of a fixed height.

The picture SHALL keep the width the poster box already has and take whatever height its own proportions give it at that width. A picture whose proportions are taller than the poster box's SHALL therefore be rendered taller than the box rather than centre-cropped to it, and a picture whose proportions are shorter SHALL be rendered shorter rather than cropped to fill it. No maximum height SHALL be imposed: an unusually tall poster SHALL be shown whole.

This holds for every portrait picture, whichever source it comes from and however close it is to the poster box's proportions. A picture SHALL NOT be classified as "close enough" to the box and then cropped to it, as the smaller poster boxes elsewhere in the app do (`artwork-presentation`'s poster/upright split). On this page every portrait picture is drawn at its own proportions.

Everything beside the picture SHALL be unaffected: the page's two-column body, the score, info, and synopsis boxes to its right, and the width of the column the picture sits in SHALL all be exactly as they are today. Only the picture's own height, and the position of the progress controls stacked beneath it in the same column, SHALL follow from the artwork.

Because a picture's proportions are not known until the image itself has loaded, the page SHALL reserve the existing portrait poster box's height until then, so the column does not collapse and then expand as the artwork arrives. The page SHALL NOT request, store, or wait on any additional data to make this decision.

The placeholder shown when an anime has no picture at all SHALL keep the existing fixed poster box, since it has no artwork whose proportions could be adopted.

#### Scenario: A taller-than-usual poster is not cropped
- **WHEN** I open the detail page of an anime whose poster is taller in proportion than the page's poster box — for example a season whose key art is unusually tall
- **THEN** the whole poster is shown, as wide as the poster box has always been and taller than it, rather than a centre-cropped slice of it

#### Scenario: An ordinary poster is unchanged in width
- **WHEN** I open the detail page of an anime whose poster is close to the poster box's proportions
- **THEN** it is drawn at the same width it always has been, with its own height, and looks as it did before

#### Scenario: A 2:3 poster is shown whole
- **WHEN** I open the detail page of an anime whose displayed picture is a 2:3 poster, such as a TMDB poster
- **THEN** the whole poster is shown at 2:3, at the poster box's width and slightly taller than the box, with nothing cut off at its top or bottom

#### Scenario: A poster wider in proportion than the box is shown whole
- **WHEN** I open the detail page of an anime whose poster is slightly wider in proportion than the poster box, such as a 0.74 picture
- **THEN** the whole poster is shown at the box's width and its own, slightly shorter height, with nothing cut off at its sides

#### Scenario: A shorter poster is not stretched or cropped
- **WHEN** I open the detail page of an anime whose poster is shorter in proportion than the poster box
- **THEN** it is shown whole at its own height rather than being cropped to fill a taller box

#### Scenario: The page beside the picture does not move
- **WHEN** I open the detail page of an anime with an unusually tall poster
- **THEN** the score, info, and synopsis boxes beside it sit exactly where they do for any other anime, and the column holding the picture is the same width

#### Scenario: The column does not collapse while the image loads
- **WHEN** I open a detail page and the picture has not finished loading
- **THEN** the column reserves the poster box's usual height, and the content beneath the picture does not jump when the image arrives

#### Scenario: A missing picture keeps the portrait placeholder
- **WHEN** I open the detail page of an anime that has no picture
- **THEN** the placeholder occupies the fixed portrait poster box as it does today
