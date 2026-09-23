## Context

**The pattern already exists.** `bound-browse-range-and-sorting` bounded the Season and Year pages with three parts, and this change reuses all three:

- **A guard/view split.** `SeasonPage`/`YearPage` resolve the URL's target and render one of three things: `null` while deciding, `<Navigate replace>` for an unaddressable target, or the page body (`…PageView`) with the target as validated props. That's the only way to stop the body's mount-time requests, because hooks can't be made conditional.
- **Dropdowns built from the range.** They're built with `yearsInRange(floor, ceiling)`, never widened against the URL's value.
- **Server-side refusal.** The controllers return `400` before any read.

The shared pieces live in `frontend/src/utils/browseRange.ts`: `EARLIEST_YEAR`, `isSeasonName`, `currentSeasonTarget`, `isAddressableSeason(target, ceiling)`, `isAddressableYear(year, ceilingYear)` and `yearsInRange`. On the backend, `SeasonCalendar.EarliestArchiveYear` and `SeasonRequestRange` hold the matching rules.

**Recap today** (`frontend/src/pages/RecapPage.tsx`):

- Period parsing accepts any positive integer.
- `yearOptions(Math.min(EARLIEST_YEAR, startYear, endYear), Math.max(currentYear, startYear, endYear))` is sized by the URL. This is the same crash the Year page had.
- The season "next" arrow is bounded at `seasonPointIndex(highYear, 'fall')`, so it reaches the current year's later seasons.
- `RecapPage` keeps its own copies of `isSeasonName` and `currentSeasonName`.
- **Reversed ranges break the page.** The multi-year From/To selects write `from`/`to` independently, so the page can produce `from > to`. `RecapPeriod.MultiYear` puts the range in order server-side, so `recap.startYear !== startYear`. `recapMatchesSelection` is then never true, and the page stays on the last period's figures, or shows nothing on a fresh load.
- **The backend.** `RecapController` validates mode, filter and season name only. `RecapPeriod.DateRange` builds `DateOnly`s, which throw outside years 1–9999, so the result is a `500`.

**Airing today** (`frontend/src/pages/AiringPage.tsx`):

- `isIsoDate` accepts `^\d{4}-\d{2}-\d{2}$` plus `Date.parse`. V8 parses `2026-02-31` as 3 March, and `0000-01-01` as a valid date, so neither is refused.
- The next arrow has no end.
- The year list is a fixed `currentYear + 1 … 1960`, so it can't crash, but a date outside it leaves the select with no matching option.
- `GET /api/airing` takes any `DateOnly`. `9999-12-31` overflows in `weekStart.AddDays(7)`, which is a `500`.
- The stored airing rows (dev database, 2026-09-23) run from 2014-07-05 to 2027-06-27, about nine months ahead.

**Local time.** The frontend's "current" comes from the browser clock. The backend's comes from `IBroadcastLocalTimeConverter.GetLocalDate(DateTimeOffset.UtcNow)`, which is fixed to Europe/Helsinki. For this self-hosted app these are normally the same zone, but nothing guarantees it.

## Goals / Non-Goals

**Goals:**

- A recap period outside winter 1917 through the current season, or an airing week outside 1 January 1917 through 31 December of next year, can't be reached by URL. It's replaced silently, before any request.
- No render path on either page is sized by a URL value.
- Every control that picks a recap period or an airing week offers exactly the addressable range. That includes the Recap a period picker.
- The two APIs refuse what the pages refuse with `400` instead of computing it or returning `500`, and never refuse something a client's own controls offer.
- A reversed multi-year range can no longer leave the recap page stuck.

**Non-Goals:**

- The Season and Year pages, `GET /api/season/bounds`, and the horizon probe.
- My list's own parsing of `recapFrom`/`recapTo`/`recapYear`/`recapSeason`. My list is not one of the pages this change bounds. With the picker bounded (D5) the app never writes an out-of-range scope there, and a hand-typed one fails today too, just with a `400` instead of a `500`.
- What a current-year recap includes. A yearly recap of the current year under "What aired" still counts anime starting later this year. The year is current, and only the season mode has a finer unit than the year.
- Links from ranking rows (`RankingSection`), which point at whatever periods the rankings hold (see Risks).
- How airing rows are fetched or stored.

## Decisions

### D1 — The recap page gets the Season page's guard/view split, without a pending state

