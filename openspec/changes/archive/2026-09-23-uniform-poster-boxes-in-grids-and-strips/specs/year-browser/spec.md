## MODIFIED Requirements

### Requirement: Year page presentation matches the season page
The Year page SHALL present its results using the same card content, the same grid, and the same header arrangement as the season browser, so the two pages differ only in the period they cover and the controls that select it.

Each card SHALL show the anime's title, picture, type and episode count at the leading edge of its meta line, and its MAL score at the trailing edge of that same line, under the same rules — `?` for an unknown episode count, nothing in the score slot for an anime with no MAL score, and no score furniture at all while the global hide-scores toggle is on.

The results grid SHALL span the full content width with no unused right-hand gutter, at a card count per row that is fixed at ordinary desktop widths and adapts below the mobile breakpoint. Every card SHALL occupy one column whatever the shape of its picture, drawing an upright or wide picture whole inside its portrait picture box exactly as the season page's cards do. The header SHALL place the year step navigation in the horizontal center, the year dropdown immediately to its right, and the sort and filter controls after them.

Results SHALL load and reveal exactly as the season page's do: the year's whole listing — the union of its four seasons under the selected sort and filters — is read in one request and revealed progressively as the grid is scrolled, with nothing capping how many of the year's anime can be reached, no request issued to reveal more, the revealed count restored on a back/forward navigation, a fresh visit opening on the first screenful, and no refresh able to leave the grid showing fewer anime than it was showing.

#### Scenario: Rendering a year card
- **WHEN** a year's anime are displayed
- **THEN** each card shows its title, picture, type and episode count at the start of its meta line, and its MAL score at the end of that same line

#### Scenario: Hiding scores removes the card score entirely
- **WHEN** the global hide-scores toggle is on
- **THEN** no Year page card shows a MAL score, a placeholder, or a reveal control, and the type and episode count are unmoved

#### Scenario: The grid fills the content width
- **WHEN** year results are displayed at any window width
- **THEN** the cards in each full row together span the content width, with no large empty space to the right of the grid

#### Scenario: A wide picture keeps its card to one column
- **WHEN** a year's grid includes an anime whose displayed picture is landscape
- **THEN** its card occupies one column, the same size as its neighbours, and shows the whole picture inside its portrait picture box

#### Scenario: Header control placement
- **WHEN** I view the Year page header
- **THEN** the arrows and year label sit centered with the year dropdown directly to their right, and the sort and filter controls after them

#### Scenario: Scrolling loads more
- **WHEN** I scroll to the end of the loaded year results
- **THEN** more results appear automatically

#### Scenario: A whole year is reachable
- **WHEN** I keep scrolling a year holding well over a thousand anime
- **THEN** I reach its last anime, with no ceiling short of the year's own size, and no read was issued to reveal them

#### Scenario: A restored year keeps what it had revealed
- **WHEN** I scroll a year well past its first screenful of cards, open one of them, and go back
- **THEN** the grid shows the same number of cards it showed when I left, both immediately and after its background refresh lands

#### Scenario: Returning to where I was in a year
- **WHEN** I go back to a year I had scrolled far down
- **THEN** the page is at the position I left it at and stays there once the refresh completes

#### Scenario: A refresh cannot shorten a scrolled grid
- **WHEN** I have scrolled a year deep into its listing and its visit-triggered refresh then completes
- **THEN** the grid still shows everything it was showing, rather than being cut back to a screenful
