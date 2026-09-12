## 1. Bulk airing reads (backend plumbing)

- [x] 1.1 Add `GetNextAiringInstantsAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset afterUtc, CancellationToken ct = default)` to `Data/Repositories/IEpisodeAiringRepository.cs`, documented like `GetMaxAiredEpisodesAsync` beside it: one read, an anime with no future row absent rather than present, an empty request valid without touching the database.
- [x] 1.2 Implement it in `EpisodeAiringRepository` as the `GroupBy(AnimeId).Select(Min(AirsAtUtc))` mirror of `GetMaxAiredEpisodesAsync`, including its `animeIds.Count == 0 → []` guard.
- [x] 1.3 Declare the two bulk reads on `IEpisodeScheduleService` — `ResolveOnLocalDateAsync(IReadOnlyCollection<AnimeMetadata>, DateOnly, ct)` returning `Dictionary<int, ResolvedEpisode>`, and `NextAiringInstantAsync(IReadOnlyCollection<AnimeMetadata>, DateTimeOffset, ct)` returning `Dictionary<int, DateTimeOffset>` — as **default interface members** that loop over the existing per-anime member (design.md D6). Document that the default exists so a test double need not restate it, and that any implementation reading a database must override it with a single read.
- [x] 1.4 Override both in `EpisodeScheduleService`: the date read as one `GetRowsInRangeAsync` over every id between the local day's two midnight instants, taking the earliest row per anime (the rows come back ordered by `AirsAtUtc`, so this is the same row the per-anime read takes); the next-instant read through the new repository method. Keep `converter.GetLocalTime` as the only source of the local time of day.
- [x] 1.5 Verify no other production implementation of `IEpisodeScheduleService` exists (`grep -rn ": IEpisodeScheduleService" backend/AnimeTracker.Api`) and that the 28 test doubles still compile untouched.

## 2. The my-list read

- [x] 2.1 In `Services/Library/MyListService.cs:30`, replace the per-entry `EpisodesAiredAsOfAsync` loop with the existing bulk `IReadOnlyCollection<AnimeMetadata>` overload over `entries.Select(e => e.Anime).ToList()`, exactly as `MainDashboardService:25` does.
- [x] 2.2 Rewrite the comment above it: the counts are resolved once in bulk for the whole list, and the same dictionary is what `airingWatchStatusService.SettleAsync` receives — no per-entry read.
- [x] 2.3 Confirm the DTO projection still reads `airedSoFarByAnimeId.TryGetValue(...)`, so a missing anime stays `null` (unknown) rather than becoming `0`.

## 3. The dashboard read

- [x] 3.1 In `Services/Dashboard/MainDashboardService.cs:106`, read `airedSoFarByAnimeId` — already built on line 25 — instead of re-querying per current-season item.
- [x] 3.2 Replace the per-entry `ResolveOnLocalDateAsync` loop (`:76`) with one bulk call for the whole entry set against `today`, then build `airingToday` from that dictionary. Keep the existing `OrderBy(x => x.Episode.LocalTime)` ordering of the DTOs.
- [x] 3.3 Materialise the ordered currently-watching entries before the DTO loop, resolve their next airing instants with one bulk call, and feed `NextEpisodeEta.From` from that dictionary instead of awaiting per card (`:65`). An anime absent from the dictionary means "no next episode", the same as today's `null`.
- [x] 3.4 Update the design-decision comments in the method so they describe one bulk read per fact, and cite `AiringScheduleService.GetWeekAsync` as the same shape.

## 4. Backend verification

- [x] 4.1 Add `Services/Library/MyListServiceBulkReadTests.cs`: a schedule double whose per-anime members throw and whose bulk members return canned values (the `PoisonedEpisodeScheduleService` idiom from `Services/Series/SeriesListNoBuildOrMalCallTests.cs`), asserting the read succeeds and every row carries the aired count the bulk result gave it.
- [x] 4.2 Add `Services/Dashboard/MainDashboardServiceBulkReadTests.cs` in the same shape, covering all three facts: the current-season aired counts, the airing-today slots, and the currently-watching countdowns.
- [x] 4.3 Add repository-level coverage for `GetNextAiringInstantsAsync` beside the existing `GetMaxAiredEpisodesAsync` tests: strictly-after semantics, an anime with only past rows absent, an empty request issuing no read.
- [x] 4.4 Add `EpisodeScheduleService` coverage that each bulk read agrees with its per-anime counterpart for the same anime and instant, including the two-rows-on-one-local-date case and a DST-shifted local day.
- [x] 4.5 Compile and run the suite in the .NET 10 SDK image (local SDK is 9.0; `~/Documents` cannot be bind-mounted): `rsync -a --exclude 'bin/' --exclude 'obj/' backend/ /private/tmp/bm-build/` then `docker run --rm -v /private/tmp/bm-build:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 bash -c "dotnet test AnimeTracker.Api.Tests/AnimeTracker.Api.Tests.csproj"`.
- [x] 4.6 With the dev stack up (`docker compose up -d`, backend on `BACKEND_PORT` from `.env`), confirm `/api/my-list` and `/api/dashboard` return the same JSON as before the change — capture both endpoints before and after and diff them — and note the new timings.

