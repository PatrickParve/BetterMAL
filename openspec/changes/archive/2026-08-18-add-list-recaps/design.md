## Context

The app already holds everything a recap needs. `UserAnimeEntry` carries my score,
progress, status, and `CompletedAt`; `AnimeMetadata` carries `AiredFrom`,
`MediaType`, `MalScore`, and (for anime whose detail page has been visited)
`AverageEpisodeDurationSeconds`. Nothing about a recap requires a MAL call, a
schema change, or new configuration — it is a second read of data already in
Postgres.

Three existing pieces set the shape of this change:

- **`MyListService`** builds a flat whole-list read model and documents that
  "grouping by status, status filtering, sorting, and rank numbers are all
  client-side concerns". The recap follows the same philosophy.
- **`ProfileService`** already computes a global mean, a population standard
  deviation, and an opinion-divergence measure that normalises my 1–10 scores
  and MAL's much narrower community averages onto a common scale. The recap's
  hot takes are the same measure over a subset, so it must share the code, not
  restate the formula.
- **`SeasonCalendar`** already maps a date onto MAL's fixed season quarters and
  offers a monotonic season point index. Season attribution and season ranking
  reuse it rather than growing a second calendar.

The frontend precedent is equally clear: `SeasonPage`, `TopAnimePage`,
`SearchPage`, and `AiringPage` all drive their parameters through
`useSearchParams` so each control change is a real history entry, while
`usePageData`/`useRestorableState` handle restore. `MyListPage` is the one
parameterised-in-spirit page that reads no search params today.

Two naming notes: the request said "autumn", but MAL, `SeasonCalendar`, and
`SeasonPage` all say `fall` — this change uses `fall` so it stays consistent
with the season browser. The request also listed "4 kinds of recaps" while
describing three; three is confirmed.

## Goals / Non-Goals

**Goals:**

- One recap surface covering three period modes, switchable in place, with every
  stat and ranking recomputed as the period or time filter changes.
- Inclusion rules that live in exactly one place, so the recap page and the
  my-list scope it hands off to can never disagree about which anime a period
  holds.
- Season and year rankings that resist the small-sample problem, and that give
  the same answer for the same season whichever recap mode reaches it.
- Instant top-10 re-ranking and media-type narrowing, with no refetch.
- Stats that agree with what the profile and series pages already report for the
  same anime — same mean, same runtime assumption, same divergence measure.

**Non-Goals:**

- Any change to the profile page. Favourite season and favourite year as profile
  cards are deliberately deferred until this ranking math has been used.
- Persisting or caching recap results. Every recap is computed on read.
- Month- or day-level precision. Periods are whole years and whole seasons.
- Any MAL traffic, schema migration, or new background work.
- Sharing, exporting, or image-rendering a recap.

## Decisions

### D1 — The server computes the recap; the client owns presentation

`GET /api/recap` returns the **whole included set** as ranked rows, plus the
stat block and the rankings. The client does no inclusion, no stat arithmetic,
and no ranking; it does the top-10 slice, the basis switch, and the media-type
narrowing locally.

Three of the computations are inherently whole-list, not whole-period, and so
cannot be done from a period's rows alone:

- the Bayesian prior `C` is my mean score across my entire list;
- hot-take normalisation needs the whole list's mean and standard deviation on
  both score scales;
- the runtime fallback constant must be the one `ProfileService` and
  `SeriesService` already share.

*Alternative considered:* extend `MyListItemDto` with `airedFrom` and
`averageEpisodeDurationSeconds` and compute everything client-side, reusing the
already-fetched list. Rejected — it would put the Bayesian prior, the divergence
normalisation, and the runtime assumption into TypeScript alongside the C#
copies that already exist, which is exactly how two surfaces start disagreeing
about the same anime.

*Alternative considered:* return only the top 10 from the server. Rejected —
media-type narrowing must re-rank *within* a type, and the top 10 overall may
contain two films while the period holds thirty. Returning the whole set also
gives the my-list handoff its id set for free.

### D2 — Availability comes from a separate summary endpoint, loaded once

