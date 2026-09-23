## ADDED Requirements

### Requirement: The schedule covers 1917 through the end of next year
The airing schedule SHALL cover the weeks from **1 January 1917** through **31 December of the year after the current one**, inclusive. The floor is the same earliest year the Season, Year and Recap pages use. 1 January 1917 is a Monday, so the first week of the range starts on that date. The ceiling leaves at least one full calendar year beyond the current one, which covers the furthest ahead a stored episode is scheduled. It is also where the year selector already stops, so years remain the unit of every bounded page.

A week SHALL be addressable when the date in its `?week=` parameter is a real calendar date between those two dates, inclusive. The week shown is still the Monday-to-Sunday week containing that date, so the final week of the range may run a few days into the following year.

**Addressing a week by URL.** A `?week=` value that is not a real calendar date, or names a date outside the range, SHALL be replaced with today's date. The replacement SHALL take effect before the schedule is requested: no schedule request SHALL be made for the rejected week. The replacement SHALL substitute the current entry in the browser's history rather than adding one, and SHALL preserve every other parameter in the query string. The system SHALL NOT show a message, an error state or any other notice about the replacement. An absent `?week=` SHALL keep resolving to today's week without the URL being rewritten, as today.

A week addressed in the URL that **is** addressable SHALL be shown as asked. That includes a past week with no stored episodes, which renders as an empty week under "Empty-week message".

#### Scenario: A far-future week by URL is replaced
- **WHEN** I open `/airing?week=2035-03-10`
- **THEN** today's week is shown, the URL is replaced with today's date, no message is shown, and no schedule is requested for March 2035

#### Scenario: A week before 1917 is replaced
- **WHEN** I open `/airing?week=1916-12-31`
- **THEN** today's week is shown with the URL replaced and nothing requested for 1916

#### Scenario: A date that does not exist is replaced
- **WHEN** I open `/airing?week=2026-02-31` or `/airing?week=0000-01-01`
- **THEN** today's week is shown with the URL replaced

#### Scenario: The last day of the range is shown
- **WHEN** I open a link to 31 December of the year after the current one
- **THEN** the week containing that date is shown as asked

#### Scenario: The first week of the range is shown
- **WHEN** I open `/airing?week=1917-01-03`
- **THEN** the week of 1 to 7 January 1917 is shown as asked, empty

#### Scenario: A replacement does not leave a step back into the bad URL
- **WHEN** a week I addressed is replaced and I press the browser Back button
- **THEN** I go back to wherever I came from, not to the rejected week

### Requirement: Schedule requests outside the range are refused
`GET /api/airing` SHALL refuse a `week` outside 1 January 1917 through 31 December of the year after the current one. The response SHALL be `400`, in the `{ error }` shape the other endpoints use. It SHALL be sent before any airing data or list entry is read. A refused week SHALL NOT be moved to the nearest valid one, and SHALL NOT be served as an empty week.

The current year SHALL be taken from the app's local date **one day ahead**. A browser whose clock has already reached the new year is then never refused a week its own controls offer. The range SHALL be worked out again for every request and SHALL NOT be stored. A request with no `week` SHALL still default to today's week and SHALL never be refused.

#### Scenario: A week past the range is refused
- **WHEN** `GET /api/airing?week=<1 January, two years after the current year>` is requested
- **THEN** the response is `400` and no airing data is read

#### Scenario: A week before 1917 is refused
- **WHEN** `GET /api/airing?week=1916-12-31` is requested
- **THEN** the response is `400`

#### Scenario: The top of the calendar is refused rather than failing
- **WHEN** `GET /api/airing?week=9999-12-31` is requested
- **THEN** the response is `400`, not a server error

#### Scenario: Both ends of the range are accepted
- **WHEN** `GET /api/airing?week=1917-01-01` or `GET /api/airing?week=<31 December of the year after the current one>` is requested
- **THEN** that week's schedule is returned as usual

#### Scenario: No week is always accepted
- **WHEN** `GET /api/airing` is requested with no `week`
- **THEN** today's week is returned as usual

## MODIFIED Requirements

### Requirement: Week navigation
The system SHALL allow navigating to other weeks via a `< current >` control near the "Schedule" title bar.

Alongside it the system SHALL provide a month selector and a year selector that jump directly to any week of any year in the schedule's range, so that no week is more than two interactions away regardless of how far it is from the current one. The selectors SHALL replace the single date input, which could only be stepped one month at a time.

The month selector SHALL offer the twelve months labelled in the viewer's locale. The year selector SHALL offer the years of the range defined by "The schedule covers 1917 through the end of next year": from the current year plus one down to 1917, listed most recent first. It SHALL be built from those two ends alone and SHALL NOT be sized by the week in the URL.

Choosing a month or a year SHALL navigate to the week containing the same day-of-month in the newly chosen month and year, clamped to the last day of that month when the day-of-month does not exist there. Changing only the year SHALL therefore land on the same point in the year.

The `<` and `>` buttons SHALL stop at the ends of the range. The previous-week button SHALL be unavailable while the first week of 1917 is displayed. The next-week button SHALL be unavailable once no later week starts on or before 31 December of the year after the current one. A step that would pass that date while a later week still starts within the range SHALL land on that date, so the final week is reached rather than skipped.

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
- **WHEN** I press the next-week button on the last week of a December before the range's final one
- **THEN** the month selector changes to January and the year selector advances to the next year

#### Scenario: Selected week survives navigation
- **WHEN** I jump to a week, open an anime from it, and navigate back
- **THEN** the same week is still displayed, with the selectors showing its month and year

#### Scenario: The year selector reaches 1917
- **WHEN** I open the year selector
- **THEN** it offers every year from the year after the current one down to 1917

#### Scenario: Stepping back stops at the first week of 1917
- **WHEN** the week of 1 to 7 January 1917 is displayed
- **THEN** the previous-week button is unavailable and does nothing

#### Scenario: Stepping forward reaches the final week and stops
- **WHEN** the current year is 2026, the view shows the week of 20 to 26 December 2027 on its Sunday, and I press the next-week button
- **THEN** the view shows the week of 27 December 2027 to 2 January 2028, the selectors show December 2027, and the next-week button is then unavailable
