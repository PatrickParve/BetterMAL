## Why

The app knows every anime individually but has no concept of a *franchise*. A five-season show with three movies, two OVA runs and a pile of recap specials is nine unrelated detail pages joined only by one-hop prequel/sequel buttons — there is nowhere to see the whole thing at once, no watch order, no "how good is this series overall", and no way to ask "which series do I like most" because the app has never had a series identity to hang an average on. Everything needed to build one is already stored: MAL's `related_anime` edges land in `AnimeRelatedAnime` on every full-detail fetch.

## What Changes

**A new series page (`/series/:animeId`)** — reachable from any anime that belongs to a series, showing the whole franchise as one thing:

- **Hero**: the first entry's poster (the earliest main-line entry), the series title, a status pill (Ongoing / Finished / Upcoming), and the year span (`2013 – 2023`).
- **Score boxes**: MAL average across the main series and across everything, plus **my average** across the main series and across everything — each with the count it averaged over (`8.42 · 5 of 6 scored`), MAL scores honoring the global hide toggle.
- **Watch order**: the main series in order — every season and movie that continues the story, no specials — as numbered rows with poster thumb, type, year, episode count, MAL score, my score and my status. Each row links to its detail page and can be edited in place through the existing entry editor.
- **More**: everything that isn't main-line — specials, OVAs, recap movies, music videos, side stories, spin-offs, alternative versions — grouped by media type, same row shape.
- **Series stats**: total episodes and **total runtime** of the main series (`4d 6h 30min`, with extras counted separately), my progress across the series (episodes watched / total, entries completed, time watched, and time left to finish it), longest gap between consecutive entries, the highest-rated entry and my favourite entry, studios and genres, and a per-entry MAL-vs-mine score strip so a series' dip in the middle is visible at a glance.

**Series composition is derived, not hand-maintained.** A series is the connected component of the related-anime graph over story relations (sequel, prequel, side story, parent story, summary, full story, spin-off, alternative version) — deliberately *not* `alternative_setting`, which would fuse unrelated universes. The main line is what the sequel/prequel chain reaches, minus specials and music videos; everything else in the component is an extra.

- Composition is computed once per series and persisted (`Series` + `SeriesMember`), so the page is a cache read after the first build and a stable series identity exists for later features.
- The build walks the graph from the anime you came in on, full-fetching any member the app has never fetched (one paced MAL call each, capped per build), and self-heals: a build that reaches members of an existing series absorbs it, so a newly announced sequel folds into the franchise on first visit.
- A **Rebuild** control on the page forces recomputation; otherwise a series older than 30 days rebuilds on visit (free when every member is already cached).

**Anime detail page** gains a **Series** button in the existing relations row, shown whenever the anime has at least one story relation, linking to that anime's series page.

## Capabilities

### New Capabilities

- `series-page`: what a series is (graph derivation, main line vs extras, watch order, root/title), how it is built and cached, the endpoints that serve it, and everything the page renders — the two MAL averages, the two of my averages, the series stats, and the two entry sections.

### Modified Capabilities

- `anime-detail`: the relations row gains a Series link for any anime that belongs to a series.
- `navigation-and-search`: the hover-highlight requirement's enumeration of full-width anime rows extends to the series page's entry rows.

## Impact

**Backend**

- New `Models/Series.cs` and `Models/SeriesMember.cs` (+ `DbContext` configuration and an `AddSeries` migration). `SeriesMember` is keyed by `AnimeId`, so an anime belongs to at most one series by construction.
- New `Services/Series/` — `SeriesGraphBuilder` (BFS over `AnimeRelatedAnime`, on-demand full fetches via the existing `IMetadataRefreshService.RefreshOneAsync`, main-line classification, watch order), `SeriesService` (resolve-or-build, stats and averages projection), `SeriesDto`.
- New `Controllers/SeriesController.cs`: `GET /api/series/by-anime/{animeId}` and `POST /api/series/by-anime/{animeId}/rebuild`.
- Reuses `RefreshGate` for build single-flight and the MAL pacer/daily budget for fetches — no new external integration, no new background job.
- Averages are computed at read time from `UserAnimeEntry`/`AnimeMetadata` rather than stored, so editing a score never leaves a stale series average behind.

**Frontend**

- New `pages/SeriesPage.tsx` + `.css`, new `components/SeriesEntryRow.tsx` + `.css`.
- `AppShell.tsx` — `/series/:animeId` route.
- `api/client.ts` / `api/types.ts` — `getSeries`, `rebuildSeries`, and the series DTO shapes.
- `pages/AnimeDetailPage.tsx` — the Series button.

**Not in this change**

- No profile "my top series" section and no series list page — this change creates the series identity and the averages they need; the list itself is a follow-up.
- No manual series editing: composition comes from MAL's relation graph, and a wrong grouping is fixed by MAL, not by a local override.
