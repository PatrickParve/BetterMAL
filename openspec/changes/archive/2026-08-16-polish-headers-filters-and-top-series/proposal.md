## Why

Several pages read as unfinished: the anime detail page's MAL/AniList/SeriesGraph buttons balloon in size whenever the genre line wraps to two lines, Search and Season have no way to filter by media type even though My List already supports it, seven pages' titles are either bare `<h1>`s colliding with surrounding layout (Settings, Profile) or plain unstyled text that doesn't fit its page, and the Top Anime page's #1–#3 entries look identical to #497–#500 despite being the whole point of the ranking.

## What Changes

- Fix the anime detail page's MAL/AniList/SeriesGraph link buttons so they stay a fixed size regardless of how many lines the genre list wraps to, centered alongside the genre content instead of stretching to match its row height.
- Add a Type filter (TV, Movie, OVA, Special, ONA, Music, etc.) to the Search results page, reusing the existing `FilterMultiSelect` component and `MEDIA_TYPE_ORDER` labels already used on My List.
- Add the same Type filter to the Seasonal Anime page, threaded through the season API's server-side pagination (season loads paginate server-side, unlike Search's fully-loaded candidate set).
- Redesign the page title/header for My List, Top Anime, Seasonal Anime, Schedule, Search results, Settings, and Profile so each has deliberate, well-fitted spacing and presentation instead of inheriting bare global `h1` margins that collide with neighboring content — most visible today on Settings and Profile, where the `h1`'s own `32px` global margin stacks on top of the page's own layout gap. Treatments may differ per page where that reads better.
- Give the Top Anime page's rank 1–3 rows distinctive, more prominent styling (size, color, layout) so the top of the ranking stands out from the flat list-row treatment shared by every other rank.

## Capabilities

### New Capabilities
- `page-header-design`: how each page's title/header is presented — spacing, layout, and page-specific treatment — covering My List, Top Anime, Seasonal Anime, Schedule, Search results, Settings, and Profile.

### Modified Capabilities
- `anime-detail`: the MAL/AniList/SeriesGraph link buttons' sizing behavior when the genres line wraps to two lines.
- `navigation-and-search`: the search results page requirement gains a media-type filter.
- `season-browser`: the season page requirement gains a media-type filter.
- `library-views`: the Top Anime page requirement gains distinct styling for rank 1–3.

## Impact

- Frontend only, except the season media-type filter which also needs a backend query param.
- Pages: `AnimeDetailPage`, `SearchPage`, `SeasonPage`, `TopAnimePage`, `MyListPage` (title-only, for consistency), `AiringPage`, `SettingsPage`, `ProfilePage`.
- Components: `FilterMultiSelect` (reused, not modified), possibly a new top-3 row/card treatment on `TopAnimePage`.
- API: `getSeasonPage` client function and the `/api/season/{year}/{season}` endpoint gain an optional `type` filter param; `getSearchPage`/`/api/anime/search/page` unaffected (search already loads its full candidate set client-side).
