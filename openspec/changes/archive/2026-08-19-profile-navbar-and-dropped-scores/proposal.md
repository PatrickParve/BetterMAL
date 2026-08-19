## Why

Three unrelated rough edges in the same area of the app — score visibility, the navbar, and the profile page — each cost a small amount of friction on every visit:

- The "Always show MAL scores for completed shows" setting hides the score of a show I **dropped**, even though I'm never going to watch it and a MAL average can no longer spoil or bias me. The one status where the score is definitively harmless is the one status the setting doesn't cover.
- The navbar's ordering doesn't match how the controls are used: Recap sits at the far end of the left group away from My List it belongs beside, and on the right the search bar is centred while Profile, the score toggle, and Settings sit in an order that puts the least-used control (Settings) not at the edge.
- The profile page reports `Episodes` as a bare number with nothing to measure it against, and it has no equivalent of the recap page's season and year rankings — a user's favourite seasons and years are only reachable by opening a recap for a period that happens to cover them.

## What Changes

**Score visibility — dropped shows**

- The "Always show MAL scores for completed shows" setting is extended to cover **Dropped** entries as well as Completed ones, and is relabelled to say so. Every surface that renders a MAL score with a per-entry reveal rule participates: my list rows, the anime detail page, Top anime, series entry rows and extras tiles, the series timeline, the recap page's top ten and hot takes, and the profile page's opinion-divergence lists.
- The series-level aggregate averages (`MAL · Main series` / `MAL · Everything` on the series page, and the profile page's Top series tiles) follow the same extension: a finished-airing member I dropped counts as settled exactly like a completed one when deciding whether the group's MAL average may be revealed.

**Navbar layout**

- Left group order becomes Home, **My List, Recap**, Top, Season, Airing — Recap moves to sit directly right of My List.
- The search bar stops being a centred element of its own and joins the right-hand group. Right group order, left to right, becomes **search bar, hide/show scores toggle, Profile, Settings** — so Settings sits at the far right edge, with Profile beside it.

**Profile page**

- A new episode-progress bar: total episodes watched against total episodes across my whole list, rendered with the app's existing progress-bar treatment. Entries whose anime has no published episode count are excluded from both sides of the figure, and the section says how many entries it counted so the exclusion is visible rather than silent.
- A new **Favourite seasons** and **Favourite years** pair of rankings, computed over my whole list on the same Bayesian basis the recap page's season and year rankings already use, attributed by air date. Each shows at most five, each offers a "see all" overlay listing every qualifying season/year in rank order, and the two sit side by side (stacking on a narrow display) exactly as the recap page's ranking pair does.

## Capabilities

### New Capabilities

None — every behavior lands in an existing capability.

### Modified Capabilities

- `score-visibility`: the always-show setting covers Dropped entries as well as Completed ones, and is renamed accordingly.
- `series-page`: the series' aggregate MAL averages reveal on dropped-or-completed finished-airing members rather than completed-only.
- `navigation-and-search`: the navbar's left and right group ordering, and the search bar's placement, are respecified.
- `profile-stats`: the profile page gains an all-list episode progress bar and a Favourite seasons / Favourite years ranking pair.

## Impact

**Frontend**

- `components/Navbar/Navbar.tsx`, `Navbar.css` — link order, search bar moved into the right group, flex layout adjusted.
- `context/ScoreVisibilityContext.tsx`, `pages/SettingsPage.tsx` — setting label; the persisted `localStorage` key and its stored value are unchanged, so an already-enabled setting stays enabled.
- `pages/SeriesPage.tsx` (`malGroupRevealed`, `isGroupCompleted`), `components/MyListRow.tsx`, `SeriesEntryRow.tsx`, `SeriesExtraTile.tsx`, `SeriesTimeline.tsx`, `pages/AnimeDetailPage.tsx`, `pages/TopAnimePage.tsx` — the per-entry `completed` predicate passed to `ScoreValue` widens to completed-or-dropped.
- `pages/ProfilePage.tsx`, `ProfilePage.css` — progress bar section, favourite-seasons/years pair reusing `RankingOverlay`.
- `api/types.ts`, `api/client.ts` — new profile DTO fields and, if fetched separately, a rankings endpoint binding.

**Backend**

- `Services/Profile/ProfileService.cs` + `ProfileDto.cs` — the divergence DTO's reveal flag widens to dropped; new episode-progress figures; new favourite-season/year rankings.
- `Services/Recap/RecapService.cs` — the recap row/hot-take reveal flag widens to dropped.
- `Services/Series/SeriesAverages.cs` (`MainLineCompletedByMe`) and `SeriesRankingIndex.cs` — the group-level reveal rule widens to dropped, which also carries into the profile's Top series tiles.
- `Services/Recap/RecapRankingBuilder.cs` — reused (or generalised) so the profile's favourite seasons/years rank on the identical Bayesian formula rather than a second implementation.
- `Controllers/ProfileController.cs` — endpoint surface for the new profile data.

**Not affected**

- My own scores, which the hide toggle never governed.
- The hide/reveal mechanics themselves — the placeholder, the per-score reveal control, the reserved-slot width rule, and the non-persistence of an individual reveal are all unchanged.
