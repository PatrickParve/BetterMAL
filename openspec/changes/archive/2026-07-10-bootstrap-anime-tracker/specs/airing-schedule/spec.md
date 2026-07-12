## ADDED Requirements

### Requirement: Weekly airing view of my list in local time
The system SHALL show a week view of time slots for anime in my list, labeled by title and episode number, with times converted from JST broadcast data to local time.

#### Scenario: Displaying the weekly schedule
- **WHEN** the airing page loads
- **THEN** it shows my-list anime in weekly time slots, each labeled with title and episode number in local time

### Requirement: Grouping by converted local day
The system SHALL group shows by their broadcast day converted to local time, not the raw JST day.

#### Scenario: Show crosses the day boundary on conversion
- **WHEN** an anime's JST broadcast day converts to a different local day
- **THEN** it appears under the converted local day in the weekly view

### Requirement: Week navigation
The system SHALL allow navigating to other weeks via a `< current >` control near the "Schedule" title bar.

#### Scenario: Navigating weeks
- **WHEN** I navigate to a different week
- **THEN** the view updates to show that week's airing slots

### Requirement: Seven day-column layout
The system SHALL lay the week out as seven day-columns left to right, each with a day-label header, stacking each day's time slots (time + small image + title + episode number) beneath its header. The number of slots per day SHALL follow how many of my-list anime air that local day, with no fixed cap.

#### Scenario: Days with differing slot counts
- **WHEN** the weekly view renders
- **THEN** each day-column shows one slot per my-list anime airing that local day, however many that is

#### Scenario: Empty day
- **WHEN** no my-list anime air on a given local day
- **THEN** that day-column is shown empty

### Requirement: Empty-week message
The system SHALL show a message in the middle of the view when nothing in my list airs during the displayed week.

#### Scenario: Nothing airing this week
- **WHEN** no my-list anime air during the displayed week
- **THEN** the view shows a centered message stating nothing is airing that week
