## 1. The detail page keeps my finish date through a rewatch

- [x] 1.1 In `frontend/src/pages/AnimeDetailPage.tsx`, widen the finish-date condition at `:664` from `detail.entry.status === "Completed"` to that status **or** `detail.entry.status === "Rewatching" && detail.entry.completedAt !== null`. Leave the line's text, the `Completed:` label and the `formatDate` call exactly as they are.
- [x] 1.2 Add a short comment above it stating both halves of design D9: `Completed` keeps its `No info` placeholder line unchanged, and `Rewatching` shows the line only when a date is stored — a rewatch is a rewatch of a viewing that was already finished, so the stored date is neither lost nor made wrong by it.
- [x] 1.3 Confirm by inspection that no other status reaches the line, and that nothing in `AnimeDetailPage.tsx` or the completion flow clears `completedAt` when the status moves to `Rewatching` (`CompletionPromptContext.tsx:60` and `EntryEditorOverlay.tsx:40` both already read a stored date on a non-completed entry).

## 2. One rule for a first viewing, reachable without a full entry row

- [x] 2.1 In `backend/AnimeTracker.Api/Services/Watching/WatchMath.cs`, add the primitive overload `FirstViewingEpisodes(int? totalEpisodes, int episodesWatched, WatchStatus? status)` beside the existing entry-shaped one, carrying the rule it states today: `status == WatchStatus.Rewatching ? totalEpisodes ?? episodesWatched : episodesWatched`.
- [x] 2.2 Make `FirstViewingEpisodes(UserAnimeEntry entry)` delegate to it, so the rule exists once. Move the existing XML doc comment onto the primitive and leave a one-line pointer on the entry-shaped one, matching how `EpisodeSeconds`/`RewatchOnlyEpisodes` are already paired.
- [x] 2.3 Note in the primitive's doc why it exists (design D7): the series ranking projection has no `UserAnimeEntry` to pass, and `MemberRewatchSeconds` already reaches `WatchMath`'s primitives for exactly this reason rather than restating their fallbacks.

## 3. Backend: which media-type scopes hold entries

- [x] 3.1 In `backend/AnimeTracker.Api/Services/Profile/TopAnimeMediaTypeScope.cs`, add `public static readonly IReadOnlyList<string> MediaTypes = ["tv", "movie", "ova", "ona", "special"]` — the five non-`all` scopes in the profile's tab order — and rebuild `ValidScopes` from `MediaTypes` plus `All` so the two can never drift. Document that `All` is deliberately absent: `Matches(All, …)` is true for everything, so including it here would make every scope look occupied.
- [x] 3.2 In `ProfileDto.cs`, add `public record ScopeOptionsDto(List<string> TopAnime, List<string> Rewatched)` with a doc comment stating design D1: per box, the media-type scopes holding at least one entry that box would list, in tab order, with `all` and `series` deliberately absent because the client offers those unconditionally (design D3).
- [x] 3.3 Add `ScopeOptions` to `ProfileDto` as a new field. Put it beside `Stats`/`EpisodeProgress` rather than at the end, and extend `ProfileDto`'s own summary comment with one sentence about it.
- [x] 3.4 In `ProfileService.cs`, add `private static ScopeOptionsDto BuildScopeOptions(List<UserAnimeEntry> entries, AnimeRankingSnapshot snapshot)`: for each scope in `TopAnimeMediaTypeScope.MediaTypes`, keep it for **My top anime** when `snapshot.RankedEntries` holds one matching it, and for **Most rewatched** when `entries` holds one with `RewatchCount > 0` matching it — both via `TopAnimeMediaTypeScope.Matches`, so the `special`/`tv_special` rule is not restated.
- [x] 3.5 Comment `BuildScopeOptions` with design D2: each predicate is the owning section's own membership rule (`BuildTopAnimeSection`'s ranking membership; `BuildRewatchedSection`'s `RewatchCount > 0`), so "offered" and "non-empty" cannot disagree; and note that the one-sided rule is deliberate — a type is offered when it has entries, not only when selecting it would also narrow what `All` shows.
- [x] 3.6 Call it from `GetProfileAsync`, passing the `entries` and `rankingSnapshot` it already holds, and pass the result into the `ProfileDto` construction. No new query and no second ordering pass.

