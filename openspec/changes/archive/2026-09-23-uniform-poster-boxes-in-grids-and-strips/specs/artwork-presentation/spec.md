## MODIFIED Requirements

### Requirement: Which surfaces are row picture slots

The whole-picture rule SHALL apply to every surface that draws an anime's picture beside text in a fixed-height row, or as a thumbnail in a list or dropdown:

- my-list rows and top-anime rows;
- the profile page's "Latest updates" feed rows and both opinion-divergence lists' rows;
- the full edit-history overlay's rows, the unresolved-episodes overlay's rows, and the top-anime tie-break selection overlay's rows;
- the recap page's hot-take rows and top-ten rows;
- the poster clusters on the ranked season and year rows, both inline and in the "See all" overlay, on the recap page and the profile page alike;
- the dashboard's "Airing today" rows;
- the related-anime overlay's rows and the completion-score overlay's picture;
- the navbar search dropdown's result thumbnails, and the settings page's difference rows and refresh-picker results.

Poster grids, strips, fixed poster boxes and banner boxes are **not** row picture slots, since their pictures do not sit beside text in a row whose height fixes the picture. They draw their pictures whole under the card, tile and box rules instead, and "Which surfaces are cards, tiles and poster boxes" assigns each of them to one. The row rule SHALL NOT be applied to them, so a grid card is never drawn as a height-bound row slot.

The airing page's slots, which were row picture slots before, are banner boxes. Their picture sits above the title rather than beside it, so a seventh-of-a-page column never has to share its width between a picture and the text.

The detail page, the update cards and the picture picker already draw pictures whole under their own capabilities' rules; this requirement SHALL NOT change any of them. The series page header, timeline cards and "More" tiles draw their pictures whole under `series-page`'s rules.

#### Scenario: A card grid is not drawn as rows
- **WHEN** a season, year, search or Series browser listing includes an anime with landscape artwork
- **THEN** its card is drawn under the fixed poster box rule, not as a height-bound row slot

#### Scenario: Already-whole surfaces are untouched
- **WHEN** an anime with landscape artwork is opened on its detail page, or shown in an update card or the picture picker
- **THEN** each renders exactly as its own capability already requires, unchanged by this one

#### Scenario: The airing page is not drawn as rows
- **WHEN** the airing page shows a slot whose anime has landscape artwork
- **THEN** its picture is drawn in the slot's banner box above the title, not beside the title at a row's height

#### Scenario: Airing today keeps its row
- **WHEN** the dashboard's "Airing today" list shows an anime with landscape artwork
- **THEN** that row draws its picture exactly as the row rule requires, unchanged by the airing page's move to banner boxes

### Requirement: Which surfaces are cards, tiles and poster boxes

Every surface that draws an anime's picture as a card, a tile or a poster box, rather than as a row picture slot, SHALL belong to exactly one of four families, and SHALL draw its pictures whole under that family's rule:

- **Column grids**, whose cards sit in fixed columns in a sorted order and may span two of them: the series page's "More" tiles, under `series-page`.
- **Fixed poster boxes**, whose box shape is part of the surface's design: the home page's "Currently watching" carousel cards and "Followed shows airing" grid cards; the season, year and search results grids' cards; the Series browser grid's cards; the Top anime page's rank 4–10 cards; the recap podium's cards; the recap score board's tiles; and the profile page's "My top anime", "Top series", "Most rewatched" (both scopes) and "Most time spent" strip tiles. The series timeline cards also follow this rule, under `series-page`, with its one widened card size.
- **Height-bound tiles**, whose height is fixed and whose width is free: the Top anime page's rank 1–3 showcase poster.
- **Banner boxes**, whose width is fixed by their card and which are wider than they are tall: the airing page's slot picture bands.

#### Scenario: Every former exception has a family
- **WHEN** any of the season, year, search or Series browser grids, the home carousel or "Followed shows airing" grid, a Top anime card or showcase, the recap podium or score board, or a profile poster strip shows an anime with landscape artwork
- **THEN** that picture is drawn whole under its surface's family rule, not cropped to a poster box

