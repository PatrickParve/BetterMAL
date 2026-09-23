# list-recaps Specification

## Purpose
The list-recaps capability generates a retrospective recap of the anime in a user's list over a period they choose — a single season, a calendar year, or a span of years — reachable from the main navigation or by scoping my list in place without leaving it. Depending on the selected time filter, a recap covers either what the user actually watched or completed in that window, or what aired during it, and it is always attributed to a single period so nothing double-counts. Each recap reports summary stats, the biggest divergences between the user's scores and MAL's community scores, a top-10 ranked list narrowable by score basis and media type, and Bayesian-weighted rankings of the best seasons and years (plus how much time was spent watching each), so a user can see at a glance what stood out during that stretch of their watching history and jump from there into a scoped view of their full list.

## Requirements

### Requirement: Recap period modes
The system SHALL provide a recap of anime in my list over a chosen period, in exactly three period modes that the user switches between without leaving the recap:

- **Multi-year** — a start year and an end year, selected as whole years with no month or day. The period runs from 1 January of the start year through 31 December of the end year, inclusive of both.
- **Yearly** — one calendar year, 1 January through 31 December.
- **Season** — one year plus one of winter, spring, summer, or fall, using MAL's fixed quarter convention (winter = January–March, spring = April–June, summer = July–September, fall = October–December).

Switching mode SHALL keep the recap on screen and recompute it for the new period rather than returning the user to an empty state. The selected mode and period SHALL be carried in the page URL, so a recap can be linked to, reloaded, and restored on back-navigation with the same period it was showing.

#### Scenario: Selecting a multi-year period
- **WHEN** I choose the multi-year mode and set the period to 2011 through 2020
- **THEN** the recap covers 1 January 2011 through 31 December 2020 inclusive

#### Scenario: Selecting a single year
- **WHEN** I choose the yearly mode and select 2022
- **THEN** the recap covers 1 January 2022 through 31 December 2022

#### Scenario: Selecting a season
- **WHEN** I choose the season mode and select fall 2019
- **THEN** the recap covers 1 October 2019 through 31 December 2019

#### Scenario: A multi-year period of one year
- **WHEN** I set a multi-year period whose start year and end year are the same
- **THEN** the recap covers that single calendar year rather than reporting an invalid period

#### Scenario: Reversed multi-year bounds
- **WHEN** a multi-year period arrives with its end year before its start year
- **THEN** the two bounds are treated as an inclusive range in ascending order rather than producing an empty recap

#### Scenario: Switching mode keeps the recap open
- **WHEN** I switch from a yearly recap to a season recap
- **THEN** the recap re-renders for the newly selected period without navigating away

#### Scenario: Recap period survives a reload
- **WHEN** I reload the page, or return to a recap with the browser's back button
- **THEN** the same mode, period, and time filter are shown as when I left

### Requirement: Recap page title names its period
The recap page SHALL carry exactly one heading naming what is being recapped, composing the selected period and the word "recap" into a single title — a season recap titled by its season and year, a yearly recap by its year, and a multi-year recap by its inclusive year range. The page SHALL NOT additionally repeat the period as a second heading below the controls.

The title SHALL update whenever the mode or period changes, without the page navigating away, and SHALL be present whether or not the period holds any entries, so an empty period is still identified by name.

#### Scenario: A season recap's title
- **WHEN** a season recap for fall 2019 is shown
- **THEN** the page's single heading names fall 2019 as the period being recapped

#### Scenario: A yearly recap's title
- **WHEN** a yearly recap for 2022 is shown
- **THEN** the page's single heading names 2022 as the period being recapped

#### Scenario: A multi-year recap's title
- **WHEN** a multi-year recap covers 2011 through 2020
- **THEN** the page's single heading names that inclusive range as the period being recapped

#### Scenario: The period is named once, not twice
- **WHEN** any recap is shown
- **THEN** the period appears in the page's title and is not repeated as a separate heading above the recap's content

#### Scenario: The title follows the period
- **WHEN** I change the period or switch mode
- **THEN** the title re-renders for the newly selected period in place

#### Scenario: An empty period is still named
- **WHEN** a selected period includes no entries under either time filter
- **THEN** the title still names that period alongside the message that there is nothing to recap for it

### Requirement: Period stepper arrows
A season recap and a yearly recap SHALL each offer a previous and a next control beside their period selection, stepping the period by one unit of the current mode: one season for a season recap, crossing into the adjacent year at the boundaries of the calendar, and one year for a yearly recap. Stepping SHALL change the period exactly as selecting it from the period controls does, leaving the mode, the time filter, the ranking basis, and the media-type narrowing untouched, and SHALL be carried in the page URL like any other period change.

A multi-year recap SHALL NOT offer these controls, since a two-ended range has no single step.

A control that would step outside the range of periods the recap's own period controls offer SHALL be presented as unavailable and SHALL NOT act.

#### Scenario: Stepping a season
- **WHEN** a fall 2019 season recap is shown and I use the next control
- **THEN** the recap moves to winter 2020, keeping its ranking basis and media-type narrowing

#### Scenario: Stepping across a year boundary
- **WHEN** a winter 2020 season recap is shown and I use the previous control
- **THEN** the recap moves to fall 2019

#### Scenario: Stepping a year
- **WHEN** a 2022 yearly recap is shown and I use the previous control
- **THEN** the recap moves to 2021, keeping its time filter

#### Scenario: Multi-year has no stepper
- **WHEN** a multi-year recap is shown
- **THEN** no previous or next control is offered

#### Scenario: Stepping is addressable
- **WHEN** I step to another period and then reload the page
- **THEN** the recap is shown for the period I stepped to

#### Scenario: The end of the range
- **WHEN** the selected period is the earliest or latest the period controls offer
- **THEN** the control that would step past it is shown as unavailable and does nothing

### Requirement: A recap period runs from winter 1917 through the current season
Every recap period SHALL fall between winter 1917 and the **current season**, inclusive. Winter 1917 is the first season in MyAnimeList's season archive and the same floor the Season and Year pages use. The current season is the newest period a recap can show, since a season that has not started has nothing to recap. For the yearly and multi-year modes this makes the range 1917 through the current year. For the season mode it makes the range winter 1917 through the current season, so a season later in the current year is outside it.

Every control that chooses a recap period SHALL offer only periods inside this range. That covers the recap page's own dropdowns and stepper arrows, and the **Recap a period** picker on my list, including when the picker scopes my list rather than opening the recap page. The picker's year choices SHALL still start no earlier than its own availability data suggests, but never before 1917, and SHALL end at the current year. Its season choices in the current year SHALL stop at the current season.

**Addressing a period by URL.** A URL SHALL reach exactly the periods the controls offer and nothing further. The page SHALL resolve the period parameters its mode reads, and SHALL replace any that are present but out of range or malformed:

- **Season mode:** when `year` or `season` is present but malformed (not a whole number, or not one of winter, spring, summer or fall), or the season they name falls outside the range, both SHALL be replaced with the current season.
- **Yearly mode:** a `year` that is present but malformed or outside 1917 through the current year SHALL be replaced with the current year.
- **Multi-year mode:** each of `from` and `to` that is present but malformed or outside 1917 through the current year SHALL be replaced with the value it takes when absent. That is the previous year for `from` and the current year for `to`. A range whose start year then lies after its end year SHALL have the two swapped, so the URL always names an ascending range.
- **Mode:** a `mode` that is present but is none of the three modes SHALL be removed, so it resolves to the yearly mode as an absent one does.

A period parameter that is absent SHALL keep resolving to its default, as today, without the URL being rewritten. Parameters the resolved mode does not read, and every other parameter in the query string, SHALL be left as they are.

The replacement SHALL take effect before the recap is requested: no recap request SHALL be made for the rejected period. The replacement SHALL substitute the current entry in the browser's history rather than adding one, so going back does not return to the rejected URL. The system SHALL NOT show a message, an error state or any other notice about the replacement.

No part of rendering the recap page SHALL allocate per-year or per-option work proportional to a value taken from the URL.

#### Scenario: The current season is the newest recap
- **WHEN** the current season is summer 2026 and a season recap for summer 2026 is shown
- **THEN** the next-season control is unavailable, and the season dropdown for 2026 offers winter, spring and summer but not fall

#### Scenario: The current year is the newest yearly recap
- **WHEN** a yearly recap for the current year is shown
- **THEN** the next-year control is unavailable and no year dropdown offers a later year

#### Scenario: A future season by URL is replaced
- **WHEN** the current season is summer 2026 and I open `/recap?mode=season&year=2026&season=fall`
- **THEN** the recap for summer 2026 is shown, the URL is replaced with it, no message is shown, and no recap is requested for fall 2026

#### Scenario: A future year by URL is replaced
- **WHEN** I open a yearly recap link for the year after the current one
- **THEN** the recap for the current year is shown with the URL replaced and nothing requested for the later year

#### Scenario: A year before the archive is replaced
- **WHEN** I open `/recap?mode=yearly&year=1916`
- **THEN** the recap for the current year is shown with the URL replaced and nothing requested for 1916

#### Scenario: An impossible year is replaced without rendering it
- **WHEN** I open `/recap?year=32932734`
- **THEN** the current year's recap is shown with the URL replaced, and the year dropdowns are built from 1917 through the current year rather than from 32932734

#### Scenario: A malformed season is replaced
- **WHEN** I open a season recap link whose season is not one of winter, spring, summer or fall
- **THEN** the recap for the current season is shown and the URL names it

#### Scenario: One bad end of a multi-year range
- **WHEN** the current year is 2026 and I open `/recap?mode=multiYear&from=1500&to=2020`
- **THEN** `from` is replaced with 2025, the two ends are put in order, and the recap for 2020 through 2025 is shown with the URL naming that range

#### Scenario: A reversed multi-year range by URL
- **WHEN** I open `/recap?mode=multiYear&from=2024&to=2020`
- **THEN** the URL is replaced to name 2020 through 2024, and the recap for that range is shown instead of the page staying blank or showing the previous period

#### Scenario: An unknown mode is dropped
- **WHEN** I open a recap link whose mode is none of multi-year, yearly or season
- **THEN** the mode parameter is removed and the yearly recap is shown

#### Scenario: An absent period keeps its default without a rewrite
- **WHEN** I open `/recap` with no parameters
- **THEN** the yearly recap for the current year is shown and the URL is left as it was

#### Scenario: A replacement keeps the rest of the query
- **WHEN** a recap link names an out-of-range year but also carries a ranking basis and a media-type narrowing
- **THEN** the current year's recap is shown with that basis and narrowing still applied

#### Scenario: A replacement does not leave a step back into the bad URL
- **WHEN** a recap period I addressed is replaced and I press the browser Back button
- **THEN** I go back to wherever I came from, not to the rejected period

#### Scenario: The picker offers only the recap's range
- **WHEN** I open **Recap a period** on my list while the current season is summer 2026, and my list holds anime that start airing in fall 2026 and in 2027
- **THEN** the picker's years end at 2026, and for 2026 its season choices end at summer

### Requirement: Recap requests outside the recap's range are refused
`GET /api/recap` SHALL refuse a period outside winter 1917 through the current season. The response SHALL be `400`, in the same `{ error }` shape as an unknown mode or season name. It SHALL be sent before the recap is computed. A refused period SHALL NOT be moved to the nearest valid one, and SHALL NOT be served as an empty recap.

A yearly request SHALL be accepted when its year is between 1917 and the current year, inclusive. A season request SHALL be accepted when its season falls, in season order, between winter 1917 and the current season. A multi-year request SHALL be accepted only when both of its ends are between 1917 and the current year. The ends SHALL be checked as sent, before being put in ascending order, and a reversed range whose ends are both in range SHALL still be accepted.

The current season and year SHALL be taken from the app's local date **one day ahead**. A browser whose clock has already crossed into the next season or year, for example around midnight at a season's turn, is then never refused a period its own controls offer. The range SHALL be worked out again for every request and SHALL NOT be stored. The existing checks for an unknown mode, filter or season name, and for a mode's missing parameters, SHALL still come first.

#### Scenario: A year past the current one is refused
- **WHEN** `GET /api/recap?mode=yearly&year=<two years after the current one>` is requested
- **THEN** the response is `400` and no recap is computed

#### Scenario: A year before the archive is refused
- **WHEN** `GET /api/recap?mode=yearly&year=1916` is requested
- **THEN** the response is `400`

#### Scenario: A year beyond the calendar is refused rather than failing
- **WHEN** `GET /api/recap?mode=yearly&year=99999` or `mode=multiYear&from=-5&to=2020` is requested
- **THEN** the response is `400`, not a server error

#### Scenario: A season past the current one is refused
- **WHEN** the current season is summer 2026, it is not the season's last day, and `GET /api/recap?mode=season&year=2026&season=fall` is requested
- **THEN** the response is `400`

#### Scenario: The archive's first season is accepted
- **WHEN** `GET /api/recap?mode=season&year=1917&season=winter` is requested
- **THEN** the recap is computed and returned as usual

#### Scenario: A multi-year range with one end out of range is refused
- **WHEN** `GET /api/recap?mode=multiYear&from=1&to=9999` is requested
- **THEN** the response is `400` and no recap is computed

#### Scenario: A reversed in-range multi-year range is accepted
- **WHEN** `GET /api/recap?mode=multiYear&from=2024&to=2020` is requested
- **THEN** the recap for 2020 through 2024 is returned

#### Scenario: A client already in the next season is not refused
- **WHEN** it is the last day of summer 2026 in the app's local time, and a client whose clock has already reached 1 October requests the fall 2026 season recap
- **THEN** the recap is computed and returned as usual

### Requirement: Recap year choices start at MyAnimeList's first season year
Every year choice the recap page offers SHALL reach back to 1917, the first year in MyAnimeList's season archive and the same floor the Season and Year pages use. This covers the multi-year start and end years, the yearly recap's year, and the season recap's year. The choices SHALL run from 1917 up to the current year, listed most recent first. They SHALL be built from those two ends alone. They SHALL NOT widen to include a year taken from the URL, since a URL can no longer address a year outside that range (see "A recap period runs from winter 1917 through the current season").

The season recap's season choices SHALL stop at the current season when the current year is selected: a season later in the current year SHALL NOT be offered. Choosing the current year while a later season is selected SHALL move the season to the current season, since the year dropdown changes only the year half of the period.

The multi-year start and end years SHALL stay in ascending order. Choosing a start year later than the end year SHALL move the end year to the chosen start year. Choosing an end year earlier than the start year SHALL move the start year to the chosen end year. The page's own controls therefore never produce a reversed range.

The recap's floor and the Season and Year pages' floor SHALL be one value, so they can't drift apart.

The period steppers follow this range through "Period stepper arrows". The previous control SHALL be unavailable only at 1917 in a yearly recap, and at winter 1917 in a season recap. The next control SHALL be unavailable at the current year in a yearly recap, and at the current season in a season recap.

#### Scenario: Choosing an early year
- **WHEN** I open the yearly recap's year dropdown
- **THEN** it lists every year from the current year down to 1917

#### Scenario: A season recap in the archive's first year
- **WHEN** I choose winter 1917 in the season recap
- **THEN** the recap is shown for winter 1917, and the previous control is unavailable

#### Scenario: Stepping below 1960
- **WHEN** a 1960 yearly recap is shown and I use the previous control
- **THEN** the recap moves to 1959

#### Scenario: A multi-year range from the archive's start
- **WHEN** I choose a multi-year recap
- **THEN** both its start-year and end-year dropdowns reach back to 1917

#### Scenario: Stepping forward stops at the current season
- **WHEN** the current season is summer 2026 and a spring 2026 season recap is shown
- **THEN** the next control moves the recap to summer 2026, and from there the next control is unavailable

#### Scenario: Choosing the current year with a later season selected
- **WHEN** the current season is summer 2026, a fall 2024 season recap is shown, and I choose 2026 from its year dropdown
- **THEN** the recap moves to summer 2026 rather than fall 2026

#### Scenario: Choosing a start year after the end year
- **WHEN** a multi-year recap covers 2018 through 2020 and I choose 2023 as its start year
- **THEN** the recap covers 2023 through 2023, and the end-year dropdown shows 2023

#### Scenario: Choosing an end year before the start year
- **WHEN** a multi-year recap covers 2018 through 2020 and I choose 2015 as its end year
- **THEN** the recap covers 2015 through 2015, and the start-year dropdown shows 2015

