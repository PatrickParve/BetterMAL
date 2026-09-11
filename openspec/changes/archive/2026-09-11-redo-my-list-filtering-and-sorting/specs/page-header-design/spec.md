## MODIFIED Requirements

### Requirement: Filter and sort controls in one cluster share one height
The system SHALL render every control that sits side by side in a page's filter/sort cluster at the same height, so the cluster reads as one row of controls rather than a ragged line. This SHALL hold regardless of the control's underlying element — a native `<select>`, a popover trigger button, a checkbox label, a text input, a toggle button, and a segmented two-option choice in the same cluster SHALL all be the same height — and SHALL hold at every font size the app renders, since the root font size is fluid.

The shared height SHALL come from one shared definition rather than from per-control padding values that each happen to land near the same number, so a control added later inherits the cluster's height instead of re-introducing the mismatch. Equal padding is not sufficient on its own: a `<select>`'s inner line box is normalized by the browser while a button or label inherits the root's computed line-height, so identical padding yields different rendered heights.

This SHALL cover the Seasonal Anime page's season, year, and sort selects together with its Type filter and its "In my list" checkbox; the Search results page's Type filter and sort select; and both of My List's control groups — in its Filter group, the find-in-list field, the Type and Airing filters (which use the same shared control as the other two pages), the score select, and the Started checkbox; in its Sort group, the sort and tiebreaker selects, the direction button, and the two-option grouping choice. Round, icon-only navigation arrows (the season and week steppers) are their own control family and are not part of this requirement.

Aligning heights SHALL NOT change any control's behaviour, its accessible name, its keyboard handling, or where its popover panel is anchored.

#### Scenario: Season page filter row is flush
- **WHEN** I open the Seasonal Anime page
- **THEN** the season, year, and sort selects, the Type filter button, and the "In my list" checkbox are all exactly the same height, with their tops and bottoms aligned

#### Scenario: Search page filter row is flush
- **WHEN** I open the Search results page with results showing
- **THEN** its Type filter button and sort select are exactly the same height

#### Scenario: My List's control groups are flush
- **WHEN** I open My List
- **THEN** within its Filter group the find-in-list field, the Type and Airing filter buttons, the score select and the Started checkbox are all the same height, and within its Sort group the sort and tiebreaker selects, the direction button and the grouping choice are that same height

#### Scenario: Height holds as the font scales
- **WHEN** the viewport width changes enough to move the app's fluid root font size
- **THEN** the controls in each cluster still match one another's height

#### Scenario: Filters keep working
- **WHEN** I open the Type filter popover after the heights are aligned
- **THEN** it opens beneath its button as before, and selecting, clearing, and closing it behave exactly as they did

## ADDED Requirements

### Requirement: The shared multi-select filter's panel reads as a layer attached to its trigger
When the shared multi-select filter is opened, its panel SHALL appear directly beneath its trigger as an opaque, elevated surface drawn above every other part of the page's content — the rows of a list, the other controls in its cluster, and any control on a line below it — and beneath the app's overlays and notices. Opening or closing the panel SHALL NOT move, resize, or reflow anything else on the page. While the panel is open the trigger SHALL show its open state, so the panel reads as belonging to that trigger.

The panel SHALL stay within the viewport horizontally. It SHALL align its leading edge with the trigger's leading edge, unless that would carry it past the viewport's trailing edge, in which case it SHALL align its trailing edge with the trigger's trailing edge instead; and it SHALL never be wider than the viewport less a small margin on each side.

This SHALL hold on every page that uses the shared control.

#### Scenario: The panel covers what is beneath it as a layer
- **WHEN** I open the Type filter on My List, with the Sort group on the row beneath it
- **THEN** the panel is drawn over the Sort group as an opaque, elevated surface, and no control or row on the page moves

#### Scenario: A trigger near the trailing edge
- **WHEN** a multi-select filter's trigger sits near the right edge of a narrow viewport and I open it
- **THEN** the panel opens aligned to the trigger's right edge and is fully visible, with no horizontal scrolling

#### Scenario: Opening and closing reflow nothing
- **WHEN** I open and then close a multi-select filter's panel
- **THEN** no other element on the page changes position or size

#### Scenario: Notices stay above the panel
- **WHEN** an app notice appears while a multi-select filter's panel is open
- **THEN** the notice is drawn above the panel

### Requirement: The shared multi-select filter's trigger shows when it is narrowing
The shared multi-select filter's trigger SHALL show that it opens a list of choices, with a dropdown indicator in keeping with the app's selects. It SHALL be drawn with the app's active accent treatment whenever the filter is on anything other than **All** — one or more options selected, or **None** — and drawn neutral on **All**. Neither the indicator nor the accent SHALL change the trigger's width or height, so "The shared multi-select filter's trigger holds one width" continues to hold, and the accent SHALL NOT be the only signal of the state: the trigger's label continues to name it.

This SHALL hold on every page that uses the shared control.

#### Scenario: On All the trigger is neutral
- **WHEN** a multi-select filter is on **All**
- **THEN** its trigger is drawn neutral, like the selects beside it, with its dropdown indicator

#### Scenario: A selection is marked
- **WHEN** I select only Movie in a Type filter
- **THEN** its trigger is drawn with the active accent and reads "Type: Movie"

#### Scenario: None is marked
- **WHEN** I press **None** in a Type filter
- **THEN** its trigger is drawn with the active accent and reads "Type: None"

#### Scenario: Marking keeps the trigger's size
- **WHEN** a filter moves between **All** and a selection
- **THEN** its trigger's width and height are unchanged
