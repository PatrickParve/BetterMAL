## RENAMED Requirements

- FROM: `### Requirement: Landscape artwork is shown whole on the series page`
- TO: `### Requirement: Every picture is shown whole on the series page`

## MODIFIED Requirements

### Requirement: Every picture is shown whole on the series page
An entry's picture SHALL be shown whole wherever the series page renders it, whatever its shape: the page header's picture, a main-line timeline card's picture, and a More tile's picture. The shapes are those the `artwork-presentation` capability defines: a **poster**, an **upright** picture, and a **wide** picture (square or landscape). Only a poster SHALL be cropped to fill its box, and only as it is today.

In the page header, a poster SHALL keep its existing box. An upright or square picture SHALL keep the width the header's portrait picture has and take whatever height its own proportions give it at that width, so a square picture is drawn square and nothing is cut off. A landscape picture, wider than it is tall, SHALL keep the header's existing landscape treatment. In every case the title, status pill, personal badge, year span, links, score averages and progress SHALL keep their existing positions relative to the picture.

On a timeline card and on a More tile, the card's picture area SHALL keep the height it has for a poster, so every card in a row still lines its picture, title and footer up with its neighbours'. A wide picture SHALL be fitted whole inside that area rather than cropped to fill it, and the card carrying it SHALL be wider than its poster neighbours so the fitted picture is shown at a useful size rather than reduced to a sliver of the card's height:

- on the timeline, the card SHALL take the one widened card size;
- in More, the tile SHALL span two of its grid's columns, under `artwork-presentation`'s rule for column grids. It stays in the group's order, leaves the row before it one column short when it does not fit at that row's end, and stays one column wide where the grid has room for only one.

An upright picture SHALL keep the width of a poster card or tile and be fitted whole inside its picture area. Wherever a fitted picture leaves part of a picture area uncovered, that space SHALL be filled from the picture itself, as `artwork-presentation` requires.

A card's extra width SHALL be a consequence of its artwork's shape alone. It SHALL NOT vary with how long the entry ran, how long the wait before it was, its episode count, its scores, or my progress on it. It SHALL take only one widened size rather than a size computed per image, so a wider card can never be read as a duration or magnitude signal.

Wherever this capability refers to a landscape timeline card, a landscape More tile, or landscape artwork on either, it SHALL mean one carrying a wide picture in this sense, square pictures included.

A poster, and the placeholder shown when an entry has no picture, SHALL keep their existing boxes and their existing card widths unchanged.

Because a picture's shape is not known until the image itself has loaded, the page SHALL render the existing poster boxes until then, and adopt a picture's treatment once its shape is known. The page SHALL NOT request, store, or wait on any additional data to make this decision.

#### Scenario: A landscape header picture is not cropped
- **WHEN** I open a series whose root entry's picture is wider than it is tall
- **THEN** the header shows that whole picture at its own proportions, and the title, pill, badge, year span, links, averages, and progress beside it are positioned exactly as on any other series page

#### Scenario: A square header picture is drawn square
- **WHEN** I open a series whose picture is exactly as wide as it is tall
- **THEN** the header shows the whole picture square, at the header's portrait picture width, with nothing cut off

#### Scenario: A landscape timeline card shows its whole picture
- **WHEN** a main-line entry's picture is wider than it is tall
- **THEN** its timeline card shows the whole picture, the card is wider than its poster neighbours, and its picture area, title, chips, and footer still line up with theirs

#### Scenario: A square timeline card shows its whole picture
- **WHEN** a main-line entry's picture is exactly as wide as it is tall
- **THEN** its timeline card takes the same widened size a landscape card takes, and shows the whole square picture inside its picture area

#### Scenario: A landscape More tile shows its whole picture
- **WHEN** an extra's picture is wider than it is tall
- **THEN** its More tile shows the whole picture and is wider than the poster tiles in its group, while its rows stay aligned with them

#### Scenario: A square More tile shows its whole picture
- **WHEN** an extra's picture is exactly as wide as it is tall
- **THEN** its More tile spans two columns and shows the whole square picture, with nothing cut off at its top, bottom or sides

#### Scenario: An upright picture is whole in a poster-width card
- **WHEN** a main-line entry's or an extra's picture is upright, such as a 4:5 picture
- **THEN** its card or tile keeps the poster width and shows the whole picture inside its picture area, with the space around it filled from the picture itself

#### Scenario: More keeps its order
- **WHEN** a wide More tile's turn falls in the last column of its group's row
- **THEN** it begins the next row, and every tile in the group still appears in the group's order

#### Scenario: Card width still says nothing about duration
- **WHEN** a series has one entry that ran a single cour and another that ran for several years, both with poster pictures
- **THEN** both cards render at the same width, and the only cards that differ in width anywhere on the page are those whose own artwork is wide

#### Scenario: Poster artwork is untouched
- **WHEN** I open a series in which every picture is a poster
- **THEN** the header picture, every timeline card, and every More tile render exactly as they do today
