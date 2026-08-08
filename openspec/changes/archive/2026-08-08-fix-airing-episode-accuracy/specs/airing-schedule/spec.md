## MODIFIED Requirements

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