`GET /api/recap/availability` returns, for the whole list, per-year entry counts
under both time filters and per-season entry counts. The recap picker loads it
once when it opens and gates every option locally — including multi-year ranges,
whose counts are exact sums of the per-year counts, because each entry has one
completion date and one start date and therefore lands in exactly one year under
either filter.

*Alternative considered:* have the picker call `/api/recap` on every selection
change to learn whether the period is empty. Rejected — it turns a spin through
the year selector into a request per year, for information that is a few hundred
integers in total.

The recap response *also* reports both filters' counts for the period it was
asked about, so the recap page can disable an option and fall back without a
second call when the period changes.

### D3 — The Bayesian `v` counts scored anime, not watched anime

The request defines `v` as "the number of anime you watched in that specific
season or year". This design uses **the number of anime in that group that I
scored**. `R` is the mean of my scores; an unscored entry contributes nothing to
`R`, so counting it in `v` would claim confidence the data does not support — a
season of one 10 and four unscored entries would be treated as fully trusted at
`m = 5` and would rank on that single score alone, which is precisely the
failure the Bayesian average exists to prevent.

Thresholds are as specified: `m = 5` for a season, `m = 20` for a year. `C` is
drawn from the whole list rather than the period, which is what makes a season's
score identical whether it is reached through a yearly recap or a multi-year one.

### D4 — Hot takes reuse the profile's normalisation but not its gates

The divergence measure — each score expressed as standard deviations from its own
scale's mean, then subtracted — is extracted from `ProfileService` into a shared
helper that both surfaces call. Mean and standard deviation are computed over the
**whole list's** rated pairs, not the period's: a twelve-anime season cannot
support its own standard deviation, and sharing the global one is what keeps the
recap and the profile from labelling the same anime differently.

The profile's *gates* are deliberately not carried over. The profile applies a
1.0-SD threshold plus score-label gates (my score ≤ 5 with MAL ≥ 7.5, or my score
≥ 8 with MAL ≤ 7.5) because it is building two named lists that must earn their
headings. A recap period may hold nothing that passes those gates and should still
surface its most divergent picks, so hot takes are a plain top three by absolute
divergence, each labelled by direction.

The profile's minimum-rated-pairs guard is kept, since it protects the shared
mean and standard deviation rather than the lists.

### D5 — Attribution is by `AiredFrom` through `SeasonCalendar`

Year and season attribution both derive from `AnimeMetadata.AiredFrom`:
`SeasonCalendar.GetSeasonFor` for the season, `AiredFrom.Year` for the year. An
anime with no `AiredFrom` is excluded from any aired-in-period selection and from
both rankings — it cannot be placed on the calendar — but still appears in a
watch-history selection whose period contains its `CompletedAt`.

*Alternative considered:* store and use MAL's own `season` field, which the wire
DTO already carries but nothing persists. Rejected for this change — it would need
a migration and a backfill, and the request explicitly asks for start-date
attribution ("started airing dec 30 → include it in the 2022 year"), which is what
the calendar quarter gives.

### D6 — "Movies watched" counts progress, not status

A movie counts when its included entry has at least one episode watched. This
parallels "episodes watched" — both read progress — so the two numbers together
account for the period's whole viewing, and the pair `episodes + movies` is the
included set's total progress.

*Alternative considered:* count only movies with status Completed. Rejected — a
film watched and left unmarked, or one sitting in a dropped state after a rewatch,
would silently vanish from a stat whose name says otherwise.

### D7 — Time spent uses cached durations with the app's standing fallback

Per-entry runtime is `EpisodesWatched × (AverageEpisodeDurationSeconds ??
AssumedMinutesPerEpisode × 60)`, exactly as `SeriesService` already computes it.
The constant stays where it is, in `ProfileService`, and is referenced rather
than duplicated.

### D8 — The recap page is URL-driven

`/recap?mode=…&from=…&to=…&year=…&season=…&filter=…&basis=…&type=…`. Mode, period,
time filter, ranking basis, and media type all live in search params, matching
`SeasonPage` and `TopAnimePage`. This buys reload survival, back/forward through
control changes, and a linkable recap, and it is what lets the "see all" handoff
carry the exact view the user was looking at.

### D9 — The my-list scope carries recap parameters, not an id list

