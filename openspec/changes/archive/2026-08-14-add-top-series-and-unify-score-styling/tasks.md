## 1. Shared series-average computation (backend)

- [x] 1.1 Extract `SeriesService`'s private `MalAverage`/`MyAverage` into a shared `Services/Series/SeriesAverages.cs` static helper that takes scores (not `SeriesMember`s), keeping the score-0-means-unscored rule and the `SeriesAverageDto(Value, ScoredCount, TotalCount)` shape.
- [x] 1.2 Rewrite `SeriesService.BuildScores` to call the helper, confirming the series page's four averages are byte-identical to before (existing `SeriesServiceBuildStatsTests` must still pass).
- [x] 1.3 Also lift the main-line completion rule (`MainLineCompletedByMe`) so the ranking lookup can reuse it rather than duplicating the "every finished-airing main-line member is Completed, and at least one exists" logic.

## 2. Series ranking read path (backend)

- [x] 2.1 Add `Services/Series/SeriesRankingLookup.cs` (scoped, registered in `Program.cs`), mirroring `SeriesSearchLookup`: one projection of **main-line** `SeriesMembers ⋈ AnimeMetadata ⋈ UserEntry` — `(SeriesId, RootAnimeId, AnimeId, MalScore, MyScore, EntryStatus, AiringStatus)` — plus each series' root title/English title/picture and its full member count.
- [x] 2.2 Return an empty result cheaply when no series are stored at all, so a fresh install pays nothing (same guard `SeriesSearchLookup` uses).
- [x] 2.3 Compute per series, in memory: MAL main-line average and my main-line average (via `SeriesAverages`), each with its scored/total counts.
- [x] 2.4 Compute per series the MAL-average reveal boolean using the series page's main-series rule — main line completed by me **and** no main-line member currently airing (design decision 5).
- [x] 2.5 Determine eligibility separately from the main-line projection: a series is eligible when **any** member (main line or extra) has a user entry, so a series known only through an extra still qualifies (design decision 2).

## 3. Top series section (backend)

- [x] 3.1 Add `TopSeriesItemDto` (series id, root anime id, title, English title, picture url, entry count, MAL main average + counts, my main average + counts, MAL reveal boolean) and `TopSeriesSectionDto` to `Services/Profile/ProfileDto.cs`.
- [x] 3.2 Add `GetTopSeriesSectionAsync` to `IProfileService`/`ProfileService`, composing the ranking lookup with the entry repository; return **every** eligible series with both averages, unsorted-by-basis (the client picks basis — design decision 4).
- [x] 3.3 Apply the deterministic base ordering in the DTO so a client that does nothing still gets something sensible: my average descending, then scored main-line count descending, then raw title case-insensitively.
- [x] 3.4 Add `GET /api/profile/top-series` to `ProfileController` with an XML doc comment explaining that both averages ship in one payload and the basis is a client-side re-sort.
- [x] 3.5 Decide whether the section also rides inside `GET /api/profile` for the initial render (as `TopAnime`/`Rewatched` do) — include it only if it does not measurably slow the profile load; otherwise leave it to its own endpoint and note why in the controller comment.

## 4. On-read backfill (backend)

- [x] 4.1 In the top-series read path, collect my-list anime ids with no `SeriesMembers` row, reusing the projection from 2.1 rather than issuing a second query.
- [x] 4.2 Enqueue at most 20 of them per request on the existing `ISeriesBuildTrigger`, fire-and-forget — never awaited, never able to throw into the response (design decision 6).
- [x] 4.3 Verify the existing dedupe in `SeriesBuildTrigger` means repeat profile visits advance to new ids instead of re-queueing the same batch.

## 5. Bulk "build all series from my list" (backend)

