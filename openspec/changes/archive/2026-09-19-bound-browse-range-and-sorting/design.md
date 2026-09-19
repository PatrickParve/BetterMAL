## Context

**The crash.** `SeasonPage.tsx` and `YearPage.tsx` each build their year dropdown as

```ts
const earliest = Math.min(EARLIEST_YEAR, year)
const latest = Math.max(ceiling, year)
Array.from({ length: latest - earliest + 1 }, (_, i) => latest - i)
```

where `year` comes straight from the URL with the only guard being `Number.isInteger(yearParam) && yearParam > 0`. `?year=32932734` therefore asks for a ~32.9-million-element array and that many `<option>` nodes. The page also starts its cache read and its MAL refresh for that year; the API refuses both with `400`, but the requests are made.

**What the server already knows.** `SeasonCalendar.EarliestArchiveYear = 1917` is the first year in MAL's season archive, and `SeasonBrowseService.GetRequestRangeAsync` returns the range the API accepts: winter 1917 through the **outer ceiling** — the current season plus `SeasonHorizon.FutureSeasonWindow` (2), raised to any later season that already has a cached listing, and *not* lowered by seasons MAL answered `404` for today. `GET /api/season/bounds` returns the **navigable ceiling**, which is the outer ceiling *after* that lowering. The client knows the navigable ceiling; it does not know the outer ceiling.

**Two ceilings, and which one governs.** The API accepts a slightly wider range than the controls offer: the accepted range ends at the outer ceiling, while the arrows and dropdown stop at the navigable ceiling, which is the outer ceiling *lowered* past any trailing season MAL answered `404` for on the current local date (it springs back the next day and re-probes). The season-browser spec today says a URL-addressed season in that gap still renders. This change closes the gap in the other direction: **the URL may address exactly what the controls offer** — the navigable ceiling — so there is one answer to "does this season exist" rather than two.

**How the window rises today, and why that is a problem.** `SeasonHorizon.ResolveOuter` is `current + FutureSeasonWindow` (2), raised to any later season that already has a cached listing. Nothing probes: season fetches are visit-triggered only. So the *only* thing that ever raises the ceiling is `RefreshYearAsync`, which loops all four of a year's seasons unconditionally (`SeasonBrowseService.cs:143`) with no horizon filter — visiting `/year?year=2027` asks MAL for winter, spring, summer and fall 2027, and whichever MAL actually lists get cached. That is how spring 2027 became reachable on the season page. It works, but it is accidental (nothing documents it as the discovery path), it needs the user to think of visiting a future year, and it spends requests on seasons that cannot exist. This change replaces it with a deliberate probe (D10) and then trims the year loop (D11).

