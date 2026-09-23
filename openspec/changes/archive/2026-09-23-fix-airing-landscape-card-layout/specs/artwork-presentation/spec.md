## ADDED Requirements

### Requirement: A banner box draws every picture whole across it

A **banner box** is a picture box whose width is fixed by its card and is wider than the box's height, so that none of the three shapes fills it naturally except a landscape picture. A banner box SHALL draw every picture whole, a poster included, on the terms below. The airing page's slot picture band is a banner box.

A banner box SHALL keep one size whatever picture it holds. Its width SHALL be its card's width, and its height SHALL be derived from that width alone, bounded above and below. It SHALL NOT be derived from the picture, so every banner box on a page whose cards share a width is the same size. The box SHALL NOT change size when its picture loads, whatever that picture's shape turns out to be.

Every picture SHALL be drawn **whole** inside a banner box, whatever its shape, a poster included. It SHALL be centred and drawn as large as the box allows at the picture's own proportions. No part of any picture SHALL be cropped away to fill a banner box.

The space a picture leaves in a banner box SHALL show the box's own flat background. A banner box SHALL NOT mount the blurred fill described under "Leftover space in a picture's box is filled from the picture itself", for any shape, and nothing SHALL be drawn over its picture.

Before its picture has loaded, a banner box SHALL show its empty box at its fixed size with no fill. An anime with no picture SHALL render a placeholder of the same size.

A banner box's picture SHALL be requested through the browser's native lazy loading, exactly as a row picture slot's is, so a week holding many slots does not request every picture at once.

#### Scenario: A landscape picture fills the band
- **WHEN** a banner box whose proportions are close to 16:9 holds a 16:9 key visual
- **THEN** the whole picture is drawn across the box with little or no space left over

#### Scenario: A poster is drawn whole in the band
- **WHEN** a banner box holds an ordinary MAL poster
- **THEN** the whole poster is shown at the box's height, centred on the box's flat background, with nothing cropped from its top or bottom

#### Scenario: A very wide picture is reduced, not cropped
- **WHEN** a banner box holds a picture far wider than the box
- **THEN** the whole picture is shown across the box's width, reduced whole, with nothing cut from its sides and only the box's flat background above and below it

#### Scenario: The band does not move as pictures load
- **WHEN** a week of slots loads a mixture of posters, landscape pictures and square pictures
- **THEN** no banner box changes size as its picture arrives, and no slot beneath it moves

#### Scenario: A missing picture keeps the band's size
- **WHEN** a slot's anime has no displayed picture
- **THEN** its banner box shows a placeholder at the same size as every other slot's band

## MODIFIED Requirements

### Requirement: A loaded picture has one of three shapes

The system SHALL classify an anime's displayed picture, once it has loaded, as exactly one of three **shapes** by the ratio of its width to its height:

- **poster**: no wider than three quarters of its height. A poster fills any of the app's portrait boxes with at most a sliver cropped away, and SHALL be drawn exactly as it is today on every surface except a banner box, which draws it whole under "A banner box draws every picture whole across it".
- **upright**: wider than three quarters of its height, but still narrower than it is tall.
- **wide**: at least as wide as it is tall. Square and landscape pictures are both wide.

Only a poster SHALL ever be cropped to fill a box, and then only as it is today. An upright or wide picture SHALL be drawn whole wherever it appears, on the terms this capability sets for the kind of surface showing it.

Every surface this capability governs SHALL share this one classification, so a picture is never whole on one surface and cropped on another because the two drew the line in different places.

#### Scenario: An ordinary MAL poster is a poster
- **WHEN** an anime's displayed picture is 425 pixels wide and 600 tall
- **THEN** it is a poster, and every surface other than a banner box draws it exactly as it does today

#### Scenario: A square picture is wide
- **WHEN** an anime's displayed picture is exactly as wide as it is tall
- **THEN** it is wide, and no surface crops it to a poster box

#### Scenario: A near-square portrait picture is upright
- **WHEN** an anime's displayed picture is 540 pixels wide and 600 tall
- **THEN** it is upright, and no surface crops it to a poster box

