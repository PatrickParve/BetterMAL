## Why

Six things across five pages, each one a surface that reports slightly less than it knows.

The series page's status pill collapses two different situations into `Ongoing`: a franchise with a season on air right now, and a franchise with nothing on air but a sequel announced. Those are the two states a viewer actually cares to tell apart, and the pill is the one place they'd expect to be told.

The recap page has two smaller versions of the same problem. Its "Browse the season" button wears the app's generic accent while every other control in a season recap wears the season family's colour, so on hover it reads as belonging to some other page. And stepping from one year or season to the next throws the page back to the top — the data swaps under a heading the reader is no longer looking at, and they have to scroll back down to compare.

The anime detail page crops every portrait picture to one fixed 260×368 box. Artwork that is taller than that ratio loses its top and bottom — Link Click's third season is the plain example — so the page shows a slice of the poster rather than the poster.

The search page has no answer at all when MAL's search API is down: the backend already swallows the failure and returns an empty list, so the page reads "No anime found" for a query the app holds hundreds of matches for locally, and says nothing about why.

Finally, "Most rewatched" ranks single anime only. A franchise rewatched season by season — the thing most actually worth measuring — has its total split across as many tiles as it has seasons, and the one figure that would answer "what have I spent the most time going back to" is nowhere on the page.

## What Changes

- **The series status pill gains `Airing`.** When a main-line entry of the series is currently on air the pill reads `Airing`. `Ongoing` narrows to what is left: nothing on air, but something announced and unaired still to come. `Finished` and `Upcoming` are untouched, and a series where only an *extra* is on air keeps reading `Ongoing` as it does today.
- **The season recap's "Browse the season" button wears the season colour.** Its hover and focus states take the season family's tint and border — the same treatment the Season tab beside it already takes on hover — instead of the generic accent.
- **Stepping the recap's period holds the scroll position.** Changing the year or the season, by either the selects or the stepper arrows, leaves the page where it was; only the data changes. This extends the hold that the top 10's own controls already have. The recap-type tabs and the time filter still return to the top, since they change the shape of the page rather than its period.
- **The anime detail page shows the whole picture.** A portrait picture taller than the page's poster box takes the height its own proportions give it at the box's width, rather than being centre-cropped to a fixed height. Landscape artwork keeps the treatment it already has, and the placeholder for an anime with no picture keeps the fixed box.
- **The search page falls back to what is stored locally.** When the live MAL search fails, the results page searches every anime the app has cached, ranks the matches, and shows them with a notice that the MAL search could not be reached and these are local results. When the MAL search succeeds, nothing about the page changes.
- **"Most rewatched" gains a Series view.** A `Series` option beside the media-type tabs re-ranks the section by franchise, ordered by total time spent rewatching: for each member of the series, the number of rewatches times its episode count times its episode duration, summed across every member — main line and extras alike. First watches never count. Each tile shows its franchise's rewatch time as `3d 7.7h`, `7.7h`, or `0.38h`, and opens that series' page.

## Capabilities

### New Capabilities

None. Every change lands on an existing capability.

### Modified Capabilities

- `series-page`: the status pill gains an `Airing` value, driven by the main line rather than by every member.
- `list-recaps`: the season-page button takes the season family's hover and focus colours; the period selects and stepper arrows join the controls that hold the scroll position.
- `anime-detail`: portrait artwork taller than the poster box is shown whole rather than cropped.
- `navigation-and-search`: the full search results page falls back to locally stored anime when the live MAL search fails, and says so.
- `profile-stats`: "Most rewatched" gains a Series scope, ranked by total rewatch time across every member of a franchise.

## Impact

**Backend**

- `Services/Series/SeriesService.cs` — `ComputeStatus` gains the main-line `Airing` arm and needs the main-line membership it currently does not receive.
- `Services/Series/SeriesDto.cs` — the `Status` field's documented value set.
- `Services/Search/AnimeSearchService.cs` — `SearchMalAsync` must distinguish a failure from an empty result, and `SearchPageAsync` gains the local-fallback path and reports which source answered.
- `Services/Search/SearchPageDto.cs` — a flag saying the results came from local storage after a failed MAL search.
- `Data/Repositories/AnimeMetadataRepository.cs` — a richer local projection for the fallback (media type, episode count, MAL score alongside the title fields the type-ahead index already carries).
- `Services/Profile/ProfileDto.cs`, `Services/Profile/ProfileService.cs`, `Services/Profile/IProfileService.cs`, `Controllers/ProfileController.cs` — the rewatched-series section and its endpoint.
- `Services/Series/SeriesRankingLookup.cs` — the member projection gains rewatch count, total episodes, and episode duration so the rewatch-time sum can be computed over the join the Top series section already runs.

**Frontend**

- `pages/SeriesPage.tsx`, `pages/SeriesPage.css`, `api/types.ts` — the `Airing` pill value, its class, and its colour.
- `pages/RecapPage.tsx` — `keepScroll` on the period controls; `pages/RecapPage.css` — the season button's family colours.
- `pages/AnimeDetailPage.css` — the poster box's height rule.
- `pages/SearchPage.tsx`, `pages/SearchPage.css`, `api/types.ts` — the fallback notice.
- `pages/ProfilePage.tsx`, `pages/ProfilePage.css`, `api/client.ts`, `api/types.ts` — the Series scope on "Most rewatched".
- `utils/anime.ts` — the `3d 7.7h` / `0.38h` rewatch-time formatter, which is a different format from the existing `formatRuntime`.

**Not affected**

The navbar type-ahead search already merges local candidates with MAL's on every keystroke, so it degrades to local results on its own and needs no fallback of its own. The completion badge beside the series status pill, the Top series section's ranking and eligibility rules, and the existing anime-scoped "Most rewatched" media-type scopes all keep their current behaviour.