**Titles.** `pickDisplayTitle(title, englishTitle)` is what every card and row displays. The season and year pages sort by a server-computed position from `SeasonRepository`, which orders on `AnimeMetadata.Title` (MAL's romaji title) — both for the alphabetical sort and as the tie-break of the other three. My list sorts client-side in `utils/anime.ts`, whose alphabetical factory also reads `item.title`. So every "Alphabetical" in the app orders by a string the user is not looking at.

**View state is in memory only.** `state/pageStateStore.ts` keeps snapshots in a module-level `Map` keyed by `location.key`, capped at 50 entries, never written to `sessionStorage` or the server. A sort key removed in this change can never come back from a snapshot written by the old code.

## Goals / Non-Goals

**Goals:**

- A year or season the API could never accept is unreachable: no read, no refresh, no MAL request, no error state, no message — the URL is replaced with the current year/season and the page renders as if that is where the user went.
- No render path is sized by a URL-supplied number, so a hostile or fat-fingered value cannot lock the tab up again even if the range logic is later loosened.
- The season and year quick-jumps reach the whole archive (1917), not just 1989.
- Every "Alphabetical" order — and every sort's title tie-break — matches the title shown on the card or row.
- The horizon climbs on its own as MAL opens each new season, at a bounded cost, instead of depending on someone visiting a future year.
- My list's sort selectors no longer offer Airing status, and its "Reset filters & sort" button reads as the way out of a narrowed list.

**Non-Goals:**

- Changing the API's accepted range, `GET /api/season/bounds`, or any DTO. The backend already refuses what it should.
- Changing the airing-status *filter* or the airing badge on my-list rows (only the badge's dependence on the removed sort key goes away).
- Widening or narrowing MAL's forward window itself (`FutureSeasonWindow = 2`), the ceiling arithmetic in `SeasonHorizon`, the refresh cadence, or the accepted request range. The probe (D10) feeds the existing raise rule rather than replacing it.
- The search page's own alphabetical sort, and the Recap page's year pickers (which have their own, wider, ranges).

## Decisions

### D1 — The dropdowns are built from the range, never from the URL

`yearOptions` on both pages is built from the addressable range's own two ends (`floor..ceiling`), with no `Math.min`/`Math.max` against the viewed year. This alone makes the crash unreachable: the list is ~110 entries whatever the URL says. The clamp that used to widen the list to include an out-of-range viewed year is no longer needed, because by the time the page body renders the viewed year is guaranteed to be inside the range (D2). Seasons in the ceiling's year are still cut to the ceiling's season, as today.

*Alternative considered:* keep the widening clamp and merely cap the array length. That leaves a `<select>` showing a year the rest of the page treats as invalid, and leaves the next person free to reintroduce the same unbounded allocation elsewhere.

### D2 — A route guard decides before the page mounts

The requests must not be made, so the check cannot live inside the page body — `usePageData` and the refresh effect run on mount, and hooks cannot be made conditional. Each page file therefore splits in two:

```
export function SeasonPage()          // guard: resolves the target, renders one of three things
  -> null                             //   still deciding (rare; see D3)
  -> <Navigate to={...} replace />    //   target is outside the addressable range
  -> <SeasonPageView year season />   //   everything as today, with year/season as validated props
```

`SeasonPageView` keeps every existing hook and behaviour; it simply receives an already-valid target instead of parsing one. The same split applies to `YearPage`/`YearPageView`.

*Alternative considered:* a `<Route>`-level guard element in `AppShell.tsx`. It works, but it splits the range rules away from the page that owns the constants, and the guard needs the same bounds value the page already uses.

*Alternative considered:* an `enabled` flag threaded through `usePageData` and the refresh effect. That spreads "is this target real" across five call sites inside the page instead of settling it once, above it.

### D3 — The addressable ceiling is the navigable ceiling: a URL reaches exactly what the controls offer

The rule is the one the controls already enforce. The arrows and the dropdown stop at the navigable ceiling from `GET /api/season/bounds`; a URL may address exactly that far and no further. There is deliberately **no** second, wider acceptance ceiling, so the page never disagrees with its own controls about whether a season exists.

That makes the ceiling a value the client must have before it can admit a *future* target. The season-browser spec guarantees the navigable ceiling never falls below the current season, which gives the guard its three-way answer without making ordinary visits wait:

| Target | Answer |
| --- | --- |
| winter 1917 through the **current** season/year | allow immediately — the ceiling can never be below it. This is the navbar's default landing and nearly every visit |
| later than the current season, bounds not resolved yet | pending — render nothing, issue no read |
| outside `[winter 1917, navigable ceiling]` once bounds settles (or fails) | replace with the current year/season |

Bounds is memoised for the session (D4), so the wait applies at most once and only to a future-dated target. A bounds request that fails falls back to the client-computed `current + 2`, the same honest fallback the pages already use for their controls — a failed request must not make the next two seasons unreachable.

**The ceiling can retreat during the day, and that is handled, not fought.** If MAL answers `404` for the top season, the navigable ceiling steps back for the rest of that local day and the controls stop offering it. Because bounds is read once per mount, a retreat cannot bounce a reader off the page they are already on: the guard has already admitted the target, and the page's "MyAnimeList has not listed this season" message renders as it does today. Re-opening that URL later the same day *is* replaced, because by then the controls refuse it too. The next local day re-probes and it comes back.

The year page's ceiling is the navigable ceiling's **year**, at most one year ahead of the current one. It is derived from the same bounds value rather than hard-coded, so it follows the season ceiling up when the year page's own four-season refresh discovers MAL has opened a further season (Context).

*Alternative considered:* accept a wider range than the controls offer — `max(current + 2, navigable ceiling)` — so a season the API still serves is never refused by the client. Rejected: it reintroduces exactly the two-answers-to-one-question split this change exists to remove, and the only case it protects is a season the controls themselves will not navigate to.

### D4 — One shared source for the constants and the bounds fetch

`YearPage` currently imports `EARLIEST_YEAR` and `FUTURE_SEASON_WINDOW` from `SeasonPage.tsx`, and both pages run their own `getSeasonBounds()` effect. The range math is now needed in four places (two guards, two page bodies), so it moves to a new `utils/browseRange.ts`: the floor (1917, matching `SeasonCalendar.EarliestArchiveYear`), the forward window (2, matching `SeasonHorizon.FutureSeasonWindow`), the current-season helper, and the pure predicates `isAddressableSeason` / `isAddressableYear`. A new `hooks/useSeasonBounds.ts` owns the fetch, module-memoised **stale-while-revalidate**: the last resolved value is returned synchronously on a later mount while a fresh request revalidates it, so the guard can answer without a round trip after the first visit and the daily re-probe still happens. Concurrent mounts share one in-flight request instead of firing two as they do today.

### D5 — Replace, don't push; keep the rest of the query

The redirect uses `replace: true` so the browser's Back button returns to wherever the user came from rather than to the rejected URL (which would bounce forward again). Everything in the query string except `year`/`season` is carried over, so a link that also carried a sort or a type filter lands on the current season with those still applied. Nothing is rendered about it: no message, no toast, no error state — as asked.

A `year` parameter that is absent, non-numeric or non-integer resolves to the current year exactly as today, and is written into the URL by the same replacement, so the address bar and the page never disagree.

### D6 — Displayed title, server-side, for all four season/year orderings

`SeasonRepository.GetListingAsync` already projects `EnglishTitle`. The projection gains a computed `DisplayTitle` — `EnglishTitle` when it is neither null nor blank, else `Title` — and the four ordering queries order and tie-break on it instead of `Title`. Keeping it inside the same `IQueryable` projection means Postgres still does the collation and the change reaches all four sorts at once, with no second copy of the rule.

The expression must be one EF Core can translate; the tests use the in-memory provider, which translates anything, so translation is verified against the real database rather than by a unit test (see Risks).

The client needs no change: `sortOrder.alphabetical` is still an opaque position.

### D7 — Displayed title, client-side, for my list

`SortableListItem` gains `englishTitle: string | null` (my-list DTOs already carry it — `MyListRow` displays it), and the alphabetical factory keys on `pickDisplayTitle(item.title, item.englishTitle)`. Because `composeComparator`'s final fallback is that same factory, every my-list sort's last tie-break follows automatically.

### D8 — Removing the Airing status sort key

`SortKey` loses `'airingStatus'`, which collapses several things that only existed for it:

- `SortChoice`, `toSortChoice`, `fromSortChoice` — the encoded `airingStatus:<status>` select value — disappear; the selects carry a plain `SortKey` again.
- the `AIRING_CHOICES` `<optgroup>` in both selectors goes away.
- `airingStatusFirst` (state, URL-free restorable value, `clearFilters` reset, `viewIdentity` member, `derived` dependency, `MyListControls` prop) goes away, as does the `airingStatusFirst` argument to `sortComparator`/`composeComparator`.
- `DIRECTION_LABELS` becomes a total `Record<SortKey, ...>`, and `DIRECTION_UNAVAILABLE_LABEL` with its disabled-direction state goes away — every remaining key has a direction.
- the airing badge's `airingBadgeActive` reduces to "the airing filter has a selection" (plus the unchanged always-on rule for Plan to watch rows).
- `compareByAiringStatus` in `utils/anime.ts` is deleted if nothing else uses it; `AIRING_STATUS_LABELS` stays — the filter needs it.

No migration is needed for stored view state: snapshots live only in memory for the life of the tab (Context), so no snapshot can outlive the code that wrote it.

### D9 — Making "Reset filters & sort" stand out

The button shares `.my-list-page__clear-filters` with "Recap a period" beside it, so the two are indistinguishable. Rather than restyle both, Reset gets a modifier class in both of its placements (the tabs row and the empty-filter-result block) drawn in the app's existing accent-filled button language — the one `EntryEditorOverlay`'s Save button already uses: `--accent` background and border, white text, and the same "a filled control doesn't change colour on hover" rule. Accent is the app's action colour, already legible in both themes, so no new token is introduced. Size, position and placement are unchanged — only the fill, so the tabs row does not reflow when Reset appears.

### D10 — A parameterless horizon probe, fixed at `current + 3`, in the last month of the season

Discovery becomes deliberate: a Season or Year page visit asks MAL about **one** season, `current + 3` — one past MAL's default forward window.

- **The target is fixed, not relative to the ceiling.** `Shift(current, +3)`, never "one past wherever the ceiling now is". The difference matters the moment a probe succeeds: a ceiling-relative target would immediately start asking about `current + 4`, which stays closed for about a season — a standing source of daily `404`s. A fixed target means a successful probe ends the probing: `current + 3` is now cached, the ceiling covers it, and nothing is asked again until the calendar rolls and a new `current + 3` appears. The probe is skipped outright whenever its target is already at or below the outer ceiling.
- **It runs only in the final month of the current season, at most once per local day.** MAL opens a season roughly then: this repo's own probe log has `current + 3` answering `404` on 2026-08-18 and open by 2026-09-19, inside the last month of summer 2026. Asking daily all season would spend ~90 requests to learn nothing; this spends at most ~30 per season and usually stops early. Not `SeasonRefreshCadence`: that keys off a season's age and puts every future season in the one-day tier, which is right for a season someone is reading and wrong for a speculative probe. The probe gate is its own rule, checked before delegating to `RefreshAsync`, and is strictly stricter than the cadence gate behind it — so the two can never disagree about whether to fetch.
- **Nothing is lost by not probing.** If no page is opened during the window, `current + 3` simply stays hidden until the calendar rolls and it becomes `current + 2`, inside the default window. That is exactly today's behaviour, which makes the probe a pure improvement with no new failure mode.
- **The target is derived, never named.** `POST /api/season/horizon/probe` takes no year and no season; it computes the target itself and returns the resulting bounds. That keeps "this fetch may reach past the accepted range" a single fixed rule rather than a parameter a caller could abuse — `SeasonController` still refuses that same season when a client names it on the season endpoints.
- **The fetch is the existing one.** Past its own gate, the probe calls `RefreshAsync`, inheriting the per-season single-flight lock, `404`-as-fact, and the caching and pruning rules. No new fetch code, and one MAL request at most per local day across every page, tab and year.
- **It cannot lower the ceiling.** The target is `current + 3`, while `LoadHorizonAsync` only loads candidate points for `current`…`current + 2`. `NotListedToday` therefore returns false for the probed season whatever MAL answered, so `Resolve`'s step-back can never reach it. A `404` probe changes exactly one thing: that season's fetch-log stamp.
- **It never blocks the page.** `useSeasonBounds` (D4) fires it alongside the bounds read and adopts the returned ceiling if it is higher. The guard decides on the `GET /bounds` answer and does not wait — except for the one case in D10a.

*Alternative considered:* raise `FutureSeasonWindow` from 2 to 3. One constant, but discovery still waits for the user to click onto the boundary season, and the dropdown would routinely offer a season that turns out unopened.

*Alternative considered:* have `GET /api/season/bounds` do the probing. Rejected: it is documented as a repository-only read that never calls MAL, the guard waits on it, and that would put a network call in front of every future-dated page render.

### D10a — Addressing the probe target by URL checks it rather than refusing it

A URL naming exactly the probe target is the user saying they believe that season exists, which is better evidence than a calendar heuristic. So the guard, and only for that one season, triggers the probe and decides on the result: data means the ceiling rises and the page renders; no data means the ordinary replacement. Every season further out is still refused with no request at all.

On the Year page the same rule applies to the year **containing** the probe target: that year is checked, any later year is replaced outright.

This on-demand check honours the once-per-local-day gate — a season answered `404` an hour ago is not re-asked — but deliberately **ignores** the last-month window, since the window exists to suppress pointless background asking, not to refuse a question the user actually asked. It is the one case where the guard waits on a MAL round trip, and it is bounded to a single season.

### D11 — The year refresh stops asking about seasons that cannot exist

With D10 owning discovery, `RefreshYearAsync` no longer needs to ask about all four seasons regardless. It filters its loop to the seasons within the outer ceiling, which on the ceiling's year saves up to three guaranteed-`404` MAL requests per visit per day and leaves every past year's behaviour exactly as it is.

Two details to get right: `FoldOutcomes` over an empty list currently returns `Failed`, so a year that yields no season to refresh must be reported `NotListed` instead — defensively, even though an addressable year always has at least one season inside the range. And the filter must use the outer ceiling (the accepted range), not the navigable one, so a season MAL `404`-ed today is still re-read rather than skipped.

## Risks / Trade-offs

- **The `DisplayTitle` expression may not translate to SQL, and the in-memory test provider will not catch it** → use a plain null/empty conditional (the shape Npgsql translates to a `CASE`), and verify against the running Postgres dev stack — load a season page, sort alphabetically, and confirm no client-side-evaluation warning or exception in the API log.
- **A season the API would still serve is now refused by the client** — the one MAL answered `404` for today, whose "not listed yet" message becomes unreachable by URL until the next local day → accepted deliberately (D3): the controls refuse that season too, and one rule beats two. The retreat cannot interrupt a visit in progress, because bounds is read once per mount.
- **A future-dated target waits for `GET /api/season/bounds` before rendering anything** → bounded and rare: the floor-to-current-season range is admitted with no wait, bounds is memoised for the session, and a failed bounds request falls back to `current + 2` rather than walling off the next two seasons.
- **The guard renders nothing while bounds is pending** → only for targets outside the default window, which is not an ordinary visit; ordinary visits never wait, and the first bounds result is memoised for the rest of the session.
- **A silent redirect hides the user's mistake** → that is the requested behaviour ("no message either, nothing, it just cancels"); the replaced URL in the address bar is the only signal, which is enough for a typed-URL mistake.
- **The probe fetches a season the API would refuse if asked by name** → the endpoint takes no parameters and derives its own target, the season endpoints keep refusing that season, and the probe's reach is exactly `current + 3` — a fixed distance that cannot creep outward as the ceiling rises (D10).
- **A probe on every page visit could add MAL traffic** → its own gate caps it at one request per local day, confines it to the final month of the current season, and stops it entirely once the target is cached: at most ~30 requests per season, none at all for the other two months. A failed probe does not consume the interval and is retried on a later visit.
- **Trimming the year loop removes the accidental discovery path** → that is why D11 depends on D10 and is sequenced after it in tasks.md; implemented in the other order, the ceiling would stop rising past `current + 2` entirely.
- **Alphabetical order changes for anime whose English and romaji titles differ** → intended; it is the point of the change, and it makes the visible order match the visible titles.
- **Removing a sort key removes a user's current selection mid-session** → impossible in practice: view state is in-memory only, so the first render after the change always starts from the default (Alphabetical).
