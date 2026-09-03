## ADDED Requirements

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
