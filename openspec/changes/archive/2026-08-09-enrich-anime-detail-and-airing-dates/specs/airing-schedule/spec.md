## MODIFIED Requirements

### Requirement: Seven day-column layout
The system SHALL lay the week out as seven day-columns left to right, each with a day-label header, stacking each day's time slots (time + small image + title + episode number) beneath its header. The number of slots per day SHALL follow how many of my-list anime air that local day, with no fixed cap.

Each day-column header SHALL show the day of the month together with the month number, formatted as `<day>.<month>` (e.g. `8.8` for 8 August, `9.8` for 9 August), alongside the weekday name, taken from that column's own local date. Neither number SHALL be zero-padded, and the year SHALL NOT be shown in the column header.

Every slot box SHALL render at the same height regardless of its content. The title area SHALL always reserve two lines of text, clamping longer titles with a trailing ellipsis, and the episode-number row SHALL always be reserved — showing a placeholder when the slot's episode number is unknown — so that no slot box is shorter or taller than another.

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
- **THEN** its box still reserves the episode row with a placeholder and matches the height of slots that do show an episode number