#### Scenario: The browse grids share the carousel's treatment
- **WHEN** the same landscape picture appears on a "Currently watching" carousel card and on a season, year, search, Series browser or "Followed shows airing" card
- **THEN** each card keeps its portrait picture box and draws the whole picture inside it over the same blurred fill

#### Scenario: The airing band is a banner box
- **WHEN** the airing page shows a slot with any picture
- **THEN** that picture is drawn under the banner-box rule

### Requirement: A wide card spans two columns of a grid, in strict order

In a column grid, a card whose picture is a poster SHALL keep exactly the size and position it has today.

A card whose picture is **wide** SHALL occupy two adjacent columns. Its picture area SHALL keep the height a one-column card's picture area has, so its title and everything beneath it line up with its row neighbours'. Within that area the picture SHALL be drawn whole at that height and at its own width. A picture too wide even for the two-column width SHALL be reduced whole rather than cropped. The card's title, information and colours SHALL be those of any other card. Only its width differs.

Cards SHALL keep exactly the order their page puts them in. A wide card that does not fit in the columns left at the end of a row SHALL begin the next row, and the row it could not join SHALL be left one column short. No later card SHALL be moved ahead of it to fill that gap.

A card whose picture is **upright** SHALL keep one column, and its picture SHALL be drawn whole inside that card's picture area.

Where a grid has room for only one column, a wide card SHALL stay one column and its picture SHALL be drawn whole inside it. A wide card SHALL never make the page scroll sideways.

This rule SHALL apply only to the surfaces "Which surfaces are cards, tiles and poster boxes" names as column grids. A grid that surface list names as fixed poster boxes, such as the season, year, search, Series browser and "Followed shows airing" grids, SHALL NOT span a card over two columns for any picture.

#### Scenario: A landscape card spans two columns
- **WHEN** a series page's More grid includes an entry whose displayed picture is landscape
- **THEN** its tile spans two columns and shows the whole picture, with nothing cut off

#### Scenario: A square card spans two columns
- **WHEN** a series page's More grid includes an entry whose displayed picture is square
- **THEN** its tile spans two columns and shows the whole picture square

#### Scenario: A wide card stays in line with its row
- **WHEN** a row of a column grid holds a two-column card and one-column cards
- **THEN** every card's picture area is the same height, and every card's title and information begin on the same lines

#### Scenario: Sort order is never changed
- **WHEN** a wide card's turn in a column grid's order falls in a row's last column
- **THEN** it begins the next row, the row before ends one column short, and every card still appears in the page's order

#### Scenario: An upright card keeps one column
- **WHEN** a column grid includes an entry whose displayed picture is upright
- **THEN** its card keeps one column and shows the whole picture inside its picture area

#### Scenario: A narrow grid does not scroll sideways
- **WHEN** a column grid is narrow enough to hold only one column and includes a wide card
- **THEN** that card stays one column with its whole picture drawn inside it, and the page does not scroll sideways

#### Scenario: A grid of posters is unchanged
- **WHEN** a column grid's pictures are all posters
- **THEN** every card is exactly the size, and in exactly the position, it is today

#### Scenario: A browse grid never spans
- **WHEN** a season, year, search, Series browser or "Followed shows airing" grid includes an anime whose displayed picture is wide
- **THEN** its card occupies one column like every other card in that grid

### Requirement: A fixed poster box draws its picture whole inside it

A fixed poster box SHALL keep its size and shape whatever picture it holds. A poster SHALL fill it exactly as it does today. An upright or wide picture SHALL be drawn whole inside the box, centred, and as large as the box allows at the picture's own proportions.

Nothing about the surface around the box SHALL change: the card or tile carrying it, how many of them are shown, how they are sized, stepped, aligned, ordered or counted, and the information they carry are the same whatever the picture's shape. Each surface's own requirements about those things therefore hold unchanged.

