## Context

Five presentation defects, described in `proposal.md`. Four are pure CSS; one (the hidden-score slot) is a small change to a shared component that every score in the app already routes through; one (broadcast progress on currently-watching cards) needs a single new field on an existing dashboard DTO.

Current state worth knowing before changing anything:

- **`ScoreValue`** (`components/ScoreValue.tsx`) is the only component that renders a MAL score anywhere in the app. Its hidden branch renders a `<span class="score-value__blur">••••</span>` next to a `<button class="score-value__reveal">` holding an inline eye SVG. Because everything funnels through it, "go through the entire project" is one edit plus the CSS that hangs off it. Its callers: `MyListRow`, `SeriesEntryRow`, `SeriesTimeline`, `SeriesExtraTile`, `AnimeDetailPage`, `TopAnimePage`, `SeriesPage` (two places), `ProfilePage` (two places).
- **The blur element carries two colour overrides** that exist only because it defaulted to `var(--text)`: `.score--mal .score-value__blur` in `index.css` and `.score-chip--mal .score-value__blur` in `ScoreChip.css`. Both die with the element.
- **`ProgressBar`** already accepts an optional `aired` prop and draws the blue fill behind the purple one, including the unknown-total half-track rule. The anime detail page uses it. The currently-watching carousel does not pass it. So the frontend side of item 2 is one prop.
- **`CurrentlyWatchingItemDto`** already carries `episodesAired`; it does not carry airing status. `CurrentSeasonItemDto`, built in the same loop of `MainDashboardService`, already derives `FinishedAiring` from `Anime.AiringStatus`.
- **Score badges over posters** (`.top-anime-strip__score`) use a dark tint for legibility plus the role colour for the digits and border. `.rewatched-strip__count` is a copy of that badge with the colour parts left out.

## Goals / Non-Goals

**Goals:**

- A hidden MAL score occupies exactly the slot a shown one does, everywhere, with the eye centred in it — no dots, no reflow on reveal.
- Currently-watching cards show broadcast progress in their existing bar, without growing.
- The detail page's two score boxes, the search series badge, and the profile rewatch badge each stop distorting the layout around them.
- Keep every change inside the shared component or the page CSS that owns the surface — no per-call-site special casing.

**Non-Goals:**

- No change to hide/reveal *semantics*: what is hidden, when it reveals, how the completed-scores setting interacts, and the fact that a hidden value never enters the DOM all stay exactly as they are.
- No change to `ProgressBar`, `AiringProgressBar`, or any other consumer of either.
- No redesign of the strips, the search grid, or the detail layout beyond the sizing named above.
- No new colour tokens for the score roles; the silver rewatch badge is deliberately *not* a role.

## Decisions

### 1. The hidden score reserves the slot by sizing `.score-value` itself, not the hidden branch

Both branches of `ScoreValue` share the `.score-value` class. Putting the reserved width there — `display: inline-flex; justify-content: center; min-width: 4ch` with the tabular figures the class already sets — makes the shown value and the hidden control land in the same box by construction, so they cannot drift apart. `4ch` is the `X.XX` MAL format measured in tabular digit widths; the dot is narrower than a digit, so the slot is marginally wider than the widest score, which is what keeps a column of them straight.

*Alternatives:* min-width on `.score-value--hidden` only — rejected, it makes hidden and shown widths two independent numbers that must be kept equal by hand. A fixed `px` width — rejected, it breaks at the two font sizes chips use (15px default, 11px compact).

*Consequence to watch:* the `placeholder` strings (`—`, `No score`) also get the centred slot. `—` centres nicely; `No score` is wider than `4ch`, so `min-width` leaves it alone.

### 2. The reveal button inherits its colour instead of setting one

With the blur element gone, the button is the only thing left to carry the MAL tone that `score-presentation` requires of a hidden score. Changing `.score-value__reveal`'s `color: var(--text)` to `color: inherit` makes it pick up `.score--mal`'s colour in dense rows and `.score-chip--mal .score-chip__value`'s colour inside chips — which is exactly what the two `.score-value__blur` overrides were doing by hand. Both override rules are deleted rather than retargeted at the button.

