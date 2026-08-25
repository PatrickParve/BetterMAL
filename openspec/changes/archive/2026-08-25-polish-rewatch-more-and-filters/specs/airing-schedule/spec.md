## MODIFIED Requirements

### Requirement: Week navigation
The system SHALL allow navigating to other weeks via a `< current >` control near the "Schedule" title bar.

Alongside it the system SHALL provide a month selector and a year selector that jump directly to any week of any year, so that no week is more than two interactions away regardless of how far it is from the current one. The selectors SHALL replace the single date input, which could only be stepped one month at a time.

The month selector SHALL offer the twelve months labelled in the viewer's locale. The year selector SHALL offer years from the current year plus one down to 1960, listed most recent first.

Choosing a month or a year SHALL navigate to the week containing the same day-of-month in the newly chosen month and year, clamped to the last day of that month when the day-of-month does not exist there. Changing only the year SHALL therefore land on the same point in the year.

Both selectors SHALL display the month and year of the currently displayed week, and SHALL stay in sync when the `< current >` buttons move the view across a month or year boundary.

Changing the displayed week — by either the `< current >` buttons or the selectors — SHALL **replace** the current browser history entry rather than adding a new one. Browsing several weeks SHALL therefore leave the history stack the same depth it was on arrival, and a single Back SHALL leave the Airing page for wherever the user came from, however many weeks were stepped through. The displayed week SHALL remain in the page's URL, so the view is still shareable and bookmarkable and still restored when returning to the page.

Changing the displayed week SHALL NOT move the page's scroll position: the schedule SHALL stay where the user had scrolled it rather than jumping to the top, since only the contents of the same view have changed.

The selected week SHALL survive back-navigation from an anime detail page, as it does today.

#### Scenario: Navigating weeks
- **WHEN** I navigate to a different week
- **THEN** the view updates to show that week's airing slots

#### Scenario: Back leaves the Airing page, not the week
- **WHEN** I open the Airing page from the navbar, step forward five weeks, and press the browser's Back button once
- **THEN** I leave the Airing page for the page I was on before it, rather than returning to the previous week

#### Scenario: The week is still in the URL
- **WHEN** I have navigated to a week several steps from the current one
- **THEN** the page's URL names that week, and opening that URL shows that same week

#### Scenario: Scroll position survives a week change
- **WHEN** I scroll down the schedule and then press the next-week button
- **THEN** the new week is shown at the scroll position I was at, rather than at the top of the page

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
