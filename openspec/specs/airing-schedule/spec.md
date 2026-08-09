# airing-schedule Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: Weekly airing view of my list in local time
The system SHALL show a week view of time slots for anime in my list, built from stored per-episode airing rows: one slot per stored episode whose air instant falls within the displayed week, labeled by title and episode number, with the instant converted to local time. Anime that have finished airing SHALL NOT appear in weeks after their last stored episode, even if stale broadcast data exists.

The view SHALL NOT compute or estimate a slot or an episode number for a date that has no stored episode row. A displayed week for which no stored episode rows fall inside it SHALL render as an empty week rather than showing projected slots.

#### Scenario: Displaying the weekly schedule
- **WHEN** the airing page loads
- **THEN** it shows one slot per stored episode of my-list anime airing that week, each labeled with title and episode number in local time

#### Scenario: Finished anime excluded
- **WHEN** a my-list anime has finished airing
- **THEN** it does not appear in weeks after its last stored episode

#### Scenario: Past week has no stored data
- **WHEN** a past week is displayed and no stored episode rows fall inside it
- **THEN** the week renders empty rather than showing episodes derived from a broadcast cadence

#### Scenario: Past week renders the same on every load
- **WHEN** the same past week is displayed on two different days
- **THEN** it shows the same anime with the same episode numbers both times

#### Scenario: Break week within a run
- **WHEN** an anime took a one-week break mid-run and that week is displayed
- **THEN** the anime has no slot that week, and the following week's slot shows the episode number that actually aired then rather than one incremented per elapsed week

#### Scenario: Long-running series across adjacent weeks
- **WHEN** two adjacent past weeks are displayed for a long-running series
- **THEN** the episode numbers shown differ by the number of episodes that actually aired between them

### Requirement: Grouping by converted local day
The system SHALL group each slot by the local day its stored air instant converts to, not by the raw JST day.

#### Scenario: Show crosses the day boundary on conversion
- **WHEN** an episode's stored air instant converts to a different local day than its JST day
- **THEN** it appears under the converted local day in the weekly view

#### Scenario: Week bounds follow local days
- **WHEN** a week spanning a daylight-saving transition is displayed
- **THEN** it covers exactly seven local days, and each episode falls in the day-column matching its local air date

### Requirement: Week navigation
The system SHALL allow navigating to other weeks via a `< current >` control near the "Schedule" title bar.

Alongside it the system SHALL provide a month selector and a year selector that jump directly to any week of any year, so that no week is more than two interactions away regardless of how far it is from the current one. The selectors SHALL replace the single date input, which could only be stepped one month at a time.

The month selector SHALL offer the twelve months labelled in the viewer's locale. The year selector SHALL offer years from the current year plus one down to 1960, listed most recent first.

Choosing a month or a year SHALL navigate to the week containing the same day-of-month in the newly chosen month and year, clamped to the last day of that month when the day-of-month does not exist there. Changing only the year SHALL therefore land on the same point in the year.

Both selectors SHALL display the month and year of the currently displayed week, and SHALL stay in sync when the `< current >` buttons move the view across a month or year boundary.

The selected week SHALL survive back-navigation from an anime detail page, as it does today.

#### Scenario: Navigating weeks
- **WHEN** I navigate to a different week
- **THEN** the view updates to show that week's airing slots

#### Scenario: Jumping to a different year
- **WHEN** the view is showing a week in August 2026 and I select 2019 in the year selector
- **THEN** the view jumps to the week containing the same day of August 2019, in one interaction

#### Scenario: Jumping to a different month
- **WHEN** I select March in the month selector
- **THEN** the view jumps to the week containing that same day-of-month in March of the displayed year

#### Scenario: Day-of-month that does not exist in the target month
- **WHEN** the view is showing a week containing 31 January and I select February
- **THEN** the view jumps to the week containing the last day of that February rather than overflowing into March

#### Scenario: Selectors follow the week buttons
- **WHEN** I press the next-week button on the last week of December
- **THEN** the month selector changes to January and the year selector advances to the next year

#### Scenario: Selected week survives navigation
- **WHEN** I jump to a week, open an anime from it, and navigate back
- **THEN** the same week is still displayed, with the selectors showing its month and year

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

### Requirement: Empty-week message
The system SHALL show a message in the middle of the view when nothing in my list airs during the displayed week.

#### Scenario: Nothing airing this week
- **WHEN** no my-list anime air during the displayed week
- **THEN** the view shows a centered message stating nothing is airing that week