Where fixed poster boxes sit in a grid, every card SHALL occupy one column whatever its picture's shape, so no card's picture can leave a row short, push a card onto a new row, or move any card when it loads. Where they sit in a strip, every tile SHALL be the strip's poster tile size whatever its picture's shape, so no picture can change how many tiles fit across the strip.

#### Scenario: A landscape podium card keeps its box
- **WHEN** the recap podium's first-ranked anime has a landscape picture
- **THEN** that card is the same size it would be with a poster, the podium still steps down from first to fifth, and the whole picture is visible inside the card's picture box

#### Scenario: The carousel still shows exactly five cards
- **WHEN** the home page's "Currently watching" carousel holds a card whose picture is wide
- **THEN** that card is the same size as every other card, its whole picture is drawn inside it, and the carousel still shows exactly five cards with no sliver of a sixth

#### Scenario: A score board tile is whole
- **WHEN** a score board slot holds an anime whose picture is square
- **THEN** its tile is the same size as its neighbours, keeps its tier border, and shows the whole square picture inside it

#### Scenario: A rank 4–10 card is whole
- **WHEN** a Top anime rank 4–10 card's anime has a landscape picture
- **THEN** the seven cards still form one row of equal cards, and that card shows its whole picture inside its picture box

#### Scenario: A landscape season card keeps one column
- **WHEN** a season grid includes an anime whose displayed picture is landscape
- **THEN** its card is the same size as its neighbours, occupies one column, and shows the whole picture inside its portrait picture box over the blurred fill

#### Scenario: A wide card in a row's last column stays there
- **WHEN** the card whose turn falls in the last column of a year, search or Series browser grid's row holds a wide picture
- **THEN** it takes that last column like any other card, and the row spans the content width

#### Scenario: Followed shows airing keeps its columns
- **WHEN** the home page's "Followed shows airing" grid includes an anime whose displayed picture is square
- **THEN** its card is the same size as its neighbours and shows the whole square picture inside its portrait picture box

#### Scenario: A browse grid does not move as pictures load
- **WHEN** a season grid loads a mixture of posters, upright pictures and landscape pictures
- **THEN** no card changes size or position as they arrive

#### Scenario: A strip tile keeps the poster tile's size
- **WHEN** a profile poster strip holds an anime whose displayed picture is landscape
- **THEN** its tile is exactly the size of the poster tiles beside it, and shows the whole picture inside it over the blurred fill

### Requirement: A height-bound tile takes its picture's width

A height-bound tile SHALL keep its height whatever picture it holds. A tile whose picture is a poster SHALL keep exactly the size it has today.

A tile whose picture is upright or wide SHALL take the width its picture's proportions give it at that height, so the whole picture is shown rather than cropped to the poster tile. The width SHALL be **bounded** in proportion to the tile's own height, as a row picture slot's is, so no picture can make a tile arbitrarily wide. A picture too wide even for that bound SHALL be reduced whole rather than cropped.

Where a tile sits beside text in its card, as the Top anime showcase's poster sits beside its rank, title and scores, the tile SHALL NOT narrow that text below the width it needs to stay readable. Where the card cannot give the picture its full width, the picture SHALL be drawn whole within the width the tile can have.

#### Scenario: A landscape showcase poster is wider, not taller
- **WHEN** the Top anime showcase's first-ranked anime has a landscape picture, at a width where the showcase cards are stacked in one column
- **THEN** its poster is the same height as a poster's would be and wider than one, and shows the whole picture

#### Scenario: A square showcase poster is square
- **WHEN** the Top anime showcase's first-ranked anime has a square picture, at a width where the showcase cards are stacked in one column
- **THEN** its poster is as wide as it is tall, at the showcase poster's height, and shows the whole picture

#### Scenario: A showcase card stays readable
- **WHEN** the Top anime showcase's first-ranked anime has a landscape picture, at a width where the three showcase cards sit side by side
- **THEN** the whole picture is shown, and the card's rank, title and both score chips remain fully readable beside it
