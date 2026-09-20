## Context

**The detail page's finish-date line.** `AnimeDetailPage.tsx:664` renders `<p>Completed: {formatDate(detail.entry.completedAt)}</p>` behind `detail.entry.status === "Completed"`. `formatDate(null)` returns the page's `NO_INFO` placeholder, so a completed entry with no stored date already shows `Completed: No info`. `completedAt` is a plain field on the entry and is not cleared when the status moves to `Rewatching` — the backend's completion flow writes it on completion and the entry editor exposes it as "finish date" under a `finishedOnce` guard (`EntryEditorOverlay.tsx:40`) that already treats `completedAt !== null || rewatchCount > 0 || status === 'Completed'` as "this has been finished at least once". The data is there; only the render condition hides it.

**How the two profile scope controls are built.** `MEDIA_TYPE_TABS` in `ProfilePage.tsx:151` is a module constant of six `{value,label}` pairs, and `REWATCHED_SCOPE_TABS` (`:167`) is that list with `Series` spliced in after `All`. Both rows `.map()` straight over their constant. Nothing on the page knows which of those scopes holds anything: each tab is its own `usePageData` resource key (`top-anime:${mediaType}`, `rewatched:${rewatchedMediaType}`), fetched only once selected, so the page learns a scope is empty by asking for it. `REWATCHED_EMPTY_MESSAGES` (`:173`) is the seven-way map that explains the answer.

**How a section's membership is decided on the server.** `BuildTopAnimeSection` (`ProfileService.cs:470`) filters `snapshot.RankedEntries` by `TopAnimeMediaTypeScope.Matches(mediaType, e.Anime.MediaType)` and then applies the score-tier/fill rules; `BuildRewatchedSection` (`:520`) filters `entries` by `RewatchCount > 0` plus the same `Matches`. Both run over data `GetProfileAsync` has already loaded — `entries` from `GetAllForListViewAsync` and the `AnimeRankingSnapshot` built at `:78`. `TopAnimeMediaTypeScope` holds the scope names in a `HashSet` used only for validation; the tab *order* lives in the frontend constant.

**How a franchise total is computed today.** `SeriesRankingIndex.RewatchedSeries()` (`SeriesRankingIndex.cs:422`) walks `_membersBySeriesId`, sums `MemberRewatchSeconds` over every member, keeps the series when the total is above zero, resolves display fields from the root member via `SeriesIdentity.Resolve`, and orders by total then title. `MemberRewatchSeconds` (`:475`) is `WatchMath.RewatchEpisodesIncludingCurrentRun(...) * WatchMath.EpisodeSeconds(...)` over the projection's null-guarded `RewatchCount`/`EpisodesWatched`/`EntryStatus`. The projection already carries every field a watch total needs: `TotalEpisodes`, `EpisodesWatched`, `RewatchCount`, `EntryStatus`, `AverageEpisodeDurationSeconds`.

**What the profile's Days stat is.** `BuildStats` (`ProfileService.cs:367`): `(FirstViewingEpisodes(e) + RewatchEpisodesIncludingCurrentRun(e)) * EpisodeSeconds(e.Anime)`, summed over **every** entry whatever its media type or status. "Most time spent" is that same per-entry expression grouped by franchise instead of summed whole — which is the strongest argument for reusing `WatchMath` rather than writing a second formula.

**Loading and restoration.** `usePageData` seeds synchronously from the history entry's snapshot on a restore (`usePageData.ts:51-66`), so on back-navigation `profile` is non-null on the very first render — the same render on which `useRestorableState` seeds `mediaType`/`rewatchedScope` from that same snapshot. On a fresh visit neither is seeded and `useRestorableState` returns its `'all'` initial. Both facts matter for D4.

**No frontend test runner.** `frontend/package.json` has `dev`, `build`, `lint`, `preview` and no test dependency; there is no `*.test.*` under `frontend/src`. The backend has xUnit tests under `backend/AnimeTracker.Api.Tests/`, including a `Services/Profile/` folder this change adds to.