#### Scenario: One classification everywhere
- **WHEN** the same upright picture appears in a my-list row and on a season card
- **THEN** both draw it whole

#### Scenario: A poster in a banner box is not cropped
- **WHEN** a poster appears in the airing page's slot band
- **THEN** it is drawn whole there, even though a poster box elsewhere would crop a sliver from it

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
- **WHEN** a season, year, search or browse listing includes an anime with landscape artwork
- **THEN** its card is drawn under the column-grid rule, not as a height-bound row slot

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

- **Column grids**, whose cards sit in fixed columns in a sorted order: the season, year and search results grids; the Series browser grid; and the home page's "Followed shows airing" grid. The series page's "More" tiles also follow this rule, under `series-page`.
- **Fixed poster boxes**, whose box shape is part of the surface's design: the home page's "Currently watching" carousel cards; the Top anime page's rank 4–10 cards; the recap podium's cards; and the recap score board's tiles. The series timeline cards also follow this rule, under `series-page`, with its one widened card size.
- **Height-bound tiles**, whose height is fixed and whose width is free: the profile page's "My top anime", "Top series", "Most rewatched" (both scopes) and "Most time spent" strips; and the Top anime page's rank 1–3 showcase poster.
- **Banner boxes**, whose width is fixed by their card and which are wider than they are tall: the airing page's slot picture bands.

#### Scenario: Every former exception has a family
- **WHEN** any of the season, year, search or Series browser grids, the home carousel or "Followed shows airing" grid, a Top anime card or showcase, the recap podium or score board, or a profile poster strip shows an anime with landscape artwork
- **THEN** that picture is drawn whole under its surface's family rule, not cropped to a poster box

#### Scenario: The airing band is a banner box
- **WHEN** the airing page shows a slot with any picture
- **THEN** that picture is drawn under the banner-box rule

### Requirement: Leftover space in a picture's box is filled from the picture itself

Wherever an upright or wide picture drawn whole leaves part of its box uncovered, that uncovered space SHALL be filled with a softened, dimmed, blurred rendering of the same picture, so the box reads as belonging to the artwork rather than as empty bands. This covers a fixed poster box, a grid card's picture area, a height-bound tile past its bound, and the series page's cards and tiles. A banner box is the exception: it draws every picture whole on its own flat background and mounts no fill for any shape.

The fill SHALL:

- sit behind the whole picture and never over any part of it;
- be decoration only, not interactive, focusable, or announced to assistive technology;
- cause no additional request, since it is the picture the box already shows;
- be legible in both the light and the dark theme without overpowering the card around it;
- leave the card's own colours, borders, badges and text exactly as they are.

A poster SHALL carry no fill. A box whose picture is drawn to its full extent shows none. A placeholder shown for an anime with no picture SHALL keep its existing appearance, including any transparency its surface requires.

#### Scenario: A landscape picture's spare space carries its colours
- **WHEN** a score board tile shows a landscape picture whole inside its poster box
- **THEN** the space above and below the picture is filled with a blurred, dimmed rendering of that same picture rather than a flat band

#### Scenario: A poster has no fill
- **WHEN** any card, tile or box shows a poster
- **THEN** it renders exactly as it does today, with no fill behind it

#### Scenario: A banner box has no fill
- **WHEN** the airing page's slot band shows a poster, a square picture or a very wide picture
- **THEN** the space around the picture shows the band's flat background, and no blurred rendering of the picture is drawn

#### Scenario: The fill costs no request
- **WHEN** a card shows an upright picture with the fill behind it
- **THEN** no request is made beyond the one that loaded the picture itself

#### Scenario: A missing picture is unchanged
- **WHEN** the recap podium's first-ranked anime has no picture
- **THEN** its placeholder looks exactly as it does today, and the card's animated sheen still shows through it

#### Scenario: Both themes
- **WHEN** I view a card with a filled picture box in the light theme and again in the dark theme
- **THEN** the picture stands clear of its fill, and the card's own colours and text read the same as its neighbours', in both
