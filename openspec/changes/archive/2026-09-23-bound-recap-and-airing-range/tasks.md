## 1. The recap API refuses periods outside winter 1917 through the current season (backend)

- [x] 1.1 Add `Services/Recap/RecapRange.cs` with `public static SeasonRequestRange For(DateOnly localToday)`. It returns `new SeasonRequestRange(SeasonCalendar.EarliestArchiveYear, y, s)`, where `(y, s) = SeasonCalendar.GetSeasonFor(localToday.AddDays(1))`. The doc comment should say why the lead is one day: the API must accept whatever a browser ahead of Helsinki already calls current (design D6).
- [x] 1.2 Widen `SeasonRequestRange`'s doc comment so it no longer says it is only the season and year endpoints' range. It now also backs the recap's winter-1917-to-current-season range.
- [x] 1.3 Give `RecapController` an `IBroadcastLocalTimeConverter` constructor parameter. After the existing mode, filter, season-name and missing-parameter checks, and before any `RecapPeriod` is built, compute `RecapRange.For(converter.GetLocalDate(DateTimeOffset.UtcNow))` and return `400 { error }` naming the accepted range when:
  - yearly: `!ContainsYear(year)`
  - season: `!ContainsSeason(year, season)`
  - multi-year: `!ContainsYear(from) || !ContainsYear(to)`, checked on the ends as sent, before `RecapPeriod.MultiYear` orders them
- [x] 1.4 Update the `Get` action's doc comment to name the new refusal and that it comes before the recap is computed.
- [x] 1.5 Add `RecapRangeTests` over fixed dates:
  - 2026-09-23 → latest (2026, summer)
  - 2026-09-30 → (2026, fall), because of the one-day lead
  - 2026-12-31 → (2027, winter)
  - 2026-06-15 → (2026, spring)
  - `EarliestYear` is always 1917
- [x] 1.6 Update every `new RecapController(...)` in `RecapControllerTests` to pass the existing `FakeBroadcastLocalTimeConverter`. Add cases.
  - **Refused with `400`, service never called:**
    - yearly 1916
    - yearly `current UTC year + 2`
    - yearly 99999
    - season (1916, fall)
    - season (`current UTC year + 2`, winter)
    - multi-year `from=1&to=9999`
    - multi-year `from=-5&to=2020`
  - **Accepted and passed through:**
    - yearly 1917
    - season (1917, winter)
    - multi-year `from=2024&to=2020`, reaching the service as 2020–2024
- [x] 1.7 Run `dotnet test` from `backend/` and confirm the new tests and the existing `RecapControllerTests` pass.

## 2. The airing API refuses weeks outside 1917 through the end of next year (backend)

- [x] 2.1 Add `Services/Airing/AiringWeekRange.cs`:
  - `public const int YearsAhead = 1;`, with a comment naming the frontend mirror `AIRING_YEARS_AHEAD`
  - `public static readonly DateOnly Earliest = new(SeasonCalendar.EarliestArchiveYear, 1, 1);`
  - `public static DateOnly Latest(DateOnly localToday) => new(localToday.AddDays(1).Year + YearsAhead, 12, 31);`
  - `public static bool Contains(DateOnly date, DateOnly localToday)`
- [x] 2.2 Give `AiringController` an `IBroadcastLocalTimeConverter` constructor parameter. In `GetWeek`, when `week` has a value and `!AiringWeekRange.Contains(week, today)`, return `400 { error }` naming the range before calling the service. An absent `week` is never checked. Update the action's doc comment.
- [x] 2.3 Add `AiringWeekRangeTests`:
  - `Earliest` is 1917-01-01
  - `Latest(2026-09-23)` is 2027-12-31
  - `Latest(2026-12-31)` is 2028-12-31, because of the one-day lead
  - `Contains` is true at both ends and false one day beyond each
- [x] 2.4 Update `AiringControllerFullRefreshTriggerTests` for the new constructor parameter, using a small fake converter. Add `AiringControllerTests` for `GetWeek` with a recording `IAiringScheduleService`.
  - **Refused with `400`, service never called:** 1916-12-31, 9999-12-31, 1 January of `current UTC year + 3`
  - **Passed through:** 1917-01-01 and a null `week`
