## Why

Four small surfaces offer controls that do nothing, or hold back information the
user wants more of. The home carousel's arrows stay fully live and hover-lit at
both ends of the row, so a click that cannot move anything looks exactly like one
that can. The series page's "In my list" control renders even when no extra of the
series is in my list, where it can only ever filter everything away, and the same
page's Studios/Genres stats sweep in every OVA, special and side story so a long
franchise's lists read as a catalogue of the whole neighbourhood rather than of the
series itself. The profile's Latest updates box stops at 20 items regardless of
when they happened, which for an active month cuts the feed off mid-week. And the
Series page's `Upcoming` status describes a franchise where nothing has ever aired
— reachable only by planning a brand-new unaired franchise with no aired relatives,
so in practice the filter button never matches anything and the fifth precedence
rule never fires.

## What Changes

- **Home page — Currently watching carousel.** Each arrow is disabled once the row
  cannot move further in its direction, and a disabled arrow takes no hover
  treatment. The pair still appears and disappears together on overflow, exactly as
  today.
- **Series page — More.** The "In my list" control is not rendered at all when no
  extra of the series is in my list. The media-type filter row, the expand/collapse
  control and the groups themselves are untouched.
- **Series page — Stats.** Studios and Genres are computed over the **visible main
  line** — the main line after the version-slot pick resolves — instead of over
  every member. This puts them on the same member basis the episode, runtime,
  watched and completed figures above them already use, so a slot pick moves them
  together with the rest.
- **Profile page — Latest updates.** The feed carries every collapsed item from the
  **last 30 days** rather than a fixed 20, keeping today's filtering, merging and
  per-anime-per-field-group collapsing exactly as they are. When 30 days yields
  fewer than 20 items, it reaches further back to reach 20, so a quiet month never
  empties the box. The box still shows five whole rows at rest and scrolls.
- **Series status — `Upcoming` removed.** **BREAKING** (wire contract) The status
  pill loses its third precedence rule: a franchise with nothing finished and
  something unaired now reads `Ongoing`, which is what rule 4 already says for every
  franchise with something still to come. The `Upcoming` filter button, pill
  variant, `SeriesStatus` union member and Status-sort slot all go with it.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `main-dashboard`: the Currently watching arrows gain a disabled state at each end
  of the row, with no hover treatment while disabled.
- `series-page`: the "In my list" control is withheld when no extra is in my list;
  Studios and Genres move to the visible main line; the status pill drops
  `Upcoming` from its precedence and from its set of values.
- `series-browser`: the Status filter group and the Status sort order drop
  `Upcoming`; the card's pill carries three values rather than four.
- `profile-stats`: the Latest updates feed is bounded by a 30-day window with a
  20-item floor instead of a flat 20-item cap.

## Impact

**Frontend**
- `components/CurrentlyWatchingCarousel.tsx` / `.css` — arrow disabled state, scroll
  position tracking, hover rule.
- `pages/SeriesPage.tsx` — conditional render of the "In my list" control.
- `pages/SeriesBrowserPage.tsx` — inherits the shortened filter option list.
- `utils/anime.ts` — `SeriesStatusFilterValue`, `SERIES_STATUS_FILTER_OPTIONS`,
  `STATUS_FILTER_VALUES`, `SERIES_STATUS_ORDER`.
- `components/SeriesStatusPill.tsx` / `.css` — the `upcoming` variant.
- `api/types.ts` — the `SeriesStatus` union.

**Backend**
- `Services/Series/SeriesStatusRules.cs` — the `Upcoming` branch.
- `Services/Series/SeriesService.cs` — `BuildStats` studios/genres basis, and the
  `SeriesStatsDto` doc comment that describes it.
- `Services/Profile/ProfileService.cs` — `RecentActivityCount` becomes a floor,
  plus a 30-day cutoff; the raw fetch window has to cover it.
- `Data/Repositories/IActivityLogRepository.cs` / `ActivityLogRepository.cs` — a
  read that can return every log since an instant, not just a fixed count.

**Tests**
- `Services/Series/SeriesServiceComputeStatusTests.cs` — the `Upcoming` case now
  expects `Ongoing`.
- `Services/Series/SeriesServiceBuildStatsTests.cs` — studios/genres basis.
- `Services/Profile/ProfileServiceActivityFeedTests.cs` — window and floor.

**Not affected**: the full edit-history overlay (unchanged by the feed's window),
the navbar updates bell, MAL sync, and every other page's status handling.
