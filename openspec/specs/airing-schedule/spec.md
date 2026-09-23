# airing-schedule Specification

## Purpose
The airing-schedule capability governs the weekly schedule grid of my list's airing anime in local time: grouping by converted local day, the seven-day column layout, marking today, week navigation, grouping same-anime episodes landing on the same local day, and the empty-week message shown when a break leaves nothing airing. Episode timing itself — when an episode airs — is read from episode-airing-data rather than computed here.
## Requirements
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

### Requirement: Grouping by converted local day
The system SHALL group each slot by the local day its stored air instant converts to, not by the raw JST day.

#### Scenario: Show crosses the day boundary on conversion
- **WHEN** an episode's stored air instant converts to a different local day than its JST day
- **THEN** it appears under the converted local day in the weekly view

#### Scenario: Week bounds follow local days
- **WHEN** a week spanning a daylight-saving transition is displayed
- **THEN** it covers exactly seven local days, and each episode falls in the day-column matching its local air date

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

### Requirement: Today is marked in the schedule's day headers
When the displayed week contains the viewer's current local date, the schedule SHALL mark that one day-column's header as today, so the current day can be found at a glance without reading the seven dates.

The mark SHALL live in the day-column header at the top of the grid — the row already carrying the weekday name and `<day>.<month>` — and SHALL distinguish that header from the other six by more than a single hue, so it survives a theme switch and is not the only cue available to a viewer who cannot distinguish the accent colour from the header's ordinary colour.

The marked header SHALL keep showing exactly what every other header shows: the same weekday name and the same unpadded `<day>.<month>`, in the same position. Marking today SHALL NOT change the header's height, the column's width, or the position of any slot beneath it, so the seven columns stay aligned and no other column moves when the week changes.

The marked column SHALL carry `aria-current="date"` so assistive technology announces it as the current date rather than relying on the visual treatment alone.

Exactly one column SHALL ever be marked. A displayed week that does not contain the current local date SHALL mark no column at all — including a past or future week that shares a weekday with today.

Which date counts as today SHALL be the viewer's local date, determined the same way the page's `current` control determines the week to jump to, so the marked column and that control can never disagree about which day today is.

#### Scenario: The current week marks today
- **WHEN** the week containing the current local date is displayed
- **THEN** exactly one day-column's header — the one whose local date is today — is visually distinguished from the other six and carries `aria-current="date"`

#### Scenario: Another week marks nothing
- **WHEN** I navigate to the previous or next week
- **THEN** no day-column header is marked as today, even though one of them falls on the same weekday as today

#### Scenario: The mark does not disturb the grid
- **WHEN** I compare the marked column with its neighbours
- **THEN** its header shows the same weekday name and `<day>.<month>` as any other column, the columns are the same width, and the slots beneath each header start at the same height

#### Scenario: Returning to the current week re-marks today
- **WHEN** I navigate away to another week and then use the `current` control
- **THEN** today's column is marked again, in the column matching the same local date the `current` control navigated to

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

### Requirement: Empty-week message
The system SHALL show a message in the middle of the view when nothing in my list airs during the displayed week.

#### Scenario: Nothing airing this week
- **WHEN** no my-list anime air during the displayed week
- **THEN** the view shows a centered message stating nothing is airing that week