- [x] 2.5 Run `dotnet test` from `backend/` and confirm everything passes.

## 3. The recap page declines periods it cannot show (frontend)

- [x] 3.1 Split `RecapPage.tsx` into a guard (`RecapPage`) and the existing body renamed to `RecapPageView`. The view receives the validated `mode`, `startYear`, `endYear` and `season` as props and stops parsing those five parameters itself. It keeps reading `filter`, `basis`, `type`, `seasonScore` and `yearScore` from the URL. Render the view unkeyed, at a fixed position, so a period step changes its props without remounting it (design D1).
- [x] 3.2 Add a pure `resolveRecapUrl(searchParams, current)` in `RecapPage.tsx` implementing design D3's table:
  - an absent parameter takes its default with no rewrite
  - an unknown `mode` is removed
  - in season mode, a malformed or out-of-range `year`/`season` replaces both with the current season
  - in yearly mode, a malformed or out-of-range `year` is replaced with the current year
  - in multi-year mode, each malformed or out-of-range end is replaced with its default, and a reversed range is then swapped

  Parse numbers as `YearPage` does (`Number` + `Number.isInteger`). Use `isAddressableSeason(target, current)` and `isAddressableYear(year, current.year)` from `utils/browseRange.ts`. It returns the resolved period and a replacement query, or `null`.
- [x] 3.3 In the guard, memoise `current` per mount with `useMemo(currentSeasonTarget, [])`. Render `<Navigate to={'/recap?' + replacement} replace />` when a replacement is needed, otherwise the view. It never renders `null` and never requests anything.
- [x] 3.4 Remove `RecapPage`'s local `isSeasonName`, `currentSeasonName` and `yearOptions`, and use the shared `isSeasonName`, `currentSeasonTarget` and `yearsInRange` instead. Replace the stale "Bounds always widen to include whatever's actually selected" comment.
- [x] 3.5 In `renderPeriodControls`:
  - build every year dropdown from `yearsInRange(EARLIEST_YEAR, current.year)`, with no `Math.min`/`Math.max` against the viewed period
  - drop `lowYear`/`highYear`
  - keep the previous arrows' 1917 floor
  - disable the yearly next arrow at `current.year`, and the season next arrow at `seasonPointIndex(current.year, current.season)`
  - update the stepper comment block to describe the new range
- [x] 3.6 Cut the season dropdown to seasons up to `current.season` when the selected year is `current.year`. In the season-mode year dropdown, choosing `current.year` while a later season is selected sets the season to `current.season` in the same `updateParams` call.
- [x] 3.7 In multi-year mode, choosing a From later than To sets both `from` and `to` to it. Choosing a To earlier than From sets both to it. Each is one `updateParams` call that keeps `keepScroll`.
- [x] 3.8 Pass `current` from the guard to the view, or recompute it with the same memoised helper, so the guard and the controls agree on "now" for the whole visit.
- [x] 3.9 Reword the `isAddressableSeason`/`isAddressableYear` comments in `utils/browseRange.ts` so the ceiling is whatever the caller supplies: the navigable ceiling for the Season and Year pages, the current season for the recap (design D2).

## 4. The Recap a period picker offers the same range (frontend)

- [x] 4.1 In `RecapPickerOverlay.tsx`, build `yearOptions` with `yearsInRange(Math.max(EARLIEST_YEAR, earliestKnown), current.year)`, where `earliestKnown` is the smallest availability year (or `current.year` before availability loads). The availability maximum no longer raises the top.
- [x] 4.2 Replace the overlay's local `currentSeasonName` with the shared `currentSeasonTarget`. Cut its season select at the current season when the current year is selected. When the year select changes to the current year while a later season is selected, move the season back to the current season.
- [x] 4.3 Confirm the confirm-time `Math.min`/`Math.max` ordering of `from`/`to` is untouched, and that the default state (`from` = last year, `to`/`year` = this year, `season` = the current season) is already inside the range.

## 5. The airing page declines weeks outside its range (frontend)

