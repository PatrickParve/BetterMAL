## Why

The series page ships a 60-member cap that silently truncates the largest franchises — One Piece loses real entries the user has watched, and the page only says "too large to show in full". Beyond that, the page is missing the affordances its siblings already have (broadcast progress, external links) and its score behaviour doesn't match how the user actually reads it: the MAL averages auto-reveal mid-run, single-entry-line series show a redundant "everything" average, and the highest-score / favourite stats collapse ties down to one arbitrary winner with no way to say which of the tied entries is really the favourite.

## What Changes

**Series size**
- Raise `SeriesGraphBuilder.MemberCap` from 60 to 400, so no real franchise is truncated.
- Rebuild becomes a client-driven loop: each round spends the existing 20-fetch budget, and the page keeps issuing rounds while the series is still partial and each round is making progress, reporting member count as it goes. Removes the "click Rebuild eight times" grind without turning Rebuild into a minute-long blocking request (MAL calls are paced at 1/sec).

**Series page additions**
- The home page's blue airing-progress bar (broadcast progress in blue, my watched progress layered in purple) replaces the plain watched/total bar in **My progress** whenever a member of the series is currently airing. A new `mainLineAiredEpisodes` stat carries the aired count.
- An external-links row — MyAnimeList, AniList, SeriesGraph — pointing at the series root (entry 1 of the main line), matching the anime detail page's row. Needs the root's AniList id in the series projection.
- A personal-completion badge next to the status pill: when every main-line entry that has finished airing is Completed in my list, the page says so ("Completed" / "Caught up" while the series is still running).

**Score and stats behaviour**
- **Highest MAL score** and **My favourite** list *every* entry tied at the top score, not just the first one.
- **Highest MAL score** auto-reveals under the hide-scores toggle when the entry holding it is one I have completed and scored.
- **My favourite** ties can be reordered by hand, and that order persists across rebuilds (new `SeriesMember.FavouriteRank` column + migration + endpoint).
- A MAL average auto-reveals only when nothing in the series is currently airing *and* the group is fully completed — a still-airing season, movie, or special now blocks the reveal regardless of my own scores.
- Completing the whole main line overrides that: **MAL · everything** is then never blurred.
- When a series has no extras, **MAL · everything** and **Mine · everything** are not rendered — they would duplicate the main-series averages exactly.

## Capabilities

### New Capabilities

None — this extends the existing series page.

### Modified Capabilities

- `series-page`: raises the member cap and makes Rebuild an auto-continuing loop; adds the airing-progress bar, external links, and personal-completion badge; changes tie handling, reveal rules, and redundant-average suppression for the score stats; adds a persisted favourite ordering.

## Impact

**Backend**
- `Services/Series/SeriesGraphBuilder.cs` — `MemberCap` 60 → 400.
- `Services/Series/SeriesService.cs` — aired-episode aggregation, root AniList id, tie-returning highest/favourite stats, favourite-order read/write.
- `Services/Series/SeriesDto.cs` — `RootAniListId`, `MainLineAiredEpisodes`, `HighestMalScoreAnimeIds`/`MyHighestScoreAnimeIds` (lists replacing the single ids), `MainLineCompletedByMe`.
- `Models/SeriesMember.cs` + new EF migration — nullable `FavouriteRank`.
- `Controllers/SeriesController.cs` — `PUT /api/series/{seriesId}/favourite-order`.
- `Services/Airing/IEpisodeScheduleService` — read-only reuse for aired counts (currently-airing members only).

**Frontend**
- `pages/SeriesPage.tsx` / `.css` — links row, completion badge, airing bar, conditional "everything" boxes, tie lists, favourite reorder controls, rebuild loop with progress.
- `components/AiringProgressBar.tsx` — reused as-is.
- `api/types.ts`, `api/client.ts` — DTO changes and the favourite-order call.

**Docs/specs**
- `openspec/specs/series-page/spec.md` via the delta in this change.
