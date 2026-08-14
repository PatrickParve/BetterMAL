## Context

**Series data.** A franchise lives in two tables — `Series` (id, `RootAnimeId`, `BuiltAt`, partial/truncated flags) and `SeriesMember` (anime id PK, series id, `IsMainLine`, `Order`, `FavouriteRank`). `Series.Id` was deliberately made stable across rebuilds with this exact feature in mind ("a later 'my top series' feature ranks series by it"). Series are built lazily by `SeriesGraphBuilder`, triggered today by a series-page visit (`SeriesService.ResolveAsync`) or by search (`ISeriesBuildTrigger` → `SeriesBuildTriggerBackgroundService`). Builds cost MAL fetches (visit budget 8, rebuild budget 20) and are single-flighted through `RefreshGate`.

**Series scores.** `SeriesService` computes four averages fresh on every read — never cached — as `MalAverage`/`MyAverage` over main-line members and over all members. My-average excludes score 0 (MAL's "unscored"). The header renders them as chips (`MAL · main series`, `Mine · main series`, plus `· everything` variants when extras exist), each carrying `value · scoredCount of totalCount scored`.

**Profile sections.** `ProfileService` builds My top anime and Most rewatched from `IUserAnimeEntryRepository.GetAllAsync()` in memory, each exposed both inside `GET /api/profile` and as its own filterable endpoint. The frontend fetches each through `usePageData` under a distinct resource key, holds the last-loaded section in a ref so switching filters doesn't collapse the strip, and keeps filter state in `useRestorableState` so back-navigation restores it.

**Score colour language.** `SeriesPage.css` declares `--mal`/`--mine`/`--airing` (plus `-bg`/`-border`) aliases on `.series-page`, deliberately page-scoped so `--status-completed` keeps meaning "Completed status" elsewhere. Blue is MAL/broadcast, purple is me, green is on-air-now. Three components consume them (`SeriesPage`, `SeriesTimeline`, `SeriesExtraTile`) because they render inside `.series-page`. Every other score render site — `AnimeDetailPage`, `MyListRow`, `TopAnimePage`, `ProfilePage`'s divergence rows and poster badges — renders both scores as plain text.

**Score hiding.** `ScoreValue` owns MAL-score blur/reveal. While hidden and unrevealed the numeric value is never placed in the DOM. Callers pass `completed` to opt a score into the "always show completed scores" setting; the series page computes a richer per-group reveal rule (`malGroupRevealed`) and passes its result through the same prop.

**Constraints shaping this design:**

- No MAL fetch may be added to a request path. Every build stays on a background queue.
- A ranking can only see franchises that have already been built, and on a fresh install almost none have.
- Series averages must not be recomputed by a second, subtly different implementation — a strip disagreeing with the page it links to would be a bug report.
- The profile page's existing strip/filter/restoration patterns are established; the new section should be recognisably the same thing, not a new pattern.

## Goals / Non-Goals

**Goals:**

- Rank my franchises on the profile page by the main-series average, switchable between my score and MAL's, showing both figures per series.
- Reuse the series page's own average computation so the two surfaces can never disagree.
- Make the ranking's coverage self-healing (background backfill) and completable on demand (a Settings action with visible progress).
- Make blue-MAL / purple-mine one app-wide convention rather than a series-page detail, including on the controls that *set* my score.
- Change nothing about whether a MAL score is visible — only how it looks once visible.

**Non-Goals:**

- Manual reordering of the top-series strip (My top anime's tier editor has no analogue here — the ranking is a continuous average, not a tier with ties to break).
- Media-type tabs on Top series. A series spans media types by definition; filtering one out would mean averaging a franchise's TV seasons while hiding its movies.
- Ranking by the "everything" averages, or by anything other than the main line.
- Caching series averages, denormalising them onto `Series`, or adding a scheduled full-catalogue build.
- Restyling non-score figures (rewatch counts, episode counts, ranks, popularity) or the score-distribution histogram.
- Changing `ScoreValue`'s hide/reveal semantics or the always-show-completed setting.

## Decisions

### 1. A dedicated lightweight read path for series averages, sharing one averaging implementation

Top series does **not** call `SeriesService.GetSeriesAsync` per series. That path loads every member with relations and user entries, resolves AniList ids, and hits `IEpisodeScheduleService` for airing counts — reasonable for one franchise, untenable for every franchise in a list.

Instead, a new `SeriesRankingLookup` (mirroring `SeriesSearchLookup`'s "one projection per request, match in memory" shape) loads a single projection of **main-line** members joined to their anime metadata and user entry: `(SeriesId, RootAnimeId, AnimeId, MalScore, MyScore, EntryStatus, AiringStatus)` plus each series' root title/picture and its total member count. Averages are then computed in memory.

The averaging itself is lifted out of `SeriesService`'s private statics into a shared `SeriesAverages` helper that both call — `SeriesService` for the page, `ProfileService` for the strip.

*Why:* one query, one implementation. The alternative — reimplementing "mean of non-null MAL scores" and "mean of my scores above zero" in `ProfileService` — is four lines that would drift the first time a rule changes (the score-0 rule already has a design decision behind it).

*Alternative considered:* denormalise the two averages onto `Series` at build time. Rejected: they'd go stale the moment a score is edited, and the series page deliberately computes fresh for exactly that reason.

### 2. Eligibility is "any member is in my list"; ranking is over the main line only

A series is eligible when **at least one of its members** — main line or extra — has a `UserAnimeEntry`. Both displayed averages are computed over **main-line members only**, matching the series page's `MAL · main series` and `Mine · main series` chips exactly.

*Why:* eligibility answers "is this one of my series", which an OVA I watched genuinely does answer. Ranking answers "how good is this series", which the user specified as the main-series score — and extras (recaps, music videos, shorts) drag averages around for reasons that have nothing to do with the franchise's quality.

*Consequence worth stating:* a series can be eligible and still unrankable — I have an entry for one of its specials but have scored none of its main line. Decision 3 covers that.

### 3. A series with no value under the active basis is omitted, not shown blank

Under **My score**, a series with no scored main-line entry is omitted. Under **MAL score**, a series whose main line carries no MAL score at all (e.g. entirely unaired) is omitted. Switching basis therefore changes membership, not just order.

*Why:* the strip's whole content is an ordering. A tile with no value under the active basis has no defensible position in it; parking such tiles at the end of an uncapped strip is a tail of noise, and parking them at the front is worse.

*Alternative considered:* keep every eligible series and sort nulls last. Rejected for the above; the series is one click away from its own page if the user wants it.

### 4. One fetch; the basis toggle sorts and filters client-side

`GET /api/profile/top-series` returns every eligible series **once**, each carrying both averages. Choosing a basis re-sorts and re-filters the already-loaded array in the browser.

*Why:* the payload for both bases is identical, so a per-basis endpoint would refetch the same data to reorder it, and the profile page's existing "hold the last section in a ref so the strip doesn't collapse" workaround exists precisely because refetching on a control change looks bad. Sorting locally makes the toggle instant and removes the need for that workaround here.

*Alternative considered:* `?basis=mine|mal` server-side, matching how the media-type tabs work. Rejected: those tabs genuinely change the underlying set (a different media-type scope is a different query); a basis change is a re-sort of one set.

Ordering under either basis: **average descending**, then **scored main-line count descending** (an 8.5 across six seasons outranks an 8.5 from one), then title, case-insensitively, over the raw title — the same tie-break vocabulary Most rewatched already uses.

### 5. The MAL average's reveal decision is computed server-side, per series

Each item carries a boolean saying whether its MAL average may be shown in full under the "always show completed scores" setting, computed with the series page's own main-series rule: the main line is completed by me (every finished-airing main-line member is Completed, and at least one exists) **and** no main-line member is currently airing. The client passes that boolean straight into `ScoreValue`'s `completed` prop.

*Why:* reproducing `malGroupRevealed` client-side would mean shipping every main-line member's airing status and my status per tile — a much larger payload than the one boolean it reduces to. The rule already lives server-side in spirit (`SeriesStatsDto.MainLineCompletedByMe`), and keeping the decision next to the data keeps the two surfaces consistent by construction.

My own average is never hidden — it never is anywhere in the app.

### 6. Backfill on read, capped; completion is an explicit Settings action

Reading Top series enqueues background builds, through the **existing** `ISeriesBuildTrigger`, for my-list anime that have no `SeriesMembers` row — capped at a small number per request (20). The trigger already dedupes ids seen since app start, so repeat visits don't re-queue the same work.

For actually finishing the job, a Settings action — **"Build all series from my list"** — runs the whole set in one background pass with progress. It mirrors the "Refresh all airing dates" trio exactly: `ISeriesBulkBuildTrigger` (signal), `SeriesBulkBuildBackgroundService` (drain + run), `ISeriesBulkBuildProgressTracker` (in-memory `Phase`/`Built`/`Total` snapshot), plus `POST /api/series/build-all` and `GET /api/series/build-all/status`, polled by the settings page while running.

*Why the cap:* a cold install's list can hold thousands of anime with no series. Each build spends up to 8 MAL fetches, so an uncapped on-read enqueue turns opening the profile page into tens of thousands of queued fetches the user never asked for. Twenty per visit makes the section visibly improve on its own; the Settings action makes "fill it all in now" a deliberate, observable choice.

*Why mirror the airing trio rather than generalise it:* the two jobs differ in target selection and in what a unit of progress means, and the existing trio is three small files. A shared abstraction over two instances would cost more than it saves.

**Progress counts targets processed, not builds run.** One build typically covers several targets at once (its whole franchise gets member rows), so the service re-checks membership before each target and counts an already-covered one as processed immediately. Without that, `built/total` would crawl while the real work raced ahead, and franchises would be rebuilt once per member.

The run is not additionally paced: `SeriesGraphBuilder`'s fetches already go through the MAL client's existing throttling, and `RefreshGate` already collapses a bulk build racing a user opening that series' page.

### 7. Colour tokens are promoted to `:root`; green stays page-scoped

`--mal`/`--mal-bg`/`--mal-border` and `--mine`/`--mine-bg`/`--mine-border` move from `.series-page` to `:root` in `index.css`, in both the light and dark blocks, keeping their aliasing (`--mal` → `--status-completed`, `--mine` → `--accent`) so the score roles can still diverge from the status colours later. `--airing` stays on `.series-page`: green means "on air right now", which is a series-timeline concept, and the promoted tokens are specifically the *score* language.

*Why aliases rather than using `--status-completed`/`--accent` directly at each site:* the existing rationale holds and gets stronger with reach — `--status-completed` means "Completed status" in `SeriesEntryRow`'s stripe and in status pills, and those must not follow a change made for scores.

### 8. Two densities of the same language: chip where a score stands alone, colour-only in dense rows

The convention is the **colour role**, applied everywhere. The *form* comes in two levels, both of which already exist on the series page the user pointed at:

- **Chip** — tinted background, coloured value, optional label above (the header's `MAL · main series` chips, the timeline and extras tiles' compact chips). Used where a score is a standalone figure: the series header, the anime detail page's score boxes, the Top series tiles, and the existing series tiles/cards.
- **Colour-only value** — coloured, tabular-nums, no box. Used in dense rows where a boxed chip per cell would bloat the row: My List rows, the Top anime page's rows, the profile divergence lines.

A shared `components/ScoreChip.tsx` (+ CSS) replaces the three near-identical chip implementations (`series-page__score-chip*`, `series-timeline__card-chip*`, `series-extra-tile__chip*`) with one component taking a `role` (`mal` | `mine`) and a size, and shared `.score--mal` / `.score--mine` utility classes cover the colour-only sites.

*Why not chips everywhere:* My List renders hundreds of rows of five aligned columns; boxing two of them changes the page's whole rhythm for no added information. The user's own reference — "the series page main series card **and more**" — already spans two densities, so this is the reference applied faithfully rather than flattened.

### 9. Editable score controls take the "mine" role, staying native

My List's inline score `<select>` and the entry editor's score `<select>` get the mine colour, tint, and border — but stay native `<select>` elements with native dropdown behaviour. The unscored state (`—`) renders neutral, not purple: there is no score to colour.

*Why:* "my score" should look the same whether I'm reading it or setting it. Replacing the selects with custom listboxes would put keyboard and mobile behaviour at risk for a colour change, which is a bad trade.

### 10. Chips wrap `ScoreValue`; they never replace it

Every MAL score inside a chip still renders through `ScoreValue`, so hiding, per-score reveal, and the always-show-completed setting behave identically to today. The blur placeholder inherits the chip's MAL colour, so a hidden score still reads as a MAL slot rather than as an empty box.

*Why:* the hide behaviour is specified separately (`score-visibility`) and is security-adjacent — the value is deliberately absent from the DOM while hidden. Restyling must not touch that logic, only what surrounds it.

## Risks / Trade-offs

- **A fresh install shows a near-empty Top series** → the on-read backfill improves it every visit, and the section's empty state names the Settings "Build all series from my list" action explicitly rather than leaving the user to guess why their franchises are missing.
- **The bulk build is the most MAL-expensive action in the app** → it is opt-in, one target at a time, reports `built/total` throughout, skips targets already covered by an earlier build in the same run, and is bounded by the list size. It uses the same fetch budget and single-flight gate as every other build.
- **A one-scored-season franchise ranks against a fully-scored one** → the scored-count tie-break only settles exact ties, so this remains real. Mitigated by surfacing the counts: each tile's accessible title reads `N of M scored`, the same figure the series page chips show inline. Accepted deliberately — a hard minimum ("at least 3 scored") would hide genuinely-favourite short franchises.
- **Switching basis changes membership, which can read as tiles vanishing** → the toggle is explicitly two named options ("My score" / "MAL score") rather than a sort dropdown, so the strip is understood as two different rankings; the counts sit in each tile's title.
- **Promoting colour tokens globally widens the blast radius of a palette change** → the tokens are aliases; nothing that means "Completed status" or "accent" was repointed, and the two roles can be given their own hues later by editing two lines.
- **Purple-tinted selects could read as disabled or as an error state in some themes** → they reuse the exact `--mine`/`--mine-bg`/`--mine-border` triple already shipping on the series page in both themes, rather than new values.
- **Two densities could be read as "the restyle wasn't finished"** → the rule is stated in the spec (chip for standalone figures, colour-only in dense rows) so it is a decision on record rather than an inconsistency.
- **`SeriesRankingLookup`'s projection grows with the number of built series** → it is bounded by 60 members per series and by however many series exist, is main-line-only, and runs once per profile-section request — the same order of work as `SeriesSearchLookup` on every keystroke.

## Migration Plan

No schema change and no data migration — `Series`, `SeriesMember`, `AnimeMetadata`, and `UserAnimeEntry` already carry everything.

Backend and frontend deploy together. An older frontend against the newer backend simply never calls the new endpoints; a newer frontend against an older backend would 404 on `/api/profile/top-series` and `/api/series/build-all`, so ship as one release.

Rollback is a straight revert. The only state that outlives it is `Series`/`SeriesMember` rows built by the backfill or the bulk action — exactly the rows a series-page visit or a search would have built anyway, and already handled by the existing rebuild/staleness rules.

## Open Questions

None blocking. Two deferred: whether Top series should eventually offer a "completed franchises only" filter (additive, and the completion state is already computed for decision 5), and whether the bulk build should become resumable across restarts if a full run turns out to be long enough that a restart mid-run is common — the airing full refresh made the same "manual, one-shot, not persisted" call and has not needed revisiting.
