## Context

The series page has been through `add-series-page` (archived 2026-08-12) and `improve-series-page` (archived 2026-08-13). What's left are correctness bugs in what it *claims*, and a layout that doesn't scale with franchise size.

Current state that shapes this change:

- The `Caught up` badge is driven by `SeriesStatsDto.MainLineCompletedByMe`, which is `finishedAiring.Count > 0 && finishedAiring.All(Completed)` over main-line members — a season mid-run is simply not in that set, so the badge fires while eight aired episodes sit unwatched.
- The page has aired-episode data only in aggregate: `stats.mainLineAiredEpisodes`. Nothing per entry, so neither the badge nor a row can say what's out.
- `AiringProgressBar` is shared with the home page and labels itself `${aired}/${total}` — correct for a home row where the context is "broadcast progress", ambiguous on a stats line labelled **My progress**.
- Scores are recomputed **client-side** after an in-place row edit (`patchSeriesEntry` → `recomputeScores` / `recomputeMyHighestIds`), so anything the header displays that depends on my list must be derivable from the entry list the page already holds, or it goes stale after an edit.
- The page is a `flex column` of `.series-box` panels: header, rebuild row, four score panels, stats `<dl>`, main line, More, score strip. Everything has the same border, radius, and weight.
- `SeriesEntryDto` already carries `airedFrom`, `malScore`, `totalEpisodes`, and the full `UserAnimeEntryDto` (including `episodesWatched`); `AnimeMetadata` additionally has `AiredTo`, which the DTO does not currently project.
- Colour tokens exist in `index.css`: `--accent` (purple, used for my watched fill) and `--status-completed` (blue, used for the aired fill). There is no `--mal` / `--mine` semantic pair.
- `IEpisodeScheduleService.EpisodesAiredAsOfAsync` is a thin reader over stored AniList rows with **no estimation** — it returns null when nothing is stored.

## Goals / Non-Goals

**Goals:**

- The header never claims I'm caught up when episodes I could have watched are already out, and says how far behind I am when I'm not.
- "How many episodes are out?" and "how many have I watched?" are answerable at series level and at entry level, without inference from a bare `x/y`.
- The page has a visual hierarchy: one hero that answers what/where-am-I, then the chronology, then the lists — and a franchise with twenty extras is not twenty screens tall.
- One colour convention: blue is MAL and broadcast, purple is me.

**Non-Goals:**

- No change to series composition, main-line classification, watch order, persistence, build budgets, or the Rebuild loop.
- No change to the score-average maths or the hide-scores reveal precedence — only where those values are rendered and in what colour.
- No new frontend dependency. The timeline is hand-rolled CSS; no charting library.
- No drag/zoom/pan interaction on the timeline, and no persisted per-user collapse state.
- No change to `AiringProgressBar`'s appearance or label on the home page.

## Decisions

### 1. Per-entry aired episodes on the DTO; the badge is derived client-side

`SeriesEntryDto` gains `AiredEpisodes` (`int?`) and `AiredTo` (`DateOnly?`). Aired is resolved per member:

| member airing status | `AiredEpisodes` |
| --- | --- |
| `finished_airing` | `TotalEpisodes` (null when the total is unknown) |
| `currently_airing` | `EpisodesAiredAsOfAsync(a, now)`, clamped to `TotalEpisodes` when that is known; **null** when the schedule reader has nothing |
| `not_yet_aired` | `0` |
| unknown / null status | `null` |

`SeriesStatsDto.MainLineAiredEpisodes` keeps its exact current meaning and value but is now *derived from that same per-member map* rather than computed by a second walk, so the aggregate and the per-entry figures can never disagree. Its known-total-only rule stays: a member with an unknown total contributes 0 to the aggregate even when its own `AiredEpisodes` is known, which is what keeps `aired ≤ total` on the bar.

The badge itself is computed **in the page component** from `series.mainLine`, not on the server. It depends on `episodesWatched` and `status`, both of which an in-place row edit changes — a server-computed badge would go stale exactly as `recomputeScores` exists to prevent. Deriving it from the entry array means the existing `patchSeriesEntry` path fixes the badge for free.

Cost: unchanged. Only `currently_airing` members hit `IEpisodeScheduleService` (typically zero or one per series), the same members that already hit it today.

Rejected: computing the badge server-side (goes stale after an in-place edit, or forces a refetch the page deliberately avoids). Rejected: a dedicated `MyBehindEpisodes` stat (same staleness problem, and it hides the per-entry data that the rows also want).

### 2. Badge precedence, and what "behind" counts

```
completed  := every member finished airing (series.status === 'Finished')
              && ≥1 main-line entry finished airing
              && every finished-airing main-line entry is Completed in my list
behind(e)  := max(0, e.airedEpisodes - (e.entry?.episodesWatched ?? 0))   for currently-airing main-line e
```

