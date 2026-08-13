## 1. Series size and rebuild loop (backend)

- [x] 1.1 Raise `SeriesGraphBuilder.MemberCap` from 60 to 400 and update its XML doc to say the cap is a runaway-component safety ceiling, not a working limit (design decision 1).
- [x] 1.2 Confirm `SeriesService.NeedsBuild` still ignores `IsTruncated` and add a comment recording why (design decision 3: a genuinely over-cap series would otherwise spend fetch budget on every visit forever).

## 2. Data model: favourite ordering

- [x] 2.1 Add nullable `int? FavouriteRank` to `Models/SeriesMember.cs` with a doc comment stating it is a tie-break hint over whatever the tied set currently is, null meaning unranked.
- [x] 2.2 Add the EF migration `AddSeriesMemberFavouriteRank` — `dotnet ef migrations add` inside the `mcr.microsoft.com/dotnet/sdk:10.0` image (local SDK is 9.0; rsync `backend/` to `/private/tmp/bm-build` first since `~/Documents` can't be bind-mounted, then copy the generated files back). Additive nullable column, no backfill.
- [x] 2.3 Verify `SeriesGraphBuilder.PersistAsync` preserves `FavouriteRank` across a rebuild — it updates existing member rows in place via `existingByAnimeId`, so no code change should be needed; add a comment there if the invariant isn't obvious.

## 3. Series projection (backend)

- [x] 3.1 Extend `SeriesDto`: add `RootAniListId`, and to `SeriesStatsDto` add `MainLineAiredEpisodes` and `MainLineCompletedByMe`; replace `HighestMalScoreAnimeId`/`MyHighestScoreAnimeId` with `HighestMalScoreAnimeIds`/`MyHighestScoreAnimeIds` (`List<int>`). Update the record doc comments.
- [x] 3.2 In `SeriesService.ProjectAsync`, read the root's AniList id from `AnimeAiringSyncs` the way `AnimeDetailService` does.
- [x] 3.3 Compute `MainLineAiredEpisodes` over main-line members with a known `TotalEpisodes` only: finished → total, currently airing → `min(IEpisodeScheduleService.EpisodesAiredAsOfAsync(...) ?? 0, total)`, not-yet-aired → 0 (design decision 4). Inject `IEpisodeScheduleService` into `SeriesService`; only airing members should hit it.
- [x] 3.4 Compute `MainLineCompletedByMe`: at least one main-line member has finished airing, and every finished-airing main-line member has `UserEntry.Status == Completed` (design decision 7).
- [x] 3.5 Return every tied entry from the highest-MAL and my-highest stats. Order tied MAL entries by watch order (main line by `Order`, then extras); order tied favourites by `FavouriteRank` ascending with unranked last, then watch order (design decision 8).
- [x] 3.6 Add `PUT /api/series/{seriesId}/favourite-order` to `SeriesController` taking an ordered anime-id list: write `0..n-1` to those members, null the rank on every other member of the series, reject ids that aren't members (400) and an unknown series (404). Put the write on `ISeriesService`/`SeriesService`.

## 4. Frontend types and client

- [x] 4.1 Update `api/types.ts` to match the new DTO shape (`rootAniListId`, `mainLineAiredEpisodes`, `mainLineCompletedByMe`, `highestMalScoreAnimeIds`, `myHighestScoreAnimeIds`).
- [x] 4.2 Add `setSeriesFavouriteOrder(seriesId, animeIds)` to `api/client.ts`.

## 5. Series page: header, links, progress

- [x] 5.1 Add the personal-completion badge beside the status pill — "Completed" when every member has finished airing, "Caught up" when the series still has an airing or unaired member, nothing otherwise — driven by `stats.mainLineCompletedByMe` and `series.status`. Style it in `SeriesPage.css` distinctly from the status pill.
- [x] 5.2 Add the external-links row (MyAnimeList / AniList / SeriesGraph) targeting `series.rootAnimeId`, with the AniList id-or-title-search fallback and the SeriesGraph title search, mirroring `AnimeDetailPage`'s block and reusing its link styling.
- [x] 5.3 In the **My progress** stat, render `AiringProgressBar` when `series.status === 'Ongoing'` (aired = `stats.mainLineAiredEpisodes`, watched = `stats.myWatchedEpisodes`, total = main-line total or null, finished = false) and keep `ProgressBar` otherwise (design decision 5).

## 6. Series page: scores and stats

- [x] 6.1 Suppress **MAL · everything** and **Mine · everything** when `series.extras.length === 0`, and let the score-box grid reflow to two columns.
- [x] 6.2 Replace `isGroupCompleted(...)` as the `completed` input to the MAL boxes with the precedence rule: `mainLineCompletedByMe` reveals both; otherwise reveal a group only when the group is completed **and** no member of the whole series is currently airing (design decision 9).
- [x] 6.3 Render every tied entry for **Highest MAL score** and **My favourite**, each linking to its detail page, instead of the single-entry lookups.
- [x] 6.4 Pass `completed` to the highest-MAL `ScoreValue` when that entry is one I have completed and scored, so it isn't blurred.
- [x] 6.5 Add up/down reorder buttons to the **My favourite** list, shown only when 2+ entries are tied: reorder locally, `PUT` the full ordered list, and revert to the server order on failure.
- [x] 6.6 Keep `recomputeScores`/`patchSeriesEntry` consistent with the server after an in-place row edit — an edit can change which entries tie for my highest score, so the tie lists must be recomputed locally too (or the page must refetch).

## 7. Series page: rebuild loop

- [x] 7.1 Turn `handleRebuild` into a round loop: call `rebuildSeries`, continue while the result is `isPartial` **and** `mainLine.length + extras.length` grew, stopping on not-partial, no growth, error, 12 rounds, or unmount (abort ref).
- [x] 7.2 Show round progress on the button — `Rebuilding… N entries` — and keep the existing partial/truncated notices for the final state.

## 8. Verification

- [x] 8.1 Compile the backend: rsync `backend/` to `/private/tmp/bm-build` and `dotnet build AnimeTracker.Api/AnimeTracker.Api.csproj -c Release` in the `mcr.microsoft.com/dotnet/sdk:10.0` image.
- [x] 8.2 Build the frontend with `PATH="$HOME/.nvm/versions/node/v22.23.1/bin:$PATH" npm run build` (default node is v16 and Vite fails on it).
- [x] 8.3 Run the stack (`docker compose up`, backend on `BACKEND_PORT`) and check One Piece end to end: Rebuild loops to completion, the series is no longer truncated, and previously missing entries appear under More. (Verified via API against the live containers/DB — no browser tool available in this session; see note below.)
- [x] 8.4 Check an ongoing series (blue aired bar, "Caught up" badge, MAL averages stay blurred with hide-scores on) and a finished one I have completed ("Completed" badge, both MAL averages revealed, no "everything" boxes when it has no extras). (Verified the underlying data for real ongoing/finished/completed series via API; visual rendering not screenshotted — no browser tool available.)
- [x] 8.5 Check the favourite reorder: tie three entries at my highest score, reorder, reload, and Rebuild — the order survives both. (Verified via API on series 10 (Shingeki no Kyojin); order persisted across reload and Rebuild, then reset back to unranked.)
- [x] 8.6 Run `openspec validate improve-series-page` and update `docs/SPEC_CONFORMANCE.md` if it tracks the series-page requirements touched here. (Validated OK; SPEC_CONFORMANCE.md is a dated 2026-07-19 snapshot audit that predates the series-page capability, so it doesn't track it — left unchanged.)
