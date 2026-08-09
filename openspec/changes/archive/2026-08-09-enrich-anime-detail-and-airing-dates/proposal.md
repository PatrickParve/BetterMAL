## Why

The anime detail page is a dead end for the two things you actually want to do there: adding a show to the list takes a trip through the edit overlay even when you just want "watching" or "plan to watch", and the only related anime you can reach are the one prequel and one sequel we happen to store — movies, side stories and specials in the same franchise are invisible. It also can't tell you when the next episode lands, even though we already store per-episode AniList airing rows that answer exactly that. Separately, the schedule page's day headers show a bare day number, which is ambiguous the moment a week straddles a month boundary.

## What Changes

**Airing page**

- Day-column headers show day *and* month as `d.M` (e.g. `8.8`, `9.8`) instead of the bare day number, so a week crossing a month boundary reads unambiguously.

**Anime detail page**

- When an anime is currently airing (or hasn't finished releasing), the Status field appends a countdown to the next episode in days and hours, sourced from the stored per-episode airing rows. No stored future episode means no countdown — nothing is estimated.
- Three stacked action buttons replace the single Edit button, in a fixed order: **Add to watching**, then **Add to list** (which becomes **Edit** once the anime is in my list), then **Refresh data**. "Add to watching" sets status Watching; "Add to list" adds it as Plan to watch. Both go through the existing entry-edit rules.
- A **More** button sits to the left of the prequel/sequel buttons whenever the anime has related entries beyond prequel and sequel. It opens an overlay listing those relations (side story, alternative version, summary, spin-off, character, other) grouped by relation type, each linking to that anime's detail page.
- When the anime being viewed is itself a side entry, a **Main series** button links to MAL's `parent_story` relation for it.
- All related-anime relations MAL reports are stored, not just the first prequel and first sequel. **BREAKING** (data-shape): `AnimeMetadata.PrequelMalId/PrequelTitle/SequelMalId/SequelTitle` are replaced by a related-anime table; the four columns are dropped.
- External links become a styled row of three: **MyAnimeList**, **AniList**, **SeriesGraph**. AniList deep-links via the AniList media id we already cache in `AnimeAiringSync`, falling back to an AniList title search when no id is stored. SeriesGraph links to a title search, since SeriesGraph indexes by TMDB id and no MAL→TMDB mapping exists in the app.

## Capabilities

### New Capabilities

None — every change extends an existing capability.

### Modified Capabilities

- `airing-schedule`: day-column headers show day and month, not the day number alone.
- `anime-detail`: next-episode countdown in the Status field; three-button action stack (add-to-watching / add-to-list-or-edit / refresh); More-relations overlay and main-series link backed by full related-anime storage; external-link row extended to AniList and SeriesGraph, replacing the requirement that no AniList link is shown.
- `mal-api-integration`: full detail fetches persist every related-anime edge MAL returns, rather than only the first prequel and first sequel.

## Impact

**Backend**

- `Models/AnimeMetadata.cs` — drop the four prequel/sequel columns; add a related-anime collection.
- New `Models/AnimeRelatedAnime.cs` + EF configuration + migration (backfilled on the next full-detail refresh of each anime).
- `Services/Mal/MalMappingExtensions.cs` — `ApplyTo` writes all related edges; `ApplyLeanTo` still never touches them.
- `Services/Detail/AnimeDetailDto.cs` / `AnimeDetailService.cs` — carry related-anime groups, next-episode ETA, and the AniList id.
- `Data/Repositories/AnimeMetadataRepository.cs` — include the related-anime rows on the detail read.
- No new endpoints; `PATCH /api/anime/{id}/entry` already backs both add buttons.

**Frontend**

- `pages/AiringPage.tsx` — header format.
- `pages/AnimeDetailPage.tsx` + `.css` — countdown, button stack, More overlay, external-link row.
- New `components/RelatedAnimeOverlay.tsx` (built on the shared `Modal`).
- `api/types.ts` — `AnimeDetailDto` gains `nextEpisode`, `aniListId`, and `relatedAnime`; loses the four prequel/sequel fields in favour of the grouped shape.

**External dependencies**

- No new API integrations. AniList ids come from data already fetched; SeriesGraph is a plain URL template.