## Goals / Non-Goals

**Goals:**

- A finish date already stored stays visible through a rewatch, without changing what a completed entry shows.
- Neither profile scope control offers a media type with nothing behind it, and no empty-state message exists to explain a button that is no longer drawn.
- A selection restored from history is never silently rewritten, and never leaves the page showing a scope that is no longer offered.
- A franchise-level "how much time have I spent on this" ranking that agrees, entry for entry, with the profile's own **Days** figure.
- One formatting rule for every series-level time total on the page, not one per section.

**Non-Goals:**

- Changing what any scope *contains*, how it is ordered, or how "My top anime" fills to ten.
- Dropping a media-type button on the grounds that it would select the same set as **All** (D2).
- Knowing, on the initial profile read, whether the Series scope is empty (D3).
- Adding a media-type or any other filter to "Most time spent" (D5).
- Consolidating the page's four poster-strip CSS blocks (D10).
- Adding a frontend test harness.

## Decisions

### D1 — The offered scopes are computed server-side and ride on the profile payload, not on each section

Which media types hold entries is a question about the **whole list**, not about the scope currently selected, so no per-scope response can answer it on its own. Three places could carry the answer:

| Where | Cost | When the client knows |
| --- | --- | --- |
| A new field on `ProfileDto` | one extra pass over `entries`/`RankedEntries` already in memory | as soon as `profile` resolves — which is also when the page stops rendering `Loading…` |
| A field on `TopAnimeSectionDto`/`RewatchedSectionDto` | the same pass, repeated on every tab switch | after whichever scope's fetch resolves |
| Derived on the client | none server-side, but needs the whole list in the browser | never — the page holds only the resolved section |

The first is taken. `ProfileDto` gains `ScopeOptions`, a `ScopeOptionsDto(List<string> TopAnime, List<string> Rewatched)` holding, per box, the media-type scopes with at least one entry, in tab order. The page already gates its whole body on `profile` being loaded (`ProfilePage.tsx:427`), so the tab rows are never drawn before the answer is in hand — which is what makes D4's derivation correct on the first painted frame rather than a frame later.

`all` and `series` are deliberately **absent** from both lists. They are not media types and are not conditional (D3), so including them would invite the client to filter its tab list by a single `includes` and quietly make `All` droppable. The client offers those two itself and consults `ScopeOptions` for the other five.

*Alternative considered:* putting the lists on the two section DTOs, so a `reloadTopAnime()` after a rank edit refreshes them too. Rejected: it answers a whole-list question in a per-scope payload, and the staleness it would fix cannot occur — the rank overlay reorders within a score tier and cannot move an anime between media types or into/out of the ranking.

### D2 — A type is offered when it holds entries: the one-sided rule, not my list's two-sided one

`archive/2026-09-20-fit-my-list-filters-to-the-list` established a **two-sided** rule for the my-list filters: an option is offered only when choosing it would both remove at least one listed entry and leave at least one. Applied here it would drop a `TV` button when every rewatched entry is a TV series, since `TV` and `All` would select the same strip.

Not adopted. The ask was for buttons "that actually have entries in them", and the two-sided rule removes buttons that *do* have entries — every entry, in fact. The two controls also differ from my list's in a way that matters: a my-list filter is one of several narrowing controls whose combination is what the user is steering, so a no-op option is genuinely noise; these are a single exclusive scope selector where `All` and `TV` coinciding is a statement about the list worth being able to read off the row.

So: **a media type is offered when the section, scoped to it, would list at least one entry.** Concretely —

- **My top anime:** at least one entry in `snapshot.RankedEntries` of that media type. Membership of the ranking (scored, not plan-to-watch, aired) is the same gate `BuildTopAnimeSection` applies before its tier/fill rules, and any non-empty scope yields at least one item once those rules run, so "offered" and "non-empty" cannot disagree.
- **Most rewatched:** at least one entry with `RewatchCount > 0` of that media type — exactly `BuildRewatchedSection`'s own predicate.

