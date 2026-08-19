## Context

Five refinements across three surfaces, all sitting on machinery that already exists:

- **Ranking rows** are rendered by two components — `RankingSection` (the inline five) and `RankingOverlay` (the "See all" list) — shared by `RecapPage` and `ProfilePage`. Both draw 28×40 posters inside a row padded `8px 10px`. `RecapRankingBuilder.BuildSeasonTimeRanking` deliberately emits no posters on any row (a decision from `polish-recap-page`: the leader's three posters made the row cramped once the ranking moved into a half-width column), so a seasons-by-time-watched row is a line of text where its column-mate is 40px of poster art.
- **The recap stat block** is `RecapStatsBuilder.Build(included, wholeList)` → `RecapStatsDto` → seven tiles in `RecapPage.renderStats`. `included` is whatever the selected time filter attributes to the period: completion date under **What I watched** (Completed/Dropped only), air-start date under **What aired** (Completed/Dropped/On-hold/Watching).
- **The rating distribution** exists only on the profile page: `ProfileService.BuildScoreDistribution` emits ten zero-filled buckets plus the mean, and `ProfilePage` renders `label · bar track · "12 (7%)"` per row. The recap's response already carries `myScore` for every included entry in `items`.
- **The Bayesian rankings** are one builder, `RecapRankingBuilder`, used by both `RecapService` and `ProfileService.BuildFavouriteSeasonsAndYears`, so the two surfaces cannot disagree. Ordering today is `weighted desc → scored count desc → chronological asc`.
- **The score toggle** is a `<button class="navbar__toggle" aria-pressed={hidden}>` whose text swaps between "Hide scores" and "Show scores", driving `ScoreVisibilityContext`.

## Goals / Non-Goals

**Goals:**

- A ranking row is the same height whether or not it carries posters, on every surface that renders one.
- The recap answers "what from this period am I still watching?" alongside what it already answers about what I finished.
- The recap shows its period's rating distribution, in the same visual language as the profile page's, without a new endpoint or DTO.
- Counts align with counts and percentages with percentages, down both pages' distribution blocks.
- Two seasons or years that look identical in the ranking are ordered by something a reader can reconstruct, all the way down to the individual scores.
- The score toggle reads as a two-state control at a glance, and which state it is in is legible from the icon alone.

**Non-Goals:**

- Changing what the two time-watched rankings rank on, or giving them posters. Their tie-break (episodes watched, then chronological ascending) is left exactly as it is — see decision 6.
- Changing the Bayesian formula, its trust thresholds, or the global mean `C`. Only the ordering *after* a tie is touched.
- Any change to `ScoreVisibilityContext`'s state, its persistence key, or the per-score reveal controls. The switch is presentation over the same `hidden`/`toggle` pair.
- A rating distribution anywhere else (season page, series page), or a distribution that responds to the top 10's media-type control — see decision 4.

## Decisions

### 1. Uniform row height comes from CSS, not from giving the poster-less ranking posters

`.recap-ranking-row__link` and `.ranking-overlay__link` get `min-height: var(--ranking-poster-h)` (a new token set to the posters' existing `40px`, which the poster rules then read from as well). Neither element is `border-box`, so a `min-height` on the content box lands the row at exactly the outer height a poster row already resolves to — 40px content + 16px padding + 2px border — with no hard-coded total to drift out of sync when the padding changes.

*Alternatives considered.* Giving the seasons-by-time-watched ranking posters would equalise the heights but reverses a deliberate decision from `polish-recap-page` and re-crowds the narrow column. Equalising only in yearly mode (where the two rankings sit side by side) would make the same ranking a different height in different modes. Setting a literal `min-height: 58px` works until someone changes the padding.

Because both components are shared, this lands on every ranking on both pages at once — which is the point: rankings are one visual family and should have one row height.

### 2. Currently watching is counted on air date, in every mode and under both filters

`RecapStatsDto` gains `CurrentlyWatching`, counted as *entries with status Watching whose anime's air-start date falls inside the recap's period* — never on the completion-date attribution the **What I watched** filter uses.

This is forced by the data: an entry still in progress has no `CompletedAt`, so under **What I watched** it is not in `included` at all and a filter-scoped count would always be zero. Air-date scoping is also the reading that matches the request — "currently watching from this season/year/period" is a question about where the *anime* comes from, not about when I finished something.

The consequence, accepted deliberately: under **What I watched**, this one tile is scoped differently from its neighbours, and `Completed + Dropped + Currently watching` can exceed **In this period**. The alternative — hiding the tile under that filter — trades a small inconsistency for a tile that appears and disappears as the filter moves, which reads as a bug.

Mechanically, `RecapService` already builds the aired-attributed set to produce `airedCount` and throws the list away; it passes that list to `RecapStatsBuilder.Build` instead. Under **What aired** the count could equivalently be taken from `included`, but taking it from one source in all cases keeps a single definition.

The tile sits third, directly after Completed and Dropped, so the three status counts read as a group.

### 3. The recap's distribution is computed client-side from `items`

`RecapRowDto` already carries `myScore` for every included entry, so the recap page folds those into the same ten-bucket shape the profile's `ScoreDistributionDto` uses and renders the same component. No new endpoint, no new DTO, no second pass over the list server-side.

This follows the rule the recap was built on (`add-list-recaps` decision 1): the server returns the whole included set and the client derives its views of it. It also means the distribution recomputes instantly when the time filter changes, from data already in hand.

*Alternative considered.* Adding `ScoreDistributionDto` to `RecapStatsDto` would centralise the bucketing, but duplicates data already on the wire and adds a round trip's worth of coupling for arithmetic over at most a few hundred numbers.

### 4. The distribution covers the period, not the top 10's type filter

The block is computed over every included entry, ignoring the "All types" select — that control is scoped to the top 10 and is labelled as such ("Filter top 10 by type"). Narrowing the distribution with it would make a control in one section silently redraw another.

The recap's block also **omits the mean-score line** the profile page's carries: the stat block directly above it already reports **Mean score** for the same set, and two different renderings of one number a few pixels apart invite the reader to look for a difference that isn't there.

### 5. Count and share become two columns; the parentheses go

Each distribution row becomes `label · bar track · count · share`, with count and share as separate fixed-width, right-aligned, `tabular-nums` cells rather than one interpolated `"12 (7%)"` string. Fixed widths (rather than content sizing) are already the established reason this column exists — a wider string must not steal width from its own bar track and distort which bar reads as longest.

The parentheses are dropped: they were punctuation joining two values in one string, and once the values are separate columns they only push the percentages out of alignment with each other. `<1%` continues to stand in for a non-zero count that rounds to zero.

The component takes a size modifier (`compact`) that shrinks the type, the bar track, and the row gap for the recap's narrower column, leaving the profile page's block at its current size.

The markup moves out of `ProfilePage` into a shared `ScoreDistribution` component so the two pages cannot drift — the same reason `RankingSection`/`RankingOverlay` are shared. The profile keeps passing its server-built buckets and mean; the recap passes locally derived buckets and no mean.

### 6. The extended tie-break runs on the full-precision score, and ends newest-first

`BuildSeasonRanking` and `BuildYearRanking` order by, in sequence:

1. **Weighted score, descending — at full precision.** The whole computed value, every decimal of it, not the two decimals the DTO rounds to for display. A genuinely higher score always wins, however far down the difference sits.
2. **Scored count, descending.** As today, reached only on an exact tie above.
3. **Score-by-score, from 10 down to 1.** Build each group's histogram of my scores; at the first score where the two counts differ, the group with more anime at that score ranks first. Two groups that agree at 10 and 9 but where one holds three 8s to the other's two are separated at 8.
4. **Newest first.** Later year, or later season point, ahead of earlier — reversing today's oldest-first fallback, reached only when the histograms are identical at every score.

Two consequences of comparing at full precision, both accepted:

- **The rows this reorders are the ones that look identical.** Two seasons can both read `50 scored · 7.80` and be ordered by a difference in the fourth decimal that the row does not show. That is the cost of never overruling a real difference in score, and step 1 is where the ranking's own definition of "better" belongs.
- **Step 2 is close to a formality.** An exact double tie with *unequal* counts requires the Bayesian pull to land two differently-sized groups on bit-identical values — possible, effectively never seen. In practice an exact tie means equal count and equal mean, and step 3 does the real work. The rule is still specified and tested, because it is the stated intent and costs nothing.

Exact `==` on the doubles is a sound tie test here rather than a float-comparison hazard: `R` and `C` are both averages of integer scores, so each is an exact integer sum divided by a count, and `W` applies the same operations in the same order to both groups. Equal inputs therefore produce bit-identical results, with no accumulation order to perturb them. No epsilon is needed, and none should be added — an epsilon would resurrect exactly the "score difference overruled by a tie-break" behaviour this decision rejects.

*Alternative considered.* Rounding to the displayed two decimals before comparing would make every visible tie resolvable by the rules below, at the price of letting a lower-scoring group outrank a higher-scoring one on the strength of a count or a histogram. Rejected: the score is what the ranking ranks on, and it wins outright.

Both builders need the same four-part comparison over different group keys, so it goes in one place in `RecapRankingBuilder`: a histogram helper plus a comparison that takes (weighted score, scored count, histogram, recency index). Seasons feed it `SeasonCalendar.GetSeasonPointIndex`; years feed it the year. `ProfileService` inherits all of it without an edit, which keeps the profile-and-recap agreement the specs already require. Rounding stays exactly where it is today — applied when building the DTO, after ordering.

The two time-watched rankings keep their existing tie-break. They rank on seconds watched, where an exact tie also requires an exact tie on episodes watched — rare enough that adding a fifth rule buys nothing, and the request was about score ties.

### 7. The switch is one button, a sliding knob, and an eye that opens and closes

Markup stays a single native `<button>` — keyboard activation, focus handling, and the click target come free — re-labelled as `role="switch"` with `aria-checked` tracking **scores shown** (checked = visible = knob right = open eye), replacing today's `aria-pressed={hidden}`. Its accessible name stays the action it performs, so screen-reader output remains "Show scores" / "Hide scores".

Inside: a pill track carrying the word **Scores**, and an absolutely positioned circular knob that slides across it on `transform: translateX()`. The label is fixed text (not swapping between "Hide"/"Show"), so the control's width never changes and the navbar never reflows on toggle — the same constraint the hidden-score slot rule imposes elsewhere in the app.

The eye is one inline SVG in the knob: an eye outline with a pupil, plus a slash whose length animates via `stroke-dasharray`/`stroke-dashoffset` — the slash draws itself across the eye as scores are hidden and retracts as they are shown, with the pupil scaling down under it. Stroke-length animation is used rather than path morphing because morphing between two `d` values needs JS or SMIL, while dash offsets are a plain CSS transition that degrades cleanly.

`@media (prefers-reduced-motion: reduce)` drops the slide, the slash draw, and the pupil scale to instant state changes; the control stays fully usable and still shows the right icon.

Hover keeps the app's navigation-control treatment (accent tint and border on the track), satisfying `navigation-and-search`'s hover requirement for this control, and `:focus-visible` keeps a ring on the track.

*Alternative considered.* A checkbox styled as a switch is the conventional pattern, but this control is a global preference triggered on click, not a form field, and the existing button already carries the app's focus and hover conventions; `role="switch"` on a button is the smaller change with the same semantics.

## Risks / Trade-offs

- **A tile that means something different from its neighbours** (Currently watching under **What I watched**) → the label names the state, not the period's activity, and the spec fixes the air-date scoping explicitly so the two readings can't drift apart in later work. The stat's value is legible in the mode it matters most in — **What aired**, where the rest of the block is also air-date scoped.
- **Two rows can read identically and still be ordered by an unseen difference** — same scored count, same `7.80` — because the tie-break only fires on an exact match at full precision (decision 6) → accepted deliberately: the score is what the ranking ranks on, and a real difference in it is never overruled. The rows that look tied and *are* tied resolve by rules the row does expose (count, then scores, then recency).
- **A uniform `min-height` lands on every ranking on both pages**, including ones nobody complained about → all rankings already share one row style; making the poster-less ones match is the consistent outcome, and the height chosen is the one poster rows already have, so no existing row changes size.
- **Extending the tie-break invalidates existing ordering assertions** in `RecapRankingBuilderTests` and `ProfileServiceFavouriteSeasonsAndYearsTests` that lean on oldest-first → those tests are updated as part of the change, with new cases pinning the histogram comparison and the newest-first fallback so the reversal is locked in rather than incidental.
- **An animated switch can read as decoration** if it overshoots → the slide is a short transition on `transform` only, the reduced-motion query removes it entirely, and the icon (not the animation) is what carries the state.

## Migration Plan

No data migration and no persisted-state change: `bettermal.scoresHidden` keeps its key and meaning, so a user who had scores hidden sees the switch already in the hidden position on first load. The one API change is an added field on `RecapStatsDto`, which older clients ignore.

## Open Questions

None.