## 4. Backend: "Most time spent" by series

- [x] 4.1 In `backend/AnimeTracker.Api/Services/Series/SeriesRankingIndex.cs`, add `private static long MemberWatchedSeconds(SeriesRankingMemberProjection m)` beside `MemberRewatchSeconds`: return 0 when `EntryStatus is null`; otherwise `(FirstViewingEpisodes(m.TotalEpisodes, m.EpisodesWatched ?? 0, m.EntryStatus) + RewatchEpisodesIncludingCurrentRun(m.RewatchCount ?? 0, m.TotalEpisodes, m.EpisodesWatched ?? 0, m.EntryStatus)) * EpisodeSeconds(m.AverageEpisodeDurationSeconds)`, cast to `long` before the multiply as `MemberRewatchSeconds` does.
- [x] 4.2 Comment it with design D7: this is the profile's **Days** expression per entry, reached through `WatchMath`'s primitives so a franchise total and the whole-list stat can never disagree; the explicit not-in-my-list guard states that rule where it is decided rather than leaving it to two null-coalescings; and it inherits `RewatchEpisodesIncludingCurrentRun`'s documented no-published-total fallback gap unchanged, still a lower bound.
- [x] 4.3 Add `public List<SeriesWatchTimeResult> TimeSpentSeries()` mirroring `RewatchedSeries()`: sum `MemberWatchedSeconds` over each series' members, skip a total of zero or less, resolve display fields from the root member via `SeriesIdentity.Resolve`, order by total descending then `Title` with `StringComparer.OrdinalIgnoreCase`.
- [x] 4.4 Document on it (design D8) that a total above zero is exactly "some member in my list has at least one episode watched", because every contribution is episodes times a strictly positive duration — so no second eligibility predicate is needed — and that neither `EligibleSeries()`'s two-aired-main-line-entries coverage rule nor `ListedSeries()`'s version-neighbour rule applies, for the reason `RewatchedSeries()` already records.
- [x] 4.5 Add `public sealed record SeriesWatchTimeResult(int SeriesId, string Title, string? EnglishTitle, string? PictureUrl, long WatchedSeconds)` beside `SeriesRewatchResult`, with a doc comment in the same shape.
- [x] 4.6 In `ProfileDto.cs`, add `TimeSpentSeriesItemDto(int SeriesId, string Title, string? EnglishTitle, string? PictureUrl, long WatchedSeconds)` and `TimeSpentSeriesSectionDto(List<TimeSpentSeriesItemDto> Items)`, documented like the rewatched-series pair and noting that `SeriesId` is the root entry's MAL id so a link built from it reaches the root anime.
- [x] 4.7 In `IProfileService.cs`, add `Task<TimeSpentSeriesSectionDto> GetTimeSpentSeriesSectionAsync(CancellationToken ct = default)`.
- [x] 4.8 Implement it in `ProfileService` beside `GetRewatchedSeriesSectionAsync`: load the index via `seriesRankingLookup`, map `TimeSpentSeries()` through a `ToTimeSpentSeriesItem` projector. Carry over the "no `ScheduleMissingSeriesBuildsAsync` call here" comment with its reason (design D6) — the same page's Top series read already backfills on every visit.
- [x] 4.9 In `ProfileController.cs`, add `[HttpGet("api/profile/time-spent-series")] GetTimeSpentSeries`, with a doc comment mirroring `GetRewatchedSeries`' and stating design D6 explicitly: it pays for its own `SeriesRankingLookup` load rather than being folded into `top-series`, which is accepted because the lookup short-circuits on a cold install and the repo already runs this shape of query per keystroke in `SeriesSearchLookup`.

## 5. Backend tests