- [x] 5.1 In `AiringPage.tsx`, add:
  - `AIRING_YEARS_AHEAD = 1`, with a comment naming `AiringWeekRange.YearsAhead`
  - `floorIso` (`${EARLIEST_YEAR}-01-01`) and `ceilingIso(currentYear)` (`${currentYear + AIRING_YEARS_AHEAD}-12-31`)
  - `isAddressableWeekDate(value, today)`, which does the regex match, then a string range check against `floorIso`/`ceilingIso`, then a real-date check (month 1–12, day from 1 up to `new Date(y, m, 0).getDate()`). It never goes through `Date.parse` (design D7).
- [x] 5.2 Split the page into a guard (`AiringPage`) and `AiringPageView`, with `referenceDate` passed as a prop and not used as a key.
  - The guard renders `<Navigate to="/airing?week=<today>" replace />` (other parameters kept) for a present `?week=` that fails `isAddressableWeekDate`.
  - An absent `?week=` renders today's week with no rewrite.
  - Remove the old `isIsoDate`.
- [x] 5.3 Build the year select from `yearsInRange(EARLIEST_YEAR, currentYear + AIRING_YEARS_AHEAD)` instead of the `1960` loop.
- [x] 5.4 Bound the week arrows.
  - Previous: disabled when `weekStartIso(referenceDate) <= floorIso`, with target `max(addDaysIso(referenceDate, -7), floorIso)`.
  - Next: disabled when `addDaysIso(weekStartIso(referenceDate), 7) > ceilingIso`, with target `min(addDaysIso(referenceDate, 7), ceilingIso)`.
  - Compare ISO strings directly, and comment why that is safe for zero-padded four-digit years.
- [x] 5.5 Update the page's header comment to mention the range and the guard.

## 6. Docs

- [x] 6.1 In `CODE_GUIDE.md`, extend the `GET /api/recap` row with "`400` outside winter 1917 … the current season (`RecapRange`, one local day ahead)". Extend the `GET /api/airing?week=` row with "`400` outside 1917-01-01 … 31 December of next year (`AiringWeekRange`)". Add the range and the guard to the `AiringPage` row, and the guard/view split to the `RecapPage` description if it has one.
- [x] 6.2 Check `docs/PROJECT_CHEAT_SHEET.md`'s `/api/airing` and recap rows and update them if they describe accepted input.

## 7. Verify

- [x] 7.1 Build and lint the frontend with nvm's Node 22: `npm run build` and `npm run lint` in `frontend/`.
- [x] 7.2 Against the running dev stack, open each URL and confirm the resulting URL and page. For each, check the network tab: no request for the rejected period or week, no message shown, and Back returns to the previous page.
  - `/recap?year=32932734` → current yearly recap, no hang
  - `/recap?mode=season&year=<current year>&season=<a later season>` → the current season
  - `/recap?mode=yearly&year=1916` → current year
  - `/recap?mode=multiYear&from=2024&to=2020` → 2020–2024, with data shown
  - `/recap?mode=multiYear&from=1500&to=2020` → 2020 to last year
  - `/recap?mode=decade&year=2020&basis=mal` → yearly 2020, basis kept
  - `/recap` → yearly current year, URL unchanged
  - `/airing?week=2035-03-10`, `/airing?week=1916-12-31`, `/airing?week=2026-02-31` → today's week
  - `/airing?week=1917-01-03` → the first week of 1917, previous arrow disabled
- [x] 7.3 On the recap page:
  - [x] step the season recap forward to the current season and confirm the next arrow disables
  - [x] confirm the season dropdown in the current year stops at the current season
  - [x] in multi-year, pick a From after To and a To before From, and confirm both land on a single-year range with the recap updating
  - [x] step a period with the score board open, and confirm the page doesn't remount (board and scroll hold) — page does not remount and scroll holds (verified: same DOM node, same scrollY). The score board itself still closes on every step (pre-existing `useRestorableState`/POP-only restore semantics from `add-recap-score-board-and-hold-scroll`, not touched by this change) — accepted as-is per user decision, not fixed here.
- [x] 7.4 On the airing page, jump to December of next year and step forward until the next arrow disables. Confirm the final week (containing 31 December) is shown and the selectors read December of next year.
- [x] 7.5 Open **Recap a period** on My list. Confirm the years end at the current year and the current year's seasons end at the current season, even if your list has anime starting next season.
- [x] 7.6 `curl` `/api/recap?mode=yearly&year=99999` and `/api/airing?week=9999-12-31` against the dev backend. Confirm both return `400`, not `500`.
