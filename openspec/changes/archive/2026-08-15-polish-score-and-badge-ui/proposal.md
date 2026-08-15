## Why

Five small presentation defects have accumulated across the app, all of the same kind: a piece of UI takes more space than its content deserves and pushes its neighbours out of line. The worst is the hidden-MAL-score placeholder — `•••• 👁` is wider than the score it stands in for, so every row that contains one sits differently from the rows around it, and inside a compact chip the eye is shoved off-centre by dots that carry no information. The remaining four are the same story in miniature: an oversized score box on the detail page, a series badge that makes search cards taller than their neighbours, a currently-watching card that omits broadcast progress its sibling section shows, and a rewatch badge that is the only uncoloured badge left on the profile page.

## What Changes

- **Hidden MAL scores show the eye alone.** The `••••` placeholder is removed everywhere. A hidden score renders only its reveal button, centred in a slot as wide as the score would be, so rows keep their column alignment whether a score is hidden or shown and revealing one shifts nothing. This is a single change in the shared `ScoreValue` component, so it lands in every view at once — My List, Top anime, the detail page, both series-page densities, the profile page's divergence lists and top-series chips.
- **Currently-watching cards show broadcast progress.** Each card's existing watched/total bar gains the blue aired fill already used by the anime detail page: aired episodes behind my watched episodes, in one bar. The card's height, its editable count, and its `+` control are unchanged. The fill is drawn only while MAL reports the anime as currently airing, matching the detail page's rule, which requires the dashboard payload to carry that flag per currently-watching item.
- **The detail page's two score boxes shrink to their content.** The rank/MAL-score and my-score/rewatch boxes no longer stretch across the full column with the padding of a full-size panel; they size to what they hold and sit at the top-right of the title as a compact pair.
- **The series badge stops adding height to search cards.** The "Series · N entries" badge is scaled down so its line occupies the same vertical space as an anime card's `TYPE · N ep` meta line, keeping every card in the results grid the same height — and shrinking the same badge in the type-ahead dropdown row.
- **The "most rewatched" count badge gets a white/silver treatment.** It picks up the bordered, tinted form of the top-anime score badge, in white/silver rather than a score colour, so a rewatch count reads as a deliberate badge without ever being mistaken for a score.

## Capabilities

### New Capabilities

None — every change refines behaviour an existing capability already owns.

### Modified Capabilities

- `score-visibility`: the hidden-score placeholder is the reveal control alone in a reserved, score-width slot — no blurred digits, no width change between hidden and shown.
- `main-dashboard`: currently-watching cards render broadcast progress behind watched progress, and the dashboard payload carries per-item currently-airing state.
- `anime-detail`: the two score boxes are sized to their content rather than filling the width of the main column.
- `navigation-and-search`: the series badge is constrained so a series card matches the height of the anime cards beside it.
- `profile-stats`: the most-rewatched tile badge carries a white/silver bordered treatment in the same form as the top-anime score badge.

## Impact

**Frontend**
- `components/ScoreValue.tsx`, `ScoreValue.css` — placeholder markup and slot sizing (the one edit that covers "everywhere").
- `components/ScoreChip.css`, `index.css` — the `.score-value__blur` colour overrides for MAL chips and coloured values become dead once the blur element is gone; they are removed with it.
- `components/CurrentlyWatchingCarousel.tsx` — passes `aired` to `ProgressBar`. `ProgressBar` itself already supports the aired fill and is unchanged.
- `pages/AnimeDetailPage.css` — score-box sizing.
- `components/SeriesBadge.css` — badge scale, affecting both `SearchBar` and `SearchPage`.
- `pages/ProfilePage.css` — `.rewatched-strip__count` treatment.
- `api/types.ts` — `CurrentlyWatchingItemDto` gains the currently-airing flag.

**Backend**
- `Services/Dashboard/MainDashboardDto.cs`, `MainDashboardService.cs` — one added field on `CurrentlyWatchingItemDto`, derived from the same `AiringStatus` the current-season items already read. No query, schema, or MAL-integration change.

**Not affected**
- The hide toggle's semantics, its persistence, the per-score reveal behaviour, and the "always show completed scores" setting all keep working exactly as they do — only what a hidden score looks like changes.
- Every other consumer of `ProgressBar` (My List, detail page, series page) is untouched, as is the home page's `AiringProgressBar` section.