### 3. The carousel gates its aired fill on airing status, and the DTO grows one field

`ProgressBar` draws the aired fill whenever `aired` is non-null, so the gate lives at the call site. The detail page's rule — draw it only while MAL says `currently_airing` — is the right one to copy: for a finished show, aired and total are the same number, so an ungated fill would paint every finished card's track solid blue behind the purple and mean nothing. `CurrentlyWatchingItemDto` therefore gains `bool CurrentlyAiring`, set from `e.Anime.AiringStatus == "currently_airing"` in the same loop that already does this for current-season items. The carousel passes `aired={item.currentlyAiring ? item.episodesAired : null}`.

*Alternatives:* infer "airing" from `nextEpisode != null` and add no backend field — rejected, a currently-airing show on a break or with unfetched upcoming episodes has no next-episode row and would silently lose its fill. Send `FinishedAiring` (mirroring `CurrentSeasonItemDto`) instead — rejected, `!finished` also includes not-yet-aired shows, which have broadcast nothing; `currently_airing` is the state the fill actually describes.

### 4. The detail page's score boxes shrink locally, not globally

The boxes are big for two independent reasons: `.anime-detail-page__score-boxes .detail-box { flex: 1 1 0 }` stretches each to half the column, and `.score-chip`'s `min-width: 130px` sets a floor under the chip inside. Both are addressed under the `.anime-detail-page__score-boxes` selector — `flex: 0 0 auto`, reduced box padding, and `min-width: 0` on the chips within — rather than by editing `.score-chip` or `.detail-box` globally. `.detail-box` is also the info and synopsis panels, which should keep their padding, and the default `.score-chip` is shared with the series page's chip row, where the 130px floor keeps the labelled chips even.

### 5. The series badge is sized against the meta line it shares a slot with

`.anime-card__meta` is 12px text, so its line box is ~16px. The badge's pill is currently 11px text with 2px vertical padding and a border — taller than that, which is what makes a series card outgrow its row. Dropping the pill to 10px with no vertical padding and a tight line-height puts its outer height under 16px, so the badge line is bounded by the same line box as an anime card's meta line and the grid row stops growing. The count text beside the pill drops to match. One rule set in `SeriesBadge.css` covers both the results grid and the dropdown row, since both render the same component.

### 6. The rewatch badge is silver by literal value, not by a new token

`.rewatched-strip__count` keeps its dark tint and gains a silver border and a silver-white digit colour, mirroring `.top-anime-strip__score`'s structure with `var(--mine)` swapped for a literal silver. It is deliberately not given a `--rewatch` custom property: the app's tokens name *roles* (MAL, mine, statuses), and inventing a role for one badge would imply a colour language that does not exist. The tint stays because the badge sits on arbitrary poster art in both themes.

## Risks / Trade-offs

- **A hidden score's slot is wider than the eye needs, so tight rows gain a little air** → Accepted deliberately: it is the price of columns that do not move when a score is revealed, which is the point of the change. It is still narrower than today's `•••• 👁`.
- **`min-width: 4ch` on `.score-value` touches every score in the app, including shown ones** → The centred slot is a no-op for anything already at least that wide, and the classes that surround `ScoreValue` (`.score--mal`, `.score-chip__value`) already set tabular figures, so the measurement is stable. Verify visually on the dense surfaces (My List, Top anime) where a shown score sits in a column of its own.
- **Deleting `.score-value__blur` rules could orphan a selector elsewhere** → Grep for `score-value__blur` before deleting; the two known rules are in `index.css` and `ScoreChip.css`.
- **The new DTO field is a payload change consumed by a typed client** → `api/types.ts` and the C# record must be updated together; a missed frontend type just fails the Vite build, which is the desired failure mode.
- **Backend build needs the .NET 10 SDK, which is not installed locally** → Compile via the `sdk:10.0` Docker image, per the project's existing workflow.

## Open Questions

None. The two judgement calls this change turned on — that a hidden score reserves the shown score's width, and that the carousel gets the aired fill inside its existing bar rather than a second bar — were settled with the user before these artifacts were written.