### Requirement: Season recap links to its season page
A season recap SHALL offer a control that opens the season browser on that same season and year, so the anime that aired that season — not only the ones in my list — are one step away. The control SHALL NOT be offered on a yearly or multi-year recap, whose period is not a single season.

The control SHALL be presented as a button rather than as inline text: it SHALL be the same height as the period controls it sits beside, and SHALL take the same hover and keyboard-focus treatment the app's other controls take. It SHALL remain a real link — openable in a new tab or window by the means the browser normally offers for links — despite being presented as a button.

The control SHALL take the **season** colour family on hover and on keyboard focus, matching the treatment the recap's own Season tab takes when hovered, rather than the app's generic accent. Its resting state SHALL stay neutral, matching the period controls beside it, so the colour appears in response to the pointer or focus rather than being worn all the time.

#### Scenario: Opening the season page from a recap
- **WHEN** a fall 2019 season recap is shown and I follow its season-page control
- **THEN** the season browser opens on fall 2019

#### Scenario: The control reads as a button
- **WHEN** a season recap is shown
- **THEN** its season-page control is presented as a button matching the height of the controls beside it, and highlights on hover and on keyboard focus as they do

#### Scenario: The control is still a link
- **WHEN** I open the season-page control in a new tab the way I would any link
- **THEN** the season browser opens in a new tab on that season and year

#### Scenario: Hovering wears the season colour
- **WHEN** I hover the season-page control
- **THEN** it highlights in the season family's colour, the same one the Season tab highlights in when hovered, rather than in the app's generic accent

#### Scenario: Focus wears the season colour
- **WHEN** I reach the season-page control by keyboard
- **THEN** its focus ring is drawn in the season family's colour, as the Season tab's is

#### Scenario: The resting control is neutral
- **WHEN** a season recap is shown and I am not pointing at or focused on the season-page control
- **THEN** it is drawn neutrally, matching the period controls beside it

#### Scenario: Not offered outside season mode
- **WHEN** a yearly or multi-year recap is shown
- **THEN** no season-page control is offered

### Requirement: Yearly recap links to its year page
A yearly recap SHALL offer a control that opens the year browser on that same year, so the anime that aired that year — not only the ones in my list — are one step away. The control SHALL NOT be offered on a season or multi-year recap, whose period is not a single year.

The control SHALL be the year-level counterpart of the season recap's season-page control and SHALL match it in every respect but subject: the same button presentation, the same height as the period controls it sits beside, the same hover and keyboard-focus treatment, and the same standing as a real link — openable in a new tab or window by the means the browser normally offers for links — despite being presented as a button.

The control SHALL take the **year** colour family on hover and on keyboard focus, matching the treatment the recap's own Yearly tab takes when hovered, rather than the app's generic accent. Its resting state SHALL stay neutral, matching the period controls beside it, so the colour appears in response to the pointer or focus rather than being worn all the time.

At most one period-browsing control SHALL be offered at a time: a season recap offers the season one, a yearly recap offers the year one, and a multi-year recap offers neither.

#### Scenario: Opening the year page from a recap
- **WHEN** a 2019 yearly recap is shown and I follow its year-page control
- **THEN** the year browser opens on 2019

#### Scenario: The control reads as a button
- **WHEN** a yearly recap is shown
- **THEN** its year-page control is presented as a button matching the height of the controls beside it, and highlights on hover and on keyboard focus as they do

#### Scenario: The control is still a link
- **WHEN** I open the year-page control in a new tab the way I would any link
- **THEN** the year browser opens in a new tab on that year

#### Scenario: Hovering wears the year colour
- **WHEN** I hover the year-page control
- **THEN** it highlights in the year family's colour, the same one the Yearly tab highlights in when hovered, rather than in the app's generic accent

#### Scenario: Focus wears the year colour
- **WHEN** I reach the year-page control by keyboard
- **THEN** its focus ring is drawn in the year family's colour, as the Yearly tab's is

#### Scenario: The resting control is neutral
- **WHEN** a yearly recap is shown and I am not pointing at or focused on the year-page control
- **THEN** it is drawn neutrally, matching the period controls beside it

#### Scenario: Not offered outside yearly mode
- **WHEN** a season or multi-year recap is shown
- **THEN** no year-page control is offered

#### Scenario: Switching between season and yearly modes swaps the control
- **WHEN** I switch a recap from season mode to yearly mode
- **THEN** the season-page control is replaced by the year-page control, rather than both appearing

### Requirement: Dynamic time filter
The multi-year and yearly recaps SHALL offer a time filter with two options that change which of my entries the period includes, with every stat, the top 10, and the rankings recomputed to match the selection:

- **What I watched** — entries I watched anything of inside the period. An entry SHALL qualify when **either** of the following holds:
  - its status is Completed or Dropped and its **completion date** falls inside the period; **or**
  - the **activity log** records episode progress for it inside the period, whatever its status and whatever its recorded dates.
- **What aired** — entries whose anime started airing inside the period, whatever the completion date, limited to entries whose status is Completed, Dropped, On hold, or Watching.

A start date SHALL NOT be required for either option. An entry with no recorded start date SHALL qualify for **What I watched** exactly as one with a start date does, on its completion date or its logged progress.

The logged-progress arm is what lets **What I watched** mean what its name says. Without it, an anime I am partway through has no completion date and so falls out of every period, and a rewatch is invisible because the entry's completion date still points at the original viewing. With it, an anime I watched episodes of in a period belongs to that period whether or not I finished anything there.

An anime SHALL be counted **once** per period however many of its viewings the period holds. An entry that qualifies on both its completion date and its logged progress, or whose logged progress in the period spans several viewings, SHALL appear once in **In this period**, once in the mean score, once in the top 10, and once in any ranking. Only the stats that measure viewing volume — Episodes watched and Time spent — SHALL count every viewing; see the recap stat block.

Because logged progress and completion dates place an anime independently, an anime MAY appear in more than one **What I watched** period: one for the period it was first watched in, and one for each later period holding a logged rewatch. This is the intended reading of a watch-history filter and does not conflict with the single-period attribution **What aired** applies.

The activity log holds history only from the point this app began tracking. For periods before that, an entry qualifies on its completion date alone, and viewing the app never recorded SHALL NOT be estimated from start dates, air dates, or stored rewatch counts.

Neither option SHALL include Plan-to-watch entries. The season recap SHALL NOT offer the filter: it always selects on what aired in that season, under the same status rule as the **What aired** option above.

An option that would include no entries for the selected period SHALL be shown as unavailable and SHALL NOT be selectable; a single matching entry is enough to make it selectable. Availability SHALL be judged under the same two-armed rule the selection itself applies, so an option holding only logged-progress entries is offered rather than reported empty. When the selected option becomes unavailable because the period changed, the recap SHALL fall back to the other option rather than showing an empty recap under an impossible selection.

#### Scenario: Watch-history selection
- **WHEN** the yearly recap for 2022 is set to **What I watched**
- **THEN** it includes the entries I completed or dropped between 1 January and 31 December 2022, together with every entry the activity log records episode progress for in that window

#### Scenario: A missing start date changes nothing
- **WHEN** an entry I completed inside the period has no recorded start date
- **THEN** it is included exactly as one with a start date is

#### Scenario: A show I am partway through
- **WHEN** the activity log records me watching episodes of a series I have not finished during 2026, and the 2026 recap is set to **What I watched**
- **THEN** that series is included in the recap

#### Scenario: A rewatch places an anime in a later period
- **WHEN** I completed a series in 2022 and the activity log records me rewatching it in 2024
- **THEN** it appears in the 2022 recap and again in the 2024 recap under **What I watched**

#### Scenario: An anime spanning the new year lands in both periods
- **WHEN** the activity log records me watching a series' episodes in December 2023 and further episodes in January 2024
- **THEN** it appears in both the 2023 and the 2024 recap, each counting the episodes watched in that year

#### Scenario: Counted once per period
- **WHEN** a period holds both an anime's completion date and logged progress for it
- **THEN** In this period counts it once, the mean score averages its score once, and it occupies one place in the top 10

#### Scenario: A multi-year range holding two viewings
- **WHEN** a 2020–2024 recap covers both my first watch of a series and a later rewatch of it
- **THEN** the series occupies one place in the recap's count, mean score, top 10 and rankings

#### Scenario: Nothing is estimated before tracking began
- **WHEN** a 2015 recap is set to **What I watched** and an entry has no completion date in 2015 and no logged progress there
- **THEN** it is left out, and no viewing is inferred from its start date, its air date, or its rewatch count

#### Scenario: Aired-in-period selection
- **WHEN** the yearly recap for 2022 is set to **What aired**
- **THEN** it includes exactly the completed, dropped, on-hold, and watching entries whose anime started airing in 2022, whenever I watched them

#### Scenario: Plan to watch is never counted
- **WHEN** either time filter is selected
- **THEN** no Plan-to-watch entry appears in the recap under either option

#### Scenario: An option with no entries is disabled
- **WHEN** I select a period in which I finished nothing, logged no episodes, but which several anime I watched debuted in
- **THEN** **What I watched** is shown as unavailable and cannot be selected, while **What aired** remains selectable

#### Scenario: Logged progress alone makes the option available
- **WHEN** a period holds no completion dates but the activity log records episode progress inside it
- **THEN** **What I watched** is selectable and the recap renders for those entries

#### Scenario: One entry is enough
- **WHEN** a period holds exactly one entry under an option
- **THEN** that option is selectable and the recap renders in full for that single entry

#### Scenario: Falling back when the selection becomes unavailable
- **WHEN** I change the period while **What I watched** is selected, and the new period has no completed or dropped entries, no logged progress, but does have anime that aired in it
- **THEN** the recap switches to **What aired** rather than reporting the period empty

#### Scenario: Season recap has no filter
- **WHEN** a season recap is shown
- **THEN** no time-filter control is offered, and the recap selects on what aired that season

### Requirement: The recap's control row groups its period-scoped controls
The row of controls beneath the recap's title SHALL be laid out in two groups: the period selection — the stepper arrows, the period label, and the period dropdowns — leads the row, and every control that acts on the *selected* period sits together at the row's trailing edge.

On a yearly recap the trailing group SHALL therefore hold the **What I watched** / **What aired** time filter immediately beside the **Browse the year** control, rather than the filter sitting alone in the middle of the row. On a season recap, whose only trailing control is **Browse the season**, and on a multi-year recap, whose only trailing control is the time filter, the single control SHALL sit at the trailing edge in the same place the group occupies, so the row reads the same way in all three modes.

Grouping SHALL change nothing about the controls themselves: each keeps its own presentation, its states, its height, and its behaviour, and the row SHALL still wrap onto further lines rather than overflow when the window is too narrow to hold both groups.

#### Scenario: The yearly recap's controls travel together
- **WHEN** a yearly recap is shown
- **THEN** the What I watched / What aired filter sits beside the Browse the year control at the trailing edge of the control row, with the period stepper leading the row

#### Scenario: A season recap keeps its shape
- **WHEN** a season recap is shown
- **THEN** the period stepper leads the row and Browse the season sits at its trailing edge

#### Scenario: A multi-year recap keeps its shape
- **WHEN** a multi-year recap is shown
- **THEN** the period stepper leads the row and the time filter sits at its trailing edge, with no period-browsing control offered

#### Scenario: Grouping changes nothing else
- **WHEN** I use the time filter on a yearly recap after this change
- **THEN** it behaves exactly as before, with the same states and the same height as the control beside it

#### Scenario: A narrow window wraps rather than overflows
- **WHEN** the window is too narrow to hold both groups on one line
- **THEN** the row wraps and no control is clipped or pushed outside the page

### Requirement: A recap counts only progress recorded in the app

Wherever a recap reads the activity log — to decide whether an entry belongs to a **What I watched** period, to judge whether that option is available for a period, and to compute a period's episode count — it SHALL read only progress recorded from **my own use of the app**, and SHALL NOT count progress because a sync observed it on MyAnimeList.

This SHALL hold because of what the activity log contains, not because of anything the recap filters out. The sync paths record nothing (see the `activity-recording` capability), so every progress record in the log was made in the app, and the recap SHALL read all of them. That includes progress applied by **declining a change held for review**, which is recorded as a change made in the app, at the moment I declined it.

A sync's moment is the moment the sync ran, not the moment I watched. A weekly reconciliation would otherwise drop a stretch of viewing into whichever period the sync happened to fall in, and a single catch-up run could move months of episodes into one day. This is one reason the sync paths record nothing.

The consequence SHALL be stated plainly rather than worked around: episodes I watched and recorded only on MyAnimeList's own site SHALL NOT contribute to a recap, exactly as they do not today. An entry that qualifies for a period on its **completion date** SHALL still qualify however that completion date arrived.

This SHALL constrain only what the recap reads from the activity log. Every other recap rule — the two-armed selection, the once-per-period counting, the per-period episode sum, the pre-tracking fallback to stored episodes and rewatch count — SHALL apply exactly as it does today to the progress that does count.

#### Scenario: Synced progress does not enter a recap

- **WHEN** an accepted reconciliation diff raises an anime's episodes watched inside a period
- **THEN** that increase contributes nothing to the period's episode count and does not by itself place the anime in the period's **What I watched** selection

#### Scenario: Availability is judged on the same terms

- **WHEN** the only change to any anime's episodes watched inside a period came from a sync
- **THEN** the period is judged as holding no logged progress

#### Scenario: My own progress counts as it does today

- **WHEN** I watch and record episodes in the app inside a period
- **THEN** they place the anime in that period and contribute to its episode count exactly as they do today

#### Scenario: Progress applied by declining a held change counts

- **WHEN** I decline a change held for review inside a period, and MyAnimeList's current value raises that entry's episodes watched from 5 to 7
- **THEN** the increase of 2 counts toward that period's episode count and places the anime in the period's **What I watched** selection

#### Scenario: A completion date still places an anime

- **WHEN** an anime's completion date falls inside a period and the only change to its progress came from a sync
- **THEN** the anime is still included on its completion date, and its episodes are counted by the rule that applies where the log holds no progress for the period

### Requirement: Period attribution by start date
Under the **What aired** selection, an anime SHALL be attributed to the period containing the date it started airing, even when it continued airing into later periods. Under that selection an anime SHALL NOT appear in more than one period of the same mode.

Single-period attribution is a rule of the **What aired** selection alone. Under **What I watched** an anime MAY appear in several periods of the same mode, because it may genuinely have been watched in several — see the dynamic time filter requirement.

An anime with no known start date SHALL be excluded from any **What aired** selection, and SHALL be excluded from the season and year rankings, since it cannot be placed on the calendar. Such an entry SHALL still be included in a **What I watched** selection whose period contains its completion date or its logged episode progress.

#### Scenario: A show spanning two years
- **WHEN** an anime started airing on 30 December 2022 and continued into 2023, and the yearly recap for 2022 is set to **What aired**
- **THEN** it is included in the 2022 recap and does not appear in the 2023 recap

#### Scenario: A show starting at the end of a multi-year range
- **WHEN** an anime started airing on 30 December 2020 and a multi-year recap covers 2011 through 2020 under **What aired**
- **THEN** that anime is included in the recap

#### Scenario: Unknown start date under aired selection
- **WHEN** an included-status entry's anime has no recorded start date and the selection is **What aired**
- **THEN** it is left out of the recap and out of the season and year rankings

#### Scenario: Unknown start date under watch-history selection
- **WHEN** that same entry was completed inside the period and the selection is **What I watched**
- **THEN** it is included in the recap's stats and top 10

#### Scenario: Multi-period appearance is confined to the watch-history filter
- **WHEN** an anime has logged viewings in two different years
- **THEN** it appears in both years' **What I watched** recaps, and still in only one year's **What aired** recap

### Requirement: Recap stat block
Every recap SHALL report the following stats, computed over the entries the selected period and time filter include, and recomputed whenever either changes:

- **In this period** — how many entries the period includes in total, under whatever time filter is selected. The label SHALL name the period rather than the medium, so this stat is not read as a count of a particular status.
- **Completed** — how many included entries have status Completed.
- **Dropped** — how many included entries have status Dropped, so the difference between the period's total and its completed count is accounted for rather than left unexplained.
- **Currently watching** — how many anime from the recap's period I am currently watching: from that season, that year, or that range of years, according to the recap shown. See the air-date rule below.
- **Mean score** — the average of my scores across included entries I have scored, to two decimals. Unscored entries SHALL be left out of the average rather than counted as zero.
- **Episodes watched** — the episodes I watched in the period across included entries whose media type is not `movie`. See the per-period episode rule below.
- **Movies watched** — how many included entries whose media type is `movie` have at least one episode watched.
- **Time spent** — the total runtime of the episodes counted by **Episodes watched**, with movies included.
- **Hot takes** — see the hot takes requirement.

