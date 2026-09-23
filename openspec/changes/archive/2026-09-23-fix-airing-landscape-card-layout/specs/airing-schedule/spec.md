## MODIFIED Requirements

### Requirement: Seven day-column layout
The system SHALL lay the week out as seven day-columns left to right, each with a day-label header, stacking each day's time slots beneath its header. The number of slots per day SHALL follow how many of my-list anime air that local day, with no fixed cap, counting a merged slot (see the grouping requirement) as one slot.

Each day-column header SHALL show the day of the month together with the month number, formatted as `<day>.<month>` (e.g. `8.8` for 8 August, `9.8` for 9 August), alongside the weekday name, taken from that column's own local date. Neither number SHALL be zero-padded, and the year SHALL NOT be shown in the column header.

Each slot SHALL be drawn as a card: a **header strip** across the card's top with the slot's local time at its left and its episode label at its right, a **picture band** beneath the strip across the card's full width, holding the anime's picture under `artwork-presentation`'s banner-box rule, and the title beneath the band. The time and the episode label SHALL NOT be drawn over the picture. The title SHALL span the card's full width beneath the band, and its width SHALL NOT depend on the shape of the slot's picture. Nothing SHALL sit beside the picture and share its width.

The title and the episode label SHALL always stay inside the slot box. An episode label too long for the strip SHALL be shortened with a trailing ellipsis, and SHALL NOT run past the slot's edge.

Every slot box SHALL render at the same height regardless of its content, including the shape of its picture or the lack of one. The title area SHALL always reserve two lines of text, clamping longer titles with a trailing ellipsis. The episode label SHALL always be shown, as a placeholder when the slot's episode number is unknown, or as an episode range (e.g. `Ep 1-8`) when the slot represents more than one merged episode, so that no slot box is shorter or taller than another.

The slot's accessible text SHALL read in the order time, title, episode label, whatever order the three are laid out on screen.

Where the page is too narrow for seven columns side by side and the days stack one beneath another, each day's slots SHALL tile across that day's width as cards of one shared size, rather than each slot stretching across the whole width. Every slot in the displayed week SHALL still be the same size.

#### Scenario: Days with differing slot counts
- **WHEN** the weekly view renders
- **THEN** each day-column shows one slot per my-list anime airing that local day, however many that is

#### Scenario: Empty day
- **WHEN** no my-list anime air on a given local day
- **THEN** that day-column is shown empty

#### Scenario: Day header shows day and month
- **WHEN** the week containing 8 August 2026 is displayed
- **THEN** that column's header shows the weekday name together with `8.8`, and the following column shows `9.8`

#### Scenario: Week crossing a month boundary
- **WHEN** a displayed week spans 30 September to 6 October
- **THEN** the first columns read `30.9` and `1.10` respectively, so the month change is visible in the header

#### Scenario: Day and month numbers are not zero-padded
- **WHEN** a column's local date is 3 May
- **THEN** its header reads `3.5`, not `03.05`

#### Scenario: Short and long titles occupy the same box
- **WHEN** one slot's title fits on a single line and another slot's title needs more than two lines
- **THEN** both slot boxes are the same height, and the longer title is clamped to two lines with a trailing ellipsis

#### Scenario: Slot with an unknown episode number
- **WHEN** a slot has no known episode number
- **THEN** its strip still shows the episode label as a placeholder, and its box matches the height of slots that do show an episode number

#### Scenario: Slot representing a merged range
- **WHEN** a slot represents more than one episode of the same anime merged together
- **THEN** its episode label shows the range as `Ep <first>-<last>` instead of a single episode number, and the box matches the height of every other slot

#### Scenario: A landscape picture leaves the title its full width
- **WHEN** a slot's picture is a 16:9 key visual and the page is 1280 pixels wide
- **THEN** the whole picture is shown in the band, and the title beneath it spans the card's full width on up to two lines, exactly as wide as the title of a slot holding a poster

#### Scenario: Time and episode never cover the picture
- **WHEN** a slot holds a poster, a landscape picture or a square picture
- **THEN** the time and the episode label sit in the header strip above the picture, and no part of the picture is covered by either

#### Scenario: Picture shape does not change a slot's size
- **WHEN** one day holds a slot with a poster, a slot with a landscape picture, a slot with a square picture and a slot with no picture
- **THEN** all four slot boxes are the same width and height, and their titles begin at the same distance below each box's top

#### Scenario: A long episode range stays inside the box
- **WHEN** a slot with a long merged range, such as `Ep 1054-1060`, sits in the narrowest day-column the seven-column layout allows
- **THEN** the episode label ends inside the slot's right edge, shortened with an ellipsis if it does not fit, and nothing of it is drawn outside the box

#### Scenario: Time, title and episode are announced in order
- **WHEN** a screen reader reads a slot's link
- **THEN** it reads the local time, then the title, then the episode label

#### Scenario: Narrow window tiles the cards
- **WHEN** the page is too narrow for seven columns and a day holds several slots
- **THEN** that day's slots sit side by side in equal cards that wrap onto further rows as needed, and no single slot stretches across the day's full width
