## MODIFIED Requirements

### Requirement: List-row posters fill the row
The system SHALL render the poster in a my-list row and a top-anime row flush with the row's top and bottom edges, filling the row's full height with no padding above or below it, so the poster reads as part of the card rather than an image floating inside it. On a top-anime row, or a my-list row preceded by a rank column, the poster SHALL also sit flush with its leading neighbour (the rank column, or the row's leading edge when there is no rank), still filling the row's height. On a my-list row with no rank shown, the poster SHALL instead sit a small fixed gap after the row's status-colour stripe, rather than flush against it, so the stripe and poster read as two distinct elements.

Rows SHALL keep the height they have without the change: the row does not grow to the poster's natural aspect ratio, so adopting this layout does not lengthen the page. The poster's width MAY grow along with its height to avoid cropping the image more tightly than before. A row whose anime has no picture SHALL render its placeholder at the same full-height size, so rows with and without a picture stay aligned.

Where the anime's displayed picture is **not a poster**, meaning an upright or a wide picture in the sense the `artwork-presentation` capability defines, the row SHALL draw it whole at its own proportions on the terms the `artwork-presentation` capability sets out: the row's height is unchanged, the picture takes the width its proportions give it at that height up to that capability's bound, and the row's title and everything after it begin further along by that extra width. This is the one case in which two rows' posters differ in width. A poster keeps exactly the box described above, and the row's height, its progress, score, and edit columns, and its status stripe are unchanged in every case.

#### Scenario: Poster fills a my-list row
- **WHEN** the my-list page renders a row in grouped (unranked) view
- **THEN** that row's poster touches the row's top and bottom edges, with a small fixed gap between the status-colour stripe and the poster's leading edge

#### Scenario: Poster fills a ranked row
- **WHEN** a my-list or top-anime row shows a rank number before its poster
- **THEN** the poster still touches the row's top and bottom edges, sitting after the rank column rather than at the leading edge

#### Scenario: Row heights are unchanged
- **WHEN** rows adopt the full-height poster
- **THEN** each row occupies the same height as before, and the list is no longer than it was

#### Scenario: Missing picture keeps the row aligned
- **WHEN** a row's anime has no poster picture
- **THEN** its placeholder occupies the same full-height area, keeping the row's content aligned with its neighbours

#### Scenario: A landscape picture is drawn landscape
- **WHEN** a my-list or top-anime row's anime has a displayed picture wider than it is tall
- **THEN** the whole picture is shown at the row's height and at its own width there, with no part cropped away, and the row's title begins after it

#### Scenario: An upright picture is not cropped
- **WHEN** a my-list or top-anime row's anime has an upright displayed picture, such as a 4:5 picture
- **THEN** the whole picture is shown at the row's height and at its own width there, a little wider than a poster, and the row's title begins after it

#### Scenario: A landscape row is no taller than its neighbours
- **WHEN** a list mixes posters with upright and landscape artwork
- **THEN** every row stands the same height, and only the width of the non-poster rows' pictures differs
