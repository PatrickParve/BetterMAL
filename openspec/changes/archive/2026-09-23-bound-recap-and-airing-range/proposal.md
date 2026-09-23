## Why

The Season and Year pages now refuse any year or season outside winter 1917 through the navigable ceiling. The Recap and Airing pages have no such limit, and a URL can send either one somewhere absurd:

- **Recap.** `renderPeriodControls` builds its year dropdown from `min(EARLIEST_YEAR, startYear, endYear)` to `max(currentYear, startYear, endYear)`, and those values come straight from `?year=`, `?from=` and `?to=`. So `/recap?year=32932734` asks for ~33 million `<option>` elements, which is the same unbounded allocation that locked up the Year page. The same URL also calls `/api/recap`. The API only builds a `DateOnly` from the year, so anything past 9999 or below 1 comes back as a `500`. A range like `from=1&to=9999` is accepted and walks 40,000 season points to answer. The season-mode "next" arrow also goes up to fall of the current year, which is past the current season. No recap can have anything in it yet for a season that hasn't started.
- **Airing.** `?week=` accepts any `YYYY-MM-DD`, from `0001-01-01` to `9999-12-31`. The page asks the API for that week. `9999-12-31` overflows `DateOnly` and returns a `500`. The next-week arrow has no end, and the year dropdown (1960 through next year) shows no matching year for a date outside it. The stored airing rows run from 2014 to at most about nine months ahead (latest today: 2027-06-27), so anything much past next year is guaranteed empty.

A reversed multi-year range (`from=2024&to=2020`) also breaks the recap today. The API puts it the right way round, the page then doesn't recognise the reply as its own, and it stays on the previous period's figures, or shows nothing on a fresh load. The From dropdown can produce this without anyone typing a URL.

## What Changes

**Recap page**

- A recap period runs from winter 1917 (the shared `EARLIEST_YEAR`) to the **current season**, which is the newest period a recap can show. For yearly and multi-year recaps, the latest year is the current year.
- Loading the page from a URL with a period outside that range, or a malformed one, replaces it with no message, no error state and no recap request. This uses the same guard/view split and `<Navigate replace>` as the Season and Year pages. A season recap is replaced with the current season. A yearly recap's year is replaced with the current year. Each out-of-range end of a multi-year range gets its default: last year for the start, the current year for the end. A reversed multi-year range is put the right way round. An unrecognised `mode` is dropped, which resolves to yearly as it does today. Parameters that aren't present still take their defaults without the URL being rewritten, and every other parameter is kept.
- The year dropdowns list only 1917 through the current year. They no longer widen to include the URL's year, which fixes the crash. In the current year, the season dropdown stops at the current season. Choosing the current year while a later season is selected moves the season back to the current one, as the Season page does. The season-mode "next" arrow stops at the current season.
- In the multi-year controls, choosing a start year after the end year moves the end year to match, and the reverse. The range can't be reversed from the page.
- The **Recap a period** picker on My list offers the same range. Its years run from its availability-based earliest year (never before 1917) to the current year. Its seasons stop at the current season. It can't send the recap page, or My list's recap scope, to a period the recap page would refuse.
- `GET /api/recap` returns `400` for any year or season outside winter 1917 through the current season, before the service is called. That also removes the `500`s.

**Airing page**

- The schedule covers **1 January 1917 through 31 December of next year**. That's at least a full year beyond the current one, and it's where the year dropdown already stops. The stored data reaches about nine months ahead, so this adds no reachable empty years. It also keeps "year" as the unit, like every other bounded page. A two-year limit would add a year of weeks that are always empty.
- Loading the page from a URL whose `?week=` is not a real calendar date, or falls outside that range, replaces it with today's week. There's no message and no schedule request for the rejected week. A missing `?week=` still means today, as before.
- The year dropdown runs from next year down to 1917 instead of 1960. The previous-week arrow is unavailable on the first week of 1917. The next-week arrow is unavailable once no later week starts on or before 31 December of next year. From the last days of December it lands on the final week rather than skipping past it.
- `GET /api/airing` returns `400` for a `week` outside the range, before the schedule is read. That also removes the `500` at the top of `DateOnly`'s range.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `list-recaps`: the recap's period range becomes winter 1917 through the current season, covering the page's controls, the Recap a period picker, the URL and the API. Year choices no longer widen to include a URL-supplied year. The season stepper and the season dropdown stop at the current season. A reversed multi-year range is corrected in the URL. The multi-year controls can't produce one.
- `airing-schedule`: week navigation is bounded to 1 January 1917 through 31 December of next year. The year selector starts at 1917 instead of 1960. The week arrows stop at both ends. A `?week=` outside the range, or not a real date, is replaced with today's week. The API refuses such weeks.

## Impact

- **Frontend:**
  - `pages/RecapPage.tsx` is split into a guard and a view. It drops its local `isSeasonName`/`currentSeasonName` for the shared ones in `utils/browseRange.ts`, and `yearOptions` is replaced with `yearsInRange`.
  - `components/RecapPickerOverlay.tsx`: bounded year and season options.
  - `pages/AiringPage.tsx`: guard, range constants, bounded arrows and year list.
  - `utils/browseRange.ts` is reused as it is. `isAddressableSeason`/`isAddressableYear` already express the recap's rule when the current season is the ceiling.
- **Backend:**
  - `Controllers/RecapController.cs` and `Controllers/AiringController.cs` each gain a range check and an `IBroadcastLocalTimeConverter` dependency.
  - The recap check reuses `SeasonRequestRange`, whose doc comment widens to say so.
  - A small `AiringWeekRange` goes in `Services/Airing/`.
  - The recap and airing controller tests are updated for the new constructor parameters, and gain cases for accepted and refused values.
  - No DTO, migration or service changes.
- **Docs:** the `/api/recap` and `/api/airing` rows in `CODE_GUIDE.md` (and the `AiringPage` row's range) name the new `400`s and bounds.
- **Not changing:**
  - The Season and Year pages and their bounds, including `GET /api/season/bounds` and the horizon probe.
  - My list's own parsing of `recapYear`/`recapFrom`/`recapTo`/`recapSeason`: My list is not one of the two pages this change bounds, and the picker change stops it from being sent anywhere out of range.
  - `GET /api/recap/availability`.
  - How airing rows are fetched or stored.