`RecapPage` becomes a guard. It resolves the URL (D3) and renders either `<Navigate to=… replace />` or `<RecapPageView mode startYear endYear season />`. `RecapPageView` is the current body with every hook unchanged. It receives the validated period as props instead of parsing `mode`/`from`/`to`/`year`/`season` itself, and keeps reading `filter`, `basis`, `type` and the score parameters from the URL as today.

Unlike the Season page, there's nothing to wait for. The recap's ceiling is the current season, a pure calendar value. The guard therefore never renders `null` and never makes a request. `current` is memoised per guard mount (`useMemo(currentSeasonTarget, [])`), as the Season and Year guards do, so the guard and the view can't disagree about "now" within one visit.

The view SHALL NOT be keyed by period. Stepping periods must keep the same `RecapPageView` instance, because its `recapDisplayRef`, open overlay and restorable state are what hold the page steady during a step. The guard returns the view at the same tree position with new props, which is enough.

*Alternative considered:* validate inside the current body and skip the fetch with an `enabled` flag. Rejected for the same reason as on the Season page: `usePageData` runs on mount, and threading "is this period real" through it spreads one decision across the page.

### D2 — Reuse the shared range helpers as they are

The recap's rule is the Season/Year rule with the current season as the ceiling:

- Season mode: `isAddressableSeason(target, current)`.
- Yearly and each multi-year end: `isAddressableYear(year, current.year)`.
- Every year dropdown: `yearsInRange(EARLIEST_YEAR, current.year)`.

`RecapPage`'s local `isSeasonName`, `currentSeasonName` and `yearOptions` are removed in favour of the shared ones. `RecapPickerOverlay`'s own `currentSeasonName` goes the same way. The comments on `isAddressableSeason`/`isAddressableYear` currently describe the navigable ceiling. They're reworded to say the caller supplies the ceiling: the navigable ceiling for the Season and Year pages, the current season for the recap.

Parsing follows `YearPage`: `Number(param)` plus `Number.isInteger`. `year=` therefore reads as 0, which is out of range and replaced.

### D3 — Recap replacement rules: replace what is wrong, keep what is absent

The guard runs a pure `resolveRecapUrl(searchParams, current)` that returns the resolved period and, if anything must change, the replacement query:

| Parameter state | Result |
| --- | --- |
| absent | its default (yearly; current year; current season; `from` = current year − 1, `to` = current year), **URL untouched** |
| present and valid | used as is |
| `mode` present but unknown | removed, resolves to yearly |
| season mode, `year`/`season` malformed or the point out of range | **both** replaced with the current season |
| yearly, `year` malformed or out of range | replaced with the current year |
| multi-year, an end malformed or out of range | that end replaced with its default |
| multi-year, `from > to` after the above | swapped |

Only the parameters the resolved mode reads are checked or rewritten. A lingering `season` in yearly mode, left behind by `switchMode`, stays as it is, as do `filter`, `basis`, `type` and the score parameters.

In season mode, the season and year are replaced together, never one half alone, as on the Season page. `year=2026&season=fall` in summer 2026 therefore becomes summer 2026, not fall of some other year. Each multi-year end is replaced on its own, because each is a separate choice with its own default.

**Why absent parameters aren't written into the URL**, unlike the Year page, which writes the current year for a missing `year`:

- The navbar already links to a complete recap URL (`?mode=yearly&year=<current>&filter=aired`).
- A bare `/recap` is unambiguous: it means the defaults, and the page shows them.
- Rewriting it would add a history replacement on every such visit with no visible gain, and would change today's behaviour for no reason tied to this change.

The page never disagrees with a *present* parameter, which is what the replacement guarantees.

### D4 — The recap's own controls never leave the range

- **Year dropdowns** (all three modes): `yearsInRange(EARLIEST_YEAR, current.year)`. No `Math.min`/`Math.max` against the viewed period. This is the crash fix.
- **Season dropdown**: when the selected year is `current.year`, cut to seasons up to `current.season`. There's no need to also keep the selected season as the Season page does, because the guard guarantees it's in range.
- **Season-mode year dropdown**: choosing `current.year` while a later season is selected moves the season to `current.season`, in one `updateParams` call. This mirrors the Season page's handler.
- **Arrows**: yearly next is disabled at `current.year`, as today. Season next is disabled at `seasonPointIndex(current)` instead of fall of `highYear`. The previous arrows keep their 1917 floor, and `lowYear`/`highYear` go away.
- **Multi-year From/To**: choosing a From later than To sets both to the chosen year. Choosing a To earlier than From sets both to the chosen year. The control the user touched always shows what they picked.

