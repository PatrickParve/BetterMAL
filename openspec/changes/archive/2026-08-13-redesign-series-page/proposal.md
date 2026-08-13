## Why

The series page tells me I'm "Caught up" on a franchise whose newest season has eight episodes out that I haven't watched — the badge only checks entries that have *finished* airing, so a season mid-run is invisible to it. The page also can't answer the two questions a franchise view exists to answer while something is airing: how many episodes are actually out, and how many of them I've seen. The **My progress** bar borrows the home page's label, which reads `aired/total` with no watched count anywhere, and the entry rows show my status but never my episode count.

On top of that the page reads as a stack of same-looking bordered boxes: a small poster, four full-width score panels that dominate the top, and a **More** section that is an undifferentiated scroll — a franchise with twenty specials and movies buries everything below it.

## What Changes

**Truthful completion state**
- `Caught up` is earned, not assumed: it requires that every main-line entry that has finished airing is Completed **and** that I have watched every episode a currently-airing main-line entry has broadcast so far.
- When I've completed everything finished but a running season has aired episodes I haven't seen, the header shows a `N behind` badge instead — the state that used to render as a false "Caught up".
- `Completed` is unchanged: nothing left to air, everything main line completed.

**Knowing what's out**
- New per-entry `airedEpisodes` on the series projection (only currently-airing members touch the schedule service, as today), plus `airedTo` for the timeline.
- **My progress** gains an explicit three-number readout — `1043 watched · 1152 aired · 1200 total` — colour-keyed to the bar, replacing the bare `aired/total` label the shared bar carries for the home page. The aired figure is only shown when something is actually airing.
- A currently-airing row states how many episodes are out (`12 of 24 aired`), and any row I've started but not completed states how far I am (`Watching · 5/24`).

**Layout and colour**
- The header poster grows from 140px to roughly 200px wide and the header becomes the page's hero: poster, title, status, badge, years, links, score chips, and progress all in one block.
- One colour language across the page: **blue is the world** (MAL scores, broadcast/aired progress), **purple is me** (my scores, my watched progress). The four score panels become small tinted chips — MAL blue, mine purple — sitting in the hero instead of a full-width row of boxes.
- **More** becomes poster tiles in collapsible per-media-type groups, collapsed by default on a large series, with an expand/collapse-all control and per-group counts — so the extras never push the rest of the page off the screen, and they look different from the numbered main line rather than identical to it.
- The **Score comparison** strip and the **Longest gap** stat line merge into one timeline ribbon: main-line entries placed on a real year axis (so a four-year gap is four years of empty space), each block filled by how much of it I've watched, each carrying a paired MAL/mine score bar. Under the hide-scores toggle the MAL bars are omitted entirely, as the existing strip already requires — a blur can't hide a graphical magnitude.

## Capabilities

### New Capabilities

None — this reworks the existing series page.

### Modified Capabilities

- `series-page`: tightens the personal-completion badge to account for currently-airing entries and adds a behind-count state; requires the progress readout to name watched, aired, and total separately; requires per-entry aired and my-progress figures on rows; replaces the score-comparison strip with a timeline ribbon carrying the longest gap and per-entry scores; restructures the header, score averages, and More section presentation.

## Impact

**Backend**
- `Services/Series/SeriesDto.cs` — `SeriesEntryDto` gains `AiredEpisodes` and `AiredTo`.
- `Services/Series/SeriesService.cs` — per-member aired-episode computation, with `MainLineAiredEpisodes` derived from it rather than computed separately.

**Frontend**
- `pages/SeriesPage.tsx` / `.css` — hero layout, score chips, badge rules, progress readout, collapsible More, timeline ribbon; the score-comparison markup is removed.
- `components/SeriesEntryRow.tsx` / `.css` — aired and watched figures on rows.
- New `components/SeriesExtraTile.tsx` / `.css` — the More tile.
- New `components/SeriesTimeline.tsx` / `.css` — the ribbon.
- `components/AiringProgressBar.tsx` — an opt-in label mode that names watched, aired, and total; the home page's current label stays the default.
- `api/types.ts` — the two new entry fields.

**Docs/specs**
- `openspec/specs/series-page/spec.md` via this change's delta.
- `CODE_GUIDE.md` if the series-page component list is enumerated there.
