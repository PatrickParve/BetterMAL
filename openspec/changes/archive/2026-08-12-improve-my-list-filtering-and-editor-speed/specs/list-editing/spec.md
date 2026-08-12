## ADDED Requirements

### Requirement: Overlays open independently of the page behind them
The system SHALL open and close the entry editor overlay and the completion-score prompt without redrawing the page underneath, so the cost of showing an overlay does not grow with how much that page is displaying. Opening the editor from a row of a list holding hundreds or thousands of entries SHALL be as immediate as opening it from a page showing a single anime — there SHALL be no perceptible delay between using an edit action and the overlay appearing.

Mounting an overlay SHALL NOT cause any page that merely holds a reference to the "open editor" or "increment episodes" action to re-render, since neither the page's data nor its own state has changed.

#### Scenario: Opening the editor on a large list
- **WHEN** I use the edit button on a my-list row while the list is showing a large library
- **THEN** the editor overlay appears immediately, with no perceptible delay, and no more slowly than it does on the anime detail page

#### Scenario: Closing the editor on a large list
- **WHEN** I close the editor overlay by saving, cancelling, or pressing Escape over a large list
- **THEN** the overlay disappears immediately and the page behind it is not redrawn

#### Scenario: Completion prompt behaves the same way
- **WHEN** an episode increment on a large list takes an entry into Completed and the score prompt opens
- **THEN** the prompt appears immediately, with the same independence from the size of the list behind it

#### Scenario: Pages are not re-rendered by an overlay opening
- **WHEN** an overlay opens or closes over any page that can open it
- **THEN** that page's own rendering is not re-run, because none of its data or state changed

### Requirement: An entry edit updates only the row it belongs to
The system SHALL confine the visible update from editing one entry in a list to that entry's own row. Changing a row's score, editing its episode count in place, or incrementing it — including the in-flight disabled state while the save is running — SHALL redraw that row alone, leaving every other row untouched, so a list stays responsive while entries are edited one after another.

#### Scenario: Changing one row's score
- **WHEN** I change the score on a my-list row
- **THEN** only that row updates, and the rest of the list is not redrawn

#### Scenario: In-flight state is row-scoped
- **WHEN** a row's edit is in flight and its controls are disabled
- **THEN** only that row reflects the pending state, and no other row is redrawn

#### Scenario: Repeated edits stay responsive
- **WHEN** I edit several entries in succession on a large list
- **THEN** each edit responds immediately, with no slowdown from the number of entries on the page
