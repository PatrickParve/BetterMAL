# artwork-presentation Specification

## Purpose
The artwork-presentation capability governs how a chosen picture is drawn wherever it appears as a row or thumbnail: shown whole rather than cropped, at its own proportions, with orientation read from the loaded image itself rather than fetched metadata. Which picture is chosen is artwork-selection's; the detail and series pages' own larger artwork treatment is anime-detail's and series-page's.

## Requirements
### Requirement: An anime's picture is drawn whole in every row and thumbnail

Wherever the app draws an anime's displayed picture into a **row picture slot** — a slot whose height is fixed by the row or control holding it, rather than a tile in a poster grid — the picture SHALL be drawn **whole, at its own proportions**. No part of it SHALL be cropped away to make it fill the slot.

The slot SHALL keep the height it has. A picture at least as wide as it is tall SHALL be drawn at that height and at whatever width its own proportions give it there, so a landscape picture is drawn landscape and correspondingly wider than a portrait poster beside it, and a square picture is drawn square. Whatever follows the picture in its row SHALL move along by that extra width; nothing else about the row SHALL change.

The drawn width SHALL be **bounded**, so that no picture can leave the text beside it unreadably narrow. The bound SHALL be derived from the slot's own height rather than being one figure shared across slots of different sizes, so a small thumbnail and a full-height list row are each bounded in proportion to themselves. A picture too wide even for that bound SHALL be reduced whole rather than cropped.

A picture taller than it is wide SHALL keep the slot exactly as it is today — the same width, the same height, and the same rendering — so ordinary poster artwork is unaffected by this requirement.

An anime with no picture SHALL render its placeholder at the slot's unchanged portrait size, so a row with no picture stays aligned with its neighbours.

#### Scenario: A landscape picture is not cropped
- **WHEN** a row shows an anime whose displayed picture is wider than it is tall
- **THEN** the whole picture is visible, drawn at the row's height and at its own width there, with nothing cut off

#### Scenario: A square picture is drawn square
- **WHEN** a row shows an anime whose displayed picture is exactly as wide as it is tall
- **THEN** it is drawn square at the row's height rather than cropped to the portrait poster box

#### Scenario: A portrait poster is unchanged
- **WHEN** a row shows an anime whose displayed picture is taller than it is wide
- **THEN** its picture occupies exactly the box it occupies today, at the same width and height

#### Scenario: The row's text moves along
- **WHEN** a landscape picture is drawn wider than a portrait poster would be
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
- the airing page's slot rows and the dashboard's "Airing today" rows;
- the related-anime overlay's rows and the completion-score overlay's picture;
- the navbar search dropdown's result thumbnails, and the settings page's difference rows and refresh-picker results.

The rule SHALL NOT apply to **poster grids and strips**, whose design is that every tile is the same size: the season, year, search and browse card grids; the Top Anime page's rank 1–3 showcase and rank 4–10 cards; the recap podium and the recap score board; and the profile page's "My top anime", "Most rewatched" and "Top series" poster strips. Those keep the fixed tile size their own capabilities require, and keep cropping to it.

The detail page, the series page header, the series timeline cards, the series "More" tiles, the update cards and the picture picker already draw pictures whole under their own capabilities' rules; this requirement SHALL NOT change any of them.

#### Scenario: A poster strip keeps its fixed tile size
- **WHEN** the profile page's "My top anime" strip holds an anime with landscape artwork
- **THEN** every tile in the strip is still the same size, and that tile is cropped to it as it is today

#### Scenario: A card grid keeps its rhythm
- **WHEN** a season, year, search or browse listing includes an anime with landscape artwork
- **THEN** every card in the grid is still the same size

#### Scenario: Already-whole surfaces are untouched
- **WHEN** an anime with landscape artwork is opened on its detail page, its series page, or shown in an update card
- **THEN** each renders exactly as its own capability already requires, unchanged by this one

### Requirement: Orientation is read from the picture, not fetched

A picture's proportions SHALL be determined from the image once it has loaded. The system SHALL NOT request, store, or wait on picture dimensions to make this decision, and no stored record or API response SHALL be required to carry them.

Until a picture's proportions are known, its slot SHALL render at the unchanged portrait size, adopting the whole-picture treatment once the picture is known to be at least as wide as it is tall. A list of portrait artwork SHALL therefore not shift as its pictures load.

A picture already held by the browser from an earlier visit SHALL be treated the same as a freshly loaded one, so returning to a page does not leave wide artwork drawn in the portrait box.

#### Scenario: Nothing extra is fetched
- **WHEN** any of these surfaces renders a list of anime
- **THEN** no request is made for picture dimensions, and no dimension is read from stored data

#### Scenario: The portrait box is the starting point
- **WHEN** a row renders before its picture has loaded
- **THEN** the slot shows the unchanged portrait box, and takes the wide treatment only once the picture is known to be wide

#### Scenario: A revisited page still draws wide artwork wide
- **WHEN** I return to a page whose pictures the browser already holds
- **THEN** wide artwork is drawn wide immediately, rather than staying in the portrait box until a reload

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