**Currently watching** SHALL count entries with status Watching whose anime's air-start date falls inside the recap's period, in every mode and under **both** time filters — never on the completion-date attribution the **What I watched** filter uses. An entry still in progress has no completion date, so a filter-scoped count would report zero under **What I watched** for a period I am demonstrably still watching anime from. This stat therefore answers where the anime came from rather than when I finished something, and under **What I watched** it is the one tile scoped to the period's airing rather than to my watch history; the completed, dropped, and currently-watching counts consequently need not sum to **In this period**. An entry whose anime has no known air date SHALL be counted for no period.

The three status counts — Completed, Dropped, and Currently watching — SHALL be presented together, so what I finished, abandoned, and am still watching read as one group.

**The per-period episode count.** Under **What I watched**, an included entry's episodes SHALL be the episode progress the activity log records for it **inside the period**: the sum of its logged increases there. A recorded decrease SHALL contribute nothing and SHALL NOT reduce the figure.

Because a rewatch resets an entry's episode count to zero and counts back up, a period holding several viewings of one anime yields the sum of them all from this rule alone. An anime watched twice inside a period therefore contributes both viewings to Episodes watched and Time spent while still occupying a single place in the period's count, mean score, top 10 and rankings. No separate rewatch multiplier SHALL be applied to a logged figure — doing so would count the rewatch the log already holds a second time.

An entry included on its **completion date** for which the activity log holds no progress inside the period — a viewing that predates tracking — SHALL instead contribute its stored episodes watched, plus one further complete run of the anime for every recorded rewatch, measured by the anime's published total episode count or by its own episodes watched when no total is published. This is the only place a stored rewatch count is read, and it applies only where there is no log to read instead: the rewatch carries no date of its own, so the period the anime was finished in is the only period that can hold it.

Under **What aired**, and on every season recap, an entry SHALL contribute its stored episodes watched with no rewatch multiplier and no log lookup. A rewatch is not something that aired in the period, and counting it would inflate a figure about the period's broadcast.

**Movies watched** SHALL remain a count of films rather than of viewings: a film watched twice inside a period counts once. It is a followable stat, and the count it shows must equal the number of anime my list holds when I follow it. The runtime of the extra viewings still reaches **Time spent**, so no watched time is lost by counting the film once.

Time spent SHALL use each anime's cached average episode duration where one is known, and SHALL otherwise fall back to the same assumed per-episode runtime the profile and series pages already use, so the three surfaces never disagree about the same anime. Time spent SHALL be computed over the same episode counts Episodes watched reports, so dividing one by the other always yields a plausible per-episode runtime.

When no included entry has a score, the mean score SHALL be reported as unavailable rather than as zero.

#### Scenario: Stats reflect the included set
- **WHEN** a recap renders for a period and time filter
- **THEN** every stat is computed only over the entries that selection includes, save for Currently watching, which is computed under its own air-date rule

#### Scenario: Stats update with the filter
- **WHEN** I switch the time filter on a yearly recap
- **THEN** every stat recomputes for the newly included entries without reloading the page

#### Scenario: The period total is distinguishable from the completed count
- **WHEN** a period includes 42 entries of which 31 are completed and 4 dropped
- **THEN** the stat block reports 42 in this period, 31 completed, and 4 dropped, each labelled for what it counts

#### Scenario: Counting what I am still watching from a season
- **WHEN** a Fall 2024 season recap is shown and I am currently watching three anime that started airing in Fall 2024
- **THEN** the stat block reports 3 currently watching

#### Scenario: Counting what I am still watching from a year
- **WHEN** a 2024 yearly recap is shown and I am currently watching five anime that started airing anywhere in 2024
- **THEN** the stat block reports 5 currently watching

#### Scenario: Counting across a multi-year range
- **WHEN** a 2020–2024 recap is shown
- **THEN** currently watching counts every anime I am watching whose air-start date falls anywhere in those five years

#### Scenario: Currently watching survives the watch-history filter
- **WHEN** a 2024 yearly recap is set to **What I watched** and I am currently watching two anime that started airing in 2024
- **THEN** the stat block still reports 2 currently watching, rather than 0

#### Scenario: An entry with no air date counts nowhere
- **WHEN** I am currently watching an anime whose air date is unknown
- **THEN** it is counted toward no period's currently-watching stat

#### Scenario: Only the period's own episodes count
- **WHEN** a series was at episode 20 when 2024 began and the log records it reaching episode 29 during 2024, under **What I watched**
- **THEN** it contributes 9 episodes to the 2024 recap, not 29

#### Scenario: An in-progress show contributes what I watched of it
- **WHEN** the log records me going from episode 0 to episode 9 of a series I am still watching during the period, under **What I watched**
- **THEN** Episodes watched counts those 9 episodes and Time spent counts their runtime

#### Scenario: A dropped show with no completion date still counts what I watched
- **WHEN** the log records episode progress during the period on a series I later dropped without recording a finish date
- **THEN** that progress is counted in Episodes watched and Time spent

#### Scenario: Two viewings in one period count twice for volume and once for rank
- **WHEN** the log records me watching a 12-episode series through and then rewatching it, both inside the period, under **What I watched**
- **THEN** Episodes watched counts 24 for it, Time spent counts the runtime of 24 episodes, and it occupies one place in In this period, the mean score, and the top 10

#### Scenario: An episode-count correction does not subtract
- **WHEN** the log records me lowering an entry's episode count inside the period
- **THEN** that decrease contributes nothing and does not reduce the period's Episodes watched

#### Scenario: A pre-tracking viewing falls back to stored counts
- **WHEN** an entry is included on a completion date in a period the activity log does not reach, and it has a published total of 12 and a rewatch count of 1
- **THEN** it contributes 24 episodes to that period

#### Scenario: The stored rewatch count is not read twice
- **WHEN** an entry has both logged progress inside the period and a stored rewatch count
- **THEN** its contribution is the logged progress alone, with no rewatch multiplier applied on top

#### Scenario: Rewatches do not count under the aired filter
- **WHEN** a 2024 recap is set to **What aired** and holds a 12-episode series I have rewatched once
- **THEN** Episodes watched counts 12 for it

#### Scenario: A film watched twice is still one film
- **WHEN** the included set holds a film I watched twice inside the period
- **THEN** Movies watched counts it once, and Time spent counts both viewings

#### Scenario: Movies are excluded from the episode count
- **WHEN** the included set holds both a 12-episode TV series I finished and two films I watched
- **THEN** episodes watched reports 12 and movies watched reports 2

#### Scenario: Unscored entries do not drag the mean down
- **WHEN** the included set holds five entries of which two are unscored
- **THEN** the mean score is the average of the three scores I gave, not an average over five

#### Scenario: No scores in the period
- **WHEN** no included entry has a score
- **THEN** the mean score is shown as unavailable rather than as 0

#### Scenario: Runtime falls back for anime with no cached duration
- **WHEN** an included anime has no cached average episode duration
- **THEN** its contribution to time spent uses the app's standing per-episode assumption, matching what the profile and series pages report for the same anime

### Requirement: Recap rating distribution
Every recap SHALL show, below its stat block, how many of the period's anime I gave each score from 10 down to 1, rendered as the same bar-per-score block the profile page's all-anime score distribution uses: a row per score value carrying that score's bar, its count, and its share of the period's scored anime.

The block SHALL be computed over the entries the selected period and time filter include — the same set every other stat is computed over — and SHALL recompute whenever either changes. It SHALL NOT be narrowed by the top 10's media-type control, which is scoped to the top 10 alone.

Each bar's length SHALL be that score's count relative to the largest count across the period's score values, so the period's most-common score fills the full width of its track. A score no included anime received SHALL render an empty track. Shares SHALL be computed against the period's scored anime rather than my whole list, and a share that rounds to zero from a non-zero count SHALL be shown as less than one percent.

Each row's bar and its score numeral SHALL be drawn in the tier colour that score carries on the score board, exactly as the "Score slots are coloured by tier" requirement assigns them — 10 the apex treatment, 9 red, 8 blue, 7 gold, 6 and 5 silver, 4 through 1 bronze — so the distribution and the board the button beside it opens describe one ladder in one set of colours. The tier colours SHALL be read from the same definition the board reads, not restated, so the two can never disagree. Each row's count and share SHALL stay in the block's ordinary text colour, so the row's colour is carried by the score and its bar rather than smeared across every cell.

Tier colour SHALL be decoration on top of a row that is otherwise unchanged: every row SHALL keep its numeral, its count, and its share, the bar lengths SHALL still be proportional as above, and an empty track SHALL still read as empty rather than as a bar of that tier's colour.

This colouring is the recap's. The profile page's all-anime distribution SHALL keep its single-colour bars, so the two pages' blocks stay the same block drawn at two densities rather than diverging in structure.

The recap's block SHALL NOT repeat the mean score, which the stat block directly above it already reports for the same set. It MAY be rendered at a smaller size than the profile page's to suit the narrower column it occupies, provided it uses the same row structure and column alignment.

#### Scenario: Distribution of a period's scores
- **WHEN** a recap renders for a period whose included anime I have scored
- **THEN** it shows a bar, a count, and a share for each score from 10 down to 1, computed over that period's included entries

#### Scenario: The period's most-common score fills its track
- **WHEN** one score has more of the period's anime than any other
- **THEN** that score's bar fills the full width of its track and the others are drawn in proportion to it

#### Scenario: Shares are of the period, not the whole list
- **WHEN** a period holds 20 scored anime of which 5 scored 8
- **THEN** the 8 row reports a 25% share, whatever proportion 8s make up across my whole list

#### Scenario: The distribution follows the time filter
- **WHEN** I switch a yearly recap's time filter
- **THEN** the distribution recomputes for the newly included entries alongside the rest of the stats

#### Scenario: The media-type control does not narrow it
- **WHEN** I narrow the top 10 to films only
- **THEN** the distribution continues to cover every included anime of the period

#### Scenario: No mean line on the recap
- **WHEN** a recap renders its distribution
- **THEN** the block carries no mean-score line of its own, the stat block above it having already reported the period's mean

#### Scenario: Nothing scored in the period
- **WHEN** no included entry of the period has a score
- **THEN** every track renders empty and no share is shown as an invalid number

#### Scenario: The distribution and the board agree on colour
- **WHEN** I look at the distribution's 9 row and then open the score board and look at its 9 slot
- **THEN** the row's bar and numeral carry the same red the slot carries, and likewise for every other score

#### Scenario: Counts and shares stay neutral
- **WHEN** a distribution row is drawn in its tier colour
- **THEN** its count and its share are drawn in the block's ordinary text colour

#### Scenario: An empty track is still empty
- **WHEN** no included anime carries a given score
- **THEN** that row's track renders empty rather than filled with the score's tier colour

#### Scenario: The profile page's distribution is unchanged
- **WHEN** I open the profile page's all-anime score distribution
- **THEN** its bars are drawn in the single colour they use today, not in the score board's tiers

### Requirement: Drilling into a rating distribution row
Each row of the recap's rating distribution SHALL be followable as a whole — its score, its bar, its count, and its share forming one target rather than only the number being clickable — opening my list scoped to the recap's period, time filter, and media type and narrowed to the anime I gave that score.

The count the row shows and the number of anime my list then holds SHALL agree. As with the stat tiles, the narrowing SHALL arrive applied to my list's own score control rather than as a hidden scope. A row whose count is zero SHALL NOT offer a target — there is nothing for it to lead to — but SHALL still take the same accent hover/focus highlight the rows beside it take, so it reads as the same kind of row rather than as plain text.

Following a row SHALL NOT narrow by status: the distribution counts every included entry carrying that score, whatever its status.

#### Scenario: Opening a score's anime
- **WHEN** a 2020 recap's distribution shows 12 anime scored 8 and I follow that row
- **THEN** my list opens scoped to 2020 showing exactly those 12 anime

#### Scenario: The whole row is the target
- **WHEN** I move the pointer anywhere along a distribution row — over its score, its bar, its count, or its share
- **THEN** the same target is offered for all of them

#### Scenario: An empty score row
- **WHEN** I move the pointer over a score row whose count is zero
- **THEN** it takes the same accent highlight the rows around it take, but no target is offered and it cannot be followed

#### Scenario: Every status is included
- **WHEN** I follow a score row and the period holds both completed and dropped anime carrying that score
- **THEN** both are shown

### Requirement: Distribution rows highlight on hover
Each row of the recap's rating distribution SHALL adopt the app's standard accent hover treatment — the same tinted background, accent border, and elevation the recap's ranking rows, top-10 rows, and hot-take rows already use — on both pointer hover and keyboard focus, so a followable row reads as one of the project's cards rather than as plain text.

The treatment SHALL NOT change the row's size or position, and SHALL NOT change the width or alignment of any of the block's columns, so the bars stay comparable while the pointer moves down the block.

#### Scenario: A hovered distribution row
- **WHEN** I move the pointer over a row of the recap's rating distribution
- **THEN** that row takes the same accent highlight a ranking row takes

#### Scenario: Keyboard focus matches hover
- **WHEN** I reach a distribution row by keyboard rather than by pointer
- **THEN** it takes the same highlight it takes on hover

#### Scenario: Hover does not disturb the bars
- **WHEN** I move the pointer down the distribution
- **THEN** no row changes size or position, and every bar keeps the same track width and alignment it had

#### Scenario: The profile page's block is unchanged
- **WHEN** the profile page renders its own score distribution
- **THEN** its rows are presented exactly as before, without this treatment

### Requirement: The rating distribution opens a score board
The recap's rating distribution SHALL offer a control, beside its heading, that opens a **score board** — an overlay naming the anime behind the distribution's counts. The control SHALL sit in the distribution section's own header, alongside the heading rather than below or inside the block, so the block's rows keep the alignment and drill-through the existing requirements give them.

Opening the board SHALL NOT change the recap's period, time filter, ranking basis, or media-type narrowing, SHALL NOT be recorded in the page URL, and SHALL NOT add a history entry — pressing back from a recap with the board open SHALL leave the recap, as it does with the board closed.

When the period holds no entry carrying one of my scores, the control SHALL still be shown, but disabled and carrying an explanation of why, rather than being removed — so the section header holds its shape as the period and time filter change.

#### Scenario: Opening the board
- **WHEN** I use the control beside the **Rating distribution** heading
- **THEN** the score board opens over the recap, with the recap's period, time filter, ranking basis, and media-type narrowing all unchanged behind it

#### Scenario: The board is not a history entry
- **WHEN** I open the score board and then press back
- **THEN** I leave the recap, exactly as I would have with the board closed

#### Scenario: A period with nothing scored
- **WHEN** the period's included entries carry none of my scores
- **THEN** the control is shown disabled with an explanation, and the distribution block beside it renders its empty tracks as before

### Requirement: The score board lays a period out by score
The score board SHALL show ten slots, one per score from 10 down to 1 in that order, each holding the poster art of every included anime I gave that score. Each slot SHALL carry its score numeral and how many anime it holds, so the score of a slot is never conveyed by its colour alone.

The board SHALL cover exactly the set the rating distribution covers: every entry the selected period and time filter include that carries one of my scores, unnarrowed by the top 10's media-type control, and recomputed whenever the period or time filter changes. The number of posters in a slot and the count the matching distribution row reports SHALL always agree.

Within a slot, anime SHALL be ordered by my ranking, best-ranked first, per the `anime-ranking` capability, so a slot reads left to right as my order of preference among the anime of that score. An anime with no rank SHALL be placed after every ranked anime of that slot, ordered by title among other unranked anime. The order SHALL be stable across reloads and across a refresh of the same period. Anime with no score of mine SHALL NOT appear in any slot.

All ten slots SHALL be shown even when a slot holds nothing — an empty slot reports the same "nothing scored this" the distribution's empty track reports, and keeping the ten fixed makes two periods comparable. An empty slot SHALL be identifiable as empty rather than appearing to be still loading.

An anime with no poster art SHALL occupy a placeholder of the same size in its slot, so a slot's count and its number of tiles agree whatever art is available.