- [x] 5.1 Add `Services/Series/ISeriesBulkBuildTrigger.cs` + `SeriesBulkBuildTrigger.cs` mirroring `IAiringFullRefreshTrigger` (signal, not a queue — the run computes its own targets).
- [x] 5.2 Add `Services/Series/ISeriesBulkBuildProgressTracker.cs` + `SeriesBulkBuildProgressTracker.cs` mirroring `AiringFullRefreshProgressTracker`: locked in-memory snapshot of `(Phase: NotStarted|Running|Complete, Built, Total)`.
- [x] 5.3 Add `Services/Series/SeriesBulkBuildBackgroundService.cs` mirroring `AiringFullRefreshBackgroundService`: on signal, resolve targets (my-list anime with no series member row), `Start(total)`, then for each target re-check membership first — count an already-covered target as processed without building — call `ISeriesService.GetSeriesAsync`, `ReportProgress`, and `Complete()` at the end.
- [x] 5.4 Log-and-continue on a per-target failure (including `SeriesNotFoundException` for a lone anime, which is a normal outcome, not an error); never abort the run.
- [x] 5.5 Register the trigger and tracker as singletons and the background service as a hosted service in `Program.cs`, beside the existing airing-refresh registrations.
- [x] 5.6 Add `POST /api/series/build-all` and `GET /api/series/build-all/status` to `SeriesController`, returning the tracker snapshot, mirroring the airing full-refresh endpoints.

## 6. Global score colour tokens (frontend)

- [x] 6.1 Move `--mal`/`--mal-bg`/`--mal-border` and `--mine`/`--mine-bg`/`--mine-border` from `.series-page` in `SeriesPage.css` to `:root` in `index.css`, in **both** the light and dark blocks, carrying over the aliasing rationale comment.
- [x] 6.2 Leave `--airing`/`--airing-bg`/`--airing-border` scoped to `.series-page`, and update that block's comment to say why green stayed behind.
- [x] 6.3 Confirm nothing that means "Completed status" or "accent" was repointed — `SeriesEntryRow`'s stripe and the status pills must still read `--status-completed`/`--accent` directly.

## 7. Shared score presentation components (frontend)

- [x] 7.1 Add `components/ScoreChip.tsx` + `ScoreChip.css`: a chip taking `role` (`'mal' | 'mine'`), an optional label, a value (children), and a size variant covering both the header-chip and the compact tile-chip densities already on the series page.
- [x] 7.2 Have the MAL variant render its value through `ScoreValue` (passing `completed` through), so hiding, per-score reveal, and the always-show-completed setting are untouched; style the blur placeholder to inherit the MAL colour.
- [x] 7.3 Add shared `.score--mal` / `.score--mine` utility classes (colour + tabular-nums, no box) for the dense-row density, in `index.css` or a small shared stylesheet.
- [x] 7.4 Replace `SeriesPage`'s `MalScoreChip`/`MineScoreChip` and their `series-page__score-chip*` CSS with `ScoreChip`, keeping the `value · N of M scored` text and the existing reveal rules exactly.
- [x] 7.5 Replace `SeriesTimeline`'s `series-timeline__card-chip*` and `SeriesExtraTile`'s `series-extra-tile__chip*` with the compact `ScoreChip`, keeping tile geometry unchanged.

## 8. Apply the language to the remaining surfaces (frontend)

