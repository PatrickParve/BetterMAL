## Why

My list holds years of watch history but the app never looks back at it. The profile
page reports lifetime totals and the top-anime section ranks everything at once, so
there is no way to ask "what did 2022 actually look like?" or "which season did I
enjoy most across the 2010s?". A recap turns the same stored entries into a
per-period read — stats, a top 10, and season/year rankings — using data already in
Postgres, with no new MAL traffic.

## What Changes

- **New recap page** (`/recap`) covering only anime in my list, in three period
  modes the user switches between:
  - **Multi-year** — a start year and an end year, no month or day.
  - **Yearly** — one calendar year.
  - **Season** — one year plus one of winter/spring/summer/fall.
- **Dynamic time filter** on the multi-year and yearly recaps, toggling the recap
  between two ways of selecting anime for the period:
  - *What I watched* — entries I completed or dropped inside the period.
  - *What aired* — entries whose anime debuted or aired in the period, counting
    completed, dropped, on-hold, and watching. A show is attributed to the period
    containing its **start** date, so a series that began 30 Dec 2022 belongs to
    2022 (and to a 2011–2020 multi-year recap if it began inside it).
  A filter option with no matching entries is disabled; one matching entry is
  enough to enable it. The season recap has no toggle — it is always "what aired".
- **Stat block** on every recap, recomputed as the period and filter change:
  mean score (my average), anime counted, how many completed, episodes watched
  (excluding movies), movies watched, time spent, and hot takes — the three anime
  in the period where my score diverges furthest from MAL's.
- **Top 10 of the period** on every recap, with three controls: rank by my score
  or MAL's score (MAL's offered only under the "what aired" filter), filter by
  media type, and — only when the period holds more than 10 anime — a button that
  opens my list already scoped to the same period, filter, and type.
- **Season and year rankings** on multi-year and yearly recaps under the
  "what aired" filter, ranked by a Bayesian average that pulls thin seasons toward
  my global mean rather than letting a single 10 top the chart. Only seasons/years
  holding at least one watched anime are ranked; the year ranking shows five at a
  time with an overlay for the rest. The number-one season and year each show
  posters of my top three anime from it.
- **"Recap a period" entry point** on the my-list page beside the status tabs,
  opening a picker for recap type, period, and time filter.
- **My list accepts an incoming recap scope** so the top-10 "see all" button lands
  on a list already narrowed to that period's anime.
- **Empty periods are stated, not hidden**: a period with no entries says so; a
  period with one entry renders the full recap.

## Capabilities

### New Capabilities
- `list-recaps`: recap period selection (multi-year, yearly, season), the dynamic
  time filter and its entry-inclusion rules, the per-period stat block, the top 10
  and its controls, and the Bayesian season/year rankings.

### Modified Capabilities
- `library-views`: the my-list page gains the "Recap a period" control beside the
  status tabs, and gains the ability to open pre-scoped to a recap period, filter,
  and media type arriving from the recap page.

## Impact

- **Backend** — new `Services/Recap/` (period resolution, entry selection, stats,
  Bayesian ranking) and a `RecapController` behind `GET /api/recap`. Reads
  `UserAnimeEntry` + `AnimeMetadata` through the existing
  `IUserAnimeEntryRepository`; no schema change, no MAL calls, no new
  configuration. Reuses `SeasonCalendar` for date→season mapping and
  `ProfileService.AssumedMinutesPerEpisode` as the runtime fallback for anime with
  no cached episode duration.
- **Frontend** — new `RecapPage` on route `/recap` (search-param driven, matching
  season/top-anime/search), a recap picker overlay built on the shared `Modal`,
  and a year-ranking overlay. `MyListPage` gains the entry-point button and reads
  an incoming recap scope from the URL.
- **Docs** — `CODE_GUIDE.md` endpoint table and section list.
- **Not in scope** — the profile page is untouched; surfacing favourite season and
  favourite year there is a follow-up once the ranking math is proven.
