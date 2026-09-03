## Why

Two things in the navbar and the lists read as unfinished.

The navbar's updates control is the only control up there that gives no sign it is the thing currently on screen. Every page control marks itself with a border and a tinted background while you are on its page; open the updates dropdown and the bell stays flat, so the panel hanging under it looks detached from the button that opened it.

And now that a picture can be *chosen* per anime (`artwork-selection`), a my-list anime can carry landscape or square artwork — and every list row in the app crops it. The detail page, the series page, the More tiles, the timeline cards and the update cards all learned to draw a wide picture whole; the rows did not. The same anime therefore shows its full key visual on its detail page and a centre-cropped sliver of it on My List, in Latest updates, in the history overlay, in Recap, on Airing, and in the search dropdown. That is the artwork you picked, shown wrong nearly everywhere you actually browse.

## What Changes

**The navbar's updates control marks itself while its panel is open**

- The bell carries the same treatment a navbar page control carries for its own page — tinted background plus a border stronger than hover's — for as long as its dropdown is open, and for as long as the full-history overlay opened from that dropdown is open, so the button that produced what is on screen stays visibly the source of it.
- Hovering the marked bell keeps it marked, exactly as hovering the current page's link does.
- The unseen-news dot is untouched: it still appears and clears on its own rules, and it can be showing or not showing independently of whether the control is marked.

**A wide picture is drawn whole in every list row and thumbnail**

One rule, applied wherever an anime's picture is drawn in a fixed-height row or thumbnail slot: the slot keeps its height, the picture is never cropped, and a picture that is wider than it is tall takes the width its own proportions give it at that height — pushing the row's text along rather than being squeezed into the poster box. Rows keep the height they have today, so no list gets longer. Portrait artwork keeps its current box exactly, so an ordinary poster looks identical to today.

Surfaces adopting it:

- My List rows and Top Anime rows.
- The profile's Latest updates feed and both opinion-divergence lists; the full edit-history overlay; the unresolved-episodes overlay; the top-anime tie-break overlay.
- The Recap page's hot-take rows and top-ten rows; the ranking rows' poster clusters, inline and in the "See all" overlay (profile and recap alike).
- The Airing page's slot rows and the dashboard's Airing today rows.
- The related-anime overlay; the completion-score overlay's picture.
- The navbar search dropdown's thumbnails, and the settings page's diff rows and refresh-picker results.

**Deliberately not changed** — answering "is it worth changing everywhere?": the poster **grids and strips** keep cropping. The season/year/search/browse card grid, the Top Anime rank-1–3 showcase and rank-4–10 cards, the Recap podium, the recap score board, and the profile's "My top anime" / "Most rewatched" / "Top series" strips are built on every tile being the same size — `profile-stats` requires poster strips to keep a fixed tile size, and `list-recaps` sizes score-board tiles by source resolution. Widening one tile there breaks the grid's rhythm for one anime's sake. A row has no such symmetry to lose: it is already free to be as wide as its content, which is exactly why the rows are worth fixing and the grids are not. If a grid ever needs this, the pattern already exists — the series page's More tiles let a landscape tile span two columns — and it can be adopted then, on its own.

## Capabilities

### New Capabilities

- `artwork-presentation`: the app-wide rule for how an anime's picture is *drawn* once chosen — whole, at its own proportions, in a height-bound row or thumbnail slot that widens for wide artwork and is bounded so a panorama cannot squeeze the text beside it. Names the surfaces it governs and the grids it deliberately excludes. Complements `artwork-selection`, which governs which picture is chosen, not how it is rendered.

### Modified Capabilities

- `anime-updates`: adds a requirement that the navbar's updates control is marked while its dropdown, or the history overlay opened from it, is open — distinct from and independent of the unseen-news indicator.
- `navigation-and-search`: the navbar's active-marking requirement is widened from "every control that leads to a page" to include the updates control while its panel is open, so the marked-control rule no longer reads as excluding it, and the score toggle stays the one navbar control that is never marked.
- `library-views`: the my-list / top-anime "List-row posters fill the row" requirement gains the wide-artwork case — the row's height and the portrait box are unchanged, and a wide picture widens rather than crops.
- `profile-stats`: the "Profile list-row posters fill the row" requirement gains the same case for Latest updates, edit-history and divergence rows, and states that the fixed-tile-size rule for the poster strips is untouched.

## Impact

**Frontend only.** No backend, API, DTO, schema, or dependency change — a picture's orientation is read off the loaded image, exactly as `useLandscapePicture` already does for the detail and series pages, so nothing new is fetched or stored.

- **New** `frontend/src/components/RowPicture.tsx` / `.css` — the one component every row and thumbnail renders its picture through: image or placeholder, orientation detection, and the shared slot geometry driven by two custom properties each caller sets.
- `frontend/src/hooks/useLandscapePicture.ts` — gains a square-inclusive variant for the row rule; its existing landscape-only behaviour, and the four pages using it, are unchanged.
- `frontend/src/components/Updates/UpdatesMenu.tsx` / `.css` — the open-state class and its styling.
- Row and thumbnail call sites: `MyListRow.tsx` + `MyListPage.css`, `TopAnimePage.tsx` + `.css`, `ProfilePage.tsx` + `.css`, `EditHistoryOverlay.tsx` + `.css`, `UnresolvedEpisodesOverlay.tsx` + `.css`, `AnimeRankOverlay.tsx` + `.css`, `RankingOverlay.tsx` + `.css`, `RankingSection.tsx` + `.css`, `RecapPage.tsx` + `.css`, `AiringPage.tsx` + `.css`, `AiringTodayList.tsx` + `.css`, `RelatedAnimeOverlay.tsx` + `.css`, `CompletionScoreOverlay.tsx` + `.css`, `SearchBar.tsx` + `.css`, `SettingsPage.tsx` + `.css`.
- `frontend/src/components/SeriesEntryRow.css` — carries a row-picture rule but the component is no longer rendered anywhere (only its label helpers are imported); left alone rather than migrated.
