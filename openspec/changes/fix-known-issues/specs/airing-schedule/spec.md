## MODIFIED Requirements

### Requirement: Weekly airing view of my list in local time
The system SHALL show a week view of time slots for anime in my list that are currently airing and have a known broadcast schedule, labeled by title and episode number, with times converted from JST broadcast data to local time. Anime that have finished airing SHALL NOT appear in the weekly view even if stale broadcast data exists.

#### Scenario: Displaying the weekly schedule
- **WHEN** the airing page loads
- **THEN** it shows my-list anime that are currently airing in weekly time slots, each labeled with title and episode number in local time

#### Scenario: Finished anime excluded
- **WHEN** a my-list anime has finished airing
- **THEN** it does not appear in the weekly airing view
