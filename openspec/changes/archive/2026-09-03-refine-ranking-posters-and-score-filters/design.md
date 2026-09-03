## Context

One component renders every season/year ranking in the app: `frontend/src/components/RankingSection.tsx`, fed by `describeSeasonRanking`/`describeYearRanking` and backed by `RecapSeasonRankingDto`/`RecapYearRankingDto` from `Services/Recap/RecapRankingBuilder.cs`. Six rankings go through it — the recap page's Season ranking, Year ranking, and two time-watched rankings, and the profile page's Favourite seasons and Favourite years. That concentration is what makes this change small: every item below is a change to one shared builder, one shared component, or one shared stylesheet.

**Posters today.** `RecapRankingBuilder.TopPosters` takes the group's scored entries, orders them `OrderByDescending(MyScore).ThenBy(Title, OrdinalIgnoreCase)`, and takes three. The `ThenBy(Title)` is the problem: the app maintains exactly one answer to "of these two anime I scored 9, which do I prefer" — the `anime-ranking` capability's total order — and every other surface reads it (the recap's top 10 ties, the score board's slots, the profile's top anime, the season/year browsers' my-score sort). The posters are the one place that answers alphabetically instead.

**The score filter today.** `polish-favourites-filters-and-browse-scroll` added `scoreCounts` (a ten-slot histogram) to both ranking DTOs and built the **All / With most: 10…1** control in `RankingSection`, but wired it only into the profile page. Its selection re-ranks the rows and rewrites each row's `meta` line; the posters were explicitly left alone ("Everything else about a ranked row SHALL be unchanged under a score selection: its name, its posters…"). That is the clause this change reverses: a row ranked on how many 8s it holds, illustrated by its 10s, states one thing and shows another.

**A live defect.** `RecapPage` renders those rankings as `rows={recap.seasonRanking.map(describeSeasonRanking)}`. `Array.prototype.map` passes `(value, index, array)`, so the row's index arrives as `describeSeasonRanking`'s `selectedScore` parameter — and `0` is not `undefined`, so every row takes the *selected* branch of the `meta` ternary. The first row reads `undefined × 0 · 6 scored` (`scoreCounts[0 - 1]`), the second `0 × 1 · 9 scored`, and so on. Confirmed against the running backend, whose DTO is correct; the fault is entirely in that call site. The component's own comment ("RecapPage's four callers never pass one") records the assumption that the point-free `.map()` quietly broke.

**Sizing the per-score posters.** Making posters follow the selection means the client needs, per row, the best three anime at *each* score rather than only the best three overall. Measured against the current database (471 scored anime carrying an air date, spread over 85 seasons and 30 years): `sum(least(count, 3))` over every (season, score) bucket is **426** poster records, and over every (year, score) bucket **258** — against 345 for today's `TopPosters` across the same two rankings. The figure is bounded twice over: by `3 × buckets`, and by the number of scored air-dated anime in the ranking, since each anime lands in exactly one bucket. This is tens of kilobytes, not a payload problem.

## Goals / Non-Goals

**Goals:**
- Which three anime illustrate a season or a year is decided by my ranking, on every surface that renders these rankings, with the best-ranked leftmost.
- Under a score selection, a row's posters come from that score, so the row's picture and the figure it was ranked on agree.
- The recap page's Season ranking and Year ranking offer the same score filter the profile's favourites rankings offer, with the selection addressable in the URL like every other recap control.
- The recap's ranking rows state their real figures again.
- The filter's buttons form an even strip whose widths do not depend on the digit count.
- A long title on a top 6-10 row stops at a fixed cap rather than at the row's edge.
- One implementation of the filter, shared by both pages — no second copy of `offeredScores`, `rankByScoreCount`, or the overlay's title composition.