## 5. Score reveal lifetime

- [x] 5.1 In `frontend/src/components/ScoreValue.tsx`, reset `revealed` when the page identity changes, using the render-time comparison `useRestorableState`/`usePageData` already use: hold `` `${pathname}|${hidden}` `` (pathname from `useLocation()`) in state beside `revealed`, and clear `revealed` when it differs. No effect, so a hidden score never paints revealed for a frame.
- [x] 5.2 Replace the comment claiming the reveal "naturally resets whenever the surrounding page unmounts on navigation" — that assumption is the bug — with why the identity is the pathname and not `location.key` (design.md D1) and why `hidden` is part of it (D2).
- [x] 5.3 Walk it through in the app: hide scores; open Fate/Zero's detail page; navigate to a related anime and reveal its score; go back — Fate/Zero's score shows its reveal control. Then reveal a score, switch the navbar toggle off and on — every score on the page is hidden again.

## 6. The series page's More section

- [x] 6.1 In `frontend/src/pages/SeriesPage.tsx:475`, change `mineOnly`'s initial value to `false`, and rewrite the block comment above it so it documents the filter as off on a fresh visit.
- [x] 6.2 Simplify `filterActive` (`:820`) to `mineOnly && unfilteredGroups.size === 0`, dropping the "at least one group expanded" clause, and rewrite its comment: the control reports the filter's real state, and group expansion never changes what it reads.
- [x] 6.3 Make `toggleMineOnly` (`:864`) a two-way toggle: while `filterActive`, `setMineOnly(false)` and touch no collapsed state; otherwise keep today's off→on branch (filter on, exemptions cleared, groups holding an admitted extra of mine opened).
- [x] 6.4 Derive `myMediaTypes` from the extras that are in my list, and narrow `selectedMediaTypes` to it in the off→on branch of `toggleMineOnly`, computing that branch's `keysToOpen` from the narrowed set so the two agree within the same render.
- [x] 6.5 Render a type button `disabled` while `mineOnly` is on and its type is not in `myMediaTypes`, with an accessible name saying why (nothing of mine carries that type), plus the disabled styling in `SeriesPage.css`.
- [x] 6.6 Leave `toggleMediaType` otherwise as it is — it must keep opening the groups holding the selected type and must still not touch `unfilteredGroups` or `mineOnly`.
- [x] 6.7 Walk through the flow from the request: open a series page — everything collapsed, no button on; press **Music** — every music entry shows and the "in my list" button stays off; press **in my list** — only my music; then on a franchise where none of my extras is music, press **in my list** with Music selected — Music is dropped and disabled, and my extras show across every group.
- [x] 6.8 Walk through the toggle's second press: with the filter on and groups open, press "in my list" — the filter goes off, the open groups widen to every extra, nothing collapses, and the button reads off.

## 7. The redundant refetch

- [x] 7.1 In `frontend/src/hooks/usePageData.ts`, track the key the current data belongs to (`loadedKeyRef`, set wherever data is stored) and, in the load effect, when the key is unchanged, data is in hand, and this is not a restore: write that data into the new snapshot (`snapshot.data.set(key, data)`) and return without loading.
- [x] 7.2 Document the guard against the hook's own contract — an unchanged key means the data does not depend on what the URL changed — and state that a restore keeps its silent background refresh.
- [x] 7.3 Check the pages that write view state to the URL still behave: the series browser's status/progress/multi filters and sort, my list's recap-scope chip and its dismissal, search's type filter, and the season/year pages' own controls. Each should filter or re-sort with no network request, and back/forward over those entries should still land on the right view.

## 8. Frontend verification

- [x] 8.1 Typecheck and build with Node 22 (default `node` here is v16 and Vite fails on it): `cd frontend && PATH="$HOME/.nvm/versions/node/v22.23.1/bin:$PATH" npm run build`.
- [x] 8.2 Run the linter the same way and confirm no new warnings, in particular none from the exhaustive-deps rule around the edited effect in `usePageData`.
- [x] 8.3 Re-check the reveal fix on the pages that render a reveal control but were not part of the walk-through: my list rows, the Top anime page, series entry rows and extra tiles, the profile divergence lists, and a recap's hot takes — a reveal on each survives a filter change on that page and is gone after navigating to another page.

## 9. Close out

- [x] 9.1 Re-read each delta spec against the implementation and confirm every scenario is satisfied, including the ones that only changed wording.
- [x] 9.2 Note in `ISSUES.md` that the two N+1 loops and the redundant refetch are fixed, if that document still tracks them.
- [x] 9.3 Run `openspec validate fix-score-reveal-and-nplus1`
