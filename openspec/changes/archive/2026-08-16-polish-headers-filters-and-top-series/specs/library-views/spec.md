## MODIFIED Requirements

### Requirement: Top anime ranked list
The system SHALL present a Top anime page as a ranked list covering up to rank 500, where each row shows rank number, picture, title, my score, MAL score (right-aligned), and a list-action button. Because this page ranks anime overall, a row's anime may not be in my list; the button SHALL therefore be conditional — "Add" when the anime is not yet in my list, and "Edit" when it is. The list SHALL be paginated at 50 rows per page, with page-number controls plus left/right arrows at the bottom, and left/right arrow controls at the top-right.

The page-number controls SHALL show the first page, the last page, and the current page with at most one page on each side of it, collapsing any gap between those groups into an ellipsis. On page 6 of 10 this yields `1 … 5 6 7 … 10`.

Rows ranked 1 through 3 SHALL be visually distinguished from the rest of the list: each SHALL render with a larger poster and row height than rank 4 and below, and a distinct rank badge or accent colour per position (a gold, silver, and bronze family for ranks 1, 2, and 3 respectively) in place of the plain `#1`/`#2`/`#3` text used elsewhere in the list. This treatment SHALL apply only on page 1, where ranks 1–3 render; it SHALL NOT apply to any other page.

#### Scenario: Rendering the top-anime ranking
- **WHEN** the top-anime page loads
- **THEN** it shows the first page of 50 rows, each with its rank number, picture, title, my score, a right-aligned MAL score, and a list-action button

#### Scenario: Paginating the ranking
- **WHEN** I use the page-number controls or the left/right arrows (at the bottom or top-right)
- **THEN** the list shows the corresponding 50-row page, up to rank 500

#### Scenario: Page numbers in the middle of the range
- **WHEN** I am on page 6 of 10
- **THEN** the page-number controls show 1, an ellipsis, 5, 6, 7, an ellipsis, and 10 — and no other page numbers

#### Scenario: Page numbers near an edge of the range
- **WHEN** I am on page 2 of 10
- **THEN** the page-number controls show 1, 2, 3, an ellipsis, and 10, with no ellipsis between adjacent pages

#### Scenario: Row not in my list
- **WHEN** a top-anime row's anime is not in my list
- **THEN** its button reads "Add"; using it adds the anime with status Plan to watch and the button changes in place to "Edit"

#### Scenario: Row already in my list
- **WHEN** a top-anime row's anime is already in my list
- **THEN** its button reads "Edit" and opens the editor overlay in place when used

#### Scenario: Top 3 rows stand out from the rest of the list
- **WHEN** the top-anime page's first page renders
- **THEN** ranks 1, 2, and 3 each render with a larger poster and row height and a distinct gold/silver/bronze rank badge, visibly different from rank 4 and below

#### Scenario: Podium treatment does not appear on later pages
- **WHEN** I view page 2 or later of the top-anime ranking
- **THEN** no row on that page uses the larger podium styling, since ranks 1–3 only appear on page 1
