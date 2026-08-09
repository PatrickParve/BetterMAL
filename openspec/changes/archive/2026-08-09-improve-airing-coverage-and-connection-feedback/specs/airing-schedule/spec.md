## MODIFIED Requirements

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