- [x] 5.1 Add `backend/AnimeTracker.Api.Tests/Services/Profile/ProfileServiceScopeOptionsTests.cs` covering `BuildScopeOptions` through `GetProfileAsync`: a media type with entries is listed; one with none is not; `all` and `series` never appear in either list; the order is `TopAnimeMediaTypeScope.MediaTypes`' order.
- [x] 5.2 In the same file, cover the two populations differing: an entry scored but never rewatched puts its type in `TopAnime` only; an entry rewatched but excluded from the ranking (unscored, or plan-to-watch) puts its type in `Rewatched` only.
- [x] 5.3 Cover the `special`/`tv_special` case: an entry of media type `tv_special` offers `special` in both lists exactly as a `special` one does.
- [x] 5.4 Add `ProfileServiceTimeSpentSeriesTests.cs`, reusing `ProfileServiceRewatchedSeriesTests`' `CreateDb`/`CreateService`/`AddSeriesShell`/`AddMember` helper shape, covering: a main-line member's first viewing counts; an extra's counts; rewatches add on top; a member not in my list contributes nothing; a partially-watched currently-airing member contributes its watched episodes; a `Rewatching` member contributes one full run plus its in-progress episodes.
- [x] 5.5 Add eligibility and ordering cases: a franchise whose listed members have zero episodes watched is omitted entirely rather than returned at zero; a franchise with one watched entry out of many is listed; ordering is total descending with an alphabetical, case-insensitive tie-break.
- [x] 5.6 Add one case pinning design D7's guarantee: a single-entry franchise's `WatchedSeconds` equals that entry's own contribution to `BuildStats`' `Days` (the same episodes × the same duration), so the two figures are wired to the same arithmetic rather than to two copies of it.
- [x] 5.7 Add a `WatchMath` case for the new overload if `WatchMathTests` exists, else assert the delegation indirectly through 5.6: `FirstViewingEpisodes(total, watched, status)` and `FirstViewingEpisodes(entry)` agree for a Rewatching entry, a Rewatching entry with no published total, and an ordinary watching entry.
- [x] 5.8 Update every existing `ProfileDto` construction and assertion the new `ScopeOptions` field breaks (`ProfileServiceStatsTests` and its neighbours), keeping their existing expectations untouched.
- [x] 5.9 Run `dotnet test` from `backend/` and get it green.

## 6. Frontend: types and client

- [x] 6.1 In `frontend/src/api/types.ts`, add `ScopeOptionsDto { topAnime: TopAnimeMediaType[]; rewatched: TopAnimeMediaType[] }` and the field `scopeOptions: ScopeOptionsDto` on `ProfileDto`. Type the arrays as `TopAnimeMediaType[]`, not `string[]`, so a tab list filtered by them type-checks without a cast.
- [x] 6.2 Add `TimeSpentSeriesItemDto { seriesId: number; title: string; englishTitle: string | null; pictureUrl: string | null; watchedSeconds: number }` and `TimeSpentSeriesSectionDto { items: TimeSpentSeriesItemDto[] }`, placed beside the rewatched-series pair.
- [x] 6.3 In `frontend/src/api/client.ts`, add `getTimeSpentSeriesSection(): Promise<TimeSpentSeriesSectionDto>` hitting `/api/profile/time-spent-series`, beside `getRewatchedSeriesSection` and carrying the same "does not trigger background series builds" note.

## 7. Frontend: the two scope controls offer only what has entries

