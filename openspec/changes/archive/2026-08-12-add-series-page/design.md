## Context

MAL's API has no franchise/series id. The only thing it exposes is `related_anime` — a per-anime list of edges (`sequel`, `prequel`, `side_story`, `parent_story`, `summary`, `full_story`, `spin_off`, `alternative_version`, `alternative_setting`, `character`, `other`) — and the app already persists all of them in `AnimeRelatedAnime` on every full-detail fetch, with `MediaType` on the edge itself. So a series is something the app has to *derive* from a graph it already stores, and the whole design question is where to cut that graph and how to pay for the parts of it that aren't cached yet.

Three existing constraints shape the answer:

- **Relations only exist on full-fetched rows.** `MalMappingExtensions.ApplyTo` writes relations; `ApplyLeanTo` never touches them. So a season-browsed (lean) row or an anime we've never opened has no outgoing edges — the graph is only as connected as the app's fetch history. Expanding it costs one MAL call per node, paced at 1 req/s by `MalRequestPacer`.
- **Everything renders from Postgres.** A page read must not turn into a live fetch storm, and a request that blocks for 40 s while it walks a franchise is not acceptable.
- **The app already has the display pieces**: `ScoreValue` (hide-toggle-aware MAL scores), `ProgressBar`, `EntryEditorContext` (one global editor overlay), `usePageData` (load + back/forward restore), `pickDisplayTitle`, and `UserAnimeEntryDto` as the shared entry shape.

The user-facing goal is a franchise overview with two MAL averages and two of *my* averages — and, downstream, a "my top series" list, which is why the derived grouping has to be persisted with a stable identity rather than recomputed anonymously per request.

## Goals / Non-Goals

**Goals:**

- Derive a franchise from the stored relation graph with a rule that is explainable in one sentence and doesn't fuse unrelated shows.
- Persist the grouping under a stable id so a later "my top series" feature can rank series without re-walking graphs.
- Keep the first visit fast and bounded, and let an incomplete series complete itself over subsequent visits without a background job.
- Keep my averages honest as scores change — no stored aggregate that can go stale.
- Reuse the existing row/editor/score components so a series row behaves like every other anime row in the app.

**Non-Goals:**

- No manual series editing or local overrides. If MAL's relations are wrong, the fix is upstream.
- No new external data source. AniList is already in the app for airing times only; its `relations` graph is not used here.
- No background series-building service. Builds are visit-triggered, like season and top-anime refreshes.
- No profile section or series list page in this change (the identity and averages it needs land here).
- No episode-level series timeline or per-episode data.

## Decisions

### 1. A series is a connected component over *story* relations, not all relations

Traversal set: `sequel`, `prequel`, `side_story`, `parent_story`, `summary`, `full_story`, `spin_off`, `alternative_version`. Everything else MAL reports — `alternative_setting`, `character`, `other`, `adaptation`, and any unrecognized string — is stored as always but **not traversed**.

`alternative_setting` and `character` are the two that would wreck this: they are how MAL links shows that merely share a universe or a cast, and traversing them fuses entire multi-decade franchises (and, through shared crossover shows, occasionally unrelated ones) into a single 200-member blob. The chosen set is exactly the relations that mean "this is the same story".

Membership is **undirected**: from a node we follow its own edges *and* edges pointing at it (`WHERE RelatedAnimeId = id`). The chosen set is closed under reversal (sequel↔prequel, side_story↔parent_story, summary↔full_story, alternative_version↔itself), so direction carries no information for membership — and the reverse lookup keeps the component connected even when one node hasn't been full-fetched and therefore has no outgoing edges of its own.

*Alternative considered:* traverse everything and cap the size. Rejected — the cap would decide membership arbitrarily, and which 60 of the 200 you get would depend on BFS order.

### 2. Main line = the sequel/prequel chain, minus specials and music

Within the component, build the subgraph of `sequel`/`prequel` edges only. Its connected components are "chains". The **main line** is the largest chain (tie broken by which chain contains the earliest-aired entry), filtered to members whose `MediaType` is not `special` or `music`. Everything else in the series is an **extra**.

Taking the largest chain rather than the chain containing the anime you arrived from matters: arriving from a spin-off's second season would otherwise make the spin-off the "main series". Filtering `special`/`music` out of the chain (rather than trusting relation types) matters because MAL routinely files a recap special or a concert short as a `sequel`.

*Alternative considered:* classify by relation type alone (main = reachable via sequel/prequel). Rejected — it lets specials in, and it can't rank two competing chains. *Alternative considered:* let the user mark main-line members. Rejected per the non-goals.

### 3. Watch order is release order

Main-line entries are ordered by `AiredFrom` ascending (nulls last, MAL id as tiebreak), and that ordering is what the page numbers 1..n as the watch order.

The tempting alternative is a topological sort of the sequel chain, which is "more correct" in principle. It is fragile in practice: chains branch (two sequels from one node), break (a missing edge on an un-fetched node), and say nothing about where a movie that aired between two seasons belongs. Release order gets all of those right for the same reason MAL's own franchise pages do — the studio released them in the order they're meant to be watched. The sequel chain is still used, just for *classification* (decision 2), where branching doesn't hurt.

