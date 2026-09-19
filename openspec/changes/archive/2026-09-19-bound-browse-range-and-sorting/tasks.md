## 1. Season and year orderings follow the displayed title (backend)

- [x] 1.1 In `SeasonRepository.GetListingAsync`, add a `DisplayTitle` member to the anonymous `projected` selection — `EnglishTitle` when it is neither null nor blank, else `Title` — written in a form EF Core translates to SQL (a plain null/empty conditional, not a C# helper call).
- [x] 1.2 Point the `alphabeticalIds` query at `DisplayTitle`, and replace every `.ThenBy(a => a.Title)` / `.ThenBy(x => x.a.Title)` title tie-break in the popularity, MAL-score and my-score queries with the same `DisplayTitle`, so all four orderings agree with what the card shows.
- [x] 1.3 Update the comment block above the four ordering queries so it says the orderings carry the *displayed* title's collation, and note why the tie-breaks moved with the alphabetical sort rather than being left on `Title`.
- [x] 1.4 In `SeasonRepositoryTests`, extend the seed helper with an optional `englishTitle` and add coverage: an anime whose English and original titles sort differently is placed by the English title; an anime with a null English title is placed by its original title; an anime with a blank English title is treated as having none; and a tie on MAL score breaks by displayed title.
- [x] 1.5 Run `dotnet test` from `backend/` and confirm the existing `SeasonRepositoryTests` orderings still pass alongside the new ones.
- [x] 1.6 Fix the stale `EARLIEST_YEAR (1989)` comment on `SeasonCalendar.EarliestArchiveYear` — the frontend floor is now the archive year itself, so the sentence explaining why the two differ no longer applies.

## 2. The horizon probes forward on its own (backend)

- [x] 2.1 Add `HorizonProbe` beside `SeasonHorizon` — pure and stateless, today always a parameter: `Target(current)` = `SeasonCalendar.Shift(current, 3)`, and `ShouldProbe(current, today, outerCeiling, lastFetchedAt)` = the target is past the outer ceiling **and** today falls in the final month of the current season **and** the target has not been fetched today.
- [x] 2.2 Unit-test `HorizonProbe` directly: it fires on the first visit of a day in the last month; not twice the same day; not at all in the first two months; not once the target is at or below the ceiling; and the target is a fixed `current + 3` that does not move when the ceiling rises.
- [x] 2.3 Add `ProbeHorizonAsync` to `ISeasonBrowseService`/`SeasonBrowseService`: resolve the current season and outer ceiling through the existing `LoadHorizonAsync`, consult `HorizonProbe`, and when it says yes delegate to the existing `RefreshAsync` so the fetch inherits the single-flight lock, `404`-as-fact and the caching rules. Return the freshly computed `SeasonBoundsDto` either way.
- [x] 2.4 Document on that method why the probe's own gate sits in front of `RefreshAsync`'s age-based one (it is strictly stricter, so the two can never disagree) and why reaching past the accepted range is safe: the target is `current + 3` while `LoadHorizonAsync` only loads candidate points for `current`…`current + 2`, so `NotListedToday` can never see it and a `404` probe cannot lower either ceiling (design D10).
- [x] 2.5 Add `POST /api/season/horizon/probe` to `SeasonController`, taking **no** year or season and returning the bounds. Note in its doc comment that the season endpoints still refuse that same season when a client names it, and that the absence of parameters is what keeps the reach-past-the-range safe.
- [x] 2.6 Give the probe an on-demand mode for the guard (design D10a): the same call, but ignoring the last-month window while still honouring the once-per-day gate, so a URL addressing the probe's target is answered by asking MAL rather than by refusing. Keep it one code path with one flag rather than a second probe implementation.
- [x] 2.7 Swallow and log a probe failure the way `RefreshAsync` already does and return the current bounds regardless — a probe must never fail a page visit.
- [x] 2.8 Add `SeasonBrowseServiceTests` coverage: a successful probe caches the season and raises the returned ceiling; a `404` probe leaves both ceilings exactly as they were and only stamps the fetch log; a second probe the same local day makes no MAL request; a probe outside the last month makes no MAL request; and the on-demand mode fetches inside that window and outside it alike.

## 3. The year refresh stops asking about seasons that cannot exist (backend)

- [x] 3.1 Filter `RefreshYearAsync`'s loop to the seasons of the year that fall within the **outer** ceiling (the accepted range, not the navigable one, so a season MAL `404`-ed today is still re-read).
- [x] 3.2 Make `FoldOutcomes` report `NotListed` rather than `Failed` for an empty outcome list, so a year that yields no season to refresh is never reported as broken.
- [x] 3.3 Add tests: a past year still refreshes all four seasons; the ceiling's year refreshes only the seasons at or before the ceiling and makes no MAL request for the rest; and a year whose only in-range season fetches successfully reports `fetched`.
- [x] 3.4 Sanity-check the ordering with task group 2: trimming this loop removes the accidental discovery path, so the probe must already be in place (design D11).

## 4. A shared browse range for the season and year pages (frontend)

- [x] 4.1 Add `frontend/src/utils/browseRange.ts` holding the archive floor (1917, named after and matching `SeasonCalendar.EarliestArchiveYear`), the forward season window (2, matching `SeasonHorizon.FutureSeasonWindow`) used only as the fallback ceiling, the current-season helper, and the pure predicates the two pages need (`isAddressableSeason`, `isAddressableYear`, and the year list for a range).
- [x] 4.2 Document in that module that the addressable ceiling *is* the navigable ceiling from `GET /api/season/bounds` — a URL reaches exactly what the arrows and dropdown offer, with no second, wider ceiling (design D3).
- [x] 4.3 Add `frontend/src/hooks/useSeasonBounds.ts`: a module-memoised, stale-while-revalidate `getSeasonBounds()` — the last resolved value is returned synchronously on later mounts, one in-flight request is shared by concurrent mounts, a failure falls back to the client-computed default, and the hook reports whether the first resolution is still pending.
- [x] 4.4 Delete `EARLIEST_YEAR` and `FUTURE_SEASON_WINDOW` from `SeasonPage.tsx` and the `getSeasonBounds` effect from both pages, re-pointing every use at the new module and hook so the two pages can no longer drift apart.
- [x] 4.5 Add `probeSeasonHorizon()` to `api/client.ts` for `POST /api/season/horizon/probe` (no parameters, with the on-demand flag), and have `useSeasonBounds` fire the background form alongside the bounds read, adopting the returned ceiling only when it is later than the one already held.
- [x] 4.6 Expose the probe's target from `browseRange.ts` (`current + 3`) so each guard can recognise the one season, and the one year, that earn an on-demand check.
- [x] 4.7 Keep the guard off the background probe's critical path — it decides on the `GET /bounds` answer alone, and waits on MAL only in the on-demand case of task 5.3a/6.2a (design D10a).

## 5. The season page declines seasons that cannot exist

- [x] 5.1 Split `SeasonPage.tsx` into a guard (`SeasonPage`) and the existing body renamed to `SeasonPageView`, with the validated `year` and `season` passed in as props; every other hook, effect and control stays where it is.
- [x] 5.2 In the guard, resolve the target from the URL and answer one of three ways (design D3): allow immediately when it is between winter 1917 and the current season (the ceiling can never be below it); render nothing while bounds is still pending for a later target; otherwise allow or replace once the navigable ceiling is known, falling back to `current + 2` if bounds failed.
- [x] 5.3 Replace a rejected target with the current season via `<Navigate replace>`, carrying every other search parameter over and setting only `year`/`season`. Confirm by inspection that no read, refresh or bounds-driven fetch for the rejected season can run — the view component never mounts.
- [x] 5.3a When the addressed season is exactly the probe's target and sits past the ceiling, call the on-demand probe and decide on its answer — render the season if MAL returned anime, replace it if not. Every season further out is replaced with no request at all (design D10a).
- [x] 5.4 Treat an absent, non-integer or malformed `year`, and a `season` that is not one of the four, as a rejected target so the URL is normalised to the current season instead of silently disagreeing with the page.
- [x] 5.5 Build `yearOptions` from the addressable range alone — drop the `Math.min`/`Math.max` widening against the viewed year — and keep the season-list cut at the ceiling's season within the ceiling's year.
- [x] 5.6 Floor the previous-season arrow at winter of the archive year (it already reads the shared floor constant, so this follows from task 4.4; verify the arrow disables there and nowhere later).

## 6. The year page declines years that cannot exist

- [x] 6.1 Split `YearPage.tsx` the same way — guard plus `YearPageView` taking a validated `year` prop.
- [x] 6.2a When the addressed year is the one containing the probe's target and sits past the ceiling, call the on-demand probe and decide on its answer; any later year, and that same year when the target lies inside an already-addressable year, is replaced with no request.
- [x] 6.2 Apply the same three-way decision against the addressable **year** range (floor 1917, ceiling the navigable ceiling's year; the current year and earlier admitted with no wait), replacing a rejected year with the current year via `<Navigate replace>` and carrying the rest of the query over.
- [x] 6.3 Build the year dropdown from the addressable range alone, removing the `Math.min`/`Math.max` widening — this is the allocation that made `?year=32932734` lock the tab up.
- [x] 6.4 Floor the previous-year arrow at the archive year.

## 7. My list stops sorting by airing status

- [x] 7.1 Remove `'airingStatus'` from `SortKey` in `utils/anime.ts`, drop the `key === 'airingStatus'` branch and the `airingStatusFirst` parameter from `sortComparator` and `composeComparator`, and delete `compareByAiringStatus` and `AIRING_STATUS_CYCLE` if nothing else uses them (keep `AIRING_STATUS_LABELS` — the filter needs it).
- [x] 7.2 In `MyListControls.tsx`, merge `SORT_OPTIONS_BEFORE_AIRING`/`SORT_OPTIONS_AFTER_AIRING` into one list, delete `AIRING_CHOICES`, the `<optgroup>`, `SortChoice`, `toSortChoice` and `fromSortChoice`, and let both selects carry a plain `SortKey` again.
- [x] 7.3 Make `DIRECTION_LABELS` a total `Record<SortKey, ...>` and remove `DIRECTION_UNAVAILABLE_LABEL` and the direction control's unavailable state, keeping the `StableLabel` width behaviour intact.
- [x] 7.4 In `MyListPage.tsx`, remove the `airingStatusFirst` restorable state and every use of it — the `MyListControls` prop, `viewIdentity`, the `derived` dependency list, `clearFilters`, and the `first` handling in `handleSortChange`/`handleThenChange`.
- [x] 7.5 Reduce `airingBadgeActive` to the airing filter alone, leaving the always-on Plan-to-watch rule untouched.
- [x] 7.6 Check nothing else imports the removed symbols (`SortChoice`, `compareByAiringStatus`, `toSortChoice`, `fromSortChoice`) and that the TypeScript build reports no unused exports left behind.

## 8. My list sorts alphabetically by the displayed title

- [x] 8.1 Add `englishTitle: string | null` to `SortableListItem` in `utils/anime.ts` (my-list DTOs already carry it).
- [x] 8.2 Key the `alphabetical` factory on `pickDisplayTitle(item.title, item.englishTitle)` and note in the comment that `composeComparator`'s final fallback inherits it, so every sort's last tie-break follows the row's own title.

## 9. The reset action reads as a call to action

- [x] 9.1 Add a modifier class to the **Reset filters & sort** button in both of its placements in `MyListPage.tsx` (the tabs-row actions and the nothing-matches block), leaving "Recap a period" on the plain class.
- [x] 9.2 In `MyListPage.css`, style that modifier in the app's existing accent-filled button language — `--accent` background and border, white text, unchanged on hover — and keep the height, padding, radius and position identical to the plain button so nothing reflows when Reset appears.
- [x] 9.3 Check the filled button against both themes (the accent token differs per theme) and confirm the text stays legible on each.

## 10. Verification

- [x] 10.1 `dotnet test` from `backend/` passes.
- [x] 10.2 `npm run lint` and `npm run build` pass in `frontend/` (use nvm's Node 22 — the default v16 cannot run Vite).
- [x] 10.3 Against the running dev stack, open a season page and sort alphabetically; confirm the order follows the English titles on the cards and the API log shows no client-side-evaluation warning or exception from the new `DisplayTitle` expression.
- [x] 10.4 Open `/year?year=32932734`: the current year renders immediately, the URL is replaced, no message is shown, the browser does not hang, and the network panel shows no `/api/year/32932734` read or refresh.
- [x] 10.5 Repeat for `/year?year=1916`, `/season?year=1916&season=winter`, `/year?year=abc` and `/season?season=bogus`; each lands on the current year/season with its URL replaced and nothing requested for the rejected target.
- [x] 10.6 Confirm Back after a replacement returns to the previous page rather than bouncing off the rejected URL again, and that a rejected link carrying `sort`/`type` keeps those on the page it lands on.
- [x] 10.7 Confirm the year dropdown on both pages now starts at 1917 and ends at the ceiling year (at most one year ahead), that the previous arrows disable at winter 1917 / 1917, and that every season and year the dropdown offers is addressable by URL while the first one past the ceiling is replaced — the two agree exactly.
- [x] 10.8 Confirm a ceiling that retreats mid-visit (MAL answers `404` for the season being viewed) leaves the page on that season with its "not listed" message rather than replacing it, since bounds is read once per mount.
- [x] 10.9 On my list, confirm neither sort selector offers Airing status, the airing filter and its row badge still work, the direction control is available for every key, and alphabetical order matches the titles shown on the rows.
- [x] 10.10 Confirm the Reset filters & sort button is visibly the accent call to action beside "Recap a period" in both themes, and that the tabs row does not shift when it appears or disappears.
- [x] 10.11 Confirm the probe fires on a Season and a Year page visit during the last month of the current season, carries no year/season, and does not delay the render; check the API log shows at most one MAL request for the probed season per local day however many pages are opened, and none at all outside that month.
- [x] 10.12 Confirm a `404` probe leaves the dropdown's ceiling exactly where it was (it must not retreat), that a successful probe raises it and then stops further probing, and that naming that same season on `/api/season/{year}/{season}` still returns `400`.
- [x] 10.12a Address the probe's target season by URL: it is checked against MAL and either renders or is replaced, while a season one further out is replaced with no request in the network panel.
- [x] 10.13 Confirm visiting the ceiling's year no longer fires MAL requests for that year's seasons beyond the ceiling, while a past year still refreshes all four.
- [x] 10.14 Run `openspec validate bound-browse-range-and-sorting` and confirm the change is still valid after implementation.