- [x] 7.1 In `frontend/src/pages/ProfilePage.tsx`, rename the two existing state reads to `storedMediaType` / `setMediaType` and `storedRewatchedMediaType` / `storedRewatchedScope` with their setters, leaving every `useRestorableState` key string exactly as it is — the stored shape does not change (design D4).
- [x] 7.2 Add a helper that answers "is this scope offered", reading `profile?.scopeOptions`: `all` and `series` always true; a media type true when the relevant list includes it; **everything** true while `profile` is null, so the pre-load render keeps today's behaviour. Comment that the null case is unreachable with a stale value in practice — a fresh visit stores `'all'`, and a restore seeds `profile` from the same snapshot on the same render (design D4).
- [x] 7.3 Derive `mediaType`, `rewatchedMediaType` and `rewatchedScope` from their stored values through that helper, falling back to `'all'`. Guard `rewatchedMediaType` as well as `rewatchedScope`: it is the value the row returns to when leaving `Series` and can go stale on its own.
- [x] 7.4 Point the three `usePageData` keys, the two `useStripScroll` keys and the tabs' `aria-selected` at the **derived** values, so what is fetched, what is scrolled and what reads as selected can never diverge from what is shown.
- [x] 7.5 Confirm by inspection that nothing writes a scope in response to `profile` loading — the only writers stay the tab buttons and `selectRewatchedScope` (design D4).
- [x] 7.6 Build the "My top anime" tab list as `MEDIA_TYPE_TABS` filtered to `all` plus the offered media types, and render the whole `profile-media-tabs` row only when that list holds more than one entry. Comment the guard: `All` alone is a control with nothing to choose between, reachable only when the ranking is empty, where the box already shows its own message.
- [x] 7.7 Build the "Most rewatched" row the same way from `REWATCHED_SCOPE_TABS`, keeping `all` and `series` unconditionally so the row always holds at least two options and is always drawn (design D3).
- [x] 7.8 Cut the now-unreachable per-media-type empty message on "My top anime" — the `` `No scored ${…} yet.` `` branch — leaving the `mediaType === 'all'` message as the section's only empty state, and simplify the conditional accordingly.
- [x] 7.9 Cut the five media-type entries from `REWATCHED_EMPTY_MESSAGES`, leaving `all` and `series`, and narrow its type from `Record<RewatchedScope, string>` to the two keys so a future re-add has to be deliberate. Update the lookup at the media-type branch to read the `all` message.

## 8. Frontend: the "Most time spent" section

- [x] 8.1 Add a `usePageData<TimeSpentSeriesSectionDto>('time-spent-series', getTimeSpentSeriesSection)` call beside the existing section loads. Unlike `rewatched-series` it is **not** keyed to an idle variant: the section is always rendered, so it always loads.
- [x] 8.2 Add `const timeSpentStripScroll = useStripScroll('time-spent-series')` beside the other three.
- [x] 8.3 Render the new `profile-box` section directly after "Most rewatched" and before the favourites row: a plain `<h2>Most time spent</h2>` with no band and no control row (design D5), then the strip or its empty message.
- [x] 8.4 Build the strip from `.time-spent-strip` / `.time-spent-strip--fits` (the `STRIP_VISIBLE_TILES` comparison the other three use), each tile a `<Link to={`/series/${item.seriesId}`}>` with the picture-or-placeholder pair and a `<span className="time-spent-strip__count">{formatRewatchTime(item.watchedSeconds)}</span>` badge.
- [x] 8.5 Give it the same "hold the last section on screen" ref the other strips use only if it is needed — it is not, since the section has no view control and so never switches resource key. Add a one-line comment saying so, mirroring the note already on the Top series load.
- [x] 8.6 Empty state: when the loaded section has no items, render `<p className="profile-page__section-empty">` with a message that names the likely cause the way Top series' does — no series with any watch time yet, with the Settings → "Build all series from my list" link — rather than a bare "nothing here".
- [x] 8.7 In `frontend/src/pages/ProfilePage.css`, add a `.time-spent-strip` block mirroring `.top-series-strip`'s (tile basis, gap, hover scale, `:active` cursor, scrollbar hiding, `--fits` variant, `__picture` and `__picture--placeholder`), and a `.time-spent-strip__count` reusing `.rewatched-strip__count--time`'s treatment. Head it with the same "Same strip mechanics as …" comment the existing blocks carry, and note design D10: four copies is deliberate for now.
- [x] 8.8 In `frontend/src/utils/anime.ts`, widen `formatRewatchTime`'s comment to say it states any series-level watch total on the profile page — "Most rewatched"'s Series scope and "Most time spent" alike (design D11). Do not rename the function and do not change its body.