Extras are grouped by media type in a fixed display order (Movie, OVA, ONA, Special, Music, TV, Other) and ordered by `AiredFrom` within each group.

### 4. The root is the earliest main-line entry, and gives the series its title and poster

`RootAnimeId` = first entry in the main-line ordering. The hero poster is its `PictureUrl`, the series title is its title verbatim (through `pickDisplayTitle` on the client, like everywhere else).

Not stripping a trailing "Season 1"/"1st Season" from the title is deliberate: the transformations that would produce a clean franchise name are guesswork on titles that vary wildly, and a wrong strip is worse than a slightly long title. The page labels the block "Series" so there's no ambiguity about what's being named.

### 5. Two tables, keyed so the invariants hold themselves

```csharp
public class Series {
    public int Id { get; set; }              // stable identity, survives rebuilds
    public int RootAnimeId { get; set; }     // unique
    public DateTimeOffset BuiltAt { get; set; }
    public bool IsPartial { get; set; }      // build hit its MAL-fetch budget
    public bool IsTruncated { get; set; }    // build hit the member cap
    public List<SeriesMember> Members { get; set; } = [];
}

public class SeriesMember {
    public int AnimeId { get; set; }   // PK — an anime belongs to at most one series, by construction
    public int SeriesId { get; set; }
    public bool IsMainLine { get; set; }
    public int Order { get; set; }     // position within the main line, or within its extras group
    public AnimeMetadata Anime { get; set; } = null!;
}
```

`SeriesMember.AnimeId` as the primary key is the whole point: "one anime, one series" is enforced by the schema rather than by code that has to remember. `AnimeId` is an FK to `AnimeMetadata` with cascade delete — unlike `AnimeRelatedAnime.RelatedAnimeId`, a member always has a metadata row, because a node without one can't be displayed and isn't admitted (decision 7).

**No averages are stored.** They're computed at read time by joining members to `AnimeMetadata` and `UserAnimeEntry`. A stored average would go stale the moment a score is edited, and would need invalidation hooks in the entry-edit path, reconciliation, and the metadata refresher. The read is a keyed join over tens of rows.

### 6. Builds are visit-triggered, single-flight, and self-healing on overlap

`GET /api/series/by-anime/{animeId}` resolves a `SeriesMember` row for that anime. It (re)builds when there is none, when the series `IsPartial`, or when `BuiltAt` is older than 30 days — otherwise it's a plain cache read. Builds go through the existing `RefreshGate` (key `series:{seriesId}` when known, else `series:anime:{seedId}`), the same single-flight the detail page and season browser use.

When a freshly computed component overlaps existing `Series` rows, the build **keeps the row with the largest overlap** (preserving its `Id`), deletes the others, and replaces the member set wholesale in one transaction. This is what makes a newly announced sequel fold in on first visit: opening the new anime builds a component that reaches the existing franchise, absorbs its `Series` row, and leaves one series with one id — the id a future "top series" list will have been ranking all along.

*Alternative considered:* delete and recreate the series on every build. Rejected — the id is the hook for later features and for anything a user might pin to it.

### 7. The fetch budget bounds the request, and partial builds complete themselves

Two limits per build: **members ≤ 60** (sets `IsTruncated`) and **MAL full-fetches ≤ 8** on a visit-triggered build, **≤ 20** on an explicit rebuild (sets `IsPartial` when exhausted). At 1 req/s that's a worst case of ~8 s for a visit and ~20 s for a user-initiated rebuild that shows a spinner.

The budget is spent in BFS order, and only where it buys something:

- A node with **no metadata row at all** must be fetched or it can't be shown — those get the budget first.
- A node with a **lean row** (season/top-anime browsing) displays perfectly well — title, poster, media type, score, episodes, aired dates — it just can't be expanded further, so it's a member but not a frontier.
- A node with a **full row** costs nothing.

A node that needed a fetch and didn't get one is skipped and the series is marked `IsPartial`, which makes the next visit rebuild and spend another 8 — and the previous fetches are cached by then, so each visit strictly advances. Ordinary browsing helps too: `AnimeDetailService` already full-fetches any anime you open. The page surfaces the partial state ("Some entries couldn't be loaded yet") with the Rebuild button next to it, rather than silently showing a short series.

*Alternative considered:* a background `SeriesBuildQueue` hosted service that completes builds asynchronously. Rejected for this change — it adds a job, a progress endpoint, and a polling UI to a page that converges in two or three visits without any of it. Worth revisiting only if real franchises turn out to routinely need more than ~24 fetches.

These fetches go through `IMetadataRefreshService.RefreshOneAsync`, the same call the detail page's first visit makes, so they're paced but outside the metadata refresher's 500-calls-per-day background budget — consistent with how visit-triggered fetching already works everywhere else.

### 8. Averages are unweighted means, computed twice over