Both are evaluated with `TopAnimeMediaTypeScope.Matches`, so `Specials` keeps matching both `special` and `tv_special` without that rule being written twice.

### D3 — `Series` is exempt and keeps its message

`Series` sits on the same row as the media types but is not one (`profile-stats` "Most rewatched media-type filter": "a different axis to slice by rather than one more type among them"), and its emptiness is not decidable from the same data:

- it can be **empty while media types have entries** — a rewatched anime whose series has not been built from my list yet belongs to no franchise;
- it can be **non-empty while every media type is empty** — a first rewatch in progress has `RewatchCount == 0`, so it is in no media-type scope, but `RewatchEpisodesIncludingCurrentRun` counts its episodes, so its franchise has a total.

Deciding it would mean running the `SeriesMembers ⋈ AnimeMetadata` join on the initial profile read, which `ProfileController`'s own comments say is exactly what that read is kept clear of. `Series` is therefore always offered and keeps `No series have been rewatched`, which now carries real information: "no franchise of yours has rewatch time — and if you have rewatched things, your series have not been built yet."

### D4 — A stale restored selection is corrected by derivation at read time, never by a write

Three `useRestorableState` values can name a scope that is no longer offered: `mediaType`, `rewatchedMediaType` and `rewatchedScope`. None of them is written as a side effect. Instead each is read through a derivation that falls back to `'all'` when the stored value is not on offer — the pattern `favouriteYearsScore`/`favouriteSeasonsScore` already use (`ProfilePage.tsx:398-407`), and the rule `fit-my-list-filters-to-the-list` D4 settled for the same reason: writing view state in response to a data load loses a selection the user will want back.

The derived value is what feeds both the `usePageData` key and the tab row's `aria-selected`, so the page never fetches a scope it will not display. While `profile` is null the stored value is taken as-is, which keeps today's behaviour on the only render where the answer is unknown. That render is not reachable with a stale value in practice: on a fresh visit the stored value *is* `'all'`, and on a restore `usePageData` seeds `profile` from the same snapshot `useRestorableState` seeds from, on the same render. The one residual case — a restored snapshot whose profile predates a list edit — costs one background fetch of a scope that is then corrected, with the restored strip already on screen throughout.

`rewatchedMediaType` is guarded as well as `rewatchedScope`, not just the one the row reads: it is a second stored value that remembers the last media type while `Series` is selected, and switching back out of `Series` would otherwise land on a scope that is no longer offered.

### D5 — "Most time spent" is series-level, with no filter control

Every part of the ask is series-level — the series picture, main line plus extras, a franchise total. A media-type row would have to mean "franchises whose … is a movie", which is not a property a franchise has. `Series` is not offered either, since the whole section is that scope. So the box carries a title and a strip and nothing else, and it is the only strip on the page with no control above it.

It sits **below "Most rewatched"** and above the favourites row: the two rewatch/time strips read as a pair, and both are franchise-shaped once "Most rewatched" is on `Series`.

Its heading stays plain and unbanded, per `profile-stats` "Profile section titles are banded by family" — it is about neither a season, a year, nor a disagreement.

### D6 — Its own endpoint, paying for its own index load