*Alternative considered for the multi-year order:* restrict each dropdown's options to its side of the other (From ≤ To). Rejected: moving a whole range forward, such as 2018–2020 to 2023–2025, would then require changing To before From, and the dropdown would silently hide years. *Also considered:* swapping the two ends on a reversed pick. Rejected because the From dropdown would then show a year the user didn't choose there.

### D5 — The Recap a period picker offers the same range

`RecapPickerOverlay`:

- `yearOptions` runs from `max(EARLIEST_YEAR, earliest availability year)` to `current.year`. The availability maximum no longer raises it, and before availability loads it's just `[current.year]`, as today.
- In the current year, the season select is cut at the current season.
- The season-mode year select applies D4's "move the season back" rule.

The multi-year order is already normalised at confirm time (`Math.min`/`Math.max`), so it needs nothing.

Availability can name future years: an anime on my list that starts airing next year gives that year an aired count. That's why the maximum must stop at the current year rather than follow the data. Bounding the picker also bounds My list's recap scope, which is picked through the same overlay. The recap page would refuse any period left out, so the scope chip's "open the recap" link would otherwise land somewhere other than the period it names.

### D6 — The recap API refuses the same range, computed one local day ahead

`RecapController` gains `IBroadcastLocalTimeConverter`. A new static `RecapRange.For(DateOnly localToday)` in `Services/Recap/` returns `new SeasonRequestRange(SeasonCalendar.EarliestArchiveYear, y, s)`, where `(y, s) = SeasonCalendar.GetSeasonFor(localToday.AddDays(1))`. This reuses `SeasonRequestRange` rather than writing a second copy of `ContainsYear`/`ContainsSeason`, including its year-first guard against `year * 4` wrapping. Its doc comment is widened to name the recap as a second user.

The check runs after the existing mode, filter, season-name and missing-parameter checks, and before `RecapPeriod` is built:

- **yearly:** `range.ContainsYear(year)`
- **season:** `range.ContainsSeason(year, season)`
- **multi-year:** `range.ContainsYear(from) && range.ContainsYear(to)`. Both ends are checked as sent, before `RecapPeriod.MultiYear` puts them in order.

A refusal is `400 { error }` naming the accepted range, like `YearController`.

**Why one day ahead.** The API's accepted range must include everything a client's controls can offer, which is the same relationship as the season API's outer ceiling and the pages' navigable ceiling. The client's "current season" follows the browser's clock, and the server's follows Helsinki's. A browser ahead of Helsinki crosses into a new season or year first. Every inhabited time zone is at most about 12 hours ahead of Helsinki, so a one-day lead covers them all. The cost: on the last local day of a season, the API also accepts the next season. That recap is empty or near-empty and harmless, and no correctly-clocked client offers it.

`RecapRange.For` takes the date as a parameter, so the boundary behaviour can be unit-tested with fixed dates. The controller only supplies `GetLocalDate(UtcNow)`, matching how `AiringScheduleService` already gets "today".

### D7 — Airing: end of next calendar year, checked on the reference date

**Why this ceiling.** The ceiling is 31 December of `currentYear + 1`.

- Stored rows reach about nine months ahead, so this always covers them with room to spare.
- It's exactly where the year dropdown already ends.
- It keeps whole years as the unit, like every other bounded page.

The options considered:

- **Two years:** rejected. It adds a whole year that can only ever be empty.
- **A rolling "today + 12 months":** rejected. The ceiling would move daily, and the ceiling year's month selector would need a cut, like the Season page's season cut.
- **A data-driven ceiling (latest stored row):** rejected. It would need a new endpoint and a pending state before any future week could render, it would shrink as shows finish, and it would hide a newly scheduled week until the next refresh. An empty future week already says "Nothing airing this week", which is honest.

The floor is 1 January 1917, the app-wide floor, even though stored data starts in 2014. One floor across pages beats a data-shaped one, and an empty past week costs one cheap query. 1 January 1917 is a Monday, so the first week of the range is exactly 1–7 January and the previous arrow needs no special case.

**What is checked.** The check applies to the `?week=` reference date, not the week it falls in. The rule is simple to state and to check on both sides: a date between two dates. The only consequence is that the final week, which contains 31 December, may show a few days of the following January.

