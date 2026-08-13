## MODIFIED Requirements

### Requirement: Weekly airing view of my list in local time
The system SHALL show a week view of time slots for anime in my list, built from stored per-episode airing rows: one slot per stored episode whose air instant falls within the displayed week, labeled by title and episode number, with the instant converted to local time — except that multiple episodes of the same anime airing on the same local day MAY be represented by a single merged slot, per the grouping rule defined separately. Anime that have finished airing SHALL NOT appear in weeks after their last stored episode, even if stale broadcast data exists.

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

### Requirement: Seven day-column layout
The system SHALL lay the week out as seven day-columns left to right, each with a day-label header, stacking each day's time slots (time + small image + title + episode number) beneath its header. The number of slots per day SHALL follow how many of my-list anime air that local day, with no fixed cap, counting a merged slot (see the grouping requirement) as one slot.

Each day-column header SHALL show the day of the month together with the month number, formatted as `<day>.<month>` (e.g. `8.8` for 8 August, `9.8` for 9 August), alongside the weekday name, taken from that column's own local date. Neither number SHALL be zero-padded, and the year SHALL NOT be shown in the column header.

Every slot box SHALL render at the same height regardless of its content. The title area SHALL always reserve two lines of text, clamping longer titles with a trailing ellipsis, and the episode-number row SHALL always be reserved — showing a placeholder when the slot's episode number is unknown, or an episode range (e.g. `Ep 1-8`) when the slot represents more than one merged episode — so that no slot box is shorter or taller than another.

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

#### Scenario: Slot representing a merged range
- **WHEN** a slot represents more than one episode of the same anime merged together
- **THEN** its episode row shows the range as `Ep <first>-<last>` instead of a single episode number, and the box matches the height of every other slot

## ADDED Requirements

### Requirement: Grouping of same-anime episodes on the same local day
Within a local day, the system SHALL represent multiple episodes of the same anime as a single merged slot when they form a contiguous run: episodes of that anime, ordered by air instant, with no other anime's episode airing strictly between the earliest and the latest of them. Episodes of the same anime separated by another anime's episode airing strictly between them SHALL NOT merge across that interruption, and SHALL instead form separate slots.

A merged slot's displayed time SHALL be its earliest episode's local time, and it SHALL sort within the day at that time.

When another anime's episode airs at the exact same instant as what would be a merged slot's earliest episode, that other anime's slot SHALL sort immediately before the merged slot rather than splitting it. When another anime's episode airs at the exact same instant as what would be a merged slot's latest episode, and that instant differs from the earliest one, that other anime's slot SHALL sort after the merged slot rather than splitting it.

An episode with no known episode number SHALL NOT merge with any other episode. It SHALL always render as its own slot with the existing unknown-episode placeholder, and it SHALL NOT prevent other episodes of the same anime from merging around it.

Grouping SHALL NOT cross a local-day boundary, and SHALL NOT merge episodes of different anime under any circumstance.

#### Scenario: Simultaneous episodes merge
- **WHEN** multiple episodes of the same anime air at the exact same instant on the same local day
- **THEN** they are shown as one slot labeled with the episode range

#### Scenario: Non-simultaneous same-day episodes merge
- **WHEN** two episodes of the same anime air at different times on the same local day and no other anime's episode airs between those two times
- **THEN** they are shown as one slot spanning both episode numbers, timed at the earlier episode

#### Scenario: Interruption splits the run
- **WHEN** a different anime's episode airs strictly between two episodes of the same anime on the same local day
- **THEN** the episodes on either side of the interruption are shown as separate slots rather than one merged slot

#### Scenario: Tie at the earliest episode pushes the other anime before
- **WHEN** a different anime's episode airs at the exact same instant as what would be a merged slot's earliest episode
- **THEN** the other anime's slot is shown immediately before the merged slot, and the tie does not split the merge

#### Scenario: Tie at the latest episode pushes the other anime after
- **WHEN** a different anime's episode airs at the exact same instant as what would be a merged slot's latest episode
- **THEN** the other anime's slot is shown after the merged slot, and the tie does not split the merge

#### Scenario: Unknown episode number never merges
- **WHEN** an episode with no known episode number airs on the same local day as another episode of the same anime
- **THEN** it is shown as its own slot with the unknown-episode placeholder, and the other episodes of that anime merge with each other as if it were not there

#### Scenario: Grouping does not cross anime
- **WHEN** two different anime each air an episode on the same local day
- **THEN** their slots are never merged together, regardless of timing

#### Scenario: Grouping does not cross a local day
- **WHEN** an anime airs an episode late on one local day and another episode early the next local day
- **THEN** the two episodes remain separate slots in their own day-columns rather than merging
