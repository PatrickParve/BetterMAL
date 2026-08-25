## Context

**Pictures today.** `AnimeMetadata.PictureUrl` holds whatever MAL's `main_picture` said — `large` if present, else `medium` — written by exactly two functions, `MalMappingExtensions.ApplyTo` (full detail) and `ApplyLeanTo` (season/top/search listings). Every surface that shows an anime's picture reads that one column through a join: the dashboard, my list, season, year, top, airing, search, recap, profile, the series page and its browser. Nothing denormalizes a copy — with one exception, `AnimeRelatedAnime.PictureUrl`, a snapshot of the far end's node picture written at full-detail time so a related-anime tile can render an anime that has no metadata row of its own. MAL publishes an anime's other artwork under `GET anime/{id}?fields=pictures`, an array of `{medium, large}`; the app has never asked for it.

**Series identity today.** `Series` stores membership and nothing presentational. Three separate places project a series' title and picture from its root member's `AnimeMetadata`: `SeriesService.GetSeriesAsync` (the page), `SeriesSearchIndex` (search results), and `SeriesRankingIndex` via `SeriesRankingLookup` (the browser cards and the profile's Top series). A rebuild in `SeriesGraphBuilder.PersistAsync` keeps the surviving row's `Id` and updates member rows in place — which is why `SeriesMember.FavouriteRank` already survives a rebuild for free — but it deletes any *other* series row it absorbs.

**Fetch paths today.** `MetadataRefreshService.RefreshStaleBatchAsync` is already **my-list only** (`.Where(a => a.UserEntry != null)`) and walks the `RefreshTiers` ladder. `RefreshOneAsync` serves both the detail page's TTL fetch and the manual Refresh action, for any anime including ones not on my list. `InitialImportService` full-fetches every anime it imports, all of which are list entries by definition. MAL rate-limits per *request* (`MalRequestPacer`, 1 req/s), so widening a request's field list is free; adding a request is not.

**Watch time today.** `WatchMath` owns the shared maths. `ProfileService` counts `RewatchInclusiveEpisodes` — first run plus every completed rewatch run. `SeriesRankingIndex` counts `RewatchEpisodesIncludingCurrentRun` for the "Most rewatched" scope. `SeriesService.BuildStats` counts neither: `MyWatchedSeconds` is `EffectiveWatchedEpisodes × EpisodeSeconds`, which treats a `Rewatching` entry as fully watched but counts a rewatched entry exactly once, on purpose. The series page renders `Time watched` from it and `Time left` as `mainLineRuntimeSeconds − myWatchedSeconds`, and hides both the moment time left reaches zero.

## Goals / Non-Goals

**Goals:**

- A chosen picture reaches every surface that shows the anime, with no per-surface work and no possibility of missing one.
- A choice is immune to MAL: no sync path, of any kind, can overwrite or clear it.
- Picture sets are fetched for my-list anime only, at zero additional MAL requests wherever an existing fetch can carry them, and never as a bulk sweep.
- Whether an anime has more than one picture is known *before* its picker opens, since that count decides whether the button exists at all.
- One resolver owns "what is this series called and what does it look like", so the page, the search index, and the browser cannot disagree.
- The series page's watch time means the same thing the profile page's watch time means.
- A series rebuild — including one that absorbs a neighbouring series — never silently loses a hand-picked title or picture.

**Non-Goals:**

- Any write to MAL. The list-status PATCH stays the only one.
- Picture sets for anime not on my list, and for anime reached only by browsing.
- Arbitrary artwork: uploads, pasted URLs, non-MAL sources.
- Per-surface artwork (a banner here, a poster there), or a per-anime *title* override.
- Re-validating a stored choice against a later picture set, or against a member that has left the series.
- Logging picture and title choices as list activity.
- A TTL of its own for picture sets. They are as fresh as the anime's last full detail fetch, and no fresher.

## Decisions

### D1: The chosen picture is written into `PictureUrl`; MAL's own moves to a new column

`AnimeMetadata.PictureUrl` is redefined from "MAL's main picture" to **"the picture to display"**, and a new `MalPictureUrl` holds what MAL says. Choosing writes the chosen URL into `PictureUrl`; the sync paths write `MalPictureUrl` unconditionally and `PictureUrl` only when nothing is overridden.

*Alternative considered:* add `SelectedPictureUrl` and resolve `SelectedPictureUrl ?? PictureUrl` at every read. Rejected on blast radius: forty-odd projections read `PictureUrl` across controllers, repositories, and services, and the requirement is literally "everywhere that anime is shown". A resolution rule applied by hand at forty sites fails the first time someone adds a forty-first; a value already correct in the column cannot fail anywhere. The cost is that one column's meaning shifts, which is a comment and a migration, paid once.

This also gives series pictures a free property: a series with no picture of its own shows its root member's *displayed* picture, so overriding the root anime's picture retitles the franchise's artwork too — until the series is given a picture of its own, which then wins.

### D2: "Is it overridden?" is derived, not stored

No boolean column. An anime is overridden exactly when `PictureUrl != MalPictureUrl`. The mapping guard is therefore one line of intent:

```
var wasOverridden = target.PictureUrl != target.MalPictureUrl;   // read before writing either
target.MalPictureUrl = node.MainPicture?.Large ?? node.MainPicture?.Medium;
if (!wasOverridden) target.PictureUrl = target.MalPictureUrl;
```

Three properties fall out of this rather than needing to be maintained:

- The migration backfills `MalPictureUrl` from `PictureUrl`, so every pre-existing row is unoverridden **by construction** and the app renders identically the instant the migration lands.
- An unoverridden anime still follows MAL when MAL changes its main picture, which a stored flag would also have to remember to do.
- Choosing the picture that *is* MAL's main picture clears the override, because it makes the two columns equal again. "Reset to default" and "pick the default" are the same operation, and there is no third state to get wrong.

*Alternative considered:* an explicit `PictureOverridden` flag. Rejected — it can disagree with the URLs it describes (flag set, URL equal to MAL's; flag clear, URL different), and nothing in the app needs to distinguish "deliberately picked the default" from "never picked".

### D3: The picture set is a column on `AnimeMetadata`, not a table

`PictureUrls` (`List<string>`, jsonb, MAL's order preserved) and `PicturesSyncedAt` (`DateTimeOffset?`). This mirrors `Genres`, which is already a `List<string>` column: the set is always read whole, always written whole, has no per-row identity, and is never queried by element. A `AnimePicture` table would add a join to every read of it and a diff to every write for no query the app makes.

`PicturesSyncedAt` earns its place separately from `LastSyncedAt`: it distinguishes **"never asked for pictures"** from **"asked, and MAL has exactly one"**. Without it, `PictureUrls` being empty or single is ambiguous, and the anime would be re-fetched on every visit forever — the same trap the `metadata-refresh` spec already calls out for genuinely-empty relation sets.

### D4: Pictures ride existing full-detail fetches for my-list anime, and are backfilled on visit

This is the "when and where to fetch" question from the brief. The answer has three parts.

**(a) Free ride.** `MalClient` gains a with-pictures field set (`FullDetailAnimeFields + ",pictures"`). It is used whenever the anime being full-fetched has a `UserAnimeEntry` — which covers the initial import (every anime it touches is a list entry), the nightly tiered refresh (already my-list-only), a manual Refresh, and a detail page's own TTL fetch of a my-list anime. MAL rate-limits per request, so these fetches cost exactly what they cost today. For an anime **not** on my list, the field is omitted and the plain full-detail set is used, so nothing is fetched or stored for it.

**(b) Backfill on visit, for rows the free ride cannot reach.** A my-list anime whose full detail is *inside* its tier will not be re-fetched for a long time — up to 28 days on the stale tier — so on the day this ships, essentially the whole list has `PicturesSyncedAt == null`. The detail page therefore reports `picturesFetchPending: true` when the anime is on my list and has never had a picture fetch, and the client calls `POST /api/anime/{animeId}/pictures/refresh`, which makes one paced MAL call and returns the set. This is deliberately the same handshake `POST /api/anime/{id}/related-anime/refresh` already uses, single-flighted through `RefreshGate` on the same `anime:{id}` key so a double-mount cannot double-fetch.

**(c) Never a bulk sweep.** Nothing walks the list fetching pictures for anime nobody is looking at. A list of ~600 would be a ten-minute paced storm for data most of which is never opened.

*Why not fetch when the picker button is clicked?* Because the button's own existence depends on the count — it is shown only when there is more than one picture. Clicking to find out means either always showing the button (and sometimes opening a picker onto a single image) or never showing it on an anime that has ten. The count must be known at render time, so the fetch must precede it.

*Why not a dedicated nightly picture job?* It would re-implement the tier ladder for a field that costs nothing to carry on the fetch the ladder already performs.

*Consequence, accepted:* picture sets have no independent staleness. A set refreshes when its anime's full detail refreshes. New MAL artwork therefore appears within the anime's own tier, which is the same freshness guarantee its genres, rank, and relations already have.

### D5: An anime added to my list becomes eligible immediately, with no new trigger

The eligibility test is "does a `UserAnimeEntry` row exist", evaluated at fetch time, so adding an anime to the list makes it eligible the moment the entry is saved. No hook in `UserAnimeEntryEditService` is needed: the detail page re-reads itself after an edit (it already does, to pick up server-applied rules), that read finds an entry with no picture set, and the pending handshake from D4(b) fires. The nightly batch picks it up on its own schedule regardless.

*Alternative considered:* trigger a picture fetch from the add-to-list path itself. Rejected — it spends a MAL request at the moment the user is least likely to want the picker, and it adds a second place that decides when pictures are fetched.

### D6: The series picture pool is the union of its main-line members' artwork, backfilled with a budget

The pool offered on the series page is, in main-line order and then MAL's order within each member, the union of:

- every main-line member's displayed picture (`PictureUrl`) and MAL picture (`MalPictureUrl`), which exist for every member regardless of list membership, and
- every main-line member's `PictureUrls`, which exist only for members on my list.

Deduplicated by URL. Extras contribute nothing — the brief scopes this to the main series, and a franchise's extras drag in commercials and promo stills that are not what anyone means by "the series' picture".

**Budget.** On a series page visit, main-line members that are on my list and have `PicturesSyncedAt == null` are fetched, at most **8 per visit** — the same figure the series build itself uses for its live MAL budget, chosen for the same reason: a bounded, obviously-terminating amount of work on a page visit. The response reports how many remain, the page says so, and a revisit continues until none remain. Like D4(b) this is a client-triggered `POST`, not work done inside the page read, so the page renders from cache first.

*Consequence, accepted:* a main-line member not on my list contributes only its main picture. That is the user's own constraint (D4) applied consistently; adding the member to my list fills in the rest.

### D7: One resolver owns a series' displayed identity

A `SeriesIdentity` helper returns `(Title, EnglishTitle, PictureUrl)` for a series given its stored overrides and its root member, and `SeriesService`, `SeriesSearchIndex`, and `SeriesRankingLookup` all call it instead of projecting the root by hand. Three hand-rolled projections is exactly the shape that drifts.

**A chosen title suppresses the English title.** The client's `pickDisplayTitle(title, englishTitle)` chooses between the two, so leaving `Beyblade: Metal Fusion`'s English title in place beside a chosen `Beyblade` would let some surfaces show the very title that was rejected. When `SelectedTitle` is set, the resolver returns it as `Title` and `null` as `EnglishTitle`.

**Search still matches every member title.** `SeriesSearchIndex` already scores a series by matching each *member's* titles and only uses the root for display; the resolver changes the display half only. So a series titled `Beyblade` is still found by searching `Metal Fusion`. The chosen title is added to the match set too, for the case where a trim produces a string no member title starts with.

### D8: The title rule is "a contiguous run of an offered title"

Offered titles are every main-line member's `Title` and its `EnglishTitle` where non-null. A custom title is accepted when, after normalizing, it appears as an unbroken substring of at least one offered title.

Normalization, applied to both sides before comparing: trim whitespace; collapse internal whitespace runs to a single space; trim leading and trailing punctuation (`:`, `;`, `,`, `-`, `–`, `—`, `.`, `!`, `?`, quotes) from the candidate. That last step is what makes `Beyblade` pass against `Beyblade: Metal Fusion` rather than failing on a dangling colon. Comparison is case-insensitive; the text is **stored as typed**, since changing case adds no words and the user's capitalisation is part of their choice.

So `Beyblade: Metal Fusion` yields `Beyblade`, `Metal Fusion`, `Beyblade: Metal`, and the whole string; it does not yield `Beyblade Fusion`, `Beyblade Metal Fusion`, or `bladusin`.

*Alternatives considered:* **prefix-only** — simpler, but forbids `Metal Fusion`, and the stated rule is about contiguity, not position. **Token subsequence** (drop any words, keep order) — this is precisely the "some from here and some from there" the brief rules out. **Free text** — loses the guarantee that a series is never named something MAL never called it.

The rule is enforced in the service and returns 400 on violation; the client mirrors it only to explain the failure before the request.

### D9: Overrides survive a rebuild, and an absorption adopts rather than discards

`PersistAsync` keeps the surviving `Series` row and its `Id`, so `SelectedTitle` and `SelectedPictureUrl` survive a rebuild untouched — the same free ride `FavouriteRank` gets, for the same reason: nothing in the rebuild writes them.

When a rebuild absorbs other series rows into the survivor, each absorbed row's overrides would be deleted with it. Rule: **the survivor's own choices always win**; when the survivor has *no* chosen title (or picture) and an absorbed row does, that value is adopted before deletion, taking the absorbed row with the largest member overlap when several qualify. Losing a hand-picked title because two franchises turned out to be one is a surprise; overwriting one is a worse surprise, so adoption is strictly a fill-in.

Stored choices are **never re-validated**. A chosen picture whose source member has left the series, or which MAL no longer lists, is kept and shown in the picker as the current selection. The graph wobbles; a deliberate choice should not.

### D10: `MyWatchedSeconds` keeps its meaning; rewatch time is a second, orthogonal figure

`SeriesStatsDto` gains `MyRewatchedSeconds` and changes nothing about `MyWatchedSeconds` or `MyWatchedEpisodes`. Per main-line member:

- `MyWatchedSeconds` ← `WatchMath.EffectiveWatchedEpisodes × EpisodeSeconds` — unchanged, still first-run-only, still treating a `Rewatching` entry as fully watched. It is what "Time left" subtracts, and what the progress bar fills.
- `MyRewatchedSeconds` ← `WatchMath.RewatchEpisodesIncludingCurrentRun × EpisodeSeconds` — the completed rewatch runs plus the episodes into a run still going.

The client renders **Time watched** as the sum and **Time left** as `mainLineRuntimeSeconds − myWatchedSeconds`, exactly as today. The two helpers provably cannot double-count: entering `Rewatching` resets episodes-watched to zero and the rewatch count only increments when a run finishes, so for a `Rewatching` entry the first term contributes the aired figure (the completed original run) and the second contributes `rewatchCount × total + episodesWatched`. A twelve-episode season completed, rewatched twice, two episodes into a third reads `12 + (24 + 2) = 38` episodes of time — which is what actually happened.

*Alternative considered:* redefine `MyWatchedSeconds` to include rewatches. Rejected — "Time left" reads it, and a rewatch would drive time left negative and make a half-watched franchise read as finished. Two numbers that each mean one thing beat one number two callers interpret differently.

*Scope:* main line only, matching every other figure in that stats box and matching "Time left". The profile page's watch time spans the whole list; this box is explicitly a main-line box and says so.

### D11: The two time stats stop hiding together

Today both hide when time left reaches zero, on the reasoning that "0min left" beside a time watched equal to the runtime says nothing new. With rewatches counted that reasoning inverts: a finished, heavily rewatched franchise is exactly where the number is interesting, and it is no longer equal to the runtime. So:

- **Time watched** is shown whenever it is non-zero.
- **Time left** is shown when it is non-zero, or when the runtime total is unknown (where a zero reports missing data rather than a finished franchise — the existing carve-out, kept).

### D12: Related-anime tiles prefer a cached row's picture over the denormalized copy

`AnimeRelatedAnime.PictureUrl` is a snapshot of MAL's node picture, written at full-detail time, and it exists so a far end with no metadata row still renders. It is left exactly as it is — it is a record of what MAL said. `RelationResolver` already joins far-end metadata where it exists (for `AiredFrom`); it starts preferring that row's `PictureUrl` when present and falling back to the snapshot when not. That is the one place an override could otherwise be missed, and it is one line.

### D13: Endpoints are `PUT`/`DELETE` on a named sub-resource

| Endpoint | Purpose |
|---|---|
| `PUT /api/anime/{animeId}/picture` | set the anime's picture; body `{ pictureUrl }` |
| `DELETE /api/anime/{animeId}/picture` | reset it to MAL's |
| `POST /api/anime/{animeId}/pictures/refresh` | one-anime picture backfill (D4b) |
| `PUT /api/series/{seriesId}/picture` | set the series' picture |
| `DELETE /api/series/{seriesId}/picture` | reset it to the root's |
| `PUT /api/series/{seriesId}/title` | set the series' title; body `{ title }` |
| `DELETE /api/series/{seriesId}/title` | reset it to the root's |
| `POST /api/series/by-anime/{animeId}/pictures/refresh` | bounded pool backfill (D6) |

`PUT`/`DELETE` rather than the entry editor's `PATCH`: each is a whole-value replacement of one named thing, and `DELETE` states "reset to default" more plainly than a null in a body. `POST .../refresh` matches the two refresh triggers the API already exposes.

**Every set is validated against the option set.** An anime's chosen picture must be in `PictureUrls ∪ {MalPictureUrl}`; a series' must be in the pool from D6; a title must satisfy D8. Anything else is a 400. This is what keeps "the options are exactly what MAL publishes" true rather than merely conventional. Setting an anime's picture to `MalPictureUrl` is accepted and clears the override (D2). A picture set on an anime not on my list is a 400 — the button is not shown there, and the API says the same thing.

### D14: The picker is one shared overlay

One `PicturePickerOverlay` component, given a list of URLs, a current selection, and a callback, serves both pages — the two differ only in what fills the list and what the callback calls. It follows the existing `Modal` pattern (Esc, click-outside, scroll lock) like every other overlay in the app. The current selection is always present in the grid, marked, even when it is no longer in the fetched set (D9), so it can be seen and replaced. The button that opens it renders only when the option count exceeds one; on the detail page it additionally requires the anime to be on my list.

The series title picker is its own overlay: the offered titles as a list, plus a text field seeded from the current title, with the D8 rule checked as you type and the confirm disabled while it fails.

## Risks / Trade-offs

- **A future sync path writes `PictureUrl` without the D2 guard, silently reverting a choice** → exactly two functions write it, both in `MalMappingExtensions`, and both get the guard. Tests assert that an overridden row survives a full upsert and a lean upsert, which is the regression that would catch a third writer.
- **Deriving overriddenness from column inequality is wrong if the migration backfill is wrong** → the migration copies `PictureUrl` into `MalPictureUrl` for every row in one statement; there is no row where the two can differ on day one, so no row starts out accidentally overridden. Verified by reading the generated `Up`.
- **A chosen picture 404s after MAL retires it** → kept, never auto-cleared, shown as the current selection in the picker so it is visible and replaceable. A broken image renders as the existing placeholder does.
- **`PictureUrl` changing meaning confuses a future reader** → the column carries a comment saying it is the displayed picture and that `MalPictureUrl` is MAL's; `CODE_GUIDE.md` gets the same sentence.
- **On a large franchise the first visit shows an incomplete picker** → bounded by design (D6); the response reports the remaining count, the page says so, and revisits complete it. The alternative is an unbounded fetch storm on a page visit.
- **The series pool is thin for a franchise mostly not on my list** → deliberate. Every main-line member still contributes its main picture, so the pool is never empty, and it fills in as members are added to the list.
- **"Time watched" exceeding the main-line runtime reads as a bug** → it sits beside "Time left", which stays first-run based; the pairing is what makes it legible. Documented in the spec as the intended reading.
- **Payload growth on every my-list full-detail fetch** → a picture set is a handful of short URLs; only my-list anime carry one; no listing fetch is touched.
- **Absorption adopting a title surprises someone** → adoption is fill-in only, never overwrite, and only from the largest-overlap absorbed row.

## Migration Plan

1. One EF migration, `AddArtworkAndTitleSelection`, generated inside the `sdk:10.0` Docker image (the local SDK is 9.0, and Docker cannot bind-mount `~/Documents` — `rsync` the backend to `/private/tmp/bm-build/` first, generate there, copy `Migrations/*` back).
2. `Up` adds `AnimeMetadata.MalPictureUrl` (text, null), `PictureUrls` (jsonb, null), `PicturesSyncedAt` (timestamptz, null), and `Series.SelectedTitle` (text, null), `SelectedPictureUrl` (text, null); then `UPDATE "AnimeMetadata" SET "MalPictureUrl" = "PictureUrl";`.
3. Post-migration state: every anime unoverridden (D2), no picture sets, no series overrides — every page renders byte-identically to before.
4. Picture sets fill in from the day's tiered refresh and from detail/series page visits (D4); nothing is backfilled in bulk.
5. **Rollback:** dropping the five columns restores previous behaviour with no data repair. `PictureUrl` still holds a displayable URL — the chosen one, if any — so a rollback leaves choices in place rather than reverting artwork, which is the more desirable of the two.

## Open Questions

- Should the profile's Top series overlay eventually offer the same picker inline, rather than sending the user to the series page? It reads the resolved identity either way, so this is additive and is not decided here.
- Should a series with no main-line member on my list — possible for an explored-only franchise — show the picture button at all? It currently would, on the main-picture-only pool, and the button hides itself when that pool has one entry. Left as-is unless it proves confusing.
- If MAL ever exposes picture dimensions, the picker could group portrait and landscape artwork, which would pair with the detail page's existing landscape handling. Not available today; not designed for.
