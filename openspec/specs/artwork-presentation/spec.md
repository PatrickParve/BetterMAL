# artwork-presentation Specification

## Purpose
The artwork-presentation capability governs how a chosen picture is drawn wherever it appears as a row, thumbnail, card, tile or poster box: shown whole rather than cropped, at its own proportions, with its shape (poster, upright or wide) read from the loaded image itself rather than fetched metadata. Which picture is chosen is artwork-selection's; the detail and series pages' own larger artwork treatment is anime-detail's and series-page's.

## Requirements
### Requirement: An anime's picture is drawn whole in every row and thumbnail

Wherever the app draws an anime's displayed picture into a **row picture slot** — a slot whose height is fixed by the row or control holding it, rather than a tile in a poster grid — the picture SHALL be drawn **whole, at its own proportions**. No part of it SHALL be cropped away to make it fill the slot.

The slot SHALL keep the height it has. A picture that is not a poster — an upright or a wide picture, in the sense of "A loaded picture has one of three shapes" — SHALL be drawn at that height and at whatever width its own proportions give it there, so a landscape picture is drawn landscape and correspondingly wider than a poster beside it, a square picture is drawn square, and an upright picture is drawn at its own width, a little wider than a poster. Whatever follows the picture in its row SHALL move along by that extra width; nothing else about the row SHALL change.

The drawn width SHALL be **bounded**, so that no picture can leave the text beside it unreadably narrow. The bound SHALL be derived from the slot's own height rather than being one figure shared across slots of different sizes, so a small thumbnail and a full-height list row are each bounded in proportion to themselves. A picture too wide even for that bound SHALL be reduced whole rather than cropped.

A poster SHALL keep the slot exactly as it is today — the same width, the same height, and the same rendering — so ordinary poster artwork is unaffected by this requirement.

An anime with no picture SHALL render its placeholder at the slot's unchanged portrait size, so a row with no picture stays aligned with its neighbours.

#### Scenario: A landscape picture is not cropped
- **WHEN** a row shows an anime whose displayed picture is wider than it is tall
- **THEN** the whole picture is visible, drawn at the row's height and at its own width there, with nothing cut off

#### Scenario: A square picture is drawn square
- **WHEN** a row shows an anime whose displayed picture is exactly as wide as it is tall
- **THEN** it is drawn square at the row's height rather than cropped to the portrait poster box

#### Scenario: An upright picture is not cropped
- **WHEN** a row shows an anime whose displayed picture is upright, such as a 4:5 picture
- **THEN** the whole picture is visible, drawn at the row's height and at its own width there, a little wider than a poster beside it

#### Scenario: A poster is unchanged
- **WHEN** a row shows an anime whose displayed picture is a poster
- **THEN** its picture occupies exactly the box it occupies today, at the same width and height

#### Scenario: The row's text moves along
- **WHEN** a landscape picture is drawn wider than a poster would be
- **THEN** the title and everything after it in that row begin further along, and the row's height, its other columns' widths, and its controls are otherwise unchanged

#### Scenario: An extremely wide picture is bounded
- **WHEN** an anime's displayed picture is far wider than it is tall
- **THEN** its drawn width stops at the slot's bound and the whole picture is still visible within it, reduced rather than cropped

#### Scenario: A small thumbnail is bounded in proportion to itself
- **WHEN** the same landscape picture is drawn in a full-height list row and in a small dropdown thumbnail
- **THEN** each is bounded relative to its own slot height, so neither the row nor the dropdown is overrun

#### Scenario: A missing picture keeps the row aligned
- **WHEN** a row's anime has no displayed picture
- **THEN** its placeholder occupies the slot's unchanged portrait box, keeping that row's content aligned with its neighbours'

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

### Requirement: Orientation is read from the picture, not fetched

A picture's proportions, and so its shape, SHALL be determined from the image once it has loaded. This holds on every surface this capability governs: rows, cards, tiles and poster boxes alike. The system SHALL NOT request, store, or wait on picture dimensions to make this decision, and no stored record or API response SHALL be required to carry them.

