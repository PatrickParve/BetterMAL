## Why

The app currently lets an entry be edited into states its anime cannot support. An anime that has not aired a single episode can be given episodes watched, a score, a rewatch count, and a status of Completed, Dropped, or On hold — none of which can be true of a show nobody has been able to watch yet. My List, the dashboard's currently-watching carousel, and the anime detail page all draw an editable `watched/total` progress row with a "+" control for such an anime, inviting exactly those edits, and the entry editor offers every field regardless.

Completion has the mirror-image problem: an entry can be marked Completed while its anime is still airing and only half of it exists, and once marked it stays Completed forever, even as new episodes air past what was watched.

## What Changes

- Define one shared notion of **whether any episode of an anime has aired**, resolved from the stored AniList aired-so-far count and, when that count is unknown, from the anime's MyAnimeList airing status. Only `not yet aired` with no aired row counts as "nothing has aired".
- **Hide the editable progress row entirely** — bar, `watched/total` count, and "+" control — wherever it appears for an anime with no aired episodes: My List rows, the dashboard's currently-watching cards, and the anime detail page. It returns everywhere the moment one episode has aired.
- **Block episode-watched edits** on an anime with no aired episodes, at the API as well as in the UI: the ceiling for episodes watched becomes 0 rather than the published total.
- **Restrict status** on an anime with no aired episodes to Watching and Plan to watch. Completed, Dropped, On hold, and Rewatching are rejected, and the editor's dropdown disables them.
- **Block score and rewatch count** on an anime with no aired episodes: neither can be set to a value above zero. Clearing either back to none/zero stays possible, so an entry that already carries one is never trapped.
- **Tighten Completed.** Completed remains available for a finished anime with a known total. For a currently-airing anime it becomes available only when the aired-so-far count is known, and choosing it sets episodes watched to that aired count — "watched every episode out" — rather than to the eventual total.
- **Re-open a completed airing show.** When a new episode of a currently-airing anime airs past what a Completed entry has watched, that entry returns to Watching, logged as activity and queued for MAL sync like any other status change. Its finish date is left alone, per the existing never-overwrite rule. Because an episode becomes aired purely by its stored air instant passing, this takes effect the first time the entry is looked at rather than waiting on a scheduled pass; the recurring airing tick repeats the same check as a backstop, so an entry nobody opens still reaches MyAnimeList.
- Carry the airing status and aired-so-far count into the surfaces that need them to make these decisions client-side: the currently-watching card, the Top anime row, and the entry editor's target.

## Capabilities

### New Capabilities

None. Every rule here refines behaviour that existing capabilities already own.

### Modified Capabilities

- `list-editing`: adds the aired-episode precondition on episodes watched, status, score, and rewatch count; tightens when Completed may be entered; adds the automatic return to Watching when a new episode airs; states that the editable progress row is absent when nothing has aired.
- `library-views`: My List rows show no progress cell and no score dropdown for an anime with no aired episodes.
- `main-dashboard`: a currently-watching card for an anime with no aired episodes shows no progress row.
- `anime-detail`: the detail page's progress bar and "+" control are absent while nothing has aired; the status line beside them stays.

## Impact

**Backend**

- `Services/Entries/UserAnimeEntryEditService.cs` — the episodes-watched cap, the status guard, the score and rewatch guards, and the Completed fill/eligibility rules.
- `Services/Entries/` exceptions — new rejection types for the blocked edits, surfaced by `Controllers/EntriesController.cs` as 400s alongside the existing `CannotCompleteUnknownEpisodeCountException`.
- A new re-open service in `Services/Entries/`, called from the read paths that display an entry (`MyListService`, `MainDashboardService`, `AnimeDetailService`) and from `Services/Airing/EpisodeScheduleRefreshBackgroundService.cs` as a backstop.
- DTOs gaining airing fields: `Services/Dashboard/MainDashboardDto.cs` (`CurrentlyWatchingItemDto`), `Services/Library/TopAnimeItemDto.cs`, and their services (`MainDashboardService`, `TopAnimeService`).

**Frontend**

- `api/types.ts` — `CurrentlyWatchingItemDto`, `TopAnimeItemDto`, and `EntryEditorTarget` gain airing status / aired-count fields.
- `utils/anime.ts` — the shared "has any episode aired" helper every surface reads.
- `components/MyListRow.tsx`, `components/CurrentlyWatchingCarousel.tsx`, `pages/AnimeDetailPage.tsx` — conditional progress row.
- `components/EntryEditorOverlay.tsx` — disabled status options, score, and rewatch controls.
- `pages/MyListPage.tsx`, `pages/TopAnimePage.tsx`, `pages/SeriesPage.tsx` — pass the new editor-target fields.

**Not affected**

The read-only aggregate bars: the profile page's all-list episode progress, the series page's series-wide progress, and the dashboard's "Followed shows airing" `aired/total` bars. None of them edits an entry, so none of them is gated.