## 9. Build and lint

- [x] 9.1 Run `dotnet build` from `backend/` and clear anything the new DTO field and interface member surface.
- [x] 9.2 Run `nvm use 22 && npm run build` in `frontend/` (`tsc -b && vite build`); the narrowed `REWATCHED_EMPTY_MESSAGES` type is the most likely thing to surface a real error.
- [x] 9.3 Run `npm run lint` in `frontend/` and clear anything new.
- [x] 9.4 Re-read the derivations added in 7.2-7.4 by hand against the stored values they wrap. A wrong fallback here is an empty strip with no message, since 7.8/7.9 removed the messages that used to explain one.

## 10. Manual walkthrough

The frontend has no test runner (`frontend/package.json` has no test script or test dependency, and there is no `*.test.*` under `frontend/src`), so each spec scenario is walked by hand. Run the backend and `npm run dev`, with a list holding at least one rewatched TV show, one media type with nothing rewatched, one completed anime now marked Rewatching, and one built series.

**The detail page's finish date**

- [ ] 10.1 Open a completed anime with a finish date: my box shows `Completed: <date>`. Mark it Rewatching and reopen: the same date is still shown, beside my score and the rewatch count.
- [ ] 10.2 Open an anime marked Rewatching that has never had a finish date stored: no finish-date line at all — not a line reading `No info`.
- [ ] 10.3 Open a completed anime with no stored finish date: it still reads `Completed: No info`, unchanged.
- [ ] 10.4 Mark a completed anime Dropped: the finish-date line is gone, even though the date is still stored (check it is still there in the entry editor's finish-date field).
- [ ] 10.5 With both boxes on screen, confirm the pair is still level and no wider than its content in all three states — no finish line, a finish line, and a finish line plus a rewatch count.

**The scope controls**

- [ ] 10.6 On the profile page, confirm "Most rewatched" offers All, Series, and only the media types you have actually rewatched — no ONA or Specials button when nothing of those types is rewatched.
- [ ] 10.7 Confirm "My top anime" offers All plus only the media types you have scored entries for, and that the two rows differ where your list makes them differ.
- [ ] 10.8 Select a media type on one box and confirm the other box's selection and contents are untouched, and that the row of options does not change as you switch between them.
- [ ] 10.9 Select a media type, open an anime from the strip, navigate back: the box is still on that type with its contents, and the strip's scroll offset is where you left it.
- [ ] 10.10 Select Series on "Most rewatched", open a series, navigate back: still on Series, with its own scroll offset.
- [ ] 10.11 With the page on a media type, edit that type's last qualifying entry elsewhere so the scope empties, return to the profile page: the box reads All rather than showing an unexplained empty strip.
- [ ] 10.12 On a database with no series built, select Series: the box reads "No series have been rewatched" and the option is still offered.
- [ ] 10.13 Confirm no `No <type>s have been rewatched` or `No scored <type> yet.` message can be reached by any button on either row.

**Most time spent**

- [ ] 10.14 Confirm the section sits directly below "Most rewatched", above the favourites row, with a plain unbanded title and no control row.
- [ ] 10.15 Confirm the tiles carry series pictures, each opening that series' page, with a white/silver time badge matching "Most rewatched"'s Series badge in shape, size and position.
- [ ] 10.16 Compare one franchise's badge against the same franchise's badge under "Most rewatched" → Series: the time-spent figure is the larger of the two whenever a first viewing exists, and identical in format.
- [ ] 10.17 Confirm a franchise whose members are all plan-to-watch is absent, and one with a single watched entry is present.
- [ ] 10.18 Confirm ordering is longest-first, and that dragging the strip reaches the last tile with the scaled-up edge tile not clipped.
- [ ] 10.19 Check the section's scroll offset is restored independently of the other three strips after a back navigation.
- [ ] 10.20 On a database with no series built, confirm the empty message appears and points at Settings.
