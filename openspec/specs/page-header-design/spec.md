# page-header-design Specification

## Purpose
TBD - created by archiving change polish-headers-filters-and-top-series. Update Purpose after archive.

## Requirements

### Requirement: Every page provides a dedicated header block for its title
The system SHALL render each of the My List, Top Anime, Seasonal Anime, Schedule, Search results, Settings, Profile, and anime detail pages' `<h1>` inside a page-specific header wrapper that owns the title's spacing, rather than leaving the `<h1>` as a bare child of the page's outer container. The wrapper SHALL zero the `h1`'s own top/bottom margin, so spacing above and below the title is set exactly once — by the wrapper's own layout — instead of the global `h1` margin stacking on top of the wrapper's own gap or padding.

The anime detail page SHALL place that wrapper above the page's body block rather than inside it, so its title begins at the same left edge and the same height on the page as every other page's title.

The Series page is unaffected by this requirement: its title continues to render beside the poster inside the page's hero block, per the `series-page` capability, rather than in a dedicated header wrapper.

#### Scenario: Settings and Profile gain a header wrapper
- **WHEN** the Settings or Profile page loads
- **THEN** its title renders inside a page-specific header wrapper whose `h1` has zero margin, rather than as a bare `h1` carrying the raw global heading margin stacked on the page's own layout gap

#### Scenario: Existing header-row pages keep their wrapper
- **WHEN** the My List, Top Anime, Seasonal Anime, Schedule, or Search results page loads
- **THEN** its title still renders inside that page's header wrapper with the `h1` margin zeroed

#### Scenario: Anime detail gains a header wrapper
- **WHEN** the anime detail page loads
- **THEN** its title renders inside a page-specific header wrapper above the page's body, with the `h1` margin zeroed

### Requirement: Page-appropriate title treatment distinct from the raw default heading
The system SHALL give each of the eight pages covered by the previous requirement a page-appropriate visual treatment beyond the raw global `h1` default, chosen to fit that page's header content. Pages whose header row also carries controls (My List, Top Anime, Seasonal Anime, Schedule, Search results, anime detail) SHALL present the title sized and weighted to share that row cleanly with its controls, rather than at the full-bleed size the raw global heading uses for a bare headline. Pages with no competing header-row content (Settings, Profile) SHALL present the title as a distinct standalone header block with more presence than a plain, unstyled line of text. Different pages MAY use different treatments from one another; the treatments are not required to match.

Every one of these titles SHALL nonetheless share the same typographic vocabulary — an explicit font size drawn from the set the other pages already use, an explicit weight, a zeroed margin, and normal letter-spacing — so that no page's title is left carrying the raw global heading's size, weight, or letter-spacing by default. The anime detail title in particular SHALL be brought onto that shared vocabulary rather than keeping its own ad-hoc values.

#### Scenario: Title shares a row with controls without dominating it
- **WHEN** a page whose header row also carries controls (for example Search's result count and sort control, or Top Anime's pagination) renders its title
- **THEN** the title and the controls sit in the header row together without the title visually overwhelming or crowding them

#### Scenario: Standalone pages present a distinct header block
- **WHEN** the Settings or Profile page renders
- **THEN** its title is presented as a distinct header block rather than a plain, unstyled line of text

#### Scenario: The anime detail title shares its row with the related links
- **WHEN** the anime detail page renders for an anime with related entries
- **THEN** its title and the related-entry links sit in the header row together, with the title sized to share the row rather than dominating it

#### Scenario: No page's title is left on the raw default
- **WHEN** I compare the titles of the eight pages this requirement covers
- **THEN** each carries an explicit size, weight, zeroed margin, and normal letter-spacing, with none falling back to the global heading defaults

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

### Requirement: The shared multi-select filter distinguishes All from None
The multi-select filter control shared by the Search results page, the Season page, the Year page and My List (its Type filter and its Airing filter) SHALL offer two shortcuts above its options — **All** and **None** — that put the control into two different states.

**All** SHALL apply no restriction: every value passes, exactly as an untouched filter does. **None** SHALL clear every option so that no value passes, so the view the filter narrows reports that nothing matches rather than falling back to showing everything. Ticking an option while **None** is in force SHALL narrow to that option alone; ticking every option SHALL be **All**.

A control in which every offered option is selected SHALL be indistinguishable from one on **All**, however it was reached — the shortcut, a fresh visit, or ticking the last unticked option by hand — so the two SHALL NOT be separately expressible in the page's URL or restorable state. Unticking an option while **All** is in force SHALL leave every other option selected.