#### Scenario: Seeing the tens of a period
- **WHEN** I open the score board for a period in which I scored four anime 10
- **THEN** the 10 slot shows those four anime's posters, and reports a count of four

#### Scenario: A slot reads in my order
- **WHEN** I open the score board for a period whose 9 slot holds anime I have ranked
- **THEN** they are laid out best-ranked first rather than alphabetically

#### Scenario: An unranked anime in a slot
- **WHEN** a slot holds both ranked anime and a scored Plan-to-watch anime
- **THEN** the ranked ones come first and the Plan-to-watch one follows them

#### Scenario: The board and the distribution agree
- **WHEN** a distribution row reports 12 anime scored 8
- **THEN** the board's 8 slot holds exactly 12 tiles

#### Scenario: The media-type control does not narrow the board
- **WHEN** I narrow the top 10 to films only and then open the score board
- **THEN** every included anime of the period is still placed in its slot, films and everything else alike

#### Scenario: The board follows the time filter
- **WHEN** I switch a yearly recap's time filter and open the score board
- **THEN** the slots hold the newly included entries, matching the recomputed distribution

#### Scenario: Unscored anime are not on the board
- **WHEN** the period includes anime I have not scored
- **THEN** they appear in no slot, and no slot's count includes them

#### Scenario: A slot nothing was scored
- **WHEN** no included anime carries a score of 3
- **THEN** the 3 slot is still shown, identifiably empty rather than missing or apparently loading

#### Scenario: An anime with no poster
- **WHEN** an included anime has no poster art
- **THEN** its slot holds a placeholder of the same size in its place, and the slot's count still matches the distribution

### Requirement: Score slots are coloured by tier
Each slot of the score board SHALL be coloured by the tier its score belongs to, so the tier is readable before the numeral is:

- **10** SHALL carry a purple-and-white mixed treatment belonging to no other slot, marking it as the top of the ladder;
- **9** SHALL be red, **8** blue, and **7** gold;
- **6** and **5** SHALL share silver;
- **4**, **3**, **2**, and **1** SHALL share bronze.

Every tier colour SHALL be legible against the overlay's background in both the light and the dark theme, and the six tiers SHALL be distinguishable from one another. The 10 slot's treatment SHALL be distinguishable from the app's ordinary accent-coloured selected controls, and the 8 slot's blue SHALL be distinguishable from the blue the app uses for MAL scores, so neither tier reads as a control or as somebody else's opinion.

Tier colour SHALL be decoration on top of the numeral and count each slot already carries, never the only thing that identifies a slot.

#### Scenario: The tens are marked out
- **WHEN** I open the score board
- **THEN** the 10 slot carries a purple-and-white treatment that no other slot carries

#### Scenario: The tiers descend
- **WHEN** I look down the board from 10 to 1
- **THEN** 9 is red, 8 is blue, 7 is gold, 6 and 5 are silver, and 4 through 1 are bronze

#### Scenario: Both themes
- **WHEN** I view the board in the light theme and again in the dark theme
- **THEN** every slot's colour is legible against the overlay's background and tellable apart from the other tiers in both

#### Scenario: Colour is not the only signal
- **WHEN** two slots share a tier colour, as 6 and 5 do
- **THEN** each still carries its own score numeral and its own count

### Requirement: Board posters are drawn near their source resolution
Each poster tile on the score board SHALL be large enough that its poster is drawn near the source art's own pixel dimensions rather than at a steep reduction of them, which is what makes a poster read soft. On a display with a device pixel ratio of 2, a tile SHALL be drawn at no less than two thirds of the source art's pixel dimensions, and never larger than them.

Each tile's box SHALL follow the proportion of the poster art itself rather than a taller one, so a poster fills its tile without part of the artwork being cropped away to fit.

Nothing else about the board SHALL change: the same anime appear in the same slots in the same order, and every poster keeps the lift, the card, the link, and the keyboard behaviour required by "Board posters answer to the pointer and the keyboard".

#### Scenario: A poster reads sharply on a high-density display
- **WHEN** I open the score board on a display with a device pixel ratio of 2
- **THEN** each poster is drawn at no less than two thirds of its source art's pixel dimensions — a gentle reduction rather than the steep one that softened it — and never at more than them

#### Scenario: Posters are not cropped to a taller box
- **WHEN** I look at a poster on the board
- **THEN** it fills its tile at the proportion of the artwork, with no visible slice cut from its sides

#### Scenario: The board still fits its slots
- **WHEN** a slot holds many anime at the larger tile size
- **THEN** the grid still fills the overlay's width, wrapping onto further lines, and the overlay never scrolls sideways

#### Scenario: The board's behaviour is unchanged
- **WHEN** I hover, focus, and follow a poster at the new size
- **THEN** it lifts, shows its card, and opens the anime exactly as before, and its slot's count is unchanged

### Requirement: Board posters answer to the pointer and the keyboard
Each poster on the score board SHALL respond when the pointer is over it or when it is reached by keyboard: it SHALL lift out of its slot and a card SHALL appear naming the anime, the score whose slot it sits in, and its media type.

The card SHALL be fully visible wherever the poster sits — including in the board's first and last slots and at the overlay's left and right edges — rather than being clipped by the overlay or running off the viewport. The card SHALL NOT intercept the pointer, so moving across a slot moves cleanly from poster to poster.

Each poster SHALL be a link to that anime's page, carrying the anime's title as its accessible name, and following it SHALL close the board and open that page. Every poster SHALL be reachable by keyboard, and a poster reached by keyboard SHALL show the same card it shows on hover.

Under a reduced-motion preference the posters SHALL NOT animate, and the card SHALL still appear on hover and on focus.

#### Scenario: Pointing at a poster
- **WHEN** I move the pointer over a poster in the 9 slot
- **THEN** it lifts and a card appears naming the anime, its score of 9, and its media type

#### Scenario: A poster at the edge of the board
- **WHEN** I point at a poster at the far right of a slot, or in the board's topmost or bottommost slot
- **THEN** the whole card is visible rather than being cut off by the overlay or the window

#### Scenario: Opening an anime from the board
- **WHEN** I follow a poster
- **THEN** the board closes and that anime's page opens

#### Scenario: Reaching a poster by keyboard
- **WHEN** I move through the board with the keyboard
- **THEN** each poster can be focused and followed, and the focused poster shows the same card hovering it would

#### Scenario: Reduced motion
- **WHEN** I have asked my system for reduced motion
- **THEN** no poster animates, and hovering or focusing one still shows its card

### Requirement: The score board is dismissible like the recap's other overlays
The score board SHALL be dismissible by the Escape key, by clicking outside it, and by an explicit close control, in the same way the recap's "see all" ranking overlay is. It SHALL be scrollable within itself when it holds more than fits, leaving the recap behind it in place.

The board SHALL read as a plain, ordinary list: no slot's header bar SHALL pin, stick, or otherwise hold its position as the board scrolls past it — every element of the board, slot headers and poster tiles alike, SHALL move with the scroll exactly as any other block of content would. The only motion in the board that is not the ordinary scroll is a tile's own hover/focus lift, per "Board posters answer to the pointer and the keyboard", and the 10 slot's decorative motion, per "The 10 slot shares the podium's first-place motion".

Closing the board SHALL return the recap exactly as it was left, at the same scroll position.

#### Scenario: Closing with the keyboard
- **WHEN** I press Escape with the board open
- **THEN** the board closes and the recap is where I left it

#### Scenario: Closing by clicking away
- **WHEN** I click outside the board
- **THEN** it closes, as the ranking overlay does

#### Scenario: A period with hundreds of scored anime
- **WHEN** the board holds more than fits on screen
- **THEN** it scrolls within itself and the recap behind it does not move

#### Scenario: No slot header pins while scrolling
- **WHEN** I scroll through a slot that holds more anime than fits on screen
- **THEN** that slot's own header bar scrolls away with its posters like any other content, rather than staying fixed in place

### Requirement: The score board is restored with the recap page
The score board's open state SHALL be part of the recap page's restorable state. When a recap page that was left with the board open is returned to by back/forward navigation, the board SHALL be shown again over that page, holding the same period's scores it held before.

The board's own scroll position SHALL be restored with it, so a board that was scrolled a long way down its slots comes back at that position rather than at the top. Restoration SHALL happen once the board's slots have been laid out, so the recorded position is reachable rather than clamped to a shorter board.

Opening the board SHALL still add nothing to the browser's history and SHALL still put nothing in the address, so the board is neither a destination the back gesture stops at nor part of a shared link. A recap page left with the board closed SHALL be restored with it closed, and a fresh visit to the recap page SHALL open with the board closed as it does today.

Restoring the board SHALL leave the page behind it where it was: closing a restored board SHALL reveal the recap at the scroll position it was left at, exactly as closing the board does when it was never navigated away from.

#### Scenario: The board comes back
- **WHEN** I open the score board, follow one of its posters to an anime, and then go back
- **THEN** the score board is open again over the recap page, showing the same period

#### Scenario: The board comes back where I was in it
- **WHEN** I scroll the score board down to a low score, follow a poster, and then go back
- **THEN** the board is scrolled to the same place rather than back at the top slot

#### Scenario: A closed board stays closed
- **WHEN** I open the score board, close it, open an anime from the page behind it, and then go back
- **THEN** the recap page is restored with no board over it

#### Scenario: A fresh visit has no board
- **WHEN** I reach the recap page by clicking its navbar link
- **THEN** the score board is closed

#### Scenario: Opening the board is still not a history entry
- **WHEN** I open the score board and then press the browser's back gesture
- **THEN** I leave the recap page altogether rather than merely closing the board

#### Scenario: The page behind a restored board is where I left it
- **WHEN** I return to a recap page whose board reopens, and I then close the board
- **THEN** the recap page beneath is at the scroll position I left it at

### Requirement: The 10 slot shares the podium's first-place motion
The score board's 10 slot SHALL carry the same continuous decorative motion as the podium's first-ranked card, per "The podium is animated": a slow, continuous drift of light across the slot's own header bar, travelling across its whole surface rather than one edge or band of it, and present at every point of its cycle rather than sweeping off and leaving the bar inert until the next pass.

The motion's highlight SHALL be coloured so that it can never read as a different tier's colour — including silver, the colour shared by the 6 and 5 slots — no matter how the motion's layers combine at any point in its cycle. In both the light and the dark theme, the 10 slot's score numeral and count SHALL remain legible at every point of the motion's cycle, including where the motion's highlight passes directly behind them.

Where the user has asked their system for reduced motion, this motion SHALL be switched off, exactly as the podium's first-place motion is, per "The podium is animated".

#### Scenario: The 10 slot drifts like the podium's first card
- **WHEN** I watch the score board's 10 slot through several full cycles
- **THEN** the motion travels across the header's whole surface, is present at every instant, and reads as the same slow drift the podium's first-ranked card carries

#### Scenario: The motion never reads as a different tier's colour
- **WHEN** I watch the 10 slot's motion through a full cycle in either theme
- **THEN** its highlight never reads as silver or as any other tier's colour, even at the point in its cycle where the motion is brightest

#### Scenario: The numeral stays legible through the motion
- **WHEN** I watch the 10 slot's numeral and count through a full cycle in either theme
- **THEN** both remain readable at every point, including when the motion's highlight passes directly behind them

#### Scenario: Reduced motion
- **WHEN** I have asked my system for reduced motion
- **THEN** the 10 slot's header shows no motion, and its numeral and count remain fully legible

### Requirement: The stat block is headed and responds to hover
The recap's stat block SHALL carry its own heading naming it as the period's statistics, in the same style as the recap's other section headings, so it reads as a section of the page rather than as an unlabelled strip of numbers.

Each stat tile SHALL give visual feedback while the pointer is over it — distinct from its resting state — so the tile under the pointer is identifiable. The feedback SHALL NOT cause any layout reflow: no tile's box, and no tile's place in the grid, SHALL change as the pointer crosses the block, so tiles never shift one another around. A tile MAY lift or otherwise move within its own box as part of the feedback, provided nothing else on the page moves with it.

A tile that leads somewhere, per the "Drilling into a recap stat" requirement, SHALL be distinguishable from one that does not, **at rest as well as on hover**: each tile SHALL carry a persistent visual marker of which kind it is, so the distinction can be seen without moving the pointer. On hover, a tile that leads somewhere SHALL take the app's standard accent hover treatment — the same tinted background, accent border, and elevation its cards and rows use — show the pointer cursor, and be reachable and followable by keyboard, showing the same treatment on keyboard focus as on hover. Every other tile — an aggregate tile that never leads anywhere, and a followable stat whose own count is zero and so has nothing to lead to — SHALL take the quieter resting marker and the quieter hover feedback, SHALL NOT show the pointer cursor, and SHALL NOT be a keyboard stop. Hovering SHALL therefore never promise a destination that does not exist.

Every tile SHALL keep the same box — the same padding, border, and size — so the block reads as one grid. Each tile's figure SHALL be set in tabular figures so the numbers of two tiles side by side share one rhythm, and its label SHALL be visually subordinate to its figure.

#### Scenario: The stat block is labelled
- **WHEN** a recap renders its stats
- **THEN** the block carries a heading naming it, styled as the page's other section headings are

#### Scenario: A hovered tile is identifiable
- **WHEN** I move the pointer over one stat tile
- **THEN** that tile is visibly distinguished from the tiles around it

#### Scenario: Hover does not reflow the block
- **WHEN** I move the pointer across the stat tiles
- **THEN** no tile other than the hovered one changes in any way, and none of them changes the space it occupies in the grid

#### Scenario: Tiles that lead somewhere differ at rest
- **WHEN** I look at the stat block without moving the pointer
- **THEN** I can tell which tiles lead somewhere and which do not from a persistent marker on each

#### Scenario: A followable tile reads as clickable
- **WHEN** I move the pointer over **Completed** on a period with completed anime in it
- **THEN** it takes the accent highlight the app's cards take and the cursor shows it can be followed

#### Scenario: An aggregate tile does not read as clickable
- **WHEN** I move the pointer over **Time spent**
- **THEN** it gives quieter feedback than the followable tiles and the cursor does not mark it as clickable

#### Scenario: Figures line up
- **WHEN** two stat tiles sit side by side
- **THEN** their figures are set in tabular figures so the digits share one rhythm

#### Scenario: Keyboard reaches the followable tiles
- **WHEN** I move through the stat block by keyboard
- **THEN** each tile that leads somewhere can be focused and followed, showing the same highlight it shows on hover, and no other tile is a stop

### Requirement: Drilling into a recap stat
Each stat that describes a set of anime SHALL be followable, opening my list showing exactly the anime that stat counts. The followable stats are **In this period**, **Completed**, **Dropped**, **Movies watched**, and **Currently watching**. **Mean score**, **Episodes watched**, and **Time spent** describe an aggregate rather than a set and SHALL NOT be followable.

Following a stat SHALL open my list scoped to the same period, time filter, and media type the recap is showing, narrowed further to the stat's own rule:

- **In this period** — the whole included set, exactly as the recap's "See all in my list" control already opens it.
- **Completed** — the included entries whose status is Completed.
- **Dropped** — the included entries whose status is Dropped.
- **Movies watched** — the included entries whose media type is `movie` and which have at least one episode watched, whatever their status, matching what the tile counts.
- **Currently watching** — the entries with status Watching whose anime's air-start date falls inside the recap's period. Because that stat is counted on air date under **both** time filters, its target SHALL carry the period's aired-attributed scope even when the recap is showing **What I watched**, so the list and the tile describe the same set.

The number the tile shows and the number of anime my list then holds SHALL agree.

The narrowing SHALL arrive applied to my list's own filter controls rather than as a second hidden scope, so it is visible on the page and can be changed or cleared there. A stat whose narrowing leaves a control at its default SHALL leave that control alone.

A followable stat whose own count is zero SHALL NOT offer a target — there is nothing for it to lead to — and SHALL be presented exactly as an aggregate tile is: the same resting marker and the same quieter hover feedback, with no pointer cursor and no keyboard stop. A tile with the accent marker therefore always leads somewhere.

#### Scenario: Movies of a year
- **WHEN** I follow **Movies watched** on a 2020 recap under **What aired**
- **THEN** my list opens scoped to 2020 showing exactly the films of that year I have watched at least one episode of, and its row count matches the number the tile showed