1. `completed` → **Completed**
2. else if every finished-airing main-line entry is Completed (≥1 such) and `Σ behind(e) === 0` → **Caught up**
3. else if every finished-airing main-line entry is Completed (≥1 such) and `Σ behind(e) > 0` → **`N` behind**
4. else → nothing

Only *currently-airing* entries contribute to `N`. A finished-airing entry I haven't completed drops to case 4 (no badge) exactly as today — it does **not** become "1000 behind". The badge is about being current with a running franchise; "you haven't watched this series" is not news the header needs to shout.

An entry not in my list counts as 0 watched, so an airing season I've never opened reads `8 behind` rather than being ignored.

**Unknown aired count**: if any currently-airing main-line entry has `airedEpisodes === null`, no badge renders at all. `EpisodesAiredAsOfAsync` does no estimation by design, so the honest options are "say nothing" or "guess"; the page already leans on that reader's null-means-unknown contract elsewhere. The progress readout (decision 3) still shows what is known, so the user isn't left with nothing.

Rejected: counting unwatched episodes across *all* main-line entries (turns into a "you're 900 behind" badge on any long series you're mid-way through — not the state the user asked to surface). Rejected: falling back to `totalEpisodes` when the schedule is unknown (that's the "assume everything aired" guess that produced the original bug in a different form).

### 3. Named progress figures, and an opt-in label mode on the shared bar

`AiringProgressBar` gains one optional prop — a label mode. Default keeps today's `${aired}/${total}` string verbatim, so the home page is untouched; the series page opts into a mode that renders no inline label at all, and the page draws its own readout beneath the bar:

```
●1043 watched   ●1152 aired   of 1200+ total
 purple          blue          muted
```

The aired chip is rendered only while a member of the series is currently airing (when nothing is airing, aired *is* the total and repeating it is noise — the readout collapses to `1043 watched of 1200 total`). The total carries the same `+` lower-bound marker `formatEpisodeTotal` already applies when `hasUnknownEpisodeCounts`.

Rejected: changing the shared bar's label globally (the home page's rows are compact and its context is unambiguous — this is a series-page problem). Rejected: leaving the bar's own label in place and adding a readout below it (two labels for the same bar, one of them the ambiguous one).

### 4. Hero header, and a page-scoped colour language

