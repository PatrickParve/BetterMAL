## ADDED Requirements

### Requirement: The shared multi-select filter is unavailable when it has nothing to choose between

The shared multi-select filter SHALL be drawn as unavailable — it SHALL NOT open its panel, and SHALL be drawn in the app's disabled treatment — whenever it offers **one option or none** and is not itself narrowing the view. With one option, every selection it could express is the view already on screen; with none, there is nothing to express.

An unavailable trigger SHALL state the fact rather than the state: with one option it SHALL read as that option's own display label (for example "Type: TV"), and with no options it SHALL read **All**. It SHALL keep its place in the cluster and its reserved width, so a filter that becomes unavailable never disappears and never moves the controls beside it.

A filter that is itself narrowing the view — on anything other than **All** — SHALL remain available however few options it offers, so its restriction can always be lifted from the control that applies it.

The **All** and **None** shortcuts SHALL be reachable only through the panel, so an unavailable filter offers neither; **None** on a filter with a single option would empty the view it is showing, and **All** is where it already is.

#### Scenario: One option is not a choice
- **WHEN** a Type filter set to **All** offers only TV
- **THEN** it reads "Type: TV", is drawn as unavailable, and does not open when pressed

#### Scenario: No options is not a choice either
- **WHEN** a Type filter set to **All** has no options to offer because nothing is listed
- **THEN** it reads "Type: All" and is drawn as unavailable

#### Scenario: A narrowing filter stays available
- **WHEN** a Type filter is on Movie and the rest of the view narrows so that Movie is the only option it can offer
- **THEN** the filter stays available, opens, and can be returned to **All**

#### Scenario: Becoming unavailable does not move the cluster
- **WHEN** a filter becomes unavailable
- **THEN** it keeps its place in the cluster and its width, and no control beside it moves

## MODIFIED Requirements

### Requirement: The shared multi-select filter's trigger holds one width
The shared multi-select filter's trigger SHALL keep one width for the life of the view it sits in, whatever is selected in it **and whatever options it currently offers**. Changing the selection — including between **All**, **None**, a single option and a count — SHALL NOT change the trigger's width or height, and neither SHALL a change to the options themselves: options arriving as the view's data loads, or being narrowed by another control. The controls beside it in the filter cluster SHALL NOT move as the filter is used.

The width SHALL be reserved from the full set of values the filter can ever offer — every media type for a type filter, every airing status for an airing filter — rather than the values it happens to be offering, so the trigger is at its final width on the first painted frame, before any data has loaded. It SHALL be that of the widest label the control could show for that set, so no label is ever clipped or wrapped and the reserved width follows real option labels rather than a fixed guess. A value outside that set — a type the API has added that the app has not yet named — SHALL still be reserved for while it is on offer, so no label is clipped. The width SHALL follow the app's fluid root font size as the cluster's shared height already does.

The trigger's label SHALL be aligned to the leading edge of that reserved width rather than centred within it, so the label begins in the same place whatever it says.

Reserving that width SHALL NOT add anything to the control's accessible name: only the label in force SHALL be announced.

#### Scenario: The cluster does not move while filtering
- **WHEN** I open the Type filter and tick and untick options
- **THEN** the trigger stays exactly the same size and the sort control beside it does not shift sideways

#### Scenario: Switching between All and None does not resize
- **WHEN** I press **All** and then **None**
- **THEN** the trigger's width is unchanged

#### Scenario: The trigger is its final size before the data loads
- **WHEN** I load a page whose filter has no options yet because its entries are still arriving
- **THEN** the trigger is already at its final width and does not grow when the options appear

#### Scenario: Losing options does not resize the trigger
- **WHEN** another control narrows the view so the filter offers fewer options
- **THEN** the trigger's width is unchanged

#### Scenario: The longest label still fits
- **WHEN** the single selected option carries the longest label the filter offers
- **THEN** that label is shown in full, neither clipped nor wrapped

#### Scenario: Labels start in the same place
- **WHEN** the trigger moves between "Type: All" and "Type: TV special"
- **THEN** both labels begin at the same position inside the trigger

#### Scenario: Width follows the font size
- **WHEN** the viewport width changes enough to move the app's fluid root font size
- **THEN** the trigger is still sized to its own widest label and still matches the height of the controls beside it

#### Scenario: Reserved labels are not announced
- **WHEN** a screen reader reads the trigger
- **THEN** it announces only the state in force, not the other labels the control has reserved room for

### Requirement: The shared multi-select filter's label names its state
The shared multi-select filter's trigger SHALL report its state as its own label, prefixed by the filter's name: **All** when no restriction is in force, **None** when nothing is selected, the option's own display label when exactly one option is selected, and a count of how many are selected when more than one but not all are.

Pressing **All** SHALL therefore make the trigger read **All**, not a count of the options it holds.

Where the filter is unavailable, its label SHALL instead name what is true of the view, as "The shared multi-select filter is unavailable when it has nothing to choose between" defines.

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

#### Scenario: One option on offer reads as that option
- **WHEN** the filter is on **All** and TV is the only type it can offer
- **THEN** the trigger reads "Type: TV" rather than "Type: All"