#### Scenario: What I dropped
- **WHEN** I follow **Dropped** on a recap
- **THEN** my list opens scoped to that same period, filter, and media type, showing only its dropped entries

#### Scenario: The period total
- **WHEN** I follow **In this period**
- **THEN** my list opens showing the recap's whole included set, with no further narrowing applied

#### Scenario: Currently watching keeps its air-date basis
- **WHEN** a 2024 recap is set to **What I watched** and I follow **Currently watching**
- **THEN** my list opens on the anime that started airing in 2024 which I am currently watching — the same set the tile counted — rather than on my 2024 watch history

#### Scenario: The narrowing is visible on my list
- **WHEN** I arrive on my list by following **Completed**
- **THEN** my list's own status control shows Completed, and I can change or clear it there

#### Scenario: Aggregates are not followable
- **WHEN** I move the pointer over **Mean score**, **Episodes watched**, or **Time spent**
- **THEN** no target is offered for them

#### Scenario: A zero-count stat reads as an aggregate tile
- **WHEN** a recap has nothing dropped this period and I look at and then hover **Dropped**
- **THEN** it carries the same resting marker and the same quieter hover feedback **Time spent** carries, no target is offered, the cursor does not mark it as clickable, and it is not a keyboard stop

#### Scenario: The recap's media-type narrowing carries through
- **WHEN** a recap narrowed to films is showing and I follow one of its followable stats
- **THEN** my list opens with that same media-type narrowing in force alongside the stat's own

### Requirement: Recap rows highlight on hover
Every row of the recap page that links to an anime, a season, or a year — the top-10 rows below the podium, the hot-take rows, and the rows of all four rankings, inline and in the "See all" overlay alike — SHALL adopt the app's standard hover treatment: the same tinted background, coloured border, and elevation that the anime cards on my list, the season page, and the top-anime page already use, applied on both pointer hover and keyboard focus.

The colour that treatment is drawn in SHALL follow what the row is about, per the `section-colour-language` capability:

- a row of a ranking whose title is banded SHALL highlight in that ranking's family — season-level rankings in the season family, year-level rankings in the year family — inline and in the "See all" overlay alike, the overlay carrying the family of the ranking that opened it and banding its own title in it;
- a **hot-take row** SHALL highlight in the score role its own direction names: blue where MAL liked it more and purple where I liked it more, matching the direction label the row already carries. The hot-takes band itself SHALL stay the hot-take family's own colour, so the section is named by one colour while each row is answered by its own;
- every other followable row, including the top-10 rows below the podium and the rating-distribution rows, SHALL keep the page's accent.

Apart from that colour, the treatment SHALL be the same one those pages use rather than a recap-specific variant, so a hovered row reads identically wherever it appears. It SHALL NOT change a row's size or position, and a row's resting appearance SHALL be unaffected.

The podium cards are not rows and are covered by "The podium is animated" instead; their richer treatment SHALL NOT be applied to any row.

#### Scenario: A hovered top-10 row
- **WHEN** I move the pointer over a row of the recap's top 10
- **THEN** that row takes the same accent highlight an anime card takes on my list

#### Scenario: A hovered hot take answers in its own direction
- **WHEN** I move the pointer over a hot-take row labelled "MAL liked it more" and then over one labelled "I liked it more"
- **THEN** the first takes the highlight in MAL's blue and the second in my own purple, while the section's band stays the hot-take family's colour

#### Scenario: A hovered ranking row
- **WHEN** I move the pointer over a row of any of the recap's rankings
- **THEN** that row takes the standard highlight drawn in that ranking's family colour

#### Scenario: An overlay row keeps its ranking's family
- **WHEN** I open the "See all" overlay from **Seasons by time watched** and hover one of its rows
- **THEN** the overlay's title is banded in the season family and the row highlights in that same family

#### Scenario: Keyboard focus matches hover
- **WHEN** I reach one of these rows by keyboard rather than by pointer
- **THEN** it takes the same highlight it takes on hover

#### Scenario: Highlighting does not reflow the list
- **WHEN** I move the pointer down a list of rows
- **THEN** no row changes size or position as it gains or loses the highlight

### Requirement: Hot takes
Every recap SHALL report up to five **hot takes** — the included entries where my score and MAL's community score disagree sharply enough, and in a definite enough direction, to count as a disagreement rather than as ordinary variation. An included entry SHALL be eligible only when it has both my score and a MAL score and it satisfies one of the two rules the profile page's opinion-divergence lists apply:

- **They liked it, I didn't** — the divergence runs at least one standard deviation in MAL's direction, my score is at or below the "average" boundary of the personal scale, and MAL's community score is at or above the community-like boundary.
- **I liked it, they didn't** — the divergence runs at least one standard deviation in my direction, my score is at or above the "very good" boundary of the personal scale, and MAL's community score is at or below that same community boundary.

The thresholds and boundaries SHALL be the ones the profile page already uses, drawn from a single shared definition rather than restated, so an anime that is a hot take in a recap is one the profile page also names, and one the profile page does not name never appears as a hot take.

Qualifying hot takes SHALL be ordered by the size of the divergence, largest first, with ties broken by title case-insensitively so the order is stable across reloads. Each hot take SHALL show the anime, both scores, and which direction the divergence runs, so a pick I rated far above the community reads differently from one I rated far below it.

Divergence SHALL be measured on a common scale rather than by subtracting a 1–10 personal score from a MAL community average directly, using the same normalisation the profile page's opinion-divergence lists already apply.

When fewer than five included entries qualify, the recap SHALL show those that do; when none qualify — including when my list holds too few scored pairs for the normalisation to be computed at all — it SHALL say so rather than present near-agreements as hot takes.

While the global hide-scores toggle is on, the recap SHALL render only those of its hot takes whose entry is **Completed, Dropped, or Rewatching**, and SHALL omit the rest **entirely** rather than rendering them with a placeholder or a reveal control. A hot take states which way the disagreement runs as row text, and qualifying at all bounds the MAL score on one side, so the row leaks a bound on the score it is hiding whatever its own score cell renders. This is the rule the profile page's opinion-divergence lists apply to the same anime for the same reason, and the two SHALL agree.

Because the five are selected by divergence before this rule is applied, omission SHALL leave **fewer than five hot takes, possibly none**, rather than promoting the next-most-divergent settled entry in a dropped one's place — selection is decided by divergence alone and SHALL NOT depend on the viewer's hide state. When it leaves none, the recap SHALL show the same "no hot takes" message it shows when nothing qualified. While the hide toggle is off, every selected hot take SHALL render, whatever its status.

#### Scenario: Five hot takes
- **WHEN** a recap's included set holds more than five entries that satisfy either rule
- **THEN** the five with the largest divergence are shown, largest first, each naming both scores

#### Scenario: Ordered by the size of the disagreement
- **WHEN** several entries qualify by different margins
- **THEN** they are ordered by how far apart the two scores are, largest first

#### Scenario: A near-agreement is not a hot take
- **WHEN** an included entry has both scores but its divergence falls short of one standard deviation
- **THEN** it is not shown as a hot take, however few other entries qualify

#### Scenario: A high score MAL also liked is not a hot take
- **WHEN** I scored an anime 9 and MAL's community score for it is 8.4
- **THEN** it is not shown as a hot take, since neither rule's like/dislike boundaries are crossed

#### Scenario: Agreement with the profile page
- **WHEN** an anime appears in one of the profile page's opinion-divergence lists and falls inside a recap's included set
- **THEN** that anime is eligible to appear as a hot take in that recap

#### Scenario: Divergence direction is visible
- **WHEN** a hot take is one I scored far above MAL's average, and another is one I scored far below it
- **THEN** the two are distinguishable on screen rather than presented identically

#### Scenario: Fewer than five qualify
- **WHEN** only two included entries satisfy either rule
- **THEN** two hot takes are shown

#### Scenario: An unsettled hot take is omitted while scores are hidden
- **WHEN** the hide toggle is on and one of a recap's hot takes is an entry I am Watching, have On-hold, or Plan to watch
- **THEN** it is not rendered at all — no row, no direction label, no reveal control — while the settled hot takes beside it are rendered as before

#### Scenario: Omission does not promote a replacement
- **WHEN** the hide toggle is on and two of a recap's five hot takes are unsettled
- **THEN** three hot takes are shown, and the sixth-most-divergent entry is not brought in to replace either omitted one

#### Scenario: Every hot take omitted
- **WHEN** the hide toggle is on and every hot take the recap selected is an entry I have not settled
- **THEN** the recap shows the same "no hot takes for this period" message it shows when nothing qualified

#### Scenario: Hiding omits nothing once scores are shown
- **WHEN** the hide toggle is off
- **THEN** every hot take the recap selected is rendered, whatever its status

#### Scenario: Nothing qualifies
- **WHEN** no included entry satisfies either rule
- **THEN** the recap states that there are no hot takes for this period

#### Scenario: Too few scored pairs to normalise
- **WHEN** my whole list holds too few entries scored by both me and MAL for the shared normalisation to be computed
- **THEN** the recap states that there are no hot takes for this period rather than ranking on an uncomputable divergence

### Requirement: Hot take rows read as columns
The hot-take list SHALL align its trailing cells into columns: my score, MAL's score, and the divergence-direction label each occupying a fixed column across every row, so scores read down against scores and the direction labels down against each other. The anime title SHALL absorb the remaining width.

The columns SHALL hold their alignment regardless of a row's title length, of how many digits either score has, and of whether the MAL score is hidden by the score-visibility setting. The two direction labels — "MAL liked it more" and "I liked it more" — differ in length, so the label column SHALL be sized for the longer of the two rather than to each row's own text.

#### Scenario: Scores line up down the list
- **WHEN** a recap shows five hot takes with titles of differing lengths
- **THEN** each row's my-score sits directly above and below the others', and likewise each row's MAL score

#### Scenario: Direction labels line up
- **WHEN** one hot take is labelled "MAL liked it more" and the next "I liked it more"
- **THEN** the two labels occupy the same column, neither shifting the scores beside it

#### Scenario: A hidden MAL score does not shift the row
- **WHEN** scores are hidden and one hot take's MAL score renders as concealed
- **THEN** that row's columns stay aligned with the rows around it

### Requirement: The top five are presented as a podium
The recap SHALL present the five highest-ranked anime of its top 10 as a podium of cards rather than as rows, so the best of a period is identifiable at a glance rather than only by reading its numeral.

The podium SHALL be laid out as a descending row: the cards SHALL sit side by side in rank order, the first-ranked card the largest and leading the row, each following card no larger than the one before it, and the fourth- and fifth-ranked cards SHALL be visibly smaller than the third. Every card's lower edge SHALL sit on one line, so the row's silhouette steps down from first to fifth. The podium SHALL occupy the full width available to the top-10 section rather than being capped short of it, so the section holds no empty band beside the cards.

Each card SHALL carry, at minimum:

- its rank, in a badge coloured for that rank — gold for first, silver for second, bronze for third, and for fourth and fifth a neutral badge visibly subordinate to the three medals and distinct from all of them, in both the light and the dark theme;
- the anime's picture at a size that reads as artwork rather than as a thumbnail, with a placeholder of the same size when the anime has no picture. A poster fills the card's poster-sized picture area. A picture that is not a poster is drawn whole and centred in that same area, so every card keeps its size whatever its picture's shape, with no blurred fill and no box behind it, only the card's own surface around it;
- the anime's title, up to two lines, with the full title available on hover when it is truncated;
- its score on the currently selected ranking basis, in the app's existing score language for that basis.

Every card's score SHALL sit in a box of one shared size — the same box on the fifth card's smaller card as on the first card's larger one — wide enough for the widest value either ranking basis can print, which is a two-digit score under my scores and a two-decimal figure under MAL's, and set in tabular figures so the five scores read as a column.

The whole card SHALL be one link to the anime's page, and following it SHALL open the same page the equivalent row would have.

The five cards SHALL appear in rank order in the page's reading and keyboard order, first-ranked first. Where the display is too narrow for five cards side by side, the podium SHALL reflow onto fewer cards per line — and at the narrowest into a single column, each card at the full width of the column — always in rank order, filling each line before starting the next. Once the cards no longer share one line the descending sizes MAY be dropped, since cards on different lines cannot be compared by size.

The podium SHALL show only as many cards as the period has entries: two entries SHALL produce two cards and one entry a single card, with no placeholder or empty card standing in for a missing rank.

#### Scenario: The best of a period stands out
- **WHEN** a recap with ten or more entries is shown
- **THEN** its top five appear as cards in one descending row, the first-ranked card largest and leading, the fourth and fifth smaller than the third, and every card's lower edge on one line

#### Scenario: A landscape picture does not change the card
- **WHEN** the podium's first-ranked anime has a landscape picture and the others hold posters
- **THEN** the first card is exactly the height it would be with a poster, is still the widest card and leads the row, and the whole picture is drawn centred in its picture area with no blurred fill and no box behind it, and the row still steps down from first to third, with the fourth and fifth the same height

#### Scenario: A square picture is drawn whole
- **WHEN** a podium card's anime has a square picture
- **THEN** the whole picture is drawn across the card's picture width, centred, with no blurred fill and no box behind it, and the card's height, badge, title and score box are unchanged

#### Scenario: An upright picture is drawn whole
- **WHEN** a podium card's anime has an upright picture, such as a 4:5 picture
- **THEN** the card is the same size it would be with a poster, and the whole picture is drawn centred in its picture area with no blurred fill and no box behind it

#### Scenario: The podium fills its section
- **WHEN** the top-10 section is wider than the podium's cards need
- **THEN** the five cards spread to fill that width rather than leaving an empty band beside them

#### Scenario: Rank is colour-coded
- **WHEN** I look at the podium
- **THEN** the first, second, and third cards carry gold, silver, and bronze rank badges, the fourth and fifth carry a neutral badge subordinate to those three, and each is legible in both the light and the dark theme

#### Scenario: Every card's score box is the same size
- **WHEN** the podium shows five cards of different widths
- **THEN** the five score boxes are identical in size and aligned as a column, and a score of `10` fits its box without wrapping or clipping

#### Scenario: A MAL score fits the same box
- **WHEN** I switch the ranking basis to MAL's scores
- **THEN** each card's two-decimal MAL figure fits the same box the my-score figures used, with no card's box resizing

#### Scenario: A card opens its anime
- **WHEN** I follow any podium card
- **THEN** that anime's page opens, exactly as following its row would have

#### Scenario: Reading order follows rank
- **WHEN** I move through the podium by keyboard or with a screen reader
- **THEN** I reach the first-ranked card first, then the second, and so on to the fifth

#### Scenario: Narrow displays reflow the podium
- **WHEN** the display is too narrow for five cards side by side
- **THEN** the cards reflow onto fewer per line in rank order, filling each line before the next

#### Scenario: The narrowest display stacks the podium
- **WHEN** the display is too narrow for even two cards side by side
- **THEN** the cards stack in one column in rank order, first-ranked at the top, each at the full width of the column

#### Scenario: A period with two entries
- **WHEN** a recap's included set holds only two anime
- **THEN** two cards are shown and no empty third, fourth, or fifth card appears

#### Scenario: A long title does not change the card
- **WHEN** one card's title needs two lines and another's needs one
- **THEN** both cards are the same height, and the truncated title is available in full on hover

### Requirement: The podium is animated
The podium SHALL be animated: its cards SHALL arrive with motion when a period's results are shown rather than appearing statically, and SHALL respond to the pointer with motion as well as with colour. The first-ranked card SHALL carry a continuous decorative motion that distinguishes it from the others.

That decorative motion SHALL be a slow drift of light across the card's surface, and SHALL satisfy all of:

- **Whole-card, not a band.** It SHALL travel across the card's whole surface rather than being confined to one edge or strip of it.
- **Continuous.** There SHALL be no part of its cycle in which the card carries no visible motion — the effect SHALL NOT sweep off the card and leave it inert until the next pass.
- **Slow.** Its pace SHALL be markedly slower than the card's pointer response, reading as an ambient drift rather than as a repeated sweep.
- **The poster is excluded.** The anime's poster art SHALL NOT be tinted, lit, or animated by it; the motion passes around the artwork, not over it.
- **The score moves with the card.** The card's score box SHALL be lit by the same pass, at the same moment and in the same way as the card surface behind it, rather than sitting inert on top of the motion.

No animation SHALL move, resize, or reflow any other element on the page, and none SHALL be required in order to read, follow, or operate the podium — a card is fully usable at every point of every animation.