Four numbers, each with the count behind it (`8.42 · 5 of 6 scored`):

- MAL average over main line / over all members — mean of `MalScore` where present.
- My average over main line / over all members — mean of `MyScore` where the entry exists and the score is non-zero (0 is MAL's "no score").

Unweighted: a 24-episode season and a 90-minute movie count the same. Episode-weighting would make the movies in a franchise nearly invisible, and "the average score of the series" in ordinary speech means the average of its entries. Values are returned unrounded and rendered to two decimals.

### 9. Runtime and progress reuse the app's existing duration assumption

Main-line runtime = Σ `TotalEpisodes × AverageEpisodeDurationSeconds`, falling back to `ProfileService`'s `AssumedMinutesPerEpisode = 24` when a duration is unknown — the same fallback the profile's "Days" stat already uses, so the two surfaces can't disagree. Entries with an unknown episode count contribute what's aired if known and otherwise nothing, and set a `hasUnknownEpisodeCounts` flag so the UI can render `4d 6h+` rather than a confidently wrong total. Extras' runtime is computed the same way and reported separately.

My watched time uses `EpisodesWatched` with the same per-episode figure and does **not** multiply by rewatch count (matching the profile's treatment); time left is main-line runtime minus watched runtime, clamped at zero.

### 10. The score strip must not leak hidden MAL scores

The per-entry MAL-vs-mine comparison strip encodes scores as bar heights, which would leak values that the hide toggle is supposed to make unreadable — `score-visibility` requires the value not be present in the rendered output, and a blur over a bar chart still shows the shape. So while the toggle is on, the **MAL row of the strip is not rendered at all** (replaced by a one-line note); my-score bars, which the toggle has never covered, stay. Every numeric MAL score elsewhere on the page goes through `ScoreValue` and inherits the normal blur-and-reveal behavior.

### 11. Page shape and client wiring

Route `/series/:animeId` (any member's id — the endpoint resolves it), so the detail page can link with the id it already has and no id-stability question arises. `SeriesPage` loads through `usePageData('series:{animeId}', …)` so back/forward restore works like every other page, renders rows via a new `SeriesEntryRow` (poster thumb, title, type/year/episodes, `ScoreValue`, my score, status pill, edit button), and opens edits through `useEntryEditor()` — the same global overlay every other page uses, with the saved entry applied back into the loaded series via `setData` rather than a refetch.

The detail page's Series button renders when `detail.relatedAnime` contains any relation in the traversal set — a zero-cost check on data the page already has, with no probe request for a series that might not exist.

## Risks / Trade-offs

- **Over-merging via `alternative_version` / `spin_off`** (e.g. a remake trilogy plus its parent series arriving as one franchise) → the member cap and `IsTruncated` keep it bounded and visible, and the traversal set is one constant to tune once real franchises have been looked at. The two relations that actually cause runaway merges (`alternative_setting`, `character`) are already excluded.
- **Under-merging when MAL's relations are thin** — a franchise whose movie is linked only by `other` won't be a member → accepted; the More overlay on the detail page still reaches it, and the fix is upstream at MAL.
- **First visit to a large, uncached franchise is slow and incomplete** (~8 s, `IsPartial`) → bounded by the budget, communicated on the page, and converges over visits; the explicit Rebuild gets 20 fetches with a spinner.
- **Wrong main line for franchises with two comparable chains** (a long spin-off series rivalling the parent) → the largest-chain rule picks one and the other lands in extras; both are on the page, just under different headings.
- **A recap special filed as `sequel` still ranks as a main entry if MAL types it `tv`** → the media-type filter catches the common cases (`special`, `music`); the residue is rare and visible.
- **Cascade delete from `AnimeMetadata`** removes a member silently, leaving a series that no longer matches its `BuiltAt` snapshot → harmless: the 30-day staleness rule and any rebuild recompute it, and metadata rows are effectively never deleted in this app.
- **Series builds bypass the 500-call daily background budget** → they are user-initiated and paced at 1 req/s like the detail page's live fetch; the per-build cap is the real bound.

## Migration Plan

1. Additive EF migration `AddSeries` — two new tables, no changes to existing ones, no data backfill. Built through the `sdk:10.0` Docker image (the local SDK is 9.0).
2. Deploy backend then frontend; the route and the Series button are inert until the endpoint exists.
3. Series populate lazily on first visit. Nothing pre-computes, so there is no import-shaped step and no MAL burst at deploy.
4. Rollback: drop the two tables and remove the route/button. No other subsystem reads them, and no existing behavior changes, so rollback is unconditional.

## Open Questions

- Should `spin_off` and `alternative_version` stay in the traversal set? They're in because a franchise overview that omits the remake or the spin-off feels incomplete; if real data shows them merging things that don't belong together, dropping them is a one-constant change.
- Should a long-running OVA series be main-line when it isn't on the sequel chain? Currently it lands in extras. Leaving it there until a concrete franchise argues otherwise.
- Whether the profile "my top series" list ranks by main-line average or all-inclusive average is a question for that change; both are computed and returned here.
