## Why

The "Followed shows airing" section on the home page shows a plain watched/total progress bar — the same bar used on My List and the detail page. For a show that is *currently airing*, the more useful question is "how far along is the broadcast?", because you cannot be further ahead than what has aired. Today the section gives no signal of broadcast progress at all, so a show that is 3 episodes into a 24-episode run looks identical to one that is 20 episodes in.

## What Changes

- Replace the watched/total progress bar in the home page's "Followed shows airing" section with an **airing progress bar**: the filled portion represents episodes that have already aired out of the show's total episode count, and the label reads `aired/total`.
- Overlay the viewer's own watching progress as a **purple fill layered on top of the aired fill** in the same track, using the site's accent colour. The aired fill renders in blue, representing broadcast progress. The purple overlay is rendered only when there is watching progress to show (episodes watched > 0).
- Expose an `episodesAired` count for each followed-airing show from the dashboard API, derived from the same episode-schedule source of truth that already backs "Airing today" and the next-episode countdown (AniList per-episode dates when cached, weekly-cadence estimate otherwise).
- When the aired count cannot be determined (no start date or no broadcast time and nothing cached), the aired portion renders empty and the label shows `?/total` rather than a misleading number; the green watched overlay still renders.
- **Scope is the home page only.** The shared `ProgressBar` component and its use on My List, the anime detail page, and the currently-watching carousel are untouched, and keep showing watched/total.

## Capabilities

### New Capabilities

None — this refines behaviour already owned by the `main-dashboard` capability.

### Modified Capabilities

- `main-dashboard`: the "Current season section with filters and progress" requirement changes what the per-card bar represents — broadcast progress (aired/total) with a layered watched overlay, instead of watched/total — and is scoped explicitly to the home page.

## Impact

**Backend**
- `Services/Airing/IEpisodeScheduleService.cs` / `EpisodeScheduleService.cs`: new method that answers "how many episodes have aired as of this instant", reusing the existing AniList cache and weekly-cadence fallback.
- `Services/Dashboard/MainDashboardDto.cs`: `CurrentSeasonItemDto` gains an `EpisodesAired` field (nullable — unknown is a real state).
- `Services/Dashboard/MainDashboardService.cs`: populates the new field for the `currentSeason` list.

**Frontend**
- `api/types.ts`: `CurrentSeasonItemDto` gains `episodesAired: number | null`.
- New `components/AiringProgressBar.tsx` + `.css`: the two-layer bar, used only by the home section.
- `components/CurrentSeasonSection.tsx`: renders the new bar instead of `ProgressBar`.
- `components/ProgressBar.tsx` and its other three call sites: unchanged.

**Not affected**: MAL sync, list editing, the airing schedule page, and persistence. No API breaking change — the DTO gains a field, it does not lose one.
