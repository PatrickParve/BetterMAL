## Why

Three unrelated-looking gaps, all about a figure the app already knows but does not show, or shows a control for when there is nothing behind it.

**The detail page loses my finish date the moment I start a rewatch.** The my-score box shows `Completed: <date>` only while my status is `Completed`. Marking an anime Rewatching moves it off that status, so the date I finished it — still stored, still exactly right, and still what the rewatch is a rewatch *of* — disappears from the page. The box is left showing a rewatch count with no first viewing attached to it.

**The profile's two media-type controls advertise scopes that are empty.** "My top anime" and "Most rewatched" each offer a fixed six-option row — All, TV, Movie, OVA, ONA, Specials — built from a constant list rather than from what my list holds. Nothing has ever been rewatched as an ONA or a special, so two of the six buttons on "Most rewatched" do nothing but produce `No ONAs have been rewatched`. Every one of those per-type messages exists only to explain a button that should not have been offered. This is the same complaint the my-list filters answered on 2026-09-20 (`archive/2026-09-20-fit-my-list-filters-to-the-list`), one page over.

**There is no answer to "what have I spent the most time on".** The profile can rank franchises by how long I have spent *re*-watching them ("Most rewatched" → Series) but not by how long I have spent on them in total. The arithmetic is already written — the whole-list **Days** stat is exactly this figure summed over every entry, and `SeriesRankingIndex.RewatchedSeries()` already sums the rewatch half of it per franchise.

## What Changes

**The detail page keeps my finish date through a rewatch**

- The my-score box shows my finish date while my status is **Rewatching**, whenever a finish date is stored, in the same `Completed: <date>` line it uses today.
- The `Completed` case is untouched, placeholder and all: a completed entry with no stored finish date still shows `Completed: No info`. A Rewatching entry with no stored finish date shows no finish-date line at all — there is nothing to keep.
- No other status gains the line. Dropped, On-hold and Watching entries that happen to carry a stored finish date keep hiding it, as today.

**The profile's media-type buttons are drawn from what my list holds**

- Both controls offer **All**, then only those of TV, Movie, OVA, ONA and Specials that hold at least one entry the section would list — a scored, ranked entry for "My top anime"; an entry with a rewatch count above zero for "Most rewatched". A type with nothing behind it is not drawn.
- "Most rewatched" keeps **Series** at all times. Its scope is a franchise total, which can be empty while the media types have entries (series not yet built from my list) and non-empty while every media type is empty (a first rewatch in progress, whose rewatch count is still zero). It is the one option on the row whose emptiness says something, so it keeps its message.
- The per-type empty messages go, being unreachable: `No scored <type> yet.` on "My top anime" and the five `No <type>s have been rewatched` lines on "Most rewatched". `No shows have been rewatched` (All), `No series have been rewatched` (Series) and `Score some anime to build your top list.` stay — each is still reachable.
- A restored selection naming a type that is no longer offered falls back to **All**, the same way a restored favourites score filter already does. Nothing writes to the stored selection as a side effect.
- "My top anime"'s button row is not drawn at all when no media type qualifies — an empty ranking leaves a row of one button with nothing to choose between.

**The profile gains a "Most time spent" section**

- A new box below "Most rewatched": one horizontal poster strip of my franchises ranked by the total time I have spent watching them, in the same form as "Most rewatched"'s Series scope — same tile size, same drag-scroll, same white/silver time badge, each tile opening that series' page.
- A franchise's total is the sum over **every** member in my list, main line and extras alike, of that member's first viewing plus every rewatch, valued at its own episode duration — the same per-entry arithmetic the profile's **Days** stat already uses, so the two can never disagree. Episodes watched of a currently-airing entry count as they are watched.
- Listed when the total is above zero, which is the same as "at least one episode of it watched". A franchise with members in my list but nothing watched is omitted; so is a member not in my list.
- Ordered by total descending, ties alphabetically by title, uncapped.
- No filter control: the section answers one question at franchise level, and the picture, badge and link are all series-level.

No **BREAKING** changes: no stored state changes shape, no existing endpoint changes its response, and every view reachable today is still reachable.

## Capabilities

### New Capabilities

None. Everything here belongs to a section, a box or a control that already has an owning capability.

### Modified Capabilities

- `anime-detail`:
  - **Modified:** "Single anime detail layout" (`spec.md:6`) — the clause "and my finish date while my status is Completed" states the exact rule this change widens, in the requirement's opening sentence and again in its first scenario.
  - **Not modified:** "The two score boxes share one size" (`spec.md:176`). It already says my box "grows as it gains a rewatch-count line and a finish-date line" and takes the pair with it — a Rewatching entry showing both lines is a case that requirement already covers, not a new one.