The handoff URL is `/my-list?recapMode=…&recapYear=…&recapFilter=…&recapType=…`.
`MyListPage` sees those params, calls `/api/recap` with them through `usePageData`,
and intersects the returned ids with its own rows. The inclusion rule therefore
exists once, on the server, and the my-list page cannot drift from the recap that
sent it there.

*Alternative considered:* put the id list in the URL. Rejected — unbounded length.
*Alternative considered:* pass ids through React Router location state. Rejected —
it does not survive a reload, and the spec requires that it does.

### D10 — The scope is additive on the my-list page, and dismissible

A recap scope narrows the candidate rows *before* the page's existing status
tabs, filter bar, and sort run, and shows as a labelled, dismissible indicator.
The existing controls are untouched — they keep their `useRestorableState`
behaviour and know nothing about the scope. `MyListPage` gains `useSearchParams`
solely for this, so nothing about its current restore behaviour changes.

### D11 — Stats describe the whole included set, never the narrowed one

The media-type control belongs to the top 10 and re-ranks only it. A stat block
that silently changed meaning when a control above the top 10 was touched would
be a trap; the same applies to the ranking basis. Both rankings and the stat
block read the full included set.

### D12 — The ranking-basis control is offered on every aired-in-period recap

The request ties the my-score/MAL-score toggle to the "what aired" filter. A
season recap has no filter but is intrinsically an aired-in-period selection, so
it gets the toggle too. Under "what I watched" the control is absent and the
ranking is by my score, since the set is not organised by air date and a MAL
ranking of it would answer a question nobody asked.

### Shape of the work

**Backend** — `Services/Recap/`:

| Piece | Responsibility |
|---|---|
| `RecapPeriod` | mode + bounds; resolves a mode and its parameters to an inclusive date range and to the list of years/seasons it covers |
| `RecapEntrySelector` | applies the two time filters and their status rules; the single home of the inclusion rules |
| `RecapStatsBuilder` | the stat block, including movies/episodes split and runtime |
| `RecapRankingBuilder` | Bayesian season and year rankings, top-three posters for the leaders |
| `ScoreDivergence` (shared) | the normalisation extracted from `ProfileService`, called by both |
| `RecapService` / `IRecapService` | assembles the DTO |
| `RecapAvailabilityService` | the per-year/per-season summary |

`RecapController` exposes `GET /api/recap` and `GET /api/recap/availability`,
thin as the other controllers are: validate and map, one service call.

**Frontend** — `pages/RecapPage.tsx` on `/recap`, `components/RecapPickerOverlay.tsx`
and `components/YearRankingOverlay.tsx` on the shared `Modal`, plus the entry
point and scope handling in `MyListPage`.

## Risks / Trade-offs

- **Calendar quarters can disagree with MAL's own season label** for a handful of
  shows — a late-December debut MAL files under the following winter lands in the
  earlier fall here. → Accepted deliberately: the request asks for start-date
  attribution, and one rule applied consistently beats two rules that disagree.
  Storing MAL's `season` field remains available as a later refinement.

- **The runtime fallback understates films.** A 120-minute film with no cached
  duration is counted as 24 minutes. → Pre-existing, shared with the profile and
  series pages, and self-correcting: visiting an anime's detail page caches its
  real duration. Not worsened by this change, and worth fixing globally rather
  than only inside the recap.

- **Every recap request scans the whole list.** → The same scan `MyListService`
  and `ProfileService` already do on every read, over a few thousand rows in a
  single-user local app. No caching added; if it ever matters it will matter for
  all three at once.

- **A multi-year "what aired" recap can return nearly the whole list.** → Bounded
  by list size and no larger than `/api/my-list`, which the app already ships on
  every visit to that page. Rows carry only the fields the top 10 renders.

- **The my-list scope costs a second `/api/recap` call.** → Cached by `usePageData`
  under the scope's own key, so it is one call per distinct scope, not per render,
  and it buys a single server-side definition of what a period contains.

- **Hot takes drop the profile's label gates**, so a period whose entries all sit
  near both means will still show three "hot takes" that are not especially hot.
  → Accepted: the block is explicitly the period's *most* divergent picks, and
  showing both scores lets the reader judge how divergent that actually is.