`GET /api/profile/time-spent-series` mirrors `/api/profile/rewatched-series` line for line, including the rule that it does **not** enqueue background series builds (the same page's Top series read already does). Because the section is always rendered rather than hidden behind a tab, this means a profile visit now loads the `SeriesMembers ⋈ AnimeMetadata ⋈ UserEntry` projection twice: once for Top series, once for this.

Accepted. `SeriesRankingLookup`'s own header calls this "the same shape of work as `SeriesSearchLookup` on every keystroke", and `SeriesRankingLookup.LoadAsync` short-circuits to `SeriesRankingIndex.Empty` without the join when no series exists at all, so the cold-install case pays nothing. The two sections also load independently, so the new one cannot delay Top series appearing.

*Alternatives considered:*

- **Fold the list into `TopSeriesSectionDto`.** One join, but it makes an endpoint named for one section carry two, and couples the two sections' loading states so neither paints until both are computed.
- **A new `GET /api/profile/series-sections` replacing `top-series`.** The honest shape if a third series-level section ever lands, but it churns an existing endpoint, its client function, its `usePageData` key and its `useRestorableScroll` key for one join's worth of saving.
- **Caching the index.** `SeriesRankingLookup` is resolved per request, so a field on it is per-request state — and the two reads are separate HTTP requests. A cross-request cache would be a new invalidation problem for a query the repo already treats as keystroke-cheap.

### D7 — The total reuses the Days arithmetic through a new `WatchMath` primitive

`SeriesRankingIndex` works over `SeriesRankingMemberProjection`, not `UserAnimeEntry`, and `WatchMath.FirstViewingEpisodes` only takes the latter. `MemberRewatchSeconds` already solves this shape of problem by calling the *primitive* overloads (`RewatchEpisodesIncludingCurrentRun`, `EpisodeSeconds(int?)`) that exist so callers without a full row do not restate the fallbacks.

So `WatchMath` gains the matching primitive:

```csharp
public static int FirstViewingEpisodes(int? totalEpisodes, int episodesWatched, WatchStatus? status) =>
    status == WatchStatus.Rewatching ? totalEpisodes ?? episodesWatched : episodesWatched;
```

and the existing entry-shaped overload delegates to it, so there is one copy of the rule. `MemberWatchedSeconds` is then the Days expression over a projection:

```csharp
private static long MemberWatchedSeconds(SeriesRankingMemberProjection m)
{
    if (m.EntryStatus is null) return 0;           // not in my list
    var episodes = WatchMath.FirstViewingEpisodes(m.TotalEpisodes, m.EpisodesWatched ?? 0, m.EntryStatus)
        + WatchMath.RewatchEpisodesIncludingCurrentRun(m.RewatchCount ?? 0, m.TotalEpisodes, m.EpisodesWatched ?? 0, m.EntryStatus);
    return (long)episodes * WatchMath.EpisodeSeconds(m.AverageEpisodeDurationSeconds);
}
```

The explicit `EntryStatus is null` guard is not redundant with a zero episode count: `FirstViewingEpisodes` reads `EpisodesWatched ?? 0` for a non-list member, which is already zero — but stating the rule makes "a member not in my list contributes nothing" readable at the point it is decided rather than inferable from two null-coalescings. It also inherits `RewatchEpisodesIncludingCurrentRun`'s documented fallback gap for an entry with no published total, unchanged and still a lower bound.

*Where the Days stat and a franchise total can still differ:* Days covers every entry in my list; a franchise total covers only entries that belong to a stored series. A franchise's total is exact for its members; the page's totals do not sum to Days until every anime has a series. That is the same coverage caveat "Most rewatched"'s Series scope already carries.

### D8 — Eligibility is "total above zero", which is exactly "at least one episode watched"

`RewatchedSeries()` keeps a series when its total is above zero; `TimeSpentSeries()` does the same. Because every member's contribution is `episodes × a strictly positive duration` (`EpisodeSeconds` falls back to 24 minutes, never zero), a total above zero holds if and only if some member in my list has at least one episode watched — which is the stated rule, reached without a second predicate. A plan-to-watch franchise, or one whose only listed member has zero episodes watched, is therefore omitted rather than listed at `0h`.

Neither `EligibleSeries()`'s two-aired-main-line-entries coverage rule nor `ListedSeries()`'s version-neighbour rule applies, for the reason `RewatchedSeries()` already records: those exist to protect an *average*, and a sum of one entry's time is exactly right.

### D9 — The detail page's finish date: additive for Rewatching, untouched for Completed

The render condition becomes: show the line while the status is `Completed` (unchanged, placeholder and all), **or** while it is `Rewatching` and `completedAt` is non-null.

The tidier-looking rule — "show it whenever a finish date exists, whatever the status" — is not taken. It would newly surface a stored date on Dropped, On-hold and Watching entries, which nobody asked for and which reads oddly on an entry that was re-opened after being completed. Keeping `Completed`'s existing `No info` line rather than hiding it when the date is missing is the same restraint in the other direction: it is today's behaviour, and changing it is not what this is for.

A Rewatching entry with no stored finish date shows no line, so my box keeps the height it has today in that case — which "The two score boxes share one size" already governs without amendment, since it defines the pair's size from what the boxes hold.

### D10 — A fourth strip block in CSS, not a shared one

`.top-anime-strip`, `.rewatched-strip` and `.top-series-strip` are three near-identical CSS blocks; `.top-series-strip`'s own comment ("Same strip mechanics as top-anime-strip/rewatched-strip") acknowledges it. `.time-spent-strip` is written the same way, carrying the same tile basis, hover scale, scroll-hiding and `--fits` variant, and its badge reuses `.rewatched-strip__count--time`'s treatment.

Folding all four into one block is the right change and is not this one: it touches three working sections to make a fourth marginally cheaper, and the page's strips have drifted deliberately in places (the top-series tile carries two chips, not a badge).

### D11 — The time formatter is bound once, by rescoping the requirement rather than restating it

`formatRewatchTime` already implements the days/decimal-hours rule the `profile-stats` requirement "Rewatch time is stated in days and decimal hours" defines, and "Most time spent" must read identically — the two strips sit one above the other. Rather than a second requirement repeating the seven rounding scenarios, that requirement is **retitled and rescoped** to cover any series-level watch total on the profile page, with its rules and scenarios otherwise unchanged. The function keeps its name (it is called from two places and renaming it is churn with no reader benefit) but its comment states the wider scope.

Totals here are much larger than rewatch totals — years of viewing render as, say, `142d 6.4h` — which the formatter already handles: days are unbounded and only the hours part is capped below 24.

## Risks / Trade-offs

- **A profile visit now loads the series projection twice.** → Accepted per D6; short-circuited entirely on a cold install, keystroke-cheap by the repo's own precedent, and the two sections load in parallel so neither blocks the other. If it ever bites, D6 records the shared-endpoint shape to move to.
- **`ScopeOptions` is computed once per profile read and can go stale within a visit.** → The profile page offers no control that can change an entry's media type, score, or rewatch count (the rank overlay reorders within a tier), so it cannot drift under the user. Any edit made elsewhere returns through a fresh profile read.
- **A restored snapshot older than a list edit can fetch a scope that is then corrected.** → One background fetch, with the restored strip on screen throughout and no empty flash (D4). Not worth gating the section fetches on `profile` to avoid.
- **Removing five empty-state messages relies on the fallback in D4 actually holding.** → If the derivation were wrong, a stale scope would render an empty strip with no explanation at all. Mitigated by deriving the `usePageData` key from the same value the tabs read, so "selected" and "fetched" cannot diverge, and by walking the restore case explicitly in the manual test plan.
- **A franchise total under-reports while series coverage is incomplete.** → Inherent to every series-level section on the page and already true of "Most rewatched" → Series; `Top series`' own empty state points at Settings → "Build all series from my list", and the new section's empty state says the same thing in its own words.
- **`WatchMath.FirstViewingEpisodes`' new overload changes a shared helper.** → The entry-shaped overload delegates to it with identical arguments, so every existing caller's behaviour is unchanged by construction; the backend's existing `ProfileServiceStatsTests` cover that path.

## Migration Plan

No migration. No schema change, no data backfill, no stored-state format change. The new endpoint is additive and the new `ProfileDto` field is additive; an older client ignoring `scopeOptions` would render exactly today's six tabs. Rollback is reverting the commit.

## Open Questions

None. The two that mattered were settled before drafting: the finish date shows for Completed and Rewatching only (D9), and `Series` stays permanently offered with its message intact (D3).
