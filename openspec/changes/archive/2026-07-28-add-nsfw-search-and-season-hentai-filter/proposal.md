## Why

MAL silently omits R+/Rx-rated anime from search results unless the request opts in with `nsfw=true` — the season listing and my-list fetches already do this, but neither search path does, so searching for an adult title returns nothing even though the same title shows up on the season page. Separately, the search results page still uses numbered pagination while every comparable browse surface (season) uses continuous scroll, and once search starts returning adult titles there is no way to keep hentai out of the season browser for users who don't want to see it.

## What Changes

- Both search paths (`GET anime?q=` via `MalClient.SearchAnimeAsync`, feeding the navbar type-ahead and the full search page) request `nsfw=true`, so R+/Rx-rated anime appear in search exactly as they already do on the season page.
- The search results page replaces its numbered pagination with continuous (infinite) scroll, matching the season page: results accumulate as the user scrolls, and the query/sort still live in the URL. The `page` URL parameter goes away. **BREAKING** for existing `/search?q=…&page=N` links — the `page` parameter is ignored, results always start from the first chunk.
- The full-search candidate depth stays at 100 MAL matches, so the server-side sort remains global across everything the user can scroll to.
- A new "Hide NSFW" toggle on the Settings page, **unchecked by default**, hides hentai — and only hentai — from the season browser. R, R+ and every other rating stay visible; a title is treated as hentai when MAL's own `rating` field is `rx`.
- The toggle applies to the season browser only. Search, my list, top anime, the airing schedule, the home dashboard, and anime detail pages are unaffected.
- `AnimeMetadata` gains a `Rating` column, populated from MAL's `rating` field on both the lean listing path (season/top-anime) and the full detail path, so the filter can be applied server-side inside the season query and result counts stay correct.

## Capabilities

### New Capabilities

None — both changes extend existing capabilities.

### Modified Capabilities

- `navigation-and-search`: search requests include NSFW-rated results; the full search results page switches from paginated to continuous scroll (the "Paginating results" scenario and the URL-state requirement change).
- `season-browser`: new user-controlled hentai filter applied server-side to the season listing and its result count, defaulting to off.
- `mal-api-integration`: the persisted-field set gains `rating`, and the search endpoint joins the season/user-list endpoints in passing `nsfw=true`.

## Impact

**Backend**

- `Services/Mal/MalClient.cs` — `nsfw=true` on the search URL; `rating` added to `DefaultAnimeFields`.
- `Services/Mal/Dto/MalAnimeNode.cs` — new `Rating` property.
- `Services/Mal/MalMappingExtensions.cs` — `Rating` mapped in both `ApplyTo` (rich) and `ApplyLeanTo` (lean).
- `Models/AnimeMetadata.cs` + a new EF migration — `Rating` column (nullable text).
- `Data/Repositories/SeasonRepository.cs`, `Services/Season/SeasonBrowseService.cs`, `Services/Season/ISeasonBrowseService.cs`, `Controllers/SeasonController.cs` — new `hideHentai` flag threaded through the season page read and applied before the count.

**Frontend**

- `pages/SearchPage.tsx` / `SearchPage.css` — infinite scroll replaces `Pagination`.
- `pages/SettingsPage.tsx` — the new toggle.
- New `context/ContentFilterContext.tsx` — `localStorage`-persisted `hideHentai` preference, mounted in `AppShell.tsx`.
- `pages/SeasonPage.tsx` — reads the preference and passes it to the season endpoint.
- `api/client.ts` — `getSeasonPage` gains `hideHentai`.

**Data**

- Existing cached rows have `Rating = null` until their next season refresh (once per local day per season), so the filter under-filters — never over-filters — for at most a day per season. Nothing is deleted or rewritten.

**Docs**

- `CODE_GUIDE.md` §1 (MAL fields), §2 (season endpoint query params), §3 (`Services/Search/`), §4 (contexts, pages).
