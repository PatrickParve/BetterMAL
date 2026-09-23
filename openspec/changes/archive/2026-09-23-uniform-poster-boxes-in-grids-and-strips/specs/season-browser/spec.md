## MODIFIED Requirements

### Requirement: Season grid fills the content width
The season results grid SHALL size its cards so that each row spans the full width of the page content, leaving no unused gutter to the right of the last card in a row, and SHALL show cover images larger than the fixed-width browse card. The number of cards per row SHALL adapt to the available width, but SHALL be a fixed count at ordinary desktop widths (above the mobile breakpoint) rather than one derived by dividing the container width by a minimum card size — the latter can flip the column count from a small, incidental change in the page's effective width (e.g. a scrollbar or a different display's exact resolution/scaling) even though nothing about the window's actual size class changed. The page's outer container SHALL likewise fill the available display width rather than being capped to a fixed design width, so the grid (and the rest of the page) render the same way regardless of which display, or display configuration, the browser is on.

Every card SHALL occupy exactly one column whatever the shape of its picture. A card whose picture is upright or wide SHALL draw it whole inside the card's portrait picture box, under the `artwork-presentation` capability's rule for fixed poster boxes, rather than spanning further columns. No card's picture SHALL leave a row ending short, push a card onto a new row, or move any card when that picture loads.

Where the grid reveals its cards progressively as it is scrolled, each reveal SHALL end on a complete row whenever more cards remain to be revealed. A reveal step whose cards do not fill its last row, as when the column count below the desktop breakpoint does not divide the step, SHALL NOT leave cards alone on that row waiting for the next reveal to fill in beside them. The grid SHALL reveal as many further cards as that row needs, and SHALL do so again whenever a change of the grid's width leaves the last revealed row short. Only the last row of the whole list MAY be short, since that is where the list ends. Every grid that shares this grid's form, including the Year, Search and Series browser grids, SHALL reveal by this same rule.

#### Scenario: Grid leaves no right-hand gutter
- **WHEN** season results are displayed at any window width
- **THEN** the cards in each full row together span the content width, with no large empty space to the right of the grid

#### Scenario: A wide picture keeps its card to one column
- **WHEN** the card whose turn falls in a row's last column holds a wide picture
- **THEN** that card takes the row's last column like any other card, shows its whole picture inside its portrait picture box, and the row spans the content width

#### Scenario: Adapting to a narrower window
- **WHEN** I narrow the window
- **THEN** fewer cards are placed per row and the cards continue to fill the row width

#### Scenario: Card count stays fixed across ordinary desktop widths
- **WHEN** the page's effective width changes slightly at ordinary desktop sizes (e.g. connecting or disconnecting an external display changes the browser's logical resolution, but the window is still a normal desktop width)
- **THEN** the grid continues to show the same number of cards per row at essentially the same size, rather than jumping to one fewer or one more card per row

#### Scenario: Page fills the display instead of showing a boxed layout
- **WHEN** the browser window is wider than the page's previous fixed design width (an external monitor, or a laptop's own unscaled display)
- **THEN** the page content still fills the available width edge to edge, rather than sitting in a centered column with empty space and a visible border on both sides

#### Scenario: A reveal ends on a full row
- **WHEN** a Season, Year, Search or Series browser grid shows a column count that does not divide its reveal step, and reveals its next cards
- **THEN** the last revealed row is still complete, with no card on it alone, and the cards continue in the page's sort order

#### Scenario: A resize keeps the last row full
- **WHEN** I narrow the window so that the grid's last revealed row no longer ends complete
- **THEN** the grid reveals the cards that complete that row, without waiting for the grid to be scrolled further