#### Scenario: All shows everything
- **WHEN** I press **All** in a type filter
- **THEN** entries of every type are shown, exactly as they are on a fresh visit

#### Scenario: None shows nothing
- **WHEN** I press **None** in a type filter
- **THEN** no entries are shown and the view says nothing matches the current filters, rather than showing every entry

#### Scenario: All and None are not the same
- **WHEN** I press **None** and then press **All**
- **THEN** the view changes from showing nothing to showing everything, rather than staying the same

#### Scenario: Building a selection up from None
- **WHEN** I press **None** and then tick Movie
- **THEN** only movie entries are shown

#### Scenario: Ticking the last option is All
- **WHEN** I press **None** and then tick every option one by one
- **THEN** the control is on **All** and no restriction is applied

#### Scenario: Unticking from All narrows by one
- **WHEN** the control is on **All** and I untick Movie
- **THEN** every other offered type is selected and only movies are excluded

### Requirement: The shared multi-select filter's label names its state
The shared multi-select filter's trigger SHALL report its state as its own label, prefixed by the filter's name: **All** when no restriction is in force, **None** when nothing is selected, the option's own display label when exactly one option is selected, and a count of how many are selected when more than one but not all are.

Pressing **All** SHALL therefore make the trigger read **All**, not a count of the options it holds.

#### Scenario: All reads as All
- **WHEN** I press **All** in the Type filter
- **THEN** its trigger reads "Type: All" rather than naming a number of types

#### Scenario: None reads as None
- **WHEN** I press **None** in the Type filter
- **THEN** its trigger reads "Type: None"

#### Scenario: One selection reads as its label
- **WHEN** only TV special is selected
- **THEN** the trigger reads "Type: TV special", using the display label rather than the raw value

#### Scenario: A partial selection reads as a count
- **WHEN** three of five offered types are selected
- **THEN** the trigger reads "Type: 3 selected"

#### Scenario: Every option ticked reads as All
- **WHEN** I tick every offered type by hand
- **THEN** the trigger reads "Type: All" rather than a count

### Requirement: The shared multi-select filter's trigger holds one width
The shared multi-select filter's trigger SHALL keep one width for as long as it offers the same options, whatever is selected in it. Changing the selection — including between **All**, **None**, a single option and a count — SHALL NOT change the trigger's width or height, so the controls beside it in the filter cluster SHALL NOT move as the filter is used.

The width SHALL be that of the widest label the control could show for the options it currently offers, so no label is ever clipped or wrapped and the reserved width follows the real option labels rather than a fixed guess. It SHALL follow the app's fluid root font size as the cluster's shared height already does.

Reserving that width SHALL NOT add anything to the control's accessible name: only the label in force SHALL be announced.

#### Scenario: The cluster does not move while filtering
- **WHEN** I open the Type filter and tick and untick options
- **THEN** the trigger stays exactly the same size and the sort control beside it does not shift sideways

#### Scenario: Switching between All and None does not resize
- **WHEN** I press **All** and then **None**
- **THEN** the trigger's width is unchanged

#### Scenario: The longest label still fits
- **WHEN** the single selected option carries the longest label the filter offers
- **THEN** that label is shown in full, neither clipped nor wrapped

#### Scenario: Width follows the font size
- **WHEN** the viewport width changes enough to move the app's fluid root font size
- **THEN** the trigger is still sized to its own widest label and still matches the height of the controls beside it

#### Scenario: Reserved labels are not announced
- **WHEN** a screen reader reads the trigger
- **THEN** it announces only the state in force, not the other labels the control has reserved room for

### Requirement: The shared multi-select filter's shortcuts read as buttons
**All** and **None** SHALL be presented as bordered buttons in the same small-control family as the app's other compact buttons — a border, a rounded corner, and a background of their own — rather than as bare text that only reveals itself on hover. The two SHALL sit side by side on one row above the options, sharing a width so they read as a pair, and SHALL keep the divider that separates them from the option list.

Neither shortcut SHALL carry a persistent selected state of its own; the trigger's label is where the control's state is read.

#### Scenario: The shortcuts look like controls at rest
- **WHEN** I open a type filter and look at **All** and **None** without hovering either
- **THEN** each is drawn as a bordered button rather than as plain text

#### Scenario: The pair is even
- **WHEN** the shortcuts are shown together
- **THEN** they are the same width as one another and sit on one row above the options

#### Scenario: Hover reads as a button
- **WHEN** I hover **All**
- **THEN** it highlights the way the app's other small buttons do, rather than underlining