Until a picture's shape is known, every surface SHALL treat it as a poster: its slot, card, tile or box renders at the unchanged poster size with no fill. It adopts its shape's treatment once the picture is known not to be a poster. A list or grid of posters SHALL therefore not shift as its pictures load.

A picture already held by the browser from an earlier visit SHALL be treated the same as a freshly loaded one, so returning to a page does not leave wide or upright artwork drawn in the poster box.

#### Scenario: Nothing extra is fetched
- **WHEN** any surface this capability governs renders a list or grid of anime
- **THEN** no request is made for picture dimensions, and no dimension is read from stored data

#### Scenario: The poster box is the starting point
- **WHEN** a row or card renders before its picture has loaded
- **THEN** it shows the unchanged poster box with no fill, and takes its shape's treatment only once the picture is known not to be a poster

#### Scenario: A grid of posters does not move
- **WHEN** a season grid whose pictures are all posters loads its pictures
- **THEN** no card changes size or position as they arrive

#### Scenario: A revisited page still draws wide artwork whole
- **WHEN** I return to a page whose pictures the browser already holds
- **THEN** wide and upright artwork is drawn whole immediately, rather than staying in the poster box until a reload

### Requirement: Row pictures are requested as they come into view

Every surface listed as a row picture slot SHALL let the browser defer a picture's request until that picture is at or near the viewport, so opening a page holding many rows does not issue a request for every picture at once. The deferral SHALL be the browser's own native lazy loading, asked for on the picture element itself; the system SHALL NOT track scroll position or manage a request queue of its own to achieve it.

Deferral SHALL change only *when* a picture is requested, never which picture is drawn or how. A deferred picture SHALL take the whole-picture treatment on load exactly as an immediately requested one does, so "Orientation is read from the picture, not fetched" holds unchanged: the slot shows the portrait box until the picture arrives, whenever it arrives, and a picture the browser already holds is still drawn wide immediately.

#### Scenario: A long list does not request every picture at once
- **WHEN** I open a page holding far more rows than fit on screen
- **THEN** the pictures near the viewport are requested and those far below it are not requested yet

#### Scenario: Scrolling requests the rest
- **WHEN** I scroll a row further down that list into view
- **THEN** its picture is requested and drawn

#### Scenario: A deferred picture still takes the wide treatment
- **WHEN** a row scrolled into view holds landscape artwork
- **THEN** its slot adopts the whole-picture treatment once that picture loads, exactly as a row visible from the start does

#### Scenario: Held pictures are unaffected
- **WHEN** I return to a page whose pictures the browser already holds
- **THEN** those pictures are drawn immediately, wide artwork included, with no wait introduced by the deferral

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

### Requirement: Leftover space in a picture's box is filled from the picture itself

Wherever an upright or wide picture drawn whole leaves part of its box uncovered, that uncovered space SHALL be filled with a softened, dimmed, blurred rendering of the same picture, so the box reads as belonging to the artwork rather than as empty bands. This covers a fixed poster box, a grid card's picture area, a height-bound tile past its bound, and the series page's cards and tiles. A banner box is the exception: it draws every picture whole on its own flat background and mounts no fill for any shape. The recap podium's box is a narrower exception: it mounts no fill for any shape, and an upright or wide picture there is drawn whole and centred on the card itself, with no box of its own colour behind it.

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

#### Scenario: A podium picture that is not a poster has nothing behind it
- **WHEN** a recap podium card shows a landscape, square or upright picture
- **THEN** the picture is drawn whole and centred in the card's picture area, with no blurred rendering of it and no box of its own colour behind it, so only the card's own surface shows around it

#### Scenario: The fill costs no request
- **WHEN** a card shows an upright picture with the fill behind it
- **THEN** no request is made beyond the one that loaded the picture itself

#### Scenario: A missing picture is unchanged
- **WHEN** the recap podium's first-ranked anime has no picture
- **THEN** its placeholder looks exactly as it does today, and the card's animated sheen still shows through it

#### Scenario: Both themes
- **WHEN** I view a card with a filled picture box in the light theme and again in the dark theme
- **THEN** the picture stands clear of its fill, and the card's own colours and text read the same as its neighbours', in both
