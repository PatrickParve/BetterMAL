## MODIFIED Requirements

### Requirement: Which surfaces are row picture slots

The whole-picture rule SHALL apply to every surface that draws an anime's picture beside text in a fixed-height row, or as a thumbnail in a list or dropdown:

- my-list rows and top-anime rows;
- the profile page's "Latest updates" feed rows and both opinion-divergence lists' rows;
- the full edit-history overlay's rows, the unresolved-episodes overlay's rows, and the top-anime tie-break selection overlay's rows;
- the recap page's hot-take rows and top-ten rows;
- the poster clusters on the ranked season and year rows, both inline and in the "See all" overlay, on the recap page and the profile page alike;
- the dashboard's "Airing today" rows;
- the related-anime overlay's rows;
- the navbar search dropdown's result thumbnails, and the settings page's difference rows and refresh-picker results.

Poster grids, strips, fixed poster boxes and banner boxes are **not** row picture slots, since their pictures do not sit beside text in a row whose height fixes the picture. They draw their pictures whole under the card, tile and box rules instead, and "Which surfaces are cards, tiles and poster boxes" assigns each of them to one. The row rule SHALL NOT be applied to them, so a grid card is never drawn as a height-bound row slot.

The airing page's slots, which were row picture slots before, are banner boxes. Their picture sits above the title rather than beside it, so a seventh-of-a-page column never has to share its width between a picture and the text.

The detail page, the update cards, the picture picker and the completion-score overlay already draw pictures whole under their own capabilities' rules; this requirement SHALL NOT change any of them. The completion-score overlay's picture, a row picture slot before, is drawn under `list-editing`'s rule for the completion prompt. That rule draws it whole at one of two fixed widths and its own height, not at a row's height. The series page header, timeline cards and "More" tiles draw their pictures whole under `series-page`'s rules.

#### Scenario: A card grid is not drawn as rows
- **WHEN** a season, year, search or Series browser listing includes an anime with landscape artwork
- **THEN** its card is drawn under the fixed poster box rule, not as a height-bound row slot

#### Scenario: Already-whole surfaces are untouched
- **WHEN** an anime with landscape artwork is opened on its detail page, or shown in an update card, the picture picker or the completion-score overlay
- **THEN** each renders exactly as its own capability already requires, unchanged by this one

#### Scenario: The completion prompt's picture is not a row slot
- **WHEN** the completion-score overlay opens for an anime with a landscape picture
- **THEN** its picture is drawn under the completion prompt's own rule, at that rule's landscape width, and not capped at 16/9 of a row slot's height

#### Scenario: The airing page is not drawn as rows
- **WHEN** the airing page shows a slot whose anime has landscape artwork
- **THEN** its picture is drawn in the slot's banner box above the title, not beside the title at a row's height

#### Scenario: Airing today keeps its row
- **WHEN** the dashboard's "Airing today" list shows an anime with landscape artwork
- **THEN** that row draws its picture exactly as the row rule requires, unchanged by the airing page's move to banner boxes
