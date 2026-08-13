## 1. Backend: per-entry aired episodes

- [x] 1.1 Add `AiredEpisodes` (`int?`) and `AiredTo` (`DateOnly?`) to `SeriesEntryDto` in `Services/Series/SeriesDto.cs`, documenting the null-means-unknown contract from design decision 1 (finished → total, airing → schedule reader clamped to total, not-yet-aired → 0, unknown status → null).
- [x] 1.2 In `SeriesService.ProjectAsync`, build one `Dictionary<int, int?>` of per-member aired counts (only `currently_airing` members hit `IEpisodeScheduleService.EpisodesAiredAsOfAsync`, as today) and pass it into `ToEntryDto`.
- [x] 1.3 Derive `MainLineAiredEpisodes` from that same map instead of the separate `MainLineAiredEpisodesAsync` walk, keeping its current value exactly: only main-line members with a known `TotalEpisodes` contribute, finished → total, airing → `min(aired ?? 0, total)`, otherwise 0. Note in a comment why the aggregate and the per-entry field differ for a member with an unknown total.
- [x] 1.4 Project `AiredTo` from `AnimeMetadata.AiredTo` in `ToEntryDto`.
- [x] 1.5 Compile the backend: rsync `backend/` to `/private/tmp/bm-build` and `dotnet build AnimeTracker.Api/AnimeTracker.Api.csproj -c Release` in the `mcr.microsoft.com/dotnet/sdk:10.0` image (local SDK is 9.0, and `~/Documents` can't be bind-mounted).

## 2. Frontend types and shared colour tokens

- [x] 2.1 Add `airedEpisodes: number | null` and `airedTo: string | null` to `SeriesEntryDto` in `api/types.ts`.
- [x] 2.2 Declare the page-scoped `--mal*` / `--mine*` aliases on `.series-page` in `SeriesPage.css` (design decision 4), mapping to `--status-completed*` and `--accent*`.

## 3. Header: hero layout, badge, progress readout

- [x] 3.1 Replace `completionBadge` with the four-case precedence of design decision 2: `Completed` / `Caught up` / `N behind` / none, computed from `series.mainLine` (per-entry `airedEpisodes` vs `entry?.episodesWatched`, treating a missing entry as 0 watched). Return null when any currently-airing main-line entry has `airedEpisodes === null`.
- [x] 3.2 Style the `N behind` badge distinctly from `Caught up` and `Completed` (its own modifier class, warning-toned rather than accent-toned).
- [x] 3.3 Restructure the header markup into a hero: larger poster (`clamp()` to roughly 200px wide, existing 3:4-ish ratio), with title, status pill, badge, year span, links row, score chips, and the progress readout inside it. Keep the Rebuild row and partial/truncated notices below the hero.
- [x] 3.4 Convert `MalScoreBox`/`MineScoreBox` into compact chips — MAL chips in `--mal*`, mine chips in `--mine*` — keeping the existing labels, `ScoreValue` reveal behaviour, count suffix, and the "no extras → no everything-averages" suppression untouched.
- [x] 3.5 Add an opt-in label mode to `AiringProgressBar` that suppresses its inline `aired/total` label, defaulting to today's behaviour so `HomePage` renders identically.
- [x] 3.6 Move **My progress** out of the stats `<dl>` into the hero and render the named readout beside the bar: watched (purple), aired (blue, only while a member is currently airing), and the main-line total with its `+` lower-bound marker when `hasUnknownEpisodeCounts`.

## 4. Timeline ribbon

- [x] 4.1 Add `components/SeriesTimeline.tsx` + `.css` taking the main-line entries and the hide-scores flag: flex row of alternating gap spacers (`flex-grow: gapDays`) and entry blocks (`flex-grow: max(1, durationDays)`, `min-width` floor), per design decision 6.
- [x] 4.2 Render each block's fills — purple watched (`episodesWatched / totalEpisodes`), blue aired underlay while that entry is airing — plus its `#n · year` label wrapped in a `Link` to the detail page, with the full title on `title`/`aria-label`.
- [x] 4.3 Render the per-entry score lane: MAL bar (blue) and my bar (purple) at `score/10` height, omitting the MAL side entirely — replaced by the existing note — while the hide-scores toggle is on.
- [x] 4.4 Mark the longest gap on its spacer: length in words plus the two entries it falls between, each a `Link`, sourced from `stats.longestGapDays` / `longestGapFromAnimeId` / `longestGapToAnimeId`.
- [x] 4.5 Handle the degradations: undated main-line entries in a trailing cluster, equal-width fallback when no entry has a date, and `overflow-x: auto` on the container so the page never scrolls sideways.
- [x] 4.6 Delete the old **Score comparison** section and its CSS from `SeriesPage.tsx` / `SeriesPage.css`, and drop the **Longest gap** row from the stats `<dl>` now that the ribbon carries it.

## 5. Main series rows and the More section

- [x] 5.1 In `SeriesEntryRow`, add the aired figure for a currently-airing entry (`12 of 24 aired`, never `12/24`) to the meta line, and my watched-against-total figure to the status cell for an entry I've started but not completed.
- [x] 5.2 Add `components/SeriesExtraTile.tsx` + `.css`: poster, 2-line-clamped title, `year · N ep`, MAL chip (blue) and my-score chip (purple), status as a coloured left border plus short label, and the shared edit control — reusing the same aired/watched wording helpers as the row.
- [x] 5.3 Turn each More group heading into a toggle button (`Movies (4)`, caret, `aria-expanded`/`aria-controls`) with tiles laid out in an `auto-fill` grid, rendering no tiles at all while collapsed.
- [x] 5.4 Initialise collapse state from `series.extras.length > 12` (all collapsed above the threshold, all expanded at or below) and add the single Expand all / Collapse all control whose label reflects whether any group is open.
- [x] 5.5 Confirm an edit saved from a tile patches in place through the existing `handleEdit` → `patchSeriesEntry` path, and that the header badge recomputes from the patched entries with no refetch.

## 6. Verification

- [x] 6.1 Build the frontend with `PATH="$HOME/.nvm/versions/node/v22.23.1/bin:$PATH" npm run build` (default node is v16 and Vite fails on it).
- [x] 6.2 Run the stack (`docker compose up`) and check an ongoing franchise where a main-line season is mid-run: the badge reads `N behind` with unwatched aired episodes, flips to `Caught up` once watched up to the broadcast, and the readout names watched/aired/total separately.
- [x] 6.3 Check a finished franchise I've completed (`Completed` badge, no aired figure in the readout) and one with an unfinished finished-airing entry (no badge at all).
- [x] 6.4 Check a franchise with many extras (One Piece): every More group starts collapsed with counts, Expand all opens them, and tiles carry the same facts the rows did.
- [x] 6.5 Check the timeline on a series with a multi-year gap: the gap is visibly wide and labelled, blocks show my progress, and with hide-scores on the MAL bars are absent from the DOM (not just blurred) while mine still render.
- [x] 6.6 Confirm the home page's airing bar still renders its `aired/total` label unchanged.
- [x] 6.7 Run `openspec validate redesign-series-page` and update `CODE_GUIDE.md`'s `SeriesPage` row (line ~482) to describe the hero, tiles, and timeline instead of the score-comparison strip.
