## MODIFIED Requirements

### Requirement: Season grid fills the content width
The season results grid SHALL size its cards so that each row spans the full width of the page content, leaving no unused gutter to the right of the last card in a row, and SHALL show cover images larger than the fixed-width browse card. The number of cards per row SHALL adapt to the available width, but SHALL be a fixed count at ordinary desktop widths (above the mobile breakpoint) rather than one derived by dividing the container width by a minimum card size — the latter can flip the column count from a small, incidental change in the page's effective width (e.g. a scrollbar or a different display's exact resolution/scaling) even though nothing about the window's actual size class changed. The page's outer container SHALL likewise fill the available display width rather than being capped to a fixed design width, so the grid (and the rest of the page) render the same way regardless of which display, or display configuration, the browser is on.

The one row allowed to end short of the content width is a row followed by a wide card that needs two columns and did not fit in the columns that row had left. Under the `artwork-presentation` capability's rule for column grids, that card begins the next row and the cards keep their sort order, so the row before it ends one column short. No other row SHALL end short, and a row SHALL never end more than one column short for this reason.

#### Scenario: Grid leaves no right-hand gutter
- **WHEN** season results are displayed at any window width
- **THEN** the cards in each full row together span the content width, with no large empty space to the right of the grid

#### Scenario: A wide card leaves one short row
- **WHEN** the card whose turn falls in a row's last column holds a wide picture
- **THEN** that card begins the next row spanning two columns, the row before it ends one column short, and every other full row still spans the content width

#### Scenario: Adapting to a narrower window
- **WHEN** I narrow the window
- **THEN** fewer cards are placed per row and the cards continue to fill the row width

#### Scenario: Card count stays fixed across ordinary desktop widths
- **WHEN** the page's effective width changes slightly at ordinary desktop sizes (e.g. connecting or disconnecting an external display changes the browser's logical resolution, but the window is still a normal desktop width)
- **THEN** the grid continues to show the same number of cards per row at essentially the same size, rather than jumping to one fewer or one more card per row

#### Scenario: Page fills the display instead of showing a boxed layout
- **WHEN** the browser window is wider than the page's previous fixed design width (an external monitor, or a laptop's own unscaled display)
- **THEN** the page content still fills the available width edge to edge, rather than sitting in a centered column with empty space and a visible border on both sides