- `profile-stats`:
  - **Modified:** "My top anime media-type filter" (`spec.md:571`) — today it states a fixed option list ("with the options All (default), TV, Movie, OVA, ONA, and Specials"), which the new rule contradicts. Gains the offered-options rule, the hidden-row case, and the fall-back-to-All rule for a restored selection.
  - **Modified:** "Most rewatched media-type filter" (`spec.md:800`) — same fixed list ("the same options as the 'My top anime' filter"), same treatment, plus the statement that **Series** is exempt and always offered.
  - **Modified:** "Most rewatched empty states" (`spec.md:849`) — today it enumerates seven messages; five of them become unreachable and are removed, leaving All and Series.
  - **Added:** "Most time spent by series" — the new section's membership, totals, ordering and empty state.
  - **Added:** "Profile scope controls offer only scopes with entries" — the offered-options rule itself, stated once for both boxes, with the restored-selection guarantee. Added rather than written twice because it is one rule about how both controls relate to my list.
  - **Modified:** "Rewatch time is stated in days and decimal hours" (`spec.md:700`) — retitled and rescoped from "a series' total rewatch time" to any series-level watch total on the page, so "Most time spent" is bound to the same formatting rather than restating it. The formatting itself does not change.
  - **Not modified:** "Most rewatched section" (`spec.md:741`), "Most rewatched by series" (`spec.md:613`), "My top anime with minimum-of-ten fill and manual selection" (`spec.md:460`), "Top-anime ordering is shared across filters" (`spec.md:598`). What each scope *contains* and how it is ordered is untouched; only which scopes are offered changes.
  - **Not modified:** "Poster strips keep a fixed tile size and scroll horizontally only" (`spec.md:883`), "Poster strips show no scrollbar" (`spec.md:920`), "Profile list-row posters fill the row" (`spec.md:110`), "Profile section titles are banded by family" (`spec.md:1675`). The new strip is a poster strip like the other three and inherits all four as written — including the last one's "remaining section titles SHALL stay plain and unbanded", which is why "Most time spent" gets a plain heading.

## Impact

- **Backend — new endpoint:** `GET /api/profile/time-spent-series`, returning `TimeSpentSeriesSectionDto`. Mirrors `/api/profile/rewatched-series` exactly, including its "does not enqueue background series builds" rule — the same page's Top series read already does that. It pays for its own `SeriesRankingLookup.LoadAsync` join, which is a second such load per profile visit; accepted rather than reshaped into a shared series-sections endpoint (design D6), on the grounds that `SeriesSearchLookup` runs the same shape of query on every keystroke.
- **Backend — DTOs:** `ProfileDto` gains `ScopeOptions` (`ScopeOptionsDto(List<string> TopAnime, List<string> Rewatched)`) — per box, the media-type scopes that hold at least one entry, in tab order, with `all` and `series` deliberately absent since the client always offers those. New `TimeSpentSeriesItemDto`/`TimeSpentSeriesSectionDto`, shaped exactly like the rewatched-series pair with `WatchedSeconds` in place of `RewatchSeconds`.
- **Backend — services:** `ProfileService` gains `BuildScopeOptions` (one pass over the entries and ranking snapshot `GetProfileAsync` already loads) and `GetTimeSpentSeriesSectionAsync`; `IProfileService` gains the matching member. `SeriesRankingIndex` gains `TimeSpentSeries()` beside `RewatchedSeries()` and a `MemberWatchedSeconds` helper beside `MemberRewatchSeconds`. `WatchMath` gains a primitive `FirstViewingEpisodes(int? totalEpisodes, int episodesWatched, WatchStatus? status)` overload so the series projection reaches the same rule the entry form already applies, rather than restating it.
- **Backend — tests:** new `ProfileServiceScopeOptionsTests` and `ProfileServiceTimeSpentSeriesTests`; `SeriesRankingIndex`'s existing rewatch-time coverage is joined by time-spent cases. Existing `ProfileDto` construction in `ProfileServiceStatsTests` and friends picks up the new field.
- **Frontend:** `api/types.ts` (three new types, one new `ProfileDto` field), `api/client.ts` (`getTimeSpentSeriesSection`), `pages/ProfilePage.tsx` (offered-tab derivation for both boxes, the fall-back-to-All derivation, the new section and its strip-scroll hook, the trimmed empty-message maps), `pages/ProfilePage.css` (`.time-spent-strip` block mirroring `.top-series-strip`), `pages/AnimeDetailPage.tsx` (the finish-date condition).
- **Restorable state:** `mediaType`, `rewatchedMediaType` and `rewatchedScope` keep their keys and shapes, and nothing in this change writes to them — a stale value is corrected by derivation at read time, never by a write (design D4). The new section adds one `useRestorableScroll` key, `time-spent-series`.
- **Database:** no schema change, no migration. Every figure here is computed from rows already loaded.
- **The frontend has no test runner** (`frontend/package.json` has no test script or test dependency; there is no `*.test.*` under `frontend/src`), so the frontend half is verified by `tsc -b && vite build`, `oxlint`, and the manual walkthrough in `tasks.md`, as this repo's frontend changes already do.
- **Out of scope:**
  - **Dropping a media-type button that would change nothing.** The my-list filters use a two-sided rule — an option must both remove an entry and leave one — under which a lone `TV` button would go because it selects exactly what `All` already shows. Not adopted here (design D2): the ask was "buttons that actually have entries in them", and the two-sided rule would remove buttons that do have entries.
  - **Computing whether the Series scope is empty on every profile load.** It would need the `SeriesMembers` join on the initial read, which the current design deliberately keeps off it. Series stays always-offered and keeps its message.
  - **Consolidating the four near-identical poster strips.** `top-anime-strip`, `rewatched-strip`, `top-series-strip` and now `time-spent-strip` share their mechanics through copied CSS blocks. Folding them into one is its own change.
  - **Showing the finish date on any other status**, and showing a *start* date anywhere.