The header becomes a hero: poster at ~200px wide (up from 140px, fluid via `clamp()` so it doesn't crowd a narrow window), with title, status pill, badge, year span, links, **the four score chips**, and the main-line progress readout all inside it. The score panels shrink from `.series-box` panels (20px value, own border, `flex: 1`) to inline chips sized like the status pill.

Colour is applied through two page-scoped aliases declared on `.series-page`:

```css
--mal:  var(--status-completed);   --mal-bg:  var(--status-completed-bg);   --mal-border:  …
--mine: var(--accent);             --mine-bg: var(--accent-bg);             --mine-border: …
```

Aliases rather than raw tokens: the page then reads `var(--mal)` where it means "MAL", and if the two ever need to diverge from the status/accent tokens it's one edit. Declaring them on `.series-page` rather than `:root` keeps the blast radius to this page — `--status-completed` still means "Completed status" everywhere else, including `SeriesEntryRow`'s left-border stripe.

The known overload: a row whose status is Completed already uses that blue for its stripe, so blue means two things within the page. Accepted — the stripe is a 4px edge on a row, the MAL colour appears on chips and score bars, and they never sit adjacent.

The Rebuild control and the partial/truncated notice stay where they are, below the hero: maintenance affordances shouldn't compete with the summary.

### 5. More: poster tiles in collapsible media-type groups

Extras arrive from the API already sorted by media-type group then aired date (`SeriesService.ProjectAsync`), and `groupExtras` already buckets consecutive runs — that stays. What changes is rendering:

- New `SeriesExtraTile` component: poster (~104px wide), title (2-line clamp), `year · N ep`, a blue MAL score chip, a purple my-score chip, list status as a coloured left border plus a short label, and the same `onEdit` control the row has. Tiles lay out in a `repeat(auto-fill, minmax(…))` grid.
- Each group heading becomes a `<button>` toggling its own open state, showing `Movies (4)` and a caret; `aria-expanded` + `aria-controls` on the button.
- Collapse state: `useState<Record<string, boolean>>` keyed by media-type group, initialised from `series.extras.length > 12` — over twelve extras, everything starts collapsed. One `Expand all` / `Collapse all` button flips them together, its label reflecting whether any group is currently open.
- A collapsed group renders **no tiles** (not `display: none`), so a 200-entry franchise costs nothing to have on the page.

Twelve is the threshold because that is roughly one screen of tiles at typical widths — below it collapsing would be an obstacle, above it the section starts burying the page. State is component-local: it resets on navigation, which is the same treatment every other transient UI state on these pages gets (`usePageData` restores fetched data, not view state).

Rejected: tabs (only one media type visible at a time, and it hides how *much* else exists — the thing the user is trying to see). Rejected: a fixed "show first N, then Show more" (arbitrary cut across group boundaries).

### 6. Timeline ribbon: flexbox proportions, not absolute positioning

The ribbon replaces both the score-comparison strip and the **Longest gap** stat line. Layout is a single flex row of alternating **gap spacers** and **entry blocks**:

- Each main-line entry with a date contributes a block with `flex-grow: max(1, durationDays)` and a `min-width` (~48px), where `durationDays = (airedTo ?? (currently airing ? today : airedFrom)) - airedFrom`.
- Between consecutive entries, a spacer with `flex-grow: gapDays` and `min-width: 8px`.
- Axis labels: first year at the left, last year at the right, with the longest-gap spacer carrying an inline `4y 2m` marker naming the two entries it sits between (each a `Link`).

Flex proportions rather than absolute `left/width` percentages: with absolute positioning, a `min-width` on a short entry makes blocks overlap and needs a collision pass; with flex, min-widths simply steal a little proportion from the gaps and nothing can ever overlap. The trade-off is that the scale is approximate when many entries cluster tightly — acceptable, since the requirement is "a four-year wait is visibly a long wait", not measurement.

Inside each block:
- a purple watched fill (`episodesWatched / totalEpisodes`), with a blue aired underlay (`airedEpisodes / totalEpisodes`) while that entry is airing — the same two-layer treatment as `AiringProgressBar`, so the ribbon reads with the same vocabulary;
- a short label (`#3 · 2019`), with the full title on `title`/`aria-label`, wrapping a `Link` to the detail page;
- beneath, a fixed-height score lane with two thin bars — MAL blue, mine purple — at `score/10` height.

Degradations:
- Hide-scores on → the MAL bars are **not rendered** (not blurred), replaced by the existing note. Carried over verbatim from the strip requirement; a bar height leaks the value a blur would hide.
- Entries with no `airedFrom` → a trailing "no date" cluster after the axis, evenly spaced, so they're visible but not fabricated onto the timeline.
- No main-line entry has a date → the whole ribbon falls back to equal-width blocks in watch order, which is exactly the old strip.
- Container is `overflow-x: auto` with a `min-width` on the row, so a 20-entry main line scrolls inside its own box rather than widening the page.

Rejected: SVG (needs measured widths and a resize observer for what CSS flex does natively). Rejected: keeping the strip *and* adding a timeline (two encodings of the same scores, which is what makes the current page feel like a pile of panels).

### 7. Row additions stay inside `SeriesEntryRow`

The airing (`12 of 24 aired`) and my-progress (`5/24`) figures go into the existing row component — it is used only by `SeriesPage`, so there's no other caller to regress. Aired goes in the meta line (where episode count already lives), my progress goes with the status cell (`Watching · 5/24`), and the tile reuses the same two helpers so a tile and a row never word the same fact differently. The wording is `N of M aired`, never `N/M`, because the ambiguity being fixed is precisely that `x/y` doesn't say which axis it's on.

## Risks / Trade-offs

- **A currently-airing entry with no stored AniList schedule rows now yields no badge at all**, where before it yielded (a wrong) "Caught up" → The progress readout and the row's own figures still render what's known, so the page degrades to "shows less" rather than "says something false". The AniList sync populates those rows for airing shows, which is the case that matters here.
- **Blue carries two meanings on the page** (MAL/broadcast, and the Completed status stripe on rows) → Kept separate by placement: the stripe is a row edge, MAL blue only ever appears on chips and score bars.
- **Timeline scale is approximate** once `min-width` floors kick in on a dense main line → Accepted; the ribbon's job is proportion at a glance, and the exact gap is still stated in words on the marker.
- **A single very long entry dominates the axis** (a 1000+ episode run spanning two decades) → Truthful, not a bug: that *is* the shape of that franchise. The per-entry blocks stay readable because of the min-width floor.
- **Collapse state resets on back/forward** → Consistent with every other transient view state in the app; persisting it would mean a new storage key for something re-derived in one click.
- **The hero grows tall on a small window** (poster + title + pills + links + four chips + readout) → The hero stacks to a single column under the existing 900px breakpoint, with the poster capped by `clamp()`.
- **`AiredTo` is not always populated by MAL** for older or irregular entries → The block falls back to a single-date minimum-width marker, which is also what a movie renders as.

## Migration Plan

Additive DTO fields only — no schema change, no migration, no stored-data change. Backend and frontend ship together; an older frontend against the new backend would simply ignore the two new fields. Rollback is reverting the commit.

## Open Questions

None blocking. Two settled by the user during proposal: the More section is poster tiles in collapsible groups (over row groups or tabs), and the "not caught up" state is a `N behind` badge (over showing nothing).