**Frontend.** `AiringPage` gets the same guard/view split (D1). The guard's validation is done without `Date.parse`, in three steps:

1. The value matches `^\d{4}-\d{2}-\d{2}$`.
2. It lies between `floorIso` and `ceilingIso` by plain string comparison. Zero-padded four-digit ISO dates compare correctly as strings, and this also rejects year `0000` before any `Date` is built. `new Date(y, …)` maps years below 100 onto 1900-something, so it must never see one.
3. It's a real date: month 1–12, and day from 1 up to `new Date(y, m, 0).getDate()`.

An invalid or out-of-range value becomes `<Navigate to="/airing?week=<today>" replace />`, keeping other parameters. An absent value renders today's week with no rewrite, as today. `AiringPageView` receives `referenceDate` as a prop and isn't keyed by it.

Inside the view:

- **Year list:** `yearsInRange(EARLIEST_YEAR, currentYear + AIRING_YEARS_AHEAD)`.
- **Previous arrow:** disabled when `weekStartIso(ref) <= floorIso`. Its target is `max(ref − 7, floorIso)`. Since the floor is a Monday, the clamp never actually fires, but it keeps the two arrows symmetric.
- **Next arrow:** disabled when `weekStartIso(ref) + 7 > ceilingIso`, meaning no later week starts in range. Its target is `min(ref + 7, ceilingIso)`, so a Sunday reference late in December lands on the final week instead of past it.
- **Month and year selects:** already stay inside the chosen year, which is always in range.

`AIRING_YEARS_AHEAD = 1` sits beside these helpers in `AiringPage.tsx`, the only file that uses them. Its comment names the backend mirror.

### D8 — The airing API refuses the same range, computed one local day ahead

A new static `AiringWeekRange` in `Services/Airing/` holds three things:

- `YearsAhead = 1`, mirroring the frontend constant.
- `Earliest = new DateOnly(SeasonCalendar.EarliestArchiveYear, 1, 1)`.
- `Contains(DateOnly date, DateOnly localToday)`. It is true when `Earliest <= date <= new DateOnly(localToday.AddDays(1).Year + YearsAhead, 12, 31)`.

`AiringController` gains `IBroadcastLocalTimeConverter`. When `week` is present and outside the range, `GetWeek` returns `400 { error }` before calling the service. An absent `week` is never checked. The one-day lead has the same reason as D6: on 31 December a browser already in the new year offers the following December, and the API must accept it.

The controller does the check, not `AiringScheduleService.GetWeekAsync`, following the season/year precedent that a refusal happens before the service is touched.

## Risks / Trade-offs

- **[Risk] Browser and server disagree about "now" at a season or year boundary.** → The APIs look one local day ahead (D6, D8), so they accept at least what any client offers. A browser *behind* Helsinki simply offers less than the API accepts, which is harmless.
- **[Risk] A ranking row can link to a season past the current one.** The rankings only include a season holding a scored anime, and scoring an unaired anime is possible on MAL, so a row can point at such a season. The guard then replaces it with the current season. → Accepted. It needs a score on an unaired show, and the replacement is the same silent, history-neutral behaviour as any other out-of-range link.
- **[Risk] The recap view gets remounted and loses its held display during a period step.** → D1 renders the view at a fixed position with no `key`, so a step only changes its props. This is checked by stepping periods with an overlay open and confirming the overlay and scroll hold.
- **[Trade-off] Bare `/recap` isn't normalised into the URL**, unlike `/year` (D3). → Deliberate. The page and a present parameter never disagree, and an absent one means its default.
- **[Trade-off] The airing floor reaches decades before any stored data.** → Those weeks render "Nothing airing this week", which is true. A single app-wide floor is simpler than a data-shaped one.
- **[Risk] Adding a constructor parameter breaks the existing controller tests.** Both `RecapControllerTests` and `AiringControllerFullRefreshTriggerTests` construct their controllers directly. → They're updated in the same task as the controller change. `RecapControllerTests` already has a `FakeBroadcastLocalTimeConverter`.
- **[Risk] There is no frontend test runner**, so the guards are verified by build, lint and manual URL checks. → The rules are pure functions over strings and numbers, `resolveRecapUrl` and the airing date check, so they're easy to reason about. tasks.md lists the exact URLs to try.