The podium SHALL re-animate its arrival whenever the set it is showing genuinely changes — a new period, time filter, ranking basis, or media-type narrowing — and SHALL NOT re-animate on a re-render that leaves the same anime in the same order.

Where the user has asked their system for reduced motion, every one of these animations SHALL be switched off — the arrival, the pointer response, and the decorative motion alike — leaving the podium fully legible and fully operable, with the colour part of the pointer response still shown.

#### Scenario: The podium arrives with motion
- **WHEN** a recap's results are shown
- **THEN** the cards animate into place rather than appearing instantly, and the page around them does not shift as they do

#### Scenario: A card responds to the pointer
- **WHEN** I move the pointer over a podium card
- **THEN** it responds with motion as well as with a colour change, and nothing else on the page moves

#### Scenario: First place is distinguished
- **WHEN** the podium is shown
- **THEN** the first-ranked card carries a continuous decorative motion the other cards do not

#### Scenario: The motion covers the card
- **WHEN** I watch the first-ranked card
- **THEN** the motion travels across the card's whole surface rather than only across a band at one edge

#### Scenario: The motion never stops
- **WHEN** I watch the first-ranked card through several full cycles
- **THEN** at no point does the card sit still waiting for the next pass

#### Scenario: The score lights with the card
- **WHEN** the motion passes the first-ranked card's score
- **THEN** the score box lights in the same pass and the same way as the card surface behind it

#### Scenario: The poster is left alone
- **WHEN** the motion passes over the first-ranked card
- **THEN** the anime's poster art is not tinted, lit, or moved by it

#### Scenario: Re-animating on a new set
- **WHEN** I change the period, the time filter, the ranking basis, or the media-type narrowing
- **THEN** the podium animates into place again for the new set

#### Scenario: Reduced motion
- **WHEN** I have asked my system for reduced motion
- **THEN** the podium shows no arrival animation, no decorative motion, and no motion on hover, and remains fully legible and followable with its colour hover feedback intact

### Requirement: The recap's segmented controls show every state
Every segmented control on the recap page — the recap-type tabs, the time filter, and the ranking-basis toggle — SHALL make each of its states visually distinct from every other:

- **unselected at rest**, and **unselected under the pointer**, which SHALL differ from it;
- **selected**, which SHALL be unmistakable as the current choice next to its unselected siblings;
- **selected under the pointer**, which SHALL differ visibly from selected at rest, so moving the pointer onto the option that is already chosen still gives feedback rather than appearing inert;
- **focused by keyboard**, which SHALL show a visible focus indicator distinct from the hover treatment;
- **disabled**, which SHALL read as unavailable and SHALL show neither the hover nor the focus treatment.

All three groups SHALL use the same state *treatments* as one another — the same fill for a selected option, the same tint for a hovered one, the same kind of focus indicator — so the six states are learned once and read the same way in every group.

An individual option MAY carry a colour family (per the `section-colour-language` capability) in place of the page's default accent, in which case every one of its states SHALL be drawn in that family's colours through the treatments above, and no state SHALL be dropped, weakened, or merged with another because of the family it carries. Specifically, the **Yearly** and **Season** recap-type tabs SHALL carry the year and season families; the **Multi-year** tab and both time-filter options SHALL keep the page's accent; and the ranking-basis toggle's options SHALL carry the score-role colours per the "Top 10 ranking basis control" requirement.

None of these states SHALL change the control's height or width, so the cluster a control sits in SHALL NOT reflow as the pointer moves across it, and the controls SHALL remain the same height as the selects and other controls beside them.

#### Scenario: Hovering the already-selected option
- **WHEN** I move the pointer over the recap-type tab that is already selected
- **THEN** it changes visibly from its resting selected appearance

#### Scenario: Hovering an unselected option
- **WHEN** I move the pointer over an unselected option
- **THEN** it changes visibly from its resting appearance, and still reads as unselected next to the selected one

#### Scenario: Keyboard focus is visible
- **WHEN** I reach one of these controls with the keyboard
- **THEN** a visible focus indicator is shown, distinct from the hover treatment

#### Scenario: A disabled filter reads as unavailable
- **WHEN** a time filter option is unavailable because the period holds nothing for it and I move the pointer over it
- **THEN** it reads as unavailable and takes neither the hover nor the focus treatment

#### Scenario: The control cluster does not reflow
- **WHEN** I move the pointer along a row of these controls
- **THEN** none of them changes size and nothing beside them moves

#### Scenario: The groups agree on their treatments
- **WHEN** a recap shows both its type tabs and its time filter
- **THEN** the selected option in each is drawn with the same treatment as the selected option in the other, differing only where an option carries its own colour family

#### Scenario: A family-coloured tab keeps every state
- **WHEN** I look at the **Season** tab unselected, hover it, select it, hover it while selected, and reach it with the keyboard
- **THEN** all five states are as distinct from one another as the **Multi-year** tab's are, drawn in the season family's colours instead of the accent

#### Scenario: Selected is unmistakable whichever family a tab carries
- **WHEN** the **Yearly** tab is selected beside the unselected **Multi-year** and **Season** tabs
- **THEN** it reads as the current choice even though the three tabs carry three different colours

### Requirement: Top 10 of the period
Every recap SHALL show the ten highest-ranked anime of the included set, ranked by the selected ranking basis. When the basis is **my score**, entries sharing a score SHALL be ordered by my ranking, best-ranked first, per the `anime-ranking` capability — a tied entry with no rank falling after every ranked entry of that score, and remaining ties broken by title case-insensitively. When the basis is **MAL's score**, ties SHALL be broken by title case-insensitively as before. Either way the order SHALL be stable across reloads. Entries with no score on the selected basis SHALL be ranked below every scored entry rather than treated as zero. When the included set holds fewer than ten entries, the recap SHALL show all of them.

The ten SHALL be presented in two forms: the first five as the podium described in "The top five are presented as a podium", and the remaining ranks as rows below it. The split SHALL be presentational only — the same ten anime in the same order are shown either way — and the rows SHALL continue the podium's numbering rather than restarting, both in the rank each row shows and in the list semantics exposed to assistive technology.

The score a row shows SHALL carry the same score colour role the podium's cards carry: my score in the mine role, MAL's score in the MAL role, per the `score-presentation` capability. Falling below the podium SHALL change a score's size and its surround, never whose opinion it is — a row's score SHALL NOT revert to neutral text.

#### Scenario: Ten of many
- **WHEN** the included set holds 40 anime
- **THEN** the ten highest-ranked are shown in descending order

#### Scenario: Fewer than ten
- **WHEN** the included set holds six anime
- **THEN** all six are shown

#### Scenario: The ten split into a podium and rows
- **WHEN** a recap shows ten anime
- **THEN** the first five are shown as podium cards and ranks six through ten as rows beneath them, numbered six through ten

#### Scenario: Five or fewer entries
- **WHEN** the included set holds five or fewer anime
- **THEN** they are all shown on the podium and no row list is shown

#### Scenario: My ranking decides a tie at the cut line
- **WHEN** the top 10 is ranked by my score and several anime share the score at the cut line
- **THEN** the one I rank highest takes the slot, and the same order is shown on every reload

#### Scenario: MAL ties still break by title
- **WHEN** the top 10 is ranked by MAL's score and several anime share that score
- **THEN** they are ordered by title, as before, since my ranking says nothing about MAL's opinion

#### Scenario: An unranked entry tied on my score
- **WHEN** the top 10 is ranked by my score and a scored Plan-to-watch anime shares a score with ranked anime
- **THEN** the ranked ones come first and the Plan-to-watch one follows them

#### Scenario: Unscored entries rank last
- **WHEN** the included set holds both scored and unscored anime on the selected basis
- **THEN** every scored anime is ranked above every unscored one

#### Scenario: The score colour survives the cut line
- **WHEN** a recap ranked by my score shows rank five on the podium and rank six as a row
- **THEN** both scores carry the mine role's colour, the row's differing only in the size and surround its density calls for

#### Scenario: A row's score follows the basis
- **WHEN** the top 10 is switched to MAL's score
- **THEN** the scores on ranks six through ten carry the MAL role's colour rather than the mine role's

### Requirement: A top 6-10 row's score is inset from the row's edge
The score at the trailing edge of each top 6-10 row SHALL be inset from the row's border, so a visible gap separates the score from the border rather than the score sitting against the row's padding edge. The inset SHALL be the same on every row of the list, so the scores stay in one column.

Nothing else about the row SHALL move: its rank, poster, and title keep their positions and the row keeps its height, so the list does not reflow.

#### Scenario: The score has room at the row's end
- **WHEN** a recap shows ranks six through ten
- **THEN** each row's score sits clear of the row's right border rather than against it

#### Scenario: The scores stay in a column
- **WHEN** the rows show scores of differing widths
- **THEN** each score is inset by the same amount, so they line up as a column

#### Scenario: Nothing else moves
- **WHEN** I compare a top 6-10 row before and after this change
- **THEN** its rank, poster, title, and height are unchanged

### Requirement: A top 6-10 row's title is capped
The anime title on each of the recap's top 6-10 rows SHALL be capped at a fixed maximum width, narrower than the row itself, so a long title is truncated at that cap rather than running the full width of the row. The cap SHALL be the same on every row of the list, and SHALL scale with the row's own text size rather than being fixed against a viewport.

A title shorter than the cap SHALL be shown in full and SHALL NOT be padded out to it. A title cut off by the cap SHALL be truncated with an ellipsis and SHALL remain available in full on hover, as it is today. Where the row is narrower than the cap, the title SHALL still shrink to what the row leaves it, so the cap never widens a row or forces it to scroll.

Nothing else about the row SHALL move: its rank, its poster, the column its score sits in, and the row's height are all unchanged.

#### Scenario: A long title stops at the cap
- **WHEN** a top 6-10 row holds an anime whose title is longer than the cap
- **THEN** the title is truncated with an ellipsis at the cap rather than spanning the row

#### Scenario: A short title is untouched
- **WHEN** a row holds a short title
- **THEN** it is shown in full, with no padding out to the cap

#### Scenario: The full title is still reachable
- **WHEN** I hover a truncated title
- **THEN** the full title is shown

#### Scenario: The cap is the same on every row
- **WHEN** ranks six through ten hold titles of differing lengths
- **THEN** every truncated one is cut at the same width

#### Scenario: A narrow row still fits
- **WHEN** the recap is shown on a display too narrow for the cap
- **THEN** the title truncates at what the row leaves it, and the row neither widens nor scrolls sideways

#### Scenario: Nothing else moves
- **WHEN** I compare a top 6-10 row before and after this change
- **THEN** its rank, poster, score column, and height are unchanged

### Requirement: Top 10 ranking basis control
The recap SHALL let the top 10 be ranked by my score or by MAL's community score whenever the included set is an aired-in-period selection — that is, on a season recap, and on a multi-year or yearly recap whose time filter is **What aired**. Under the **What I watched** filter the control SHALL NOT be offered and the ranking SHALL be by my score.

The control's two options SHALL carry the colour of the score role each selects: **My score** the mine role, **MAL score** the MAL role, per the `score-presentation` capability's requirement that a score-role control carries its role's colour. The option carrying the MAL role SHALL NOT be drawn in the purple this app reserves for my own score.

MAL scores shown in the recap SHALL follow the app's existing MAL-score visibility rules, so a hidden score stays hidden here as it does elsewhere.

#### Scenario: Switching to MAL's ranking
- **WHEN** a season recap's top 10 is switched to MAL's score
- **THEN** the ten are re-ranked by MAL's community score

#### Scenario: Basis control hidden under watch history
- **WHEN** a yearly recap's time filter is **What I watched**
- **THEN** no ranking-basis control is offered and the top 10 is ranked by my score

#### Scenario: Basis control available on a season recap
- **WHEN** a season recap is shown
- **THEN** the ranking-basis control is offered, since a season recap always selects on what aired

#### Scenario: Hidden MAL scores stay hidden
- **WHEN** MAL scores are hidden by the global toggle and a recap row carries one
- **THEN** that score is hidden in the recap under the same rules as on every other page

#### Scenario: The basis control wears its role's colour
- **WHEN** I select **MAL score** and then **My score**
- **THEN** the selected option is blue in the first case and purple in the second, matching the scores the ranking then shows

### Requirement: Top 10 media-type control
The recap SHALL let the top 10 be narrowed to one media type — TV, movie, OVA, ONA, special, and so on — offering all types alongside only those types actually present in the included set, so a control is never offered for a type the period holds none of. Narrowing by media type SHALL re-rank the top 10 within that type. The stat block SHALL continue to describe the whole included set rather than the narrowed one.

#### Scenario: Narrowing to films
- **WHEN** I narrow a recap's top 10 to movies
- **THEN** only films from the included set are ranked, and the ten (or fewer) best of them are shown

#### Scenario: Absent types are not offered
- **WHEN** the included set holds no ONA entries
- **THEN** no ONA option is offered

#### Scenario: Stats are unaffected by the type control
- **WHEN** the top 10 is narrowed to movies
- **THEN** the stat block still reports mean score, counts, and time spent for the whole included set

### Requirement: Mid-page recap controls hold the scroll position
Changing the top 10's **ranking basis**, its **media-type narrowing**, or the recap's **period** SHALL leave the page at the scroll position it was at, so the section being read stays where it is on screen instead of jumping away. None of these changes what the recap is about: the first two sit within the top 10's own header partway down the page, and the third swaps the data for another period of the same kind.

Holding the position on a period change SHALL apply to every control that selects a period without changing the recap's kind: the year select and its stepper arrows in yearly mode, the season select, the year select, and both stepper arrows in season mode, and the from-year and to-year selects in multi-year mode.

The controls that do change the recap's kind or its membership rule — the recap-type tabs and the time filter — SHALL continue to return to the top of the page, as SHALL following any link away from the recap.

When a period change lands on a period where the selected time filter has nothing to show, the automatic fall-back to the other filter SHALL preserve the held position rather than undoing it, since the user changed the period and not the filter.

A held scroll position SHALL be remembered for the entry it belongs to: after changing the ranking basis, the media type, or the period, following a link away from the recap, and returning with back, the recap SHALL be restored at the position it was held at rather than at the top.

Every other page's scroll behaviour SHALL be unchanged, including pages that deliberately return to the top when a control in the page URL changes.

#### Scenario: Switching ranking basis
- **WHEN** I scroll down to the top 10 and switch it from my score to MAL's
- **THEN** the ten re-rank in place and the page stays exactly where it was

#### Scenario: Narrowing by media type
- **WHEN** I narrow the top 10 to films
- **THEN** the list narrows in place and the page stays exactly where it was

#### Scenario: Stepping to the next year
- **WHEN** I scroll partway down a yearly recap and press its next-year arrow
- **THEN** the year's data is replaced in place and the page stays exactly where it was

#### Scenario: Choosing a year from the select
- **WHEN** I scroll partway down a yearly recap and choose another year from its year select
- **THEN** the page stays exactly where it was

#### Scenario: Stepping to the next season
- **WHEN** I scroll partway down a season recap and press its next-season arrow
- **THEN** the page stays exactly where it was, including when the step rolls over into the following year

#### Scenario: Changing a multi-year range
- **WHEN** I scroll partway down a multi-year recap and change its from-year or to-year select
- **THEN** the page stays exactly where it was

#### Scenario: A period change that also changes the filter
- **WHEN** I step to a year that has nothing completed or dropped in it, so the recap falls back from the watched filter to the aired one
- **THEN** the page stays where it was rather than being returned to the top by the fall-back

#### Scenario: Switching the recap type still returns to the top
- **WHEN** I switch from a yearly recap to a season recap, or change the time filter myself
- **THEN** the page returns to the top, as it does today

#### Scenario: Coming back to a held position
- **WHEN** I step the period partway down a recap, open an anime from the top 10, and press back
- **THEN** the recap is restored at the position I stepped it at, not at the top

#### Scenario: Back undoes the switch
- **WHEN** I switch the ranking basis and press back
- **THEN** the previous basis is restored, as it is today

#### Scenario: Back undoes a period step
- **WHEN** I step the recap to another year and press back
- **THEN** the previous year's recap is shown, as it is today

### Requirement: Opening the period in my list
When the included set — after any media-type narrowing — holds more than ten anime, the recap SHALL offer a control that opens the my-list page already scoped to that same period, time filter, and media type, showing every one of those anime rather than the top ten. When the set holds ten or fewer, the control SHALL NOT be offered, since the recap is already showing all of them.