**Non-Goals:**
- No change to how the Bayesian ranking is computed, to its trust thresholds, or to its four-step tie-break sequence. Only the posters and the presence of the filter change; the order rows come back in under **All** is byte-identical to today's.
- No change to how a score selection re-ranks (count of the selected score, most first, ties falling back to the ranking's own order). That rule stands as `polish-favourites-filters-and-browse-scroll` set it.
- No score filter on the two time-watched rankings. They rank on seconds watched and hold no per-score figure to filter by.
- No change to the time rankings' posters. `BuildSeasonTimeRanking` shows none, `BuildYearTimeRanking` illustrates its leader by episodes watched; both stay as they are.
- No change to the ranking-row height, the five-row cap, the "See all" overlay's behaviour, or the family colouring.
- No new endpoint and no schema change. The ranking is derived at read time, as it already is.
- No revisit of the podium's own two-line title treatment; only the 6-10 rows are in scope.

## Decisions

### D1 — Posters are picked by the one ranking, not by title

`RecapRankingBuilder.BuildSeasonRanking` and `BuildYearRanking` take an `AnimeRankingSnapshot` and order each group's posters by `snapshot.RankOf(animeId)` ascending, with anime the ranking does not cover falling last, ordered between themselves by title case-insensitively and then by anime id for a total order. The `OrderByDescending(MyScore)` that opened the old comparator disappears: the ranking is *already* score-descending by construction (`AnimeRankingKey` compares score first), so ordering by rank alone gives score-descending order and settles a within-score tie by where I actually placed the anime — which is the whole point.

Nulls-last is not a new convention here. `RecapPage.compareByRankThenTitle` (the top 10 and the score board) and the `list-recaps` spec's own "an unranked entry tied on my score follows the ranked ones" already say it; this is the same rule applied one surface further. It matters in exactly one case: a scored Plan-to-watch or not-yet-aired anime, which `RankBandResolver` puts outside the ranking entirely while `TopPosters` still counts it as scored.

Both callers already hold a snapshot or the parts to build one — `RecapService` builds `rankingSnapshot` from the whole list at the top of `GetRecapAsync`, and `ProfileService.GetProfileAsync` already reads `orderedAnimeIds` for `BuildTopAnimeSection` (D6).

*Alternative rejected:* re-implementing the band/position ordering inside `RecapRankingBuilder` from the entries alone. `AnimeRankingKey` and its SQL twin already carry a "these must move together" warning over two copies of that rule; a third copy for three posters is not a trade worth making, and the snapshot is already in hand at both call sites.

### D2 — `PostersByScore` replaces `TopPosters`, and **All** is derived from it

Both score-ranking DTOs drop `TopPosters` and gain `PostersByScore`: one entry per score the group actually holds, descending from 10, each carrying that score's best three anime in ranking order.

**All**'s three posters are then the first three of that list read from the top — and that is exactly the group's best three by ranking, because the buckets are already in score order and each holds its own score's best. So the unfiltered posters are *derived*, not shipped: there is no second list that could disagree with the per-score one, and nothing to keep in step when either rule changes. This is the same reasoning `describeSeasonRanking` already applies to the inline rows and the overlay — one description, two renderings.

Sparse rather than a ten-slot array: a season holding one score would otherwise ship nine empty slots, and the client never iterates by index (it looks up the selected score, or reads from the top). `ScoreCounts` stays exactly as it is — it holds *full* counts, which the filter's ordering and its offered-score list both need, and which a three-capped bucket cannot provide.

*Alternatives rejected:* (a) keeping `TopPosters` alongside `PostersByScore` — 345 redundant poster records and two rules to keep in agreement, for data one `take(3)` already yields; (b) a second endpoint fetched when a score button is pressed — a round trip, a loading state, and a cache key, for tens of kilobytes that ride along free with a read the page already makes.

### D3 — One client helper picks a row's posters, called from the two `describe` functions

`RankingSection.tsx` gains `postersFor(row, selectedScore)`: with a score, that score's bucket (empty when the group holds none, which cannot happen for a row the filter kept); without one, the first three across the buckets from the top. `describeSeasonRanking`/`describeYearRanking` call it while composing the row they already compose, so the inline five rows and the "See all" overlay — both of which render an already-described row — cannot show different posters. `RankingOverlay` and `renderPosters` are untouched: they consume `RankingOverlayRow.posters` and neither knows a filter exists.

### D4 — `offeredScores`, `rankByScoreCount`, and the overlay title move into `RankingSection`

Three pieces of the filter live in `ProfilePage.tsx` today: `offeredScores` (which buttons to show), `rankByScoreCount` (the re-rank), and the `— with most Ns` suffix composed inside each `onSeeAll` callback. Wiring the same control into `RecapPage` would otherwise copy all three.

`offeredScores` and `rankByScoreCount` move to `RankingSection.tsx` beside `scoreCountAt`, exported as the pure functions they already are — no hook, since each page still owns its own selection state and its own restoration mechanism (D5). The overlay title moves *inside* `RankingSection`: it already holds both `title` and `scoreFilter.selected`, so it composes the suffix itself before calling `onSeeAll`, and both pages' callbacks reduce to their plain setter. `ProfilePage` keeps only the two `useRestorableState` calls and the validity check that falls back to **All**.

*Alternative rejected:* a `useScoreFilter` hook owning selection, offered scores, and re-ranked rows together. It would have to serve two different state mechanisms (restorable state vs. URL parameters), so it would take the getter and setter as arguments and earn nothing over the two exported functions.

### D5 — The recap's selections are URL parameters; the profile's stay restorable state

`RecapPage` holds every view control in the query string — `mode`, `from`/`to`/`year`/`season`, `filter`, `basis`, `type` — and the `list-recaps` capability requires a recap be linkable and reload to what it was showing. The two new selections join them as `seasonScore` and `yearScore`, written through the page's own `updateParams` with `{ keepScroll: true }`, exactly as the basis toggle and the type select do: pressing a score button must not throw the page back to the top.

`ProfilePage` keeps `useRestorableState`. The profile page has no URL vocabulary for its view controls and adding one for these two alone would be inconsistent with the media-type tabs and the rewatched scope beside them.

The two share one validity rule, which each already needs for its own reasons: a selection naming a score the currently-shown ranking does not offer reads as **All**. On the profile page that covers a restored selection after the list changed; on the recap page it covers the far more ordinary case of stepping to another period, where the parameter is deliberately *kept* in the URL so stepping back restores the selection rather than silently dropping it.

The filter is offered only where the ranking is: the recap's score rankings appear only under **What aired** on a multi-year or yearly recap, so the control comes and goes with them and needs no gate of its own.

### D6 — `ProfileService` builds one snapshot per profile read

`BuildFavouriteSeasonsAndYears` needs the ranking (D1), and `BuildTopAnimeSection` already builds one — so a naive fix would sort the whole list twice per profile read. Instead `GetProfileAsync` builds the `AnimeRankingSnapshot` once from the `entries` and `orderedAnimeIds` it already has, and passes it to both. `GetTopAnimeSectionAsync`, which serves the media-type tabs from its own read, keeps building its own.

### D7 — Equal-width buttons through `min-width`, not a monospace numeral font

`.ranking-score-filter__button` sizes from its own text (`padding: 5px 12px`), so `1`, `10`, and `All` are three different widths. The fix is a `min-width` on the button sized to the widest label (**All**), with the label centred and the horizontal padding reduced so short labels are not padded past it. Every button then occupies one width whatever its label, and — since `min-width` is on the base rule, not on a state — the strip still cannot reflow as the pointer moves along it, which the existing requirement already demands.

*Alternative rejected:* `font-variant-numeric: tabular-nums`, which evens out the *digits* but leaves `1` narrower than `10` and does nothing about `All`.

### D8 — The top 6-10 title is capped by `max-width` on the title alone

`.recap-top-ten-row__title` already carries `overflow: hidden; text-overflow: ellipsis; white-space: nowrap`, but its flex parent lets it grow to the row's full width first, so it truncates only at the row's edge. A `max-width` on the title caps it earlier. Because the score is a separate flex item pinned at the row's trailing edge and the link keeps `flex: 1 1 auto`, capping the title moves nothing else: rank, poster, score column, and row height are all unchanged, which is what the existing "A top 6-10 row's score is inset from the row's edge" requirement demands of any change to these rows.

The cap is expressed in `ch` so it tracks the row's own `calc(1em + 1px)` size rather than fixing a pixel count against a fluid root, and the existing native `title` attribute keeps carrying the full title on hover.

*Alternative rejected:* switching the row to the shared `TruncatedTitle` component. It would portal a tooltip element per row to replace a native tooltip the row already has, for no behaviour the ask needs.

## Risks / Trade-offs

- **[The `.map()` defect could recur]** → The two `describe` functions keep an optional second parameter, so any future point-free `.map(describeYearRanking)` re-introduces exactly this bug. Every call site becomes an explicit arrow (`(row) => describeYearRanking(row, selected)`), and the functions' doc comment states that they must never be passed to `.map` point-free.
- **[Two poster rules could drift apart]** → They cannot: **All** is derived from `PostersByScore` (D2), so there is one rule and one list. This is the reason `TopPosters` is removed rather than kept.
- **[A ranking rule now has three consumers on the backend]** → `AnimeRankingKey`, its SQL twin, and `SeasonRepository`'s inlining already carry "these must move together" comments. This change adds no fourth copy: it reads `AnimeRankingSnapshot`, which is built from `AnimeRankingKey` itself.
- **[Payload growth on the profile read]** → Measured: 684 poster records across both rankings against 345 today, on a 471-anime scored set; bounded by `3 × buckets` and by the scored count. The profile response already carries the activity feed, two divergence lists, and a ten-card top-anime section.
- **[A wide `ch` cap on a narrow viewport]** → `max-width` caps but never floors: below the cap the title still shrinks to whatever the row leaves it, exactly as today, so a narrow layout is unaffected.
- **[Backend tests assert on `TopPosters`]** → `RecapRankingBuilderTests` and `ProfileServiceFavouriteSeasonsAndYearsTests` follow the rename in the same commit, gaining coverage for the ranking tie-break and the per-score buckets rather than only re-pointing.
