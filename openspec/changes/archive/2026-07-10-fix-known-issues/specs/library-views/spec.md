## MODIFIED Requirements

### Requirement: Top anime ranked list
The system SHALL present a Top anime page as a ranked list covering up to rank 500, where each row shows rank number, picture, title, my score, MAL score (right-aligned), and a list-action button. Because this page ranks anime overall, a row's anime may not be in my list; the button SHALL therefore be conditional — "Add" when the anime is not yet in my list, and "Edit" when it is. The list SHALL be paginated at 50 rows per page, with page-number controls plus left/right arrows at the bottom, and left/right arrow controls at the top-right.

#### Scenario: Rendering the top-anime ranking
- **WHEN** the top-anime page loads
- **THEN** it shows the first page of 50 rows, each with its rank number, picture, title, my score, a right-aligned MAL score, and a list-action button

#### Scenario: Paginating the ranking
- **WHEN** I use the page-number controls or the left/right arrows (at the bottom or top-right)
- **THEN** the list shows the corresponding 50-row page, up to rank 500

#### Scenario: Row not in my list
- **WHEN** a top-anime row's anime is not in my list
- **THEN** its button reads "Add"; using it adds the anime with status Plan to watch and the button changes in place to "Edit"

#### Scenario: Row already in my list
- **WHEN** a top-anime row's anime is already in my list
- **THEN** its button reads "Edit" and opens the editor overlay in place when used