#### Scenario: More than ten in the period
- **WHEN** a recap's included set holds 40 anime
- **THEN** a control is offered that opens my list scoped to those 40

#### Scenario: Exactly ten in the period
- **WHEN** a recap's included set holds exactly ten anime
- **THEN** no such control is offered

#### Scenario: Narrowing below the threshold
- **WHEN** narrowing by media type brings the set to eight anime
- **THEN** the control is no longer offered

#### Scenario: The scope carries across
- **WHEN** I open my list from a recap of fall 2019 narrowed to TV
- **THEN** my list opens showing exactly the TV anime that recap included

### Requirement: Season ranking
A multi-year or yearly recap under the **What aired** filter SHALL rank the anime seasons its period covers, best first, so the user can see which season they enjoyed most. A yearly recap SHALL rank the four seasons of that year; a multi-year recap SHALL rank every season its years cover. Only seasons holding at least one included anime that I scored SHALL be ranked; a season I watched nothing from SHALL be omitted rather than shown as empty or zero.

Each ranked season SHALL show its name and year, how many of its anime I scored, and the score it was ranked on. The ranking SHALL NOT be shown on a season recap (its period is a single season) nor under the **What I watched** filter, where the included set is not organised by air date.

At most five seasons SHALL be shown at once. When more than five qualify, the ranking SHALL offer a control that opens an overlay listing every qualifying season in rank order — the same treatment the year ranking already gives its own overflow.

#### Scenario: Ranking the seasons of a year
- **WHEN** a 2022 yearly recap is set to **What aired** and I scored anime from three of its four seasons
- **THEN** those three seasons are ranked best first and the fourth is omitted

#### Scenario: Ranking across many years
- **WHEN** a multi-year recap covers 2011 through 2020 under **What aired**
- **THEN** the five best-ranked seasons are shown and a control opens an overlay listing every qualifying season, each labelled with its season and year

#### Scenario: Five or fewer seasons
- **WHEN** only four seasons qualify
- **THEN** all four are shown and no overlay control is offered

#### Scenario: Not shown on a season recap
- **WHEN** a season recap is shown
- **THEN** no season ranking is offered

#### Scenario: Not shown under watch history
- **WHEN** a yearly recap's time filter is **What I watched**
- **THEN** no season ranking is offered

### Requirement: Year ranking
A multi-year recap under the **What aired** filter SHALL rank the years its period covers, best first, on the same basis as the season ranking. Only years holding at least one included anime that I scored SHALL be ranked.

At most five years SHALL be shown at once. When more than five qualify, the recap SHALL offer a control that opens an overlay listing every qualifying year in rank order. The ranking SHALL NOT be shown on a yearly or season recap, whose period is a single year or less.

The year ranking's placement relative to the season ranking is governed by the "Rankings grouped into a season column and a year column" requirement.

#### Scenario: Ranking a decade
- **WHEN** a multi-year recap covers 2011 through 2020 under **What aired** and I scored anime from eight of those years
- **THEN** the five best-ranked years are shown, and a control opens an overlay listing all eight

#### Scenario: Five or fewer years
- **WHEN** only four years qualify
- **THEN** all four are shown and no overlay control is offered

#### Scenario: Years with nothing watched are omitted
- **WHEN** I scored nothing that debuted in 2013 within the selected range
- **THEN** 2013 does not appear in the ranking or in the overlay

#### Scenario: Not shown on a yearly recap
- **WHEN** a yearly recap is shown
- **THEN** no year ranking is offered

#### Scenario: Both rankings sit side by side
- **WHEN** a multi-year recap shows both a season ranking and a year ranking on a wide display
- **THEN** the two are presented next to each other rather than one above the other

### Requirement: Rankings grouped into a season column and a year column
Where a multi-year recap shows more than one ranking, the rankings SHALL be laid out in two columns grouped by the level they rank: the year ranking and the years-by-time-watched ranking in one column, the season ranking and the seasons-by-time-watched ranking in the other, with the **year column leading** — placed first, on the left, and the season column beside it. A period's year story therefore reads down the leading column and its season story down the other, rather than the score rankings occupying one row and the time rankings another.

Within a column, the score ranking SHALL come before the time-watched ranking. A column whose rankings are both absent SHALL NOT be rendered as an empty column; where only one column has any ranking to show, that column SHALL occupy the layout on its own.

The two columns' rankings SHALL align row for row across the layout: the year-level and season-level score rankings SHALL begin on the same line, and so SHALL the two time-watched rankings, whatever differences in height the rankings above them have. In particular, a ranking that shows every one of its rows offers no "See all" control while the ranking beside it may offer one — and that difference SHALL NOT lift the ranking below it out of line with its counterpart. This alignment SHALL be achieved without introducing a "See all" control for a ranking that is already showing every row.

A yearly recap has no year-level rankings, so this two-column grouping does not apply to it. Where a yearly recap shows both the season ranking and the seasons-by-time-watched ranking, the two SHALL instead be laid out side by side in separate columns — the season ranking leading — rather than stacked one above the other, so a period's best season and most-watched season are directly comparable. Where only one of the two has rows, it SHALL occupy the layout on its own.

On a display too narrow for two columns, the rankings SHALL stack in a single column: for a multi-year recap, year rankings before season rankings; for a yearly recap, the season ranking before seasons-by-time-watched.

#### Scenario: A multi-year recap's four rankings
- **WHEN** a multi-year recap under **What aired** shows a season ranking, a year ranking, and both time-watched rankings
- **THEN** the year ranking and years-by-time-watched sit in the leading (left) column, and the season ranking and seasons-by-time-watched in the column beside it

#### Scenario: Order within a column
- **WHEN** a column shows both a score ranking and a time-watched ranking
- **THEN** the score ranking is shown above the time-watched one

#### Scenario: A five-year recap's time rankings stay in line
- **WHEN** a 2020–2024 recap shows a year ranking of exactly five years, which offers no "See all" control, beside a season ranking of twenty seasons, whose overflow offers one
- **THEN** years-by-time-watched and seasons-by-time-watched still begin on the same line

#### Scenario: No redundant overflow control
- **WHEN** a ranking is already showing every one of its rows
- **THEN** no "See all" control is offered for it, whatever the ranking beside it offers

#### Scenario: Only the season column has rankings
- **WHEN** a multi-year recap has season-level rankings to show but no year-level ranking has any rows
- **THEN** the season column occupies the layout on its own, with no empty year column beside it

#### Scenario: A yearly recap has no year column
- **WHEN** a yearly recap under **What aired** shows only the season-level rankings
- **THEN** no empty year column is rendered beside them

#### Scenario: A yearly recap's two rankings sit side by side
- **WHEN** a yearly recap under **What aired** shows both a season ranking and a seasons-by-time-watched ranking
- **THEN** the two are presented in separate columns next to each other, the season ranking leading, rather than one stacked above the other

#### Scenario: Narrow display stacks the columns
- **WHEN** a multi-year recap showing both columns is displayed too narrow for two columns
- **THEN** the rankings stack in one column with the year rankings before the season rankings

#### Scenario: Narrow display stacks a yearly recap's two rankings
- **WHEN** a yearly recap showing both the season ranking and seasons-by-time-watched is displayed too narrow for two columns
- **THEN** the two stack in one column with the season ranking first

### Requirement: Bayesian ranking of seasons and years
The season and year rankings SHALL rank on a Bayesian weighted average rather than a raw mean, so a season carrying one 10 does not outrank a season of many strong scores. The weighted score SHALL be computed as:

`W = (v / (v + m)) * R + (m / (v + m)) * C`

where `R` is the mean of my scores for the anime attributed to that season or year, `v` is how many of them I scored, `C` is my mean score across every scored entry in my whole list (not merely the recap period), and `m` is the count at which a group is treated as fully trusted — **5** for a season and **20** for a year.

Because `C` is drawn from the whole list, the same season SHALL rank identically whether it is reached through a yearly recap or a multi-year one covering the same year.

Groups SHALL be ordered by the following comparisons, each applied only when every comparison before it has tied:

1. **Weighted score, highest first**, compared at full precision — the whole computed value, every decimal of it, not the two decimals the row displays. A group whose weighted score is higher by any amount SHALL rank above one whose score is lower, and no comparison below SHALL ever overrule that. Two groups SHALL be treated as tied only when their weighted scores are exactly equal, which the comparisons below then resolve.
2. **Number of scored anime, most first.**
3. **Score by score, from 10 down to 1**: at the highest score where the two groups hold a different number of my anime, the group holding more ranks first. Two groups agreeing at every score from 10 down to 9 but where one holds three 8s to the other's two are therefore separated at 8, and the comparison continues to 1 if needed.
4. **Recency, newest first** — the later year, or the later season, ahead of the earlier one.

This ordering SHALL be identical wherever these rankings are rendered, the profile page's Favourite seasons and Favourite years included, and SHALL be stable across reloads.

#### Scenario: A thin season is pulled toward the global mean
- **WHEN** one season holds a single anime I scored 10 and another holds eight anime averaging 8.5, and my global mean is 7.4
- **THEN** the eight-anime season ranks above the single-anime one

#### Scenario: A well-covered season keeps its average
- **WHEN** a season holds well over five scored anime
- **THEN** its weighted score sits close to its raw mean

#### Scenario: Consistent across recap modes
- **WHEN** I compare fall 2019's rank in a 2019 yearly recap against its rank in a 2011–2020 multi-year recap
- **THEN** the same weighted score is reported for that season in both

#### Scenario: Different trust thresholds
- **WHEN** a season and a year each hold ten scored anime
- **THEN** the season is treated as past its trust threshold while the year is not, and the year is pulled further toward the global mean

#### Scenario: A higher score is never overruled
- **WHEN** two years both display a weighted score of 7.80 but one's underlying score is higher past the second decimal
- **THEN** the higher-scoring year ranks first, whatever their scored counts or score histograms

#### Scenario: The better-covered group wins an exactly equal score
- **WHEN** two years' weighted scores are exactly equal but one was computed over 40 scored anime and the other over 25
- **THEN** the 40-anime year ranks first

#### Scenario: Equal score and equal coverage resolved score by score
- **WHEN** two years' weighted scores are exactly equal, each computed over 50 scored anime, and the first holds two 10s where the second holds one
- **THEN** the first year ranks ahead, on the strength of its extra 10

#### Scenario: The comparison continues below the top scores
- **WHEN** two seasons' weighted scores are exactly equal, they hold the same number of scored anime, and they hold identical numbers of 10s and 9s but one holds more 8s than the other
- **THEN** the season with more 8s ranks first

#### Scenario: Wholly identical groups fall back to recency
- **WHEN** two years' weighted scores are exactly equal, they hold the same number of scored anime, and they hold the same number of my anime at every score from 10 down to 1
- **THEN** the newer year ranks ahead of the older one

### Requirement: The season and year rankings can be ranked by how many of a score they hold
The recap page's **Season ranking** and **Year ranking** SHALL each offer the same score filter the profile page's favourites rankings offer, as defined by the `profile-stats` capability's "Favourites rankings can be ranked by how many of a score they hold": an **All** button, the words **With most:**, and one button per score from **10 down to 1**, sitting between the ranking's title and its rows. Its presentation SHALL be identical on both pages — the score-tier colours, the family colour on **All**, the single button width, and no size change between states.

A score SHALL be offered only when at least one group in that ranking holds at least one anime I scored it, computed over the ranking as the current period produced it, so no offered button can produce an empty ranking. **All** SHALL always be offered. Selecting a score SHALL re-rank that ranking by how many anime of that score each group holds, most first, omitting every group holding none, with ties falling back to the ranking's own order — the identical rule the profile page applies. The five-row cap, the "See all" overlay and its naming of the selection, and the row's figures all follow that same requirement.

The two rankings SHALL hold **independent** selections, and neither SHALL be affected by the other. The two time-watched rankings SHALL NOT offer the control: they rank on time watched and hold no per-score figure to rank by.

Each selection SHALL be carried in the page URL alongside the recap's period, time filter, ranking basis, and media type, so a filtered ranking can be linked to and reloads to what it was showing. Selecting a score SHALL hold the page's scroll position, as the ranking basis and media-type controls do, rather than returning to the top.

Changing the period SHALL keep a selection that the new period's ranking still offers. A selection naming a score the currently shown ranking does not offer SHALL read as **All** rather than emptying the ranking, and SHALL NOT be discarded — stepping back to a period that does offer that score SHALL show the selection again.

The control SHALL appear only where its ranking appears: neither ranking is shown under the **What I watched** filter, the year ranking is shown only on a multi-year recap, and neither is shown on a season recap.

#### Scenario: The control is offered under each ranking's title
- **WHEN** a multi-year recap under **What aired** shows its Season ranking and Year ranking
- **THEN** each shows an All button and a "With most:" row of score buttons between its title and its rows

#### Scenario: All is the default
- **WHEN** I open a recap without touching the control
- **THEN** both rankings are on All and ranked exactly as they are by weighted average

#### Scenario: Ranking the seasons by how many 9s
- **WHEN** I select 9 in the Season ranking
- **THEN** the seasons are re-ranked with the season holding the most anime I scored 9 first, numbered from one, and seasons holding no 9 are omitted

#### Scenario: The two rankings are independent
- **WHEN** I select 10 in the Year ranking
- **THEN** the Season ranking is still on All

#### Scenario: The time rankings offer no filter
- **WHEN** a recap shows seasons-by-time-watched or years-by-time-watched
- **THEN** neither offers a score filter

#### Scenario: A selection is addressable
- **WHEN** I select 8 in the Season ranking and reload the page
- **THEN** 8 is still selected and the ranking is still ranked by it

#### Scenario: Selecting a score holds the scroll position
- **WHEN** I scroll down to a ranking and press one of its score buttons
- **THEN** the ranking re-ranks in place and the page does not jump to the top

#### Scenario: Stepping to a period without that score
- **WHEN** 3 is selected and I step to a period whose ranking holds no anime I scored 3
- **THEN** that ranking is shown on All rather than empty

#### Scenario: Stepping back restores the selection
- **WHEN** I step back to a period whose ranking does hold anime I scored 3
- **THEN** 3 is the selection again

#### Scenario: No control where there is no ranking
- **WHEN** a recap's time filter is **What I watched**, or the recap is a season recap
- **THEN** no score filter is shown, since neither score ranking is shown

### Requirement: Top three posters for every ranked season and year
Every row of the season ranking and of the year ranking SHALL display the posters of three of my anime from that season or year, so each ranked row is illustrated rather than only the leader. Where fewer than three qualify, those available SHALL be shown. This SHALL hold for the rows shown inline and for every row of the "See all" overlay alike.

Which three, and in what order, SHALL follow **my ranking** — the single total order the `anime-ranking` capability defines — rather than a title comparison. The group's best-ranked anime SHALL be shown first and the others in ranking order after it, laid out left to right so the best-ranked poster is leftmost. Because that ranking orders by my score before anything else, the three SHALL still be my highest-scored anime of the group; what changes is that two anime I scored the same are separated by where I placed them, not by their titles. An anime the ranking does not cover — one I scored but have at Plan to watch, or one that has not aired — SHALL fall after every ranked anime carrying its score, ordered among such anime by title case-insensitively, matching the nulls-last treatment the recap's own top 10 and score board already apply.

Where a ranking is showing a score selection, per "The season and year rankings can be ranked by how many of a score they hold", each row's posters SHALL instead be drawn from **the selected score alone**: the three best-ranked anime of that group carrying that score, in the same ranking order. A row's illustration therefore always agrees with the figure the row was ranked on. With no score selected the posters are the group's three best overall, as above.

The same rules SHALL apply wherever these rankings are rendered, the profile page's Favourite seasons and Favourite years included, since both surfaces rank the same groups on the same basis.

The two time-watched rankings are not covered by this requirement. The seasons-by-time-watched ranking shows no posters on any row, including the leading one. The years-by-time-watched ranking continues to illustrate its leading row alone, with posters picked by episodes watched.

#### Scenario: Every ranked season is illustrated
- **WHEN** a season ranking shows five seasons, each holding at least three scored anime
- **THEN** all five rows show three posters, not only the first

#### Scenario: Every ranked year is illustrated
- **WHEN** a year ranking shows five years
- **THEN** all five rows show the posters of three of my anime from that year