- [x] 8.1 `AnimeDetailPage`: render the MAL score and my score as `ScoreChip`s in the score boxes, labelled as they are today.
- [x] 8.2 `MyListRow`: colour-only treatment (`.score--mal` on the MAL cell), keeping row height and column alignment unchanged.
- [x] 8.3 `TopAnimePage`: colour-only treatment on the `top-anime-row__mal-score` and `top-anime-row__my-score` cells.
- [x] 8.4 `ProfilePage` divergence rows: colour-only treatment on the `Me N · MAL N` trailing line, so the two numbers are distinguishable without the labels doing all the work.
- [x] 8.5 `ProfilePage` My-top-anime poster badge: give the score badge the "mine" role while keeping it legible over the poster (tint, not plain text on an image).
- [x] 8.6 `SeriesEntryRow`: apply the roles to `series-entry-row__mal-score`/`__my-score`, which are currently uncoloured despite sitting on the series page.
- [x] 8.7 Audit for any score render site missed — grep `ScoreValue`, `myScore`, and `malScore` across `pages/` and `components/` and confirm each hit is either restyled or deliberately excluded (e.g. the settings page's reconciliation-diff prose, the score-distribution histogram). Found and fixed two additional gaps: `SeriesPage`'s "Highest MAL score" and "My favourite" tie-lists rendered raw uncoloured scores.

## 9. Score controls (frontend)

- [x] 9.1 `MyListRow`'s inline score `<select>`: apply the "mine" role (colour, tint, border) while keeping it a native select; render the unscored `—` state neutrally.
- [x] 9.2 `EntryEditorOverlay`'s score `<select>`: same treatment, same neutral unscored state.
- [x] 9.3 Verify keyboard, pointer, and mobile behaviour of both selects is unchanged, and that contrast holds in light and dark themes. No handler/markup logic changed (only a conditional class), so interaction is unaffected; visually verified colour/contrast in both themes via a static preview harness rendered through headless Chromium — screenshots confirmed clear blue/purple separation, legible contrast, and the hidden-score blur placeholder correctly inheriting the MAL tint (design.md decision 10).

## 10. Top series section (frontend)

- [x] 10.1 Add `TopSeriesItemDto`/`TopSeriesSectionDto` to `api/types.ts` and `getTopSeriesSection()` to `api/client.ts`.
- [x] 10.2 Add the Top series section to `ProfilePage.tsx` between My top anime and Most rewatched, loaded through `usePageData` under its own resource key, with a drag-scroll strip reusing `useDragScroll`.
- [x] 10.3 Add the basis control (My score / MAL score) backed by `useRestorableState`, defaulting to `mine`; sort and filter the loaded array client-side (average desc → scored count desc → title), omitting items with no value under the active basis.
- [x] 10.4 Render each tile: poster, link to `/series/{rootAnimeId}`, both averages as compact `ScoreChip`s (MAL through `ScoreValue` with the server's reveal boolean), and a `title` attribute carrying both `N of M scored` counts.
- [x] 10.5 Add `ProfilePage.css` rules for `top-series-strip*` matching the existing strips' tile size, hover-scale, padding, and scroll behaviour — factor the shared strip rules if the third copy makes that obviously cleaner.
- [x] 10.6 Empty states: nothing eligible → explain that series are still being discovered and name the Settings "Build all series from my list" action; nothing rankable under the active basis → say so for that basis.

## 11. Settings bulk-build UI (frontend)

- [x] 11.1 Add `SeriesBulkBuildStatusDto` to `api/types.ts` and `triggerSeriesBulkBuild()` / `getSeriesBulkBuildStatus()` to `api/client.ts`.
- [x] 11.2 Add a "Build all series" section to `SettingsPage.tsx` modelled on the "Airing dates" section: hint text explaining what it does and that it runs in the background, a progress line (`Building… built/total`, then `Last run complete: built/total processed.`), and a button disabled while running.
- [x] 11.3 Load its status in the page's `load()` callback and poll it while the phase is `Running`, reusing the existing polling effect pattern.

## 12. Tests

- [x] 12.1 `SeriesAverages` tests: score-0 exclusion, null-when-nothing-scored, counts, and parity with the pre-extraction `SeriesService` results.
- [x] 12.2 `SeriesRankingLookup` tests: main-line-only averaging, eligibility via an extra-only list entry, the MAL reveal boolean (completed main line with and without a currently-airing member), and the empty-store fast path.
- [x] 12.3 `ProfileService` top-series tests: base ordering including the scored-count tie-break, series with no list member excluded, both averages present on every item.
- [x] 12.4 Backfill tests: at most 20 ids enqueued per read, only ids with no series membership, never throwing into the response, and dedupe across repeated reads.
- [x] 12.5 Bulk-build tests: targets resolved from my list, an already-covered target counted as processed without a build, a failing target not aborting the run, and the tracker's phase/count transitions.
- [x] 12.6 Run `dotnet test` for the backend suite (via the project's Docker `sdk:10.0` build path) and `npm run build` + `npm run lint` for the frontend.

## 13. Manual verification

- [ ] 13.1 Profile page: Top series ranks by my score by default, both averages show on every tile, and a tile opens the right series page.
- [ ] 13.2 Switching the basis to MAL reorders instantly, changes membership where expected, and the strip neither collapses nor changes height.
- [ ] 13.3 Back-navigation from a series page restores the chosen basis; a fresh visit resets it.
- [ ] 13.4 A tile's two averages match that series' page chips exactly.
- [ ] 13.5 Settings: "Build all series from my list" starts, shows advancing `built/total`, disables its button while running, and reports final counts; new series show up in Top series afterwards.
- [ ] 13.6 With the hide-scores toggle on: every restyled MAL score still blurs, reveals per score, and honours "always show completed" — including inside chips and on the Top series tiles.
- [ ] 13.7 Walk every score surface (anime detail, My List, Top anime, profile lists and strips, series page, timeline, extras tiles) in both light and dark themes and confirm one consistent blue/purple reading with legible contrast.
