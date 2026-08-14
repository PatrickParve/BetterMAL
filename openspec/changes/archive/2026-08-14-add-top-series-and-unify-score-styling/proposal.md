## Why

The profile page ranks my anime two ways — My top anime and Most rewatched — but never my *franchises*, even though the app already models franchises as series and computes exactly the numbers a ranking needs. "What are my favourite series?" is a different and more interesting question than "what are my favourite single entries", and it is currently unanswerable without opening series pages one at a time.

Separately, the series page redesign gave MAL scores and my scores a colour language — blue is MAL, purple is me — that makes which side of a figure is "the world" and which is "me" readable without labels. That language stops at the edge of the series page. Every other surface (anime detail, My List, Top anime, the profile lists and strips) still renders both scores as undifferentiated text, so the same two numbers look like two different things depending on which page you are on.

## What Changes

### Top series (profile page)

- A new **Top series** section on the profile page, presented like My top anime and Most rewatched: a horizontally-scrolling, drag-scrollable poster strip of fixed-size tiles.
- Each tile shows **both** averages — MAL's main-series average and my main-series average — using the same figures the series page's main-series score chips show, colour-keyed blue/purple.
- Tiles link to that series' page, not to an anime detail page.
- The ranking is over the **main-series** average (the main line only, ignoring extras), and a control switches the ranking basis between **my average** and **MAL's average**. It defaults to my average and is restored on back-navigation like the existing media-type filters.
- Only series with at least one member in my list are eligible. A series with no scored main-line member under the *active* basis can't be ranked by it and is omitted while that basis is selected.
- The strip is uncapped and scrolls, matching Most rewatched.

### Series coverage for the ranking

- Series are built lazily today, so a ranking can only see franchises that have already been built. Requesting Top series therefore **enqueues background builds** for my-list anime that belong to no stored series yet, through the existing background build queue — the section fills out over time and never blocks a request.
- A new **"Build all series from my list"** action on the Settings page builds every missing series in one background run and **reports progress** (`built / total`) while it runs, mirroring the existing "Refresh all airing dates" action.

### Unified score presentation

- The blue-MAL / purple-mine colour language moves out of the series page and becomes an **app-wide** convention, applied wherever a MAL score or my score is rendered: the anime detail page's score boxes, My List rows, the Top anime page's rows, the profile page's divergence lists and poster-tile badges, and the new Top series tiles — alongside the series page surfaces that already have it.
- **Editable score controls are included**: My List's inline score dropdown and the entry editor's score select take the same "mine" treatment, so setting a score looks like the score it sets.
- MAL scores keep every existing hide/reveal behaviour unchanged — the restyle is presentation only and never affects whether a value is in the rendered output.

## Capabilities

### New Capabilities

- `score-presentation`: the app-wide visual language for MAL scores and my scores — the two colour roles, where the convention applies, what it must not be used for, and its relationship to score hiding.

### Modified Capabilities

- `profile-stats`: gains the Top series section — its membership rule, ranking basis and toggle, the two averages shown per tile, ordering, empty states, and state restoration.
- `series-page`: gains two further build triggers alongside the existing page-visit and search triggers — one from the profile's Top series read, one from an explicit bulk "build all series from my list" action with progress reporting.

## Impact

- **Backend**
  - `Services/Profile/ProfileDto.cs` — new `TopSeriesSectionDto` / `TopSeriesItemDto` carrying both main-series averages per series.
  - `Services/Profile/ProfileService.cs`, `IProfileService.cs` — build the section from stored series joined against my list entries.
  - `Controllers/ProfileController.cs` — new `GET /api/profile/top-series` (ranking basis as a query parameter).
  - `Services/Series/SeriesService.cs` — the main-series average computation (`MalAverage`/`MyAverage` over main-line members) is reused rather than reimplemented, so the strip and the series page can never disagree.
  - `Services/Series/` — a new bulk-build trigger + background service + progress tracker, mirroring `AiringFullRefreshTrigger`/`AiringFullRefreshBackgroundService`/`AiringFullRefreshProgressTracker`; `SeriesController` gains build-all trigger and status endpoints.
  - Existing `ISeriesBuildTrigger` reused for the on-read backfill.
- **Frontend**
  - `api/types.ts`, `api/client.ts` — top-series section types + fetchers, build-all trigger/status.
  - `pages/ProfilePage.tsx` + `ProfilePage.css` — the new section, its basis toggle, and its tiles.
  - `pages/SettingsPage.tsx` — the build-all action with its progress line.
  - New shared score-presentation styling (score colour tokens promoted from `SeriesPage.css` to the global stylesheet, plus a shared score-chip component/classes) consumed by `AnimeDetailPage`, `MyListRow`, `TopAnimePage`, `ProfilePage`, `EntryEditorOverlay`, and the existing series surfaces.
- **No schema migration** — `Series`, `SeriesMember`, and `UserAnimeEntry` already carry everything the ranking needs.
- **No new external dependencies**, and no MAL fetches added to any request path (all building stays on background queues).