#### Scenario: My ranking breaks a tie at one score
- **WHEN** a ranked season holds four anime I scored 9 and none higher
- **THEN** the three I rank highest are shown, in my ranking's order, rather than the three whose titles come first alphabetically

#### Scenario: The best-ranked poster is leftmost
- **WHEN** a row shows three posters
- **THEN** the best-ranked of the three is the leftmost and the others follow it in ranking order

#### Scenario: Score still leads
- **WHEN** a ranked year holds one anime I scored 10 and several I scored 8
- **THEN** the 10 is shown first, whatever my ranking says about the 8s

#### Scenario: An anime outside the ranking falls last
- **WHEN** a ranked season holds a scored Plan-to-watch anime and three ranked anime carrying the same score
- **THEN** the three ranked ones are shown and the Plan-to-watch one is not

#### Scenario: Posters follow a score selection
- **WHEN** a ranking is showing the selection **8** and a row holds anime I scored 10, 9, and 8
- **THEN** that row's posters are its best-ranked anime scored 8, not its 10s and 9s

#### Scenario: Posters return to the group's best under All
- **WHEN** I return a ranking to **All**
- **THEN** every row's posters are its three best-ranked anime again, whatever their scores

#### Scenario: Fewer than three available
- **WHEN** a ranked season holds only two scored anime
- **THEN** that row shows two posters

#### Scenario: Fewer than three at the selected score
- **WHEN** a ranking is showing the selection 7 and a row holds only one anime I scored 7
- **THEN** that row shows one poster

#### Scenario: The overlay matches the inline list
- **WHEN** I open the "See all" overlay of a season or year ranking
- **THEN** every row in it shows posters under the same rule as the inline rows, including under a score selection

#### Scenario: Seasons by time watched shows no posters
- **WHEN** the seasons-by-time-watched ranking is shown
- **THEN** no row, including the leading one, shows posters

#### Scenario: Years by time watched keeps leader-only posters
- **WHEN** the years-by-time-watched ranking is shown
- **THEN** only its leading row shows posters, picked by episodes watched rather than by my ranking

### Requirement: The recap's ranking rows state their own figures
Each row of the season ranking and of the year ranking SHALL state the figures it was actually ranked on. Under **All** a row SHALL state how many of my anime the group was computed over and the weighted score it was ranked on. Under a score selection it SHALL state how many anime of the selected score the group holds alongside how many of my anime it was computed over, exactly as the profile page's favourites rankings do.

No row SHALL show a placeholder, an empty value, or a figure belonging to another row's position in the list.

#### Scenario: An unfiltered row states its score and coverage
- **WHEN** a season ranking is shown on All and its first row covers six of my anime at a weighted score of 7.64
- **THEN** that row reads as six scored at 7.64

#### Scenario: Every unfiltered row is stated the same way
- **WHEN** a season ranking shows five rows on All
- **THEN** each states its own scored count and weighted score, and none states a count of a score it was not ranked by

#### Scenario: A filtered row states the count it was ranked on
- **WHEN** the Year ranking is showing the selection 9
- **THEN** each row states how many anime I scored 9 it holds, alongside how many of my anime it was computed over

### Requirement: Ranking rows share a single height
Every row of every ranking the recap page renders — the season ranking, the year ranking, and both time-watched rankings, inline and in the "See all" overlay alike — SHALL occupy the same minimum height, whether or not that row carries posters. A ranking that shows no posters SHALL therefore stand the same height as one that does, so two rankings placed side by side read as a matched pair rather than as one full list beside one compressed list.

The shared height SHALL be the height a poster-carrying row already resolves to, so applying this rule changes no illustrated row's size. The same rule SHALL apply wherever these rankings are rendered, the profile page's Favourite seasons and Favourite years included.

#### Scenario: A yearly recap's two season rankings match
- **WHEN** a yearly recap shows the season ranking beside seasons-by-time-watched, the first illustrated with posters and the second not
- **THEN** the rows of the two rankings are the same height

#### Scenario: Illustrated rows are unchanged
- **WHEN** a ranking whose rows carry three posters is rendered
- **THEN** its rows stand at the same height they did before this rule applied

#### Scenario: A partly illustrated ranking is even
- **WHEN** the years-by-time-watched ranking shows posters on its leading row only
- **THEN** every row of that ranking, illustrated or not, is the same height

#### Scenario: The overlay matches the inline list
- **WHEN** I open a ranking's "See all" overlay
- **THEN** its rows follow the same shared height as the inline rows

### Requirement: The recap's "See all" overlay behaves as the profile's does
The overlay a recap ranking's "See all" control opens SHALL be the same overlay the profile page's favourites rankings open, and SHALL therefore be sized and dismissed by the rule the `profile-stats` capability defines for it: eight whole rows on open with no part of a ninth visible, its height derived from row height rather than from the viewport, the rest reached by scrolling, and no close control of its own — neither a Close button beneath the list nor a ✕ in its corner — leaving Esc and a click outside.

The overlay's own content SHALL be unchanged: which rows it lists, their order, their posters, the family it carries from the ranking that opened it, and where following a row leads SHALL be exactly as they are today. Rankings that show every one of their rows SHALL still offer no "See all" control at all.

#### Scenario: Eight whole rows from a recap ranking too
- **WHEN** I open the "See all" overlay from **Seasons by time watched** on a recap with more than eight qualifying seasons
- **THEN** eight rows are visible in full, no part of a ninth is shown, and the rest are reached by scrolling

#### Scenario: Dismissed the same way
- **WHEN** that overlay is open
- **THEN** it closes on Esc or on a click outside, and it offers no close control of its own — no Close button beneath the list and no ✕ in its corner

#### Scenario: The ranking's family is still carried
- **WHEN** I open the "See all" overlay from a season-level ranking
- **THEN** its title is banded in the season family and its rows highlight in that same family

### Requirement: Empty periods are stated plainly
When a selected period includes no entries under either time filter, the recap SHALL say so plainly and offer the period controls so another period can be chosen, rather than rendering empty stats, a zero mean, or a blank top 10. When a period includes at least one entry, the recap SHALL render in full — stats, top 10, and any applicable rankings — however small the set.

#### Scenario: A period with nothing in it
- **WHEN** I select a period in which I watched nothing and nothing I have watched aired
- **THEN** the recap states that there is nothing to recap for that period, and the period controls stay available

#### Scenario: A period with one entry
- **WHEN** a period includes exactly one entry
- **THEN** the recap renders its stats, a top 10 holding that one anime, and any applicable rankings

### Requirement: Recap entry point in the navigation bar
The primary navigation SHALL offer a Recap destination alongside the app's other top-level pages, reachable from anywhere without first opening a picker. Following it SHALL open the recap page on the yearly mode for the current calendar year under the **What aired** time filter, so the default recap answers "what came out this year".

The nav entry SHALL be marked as the active destination whenever the recap page is open, whatever period that page is currently showing. When the default period would include no entries under **What aired**, the recap page's existing fallback rules SHALL apply rather than the nav entry choosing a different period.

#### Scenario: Opening a recap from the nav bar
- **WHEN** I follow the Recap entry in the navigation bar
- **THEN** the recap page opens on the current year under **What aired**, without an intervening picker

#### Scenario: Nav entry reflects the open page
- **WHEN** I am on the recap page showing fall 2019
- **THEN** the Recap nav entry is shown as the active destination

#### Scenario: Nothing aired this year yet
- **WHEN** the current year holds no entries under **What aired** but does hold entries I completed
- **THEN** the recap falls back to **What I watched** under the existing fallback rule rather than reporting the year empty

### Requirement: Scoping my list to a period from my list
My list SHALL offer a period control that narrows the list in place to the anime of a chosen period, rather than navigating away to the recap page. Choosing a period SHALL apply that period's included set — the same set the recap of that period would cover, under the same period mode, time filter, and media type — as a scope on the list, leaving every other list filter, sort, and grouping control untouched and still usable on top of it.

The control SHALL sit in my list's page header, beside the page title, set apart from both the status filter tabs and the filter/sort controls, per the `library-views` capability's "Recap a period control on my list". The applied scope SHALL be shown as a dismissible indicator naming the period, the time filter, and the media type — a compact chip in my list's results line rather than a banner — and dismissing it SHALL return the list to its unscoped contents.

The scope indicator SHALL offer a link to the full recap of that same period, filter, and media type, so the recap page remains one step away from a scoped list.

#### Scenario: Choosing a period scopes the list
- **WHEN** I choose fall 2019 from my list's period control
- **THEN** my list narrows to the anime that recap includes, and I stay on my list

#### Scenario: The period control sits in the page header
- **WHEN** my list is shown
- **THEN** the period control is presented in the page header beside the title, not in the status-tab row and not among the filter or sort controls

#### Scenario: List controls still apply over a scope
- **WHEN** a period scope is applied and I then filter to Completed and sort by score
- **THEN** the list shows the completed anime of that period ordered by score

#### Scenario: Dismissing the scope
- **WHEN** I dismiss the scope indicator
- **THEN** my list returns to its full contents with my other filters unchanged

#### Scenario: Opening the recap from a scoped list
- **WHEN** my list is scoped to fall 2019 narrowed to TV and I follow the scope indicator's recap link
- **THEN** the recap page opens on fall 2019 with the same media-type narrowing

### Requirement: Recap page section order
The recap page SHALL present its sections in a fixed order, leading with the period's top anime rather than with its statistics:

1. The top anime of the period, with the stat block beside it — and the period's rating distribution directly below that stat block — positioned to the **right** of the top anime on a display wide enough for the two to sit side by side. On a narrow display these SHALL stack, with the top anime first and the stat block, then the distribution, below it rather than hidden or truncated.
2. The rankings — season, year, and most time watched — laid out per the "Rankings grouped into a season column and a year column" requirement.
3. The hot takes.

The period controls, mode tabs, time filter, and period stepper SHALL remain above all of these, so the period can be changed without scrolling past the recap.

#### Scenario: Top anime leads the page
- **WHEN** a recap renders for a period holding entries
- **THEN** the top anime is the first content below the period controls, with the stat block to its right

#### Scenario: The distribution sits under the stats
- **WHEN** a recap renders its stat block
- **THEN** the period's rating distribution is shown directly below it, above the rankings

#### Scenario: Narrow display stacks the two
- **WHEN** the recap is shown on a display too narrow for a side-by-side layout
- **THEN** the top anime is shown first with the stat block and its distribution below it, all fully readable

#### Scenario: Hot takes come last
- **WHEN** a recap shows stats, a top anime list, rankings, and hot takes
- **THEN** the hot takes appear after every ranking

### Requirement: Most time watched ranking
A recap that qualifies for a season or year ranking SHALL additionally rank the same groups by how much time I spent watching their anime, largest first, so a period I consumed the most of is visible separately from the one I scored highest. The groups ranked SHALL match the score ranking's level: seasons on a yearly recap, and both seasons and years on a multi-year recap.

Time watched for a group SHALL be the total runtime of the episodes I watched across the included anime attributed to that group, computed on the same basis as the recap's **Time spent** stat. Only groups with more than zero time watched SHALL be ranked; a group whose anime I have watched no episodes of SHALL be omitted rather than shown as zero. Each ranked row SHALL show the group's name, the time watched, and how many episodes that covers. Ties SHALL be broken by episodes watched, then chronologically, so the order is stable across reloads.

Unlike the score rankings, this ranking SHALL NOT require a group's anime to be scored — time watched is a fact about what I watched, not about what I rated.

#### Scenario: Ranking a year's seasons by time
- **WHEN** a 2022 yearly recap is set to **What aired**
- **THEN** its seasons are ranked by total time watched, largest first, each row naming the time and the episode count

#### Scenario: Unscored anime still count toward time
- **WHEN** I watched a full season of anime in summer 2021 but scored none of it
- **THEN** summer 2021 appears in the time-watched ranking despite being absent from the score ranking

#### Scenario: Groups with nothing watched are omitted
- **WHEN** a season's included anime are all entries I have watched no episodes of
- **THEN** that season does not appear in the time-watched ranking

#### Scenario: Not shown where the score rankings are not
- **WHEN** a recap's time filter is **What I watched**, or a season recap is shown
- **THEN** no time-watched ranking is offered, matching the score rankings' own gate

### Requirement: The recap's supporting text sits on one legible scale
The recap's supporting text SHALL be set large enough to read as part of the page beside its artwork rather than as fine print beneath it. This SHALL apply to the top-10 section — both the podium cards' titles and the rank, title, and score of every row beneath them — the stat tiles' figures and labels, the rating distribution's rows, the rows of all four season and year rankings, and the hot-take rows.

Raising the scale SHALL preserve the existing hierarchy rather than flatten it: a stat tile's label SHALL remain visually subordinate to its figure, and a ranking row's meta text SHALL remain subordinate to its label.

Raising the scale SHALL NOT break any layout the smaller text fitted: no text SHALL be clipped or overflow its box, the stat block SHALL keep its grid in the lead column beside the top 10, every rating-distribution row's bar track SHALL still start and end on one line with the others, and ranking rows SHALL still share one height per the "Ranking rows share a single height" requirement.

#### Scenario: The top 10 reads at page weight
- **WHEN** I look at the top-10 section
- **THEN** the podium cards' titles and the rows' rank, title, and score are set larger than the page's smallest supporting text and read comfortably beside the poster art

#### Scenario: Stats, distribution, rankings, and hot takes follow
- **WHEN** I look at the stat block, the rating distribution, the season and year rankings, and the hot takes
- **THEN** each is set on the same raised scale as the top 10 rather than at its previous smaller size

#### Scenario: Hierarchy survives the increase
- **WHEN** I look at a stat tile and a ranking row
- **THEN** the tile's label is still visibly subordinate to its figure, and the row's meta text still subordinate to its label

#### Scenario: Nothing overflows at the larger size
- **WHEN** the recap is shown at the width where the stat block sits beside the top 10, and again where it goes full width
- **THEN** no figure, label, title, or meta text is clipped or spills out of its box, and the distribution's bars all start and end on one line

### Requirement: Recap section titles are banded by family
The recap page SHALL present the titles of the sections whose subject a colour family names as bands, using the banded-title treatment the `section-colour-language` capability defines:

- **Biggest Hot takes** SHALL carry the hot-take family;
- **Season ranking** and **Seasons by time watched** SHALL carry the season family;
- **Year ranking** and **Years by time watched** SHALL carry the year family.

Each band SHALL span the full width of the list it heads, from that list's left edge to its right edge, with its title centred on it. On a multi-year recap, where the four rankings sit in two columns, each band SHALL span its own column rather than the grid, so a band always measures the list directly beneath it.

A season-level ranking and a year-level ranking SHALL therefore be tellable apart by colour alone, which matters most on a multi-year recap where the two sit side by side in adjacent columns saying nearly the same words.

The page's remaining section titles — **Top N**, **Stats**, and **Rating distribution** — SHALL stay plain and unbanded: none of them is about a season, a year, or a disagreement, and banding every title would leave the banded ones saying nothing.

Banding a ranking's title SHALL NOT change anything else about that ranking: its rows, its shared row height, its posters, its "See all" control and overlay, and the two-column grouping of the four rankings SHALL be exactly as they are unbanded.

#### Scenario: A season ranking and a year ranking are tellable apart
- **WHEN** a multi-year recap shows a season ranking beside a year ranking
- **THEN** the two bands are filled in different families' colours, so which column ranks seasons and which ranks years is readable before either title is read

#### Scenario: A band spans its own column
- **WHEN** a multi-year recap shows its four rankings in two columns
- **THEN** each band runs the full width of the ranking directly beneath it and no wider

#### Scenario: Both of a level's rankings share a family
- **WHEN** a multi-year recap shows **Season ranking** and **Seasons by time watched** in one column
- **THEN** both bands carry the season family

#### Scenario: Hot takes carry their own family
- **WHEN** a recap shows its **Biggest Hot takes** section
- **THEN** its band carries the hot-take family, distinct from every other family on the page

#### Scenario: Plain titles stay plain
- **WHEN** a recap renders its **Top N**, **Stats**, and **Rating distribution** headings
- **THEN** each is drawn as a plain heading with no band behind it

#### Scenario: A banded ranking behaves identically
- **WHEN** a ranking whose title is banded holds more rows than it shows
- **THEN** its "See all" control opens the same overlay with the same rows in the same order as it does unbanded
