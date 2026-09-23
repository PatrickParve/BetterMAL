## ADDED Requirements

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

## MODIFIED Requirements

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
