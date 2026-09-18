## Why

This change fixes **PF6** from `docs/ISSUE_TRIAGE.md:179` (Phase 8, "Score-visibility consistency"), the last open item before the Phase 9 security pass.

PF6's fixed half landed already: divergence rows pass `completed={item.isCompleted}` so the digit itself follows the "always show completed" rule. What stayed open is that **a MAL score can still be inferred, in whole or in part, while its digit sits behind the hide toggle**. Four places leak it, by three different routes:

- **Membership in a labelled list is itself a score claim.** Being in "They liked it, I didn't" means MAL's score is ≥ 7.5 and mine ≤ 5; "I liked it, they didn't" means mine ≥ 8 and MAL's ≤ 7.5 (`backend/AnimeTracker.Api/Services/Profile/ScoreDivergence.cs:50-63`, specified at `profile-stats/spec.md:965-966`). Recap hot takes state the direction outright as row text — "MAL liked it more" / "I liked it more" (`RecapPage.tsx:649-657`).
- **Rank is the score sorted descending.** `AnimeDetailPage.tsx:619` renders `Rank: #123` as plain text with no gating at all, three lines above a MAL score that follows the full hide/reveal rule (`:616`). It is a closer proxy for the exact digit than any of the other three leaks.
- **A comparative stat is a claim about entries you have not reached.** The series page's "Highest MAL score" box (`SeriesPage.tsx:1145-1178`) names the tied-highest entry the moment the page loads, even while the franchise is unfinished or still airing — so it can bias anticipation for seasons not yet out, and can silently change once an airing season finishes and gathers votes.

The design was worked out interactively on 2026-09-18 and is settled on all four points. This change is **frontend-only**: no backend file, no DTO, no query changes.

**Verified against the code at `54c2e31` on 2026-09-18.** Paths without a prefix are under `frontend/src/`; backend paths start with `backend/AnimeTracker.Api/`. Every line number below was re-checked against the file.

## What Changes

### 1. Profile divergence lists drop unsettled rows while scores are hidden

`DivergenceList` (`pages/ProfilePage.tsx:265-289`) renders every `OpinionDivergenceItemDto` unconditionally. When `useScoreVisibility().hidden` is on, it will instead filter to `item.isCompleted === true` and **drop the rest entirely** — no placeholder row, since a placeholder in a labelled list still asserts membership. With the toggle off, every item renders exactly as today. A list emptied by the filter falls back to the existing "Nothing here yet." (`:266-268`).

`IsCompleted` is already the right flag despite its name: it is set from `e.Status.IsScoreRevealable()` (`Services/Profile/ProfileService.cs:544`), i.e. Completed/Dropped/Rewatching, as its own doc comment states (`Services/Profile/ProfileDto.cs:165-175`). **No backend change.**

### 2. Recap hot takes take the same filter

`renderHotTakes` (`pages/RecapPage.tsx:659-670`) drops any take with `malRevealed === false` while hiding is on — the same status set, set from the same `Status.IsScoreRevealable()` (`Services/Recap/RecapStatsBuilder.cs:89`). An emptied list falls back to the existing "No hot takes for this period." (`:667`).

**Client-side only, and the server's cap stays.** `BuildHotTakes` (`RecapStatsBuilder.cs:74-91`) already ranks by `Math.Abs(Divergence)` and takes the top `HotTakeCount = 5` before the DTO is sent, status-agnostic (`:80`), and the server cannot know the client's hide state — that is `localStorage` (`context/ScoreVisibilityContext.tsx:14`), never sent with the recap request. Filtering client-side therefore leaves **fewer than five, possibly zero**, rather than backfilling from the sixth-most-divergent settled entry. Confirmed acceptable 2026-09-18.

### 3. The anime detail page's rank hides like its MAL score

Rank gets the same rule as the MAL score directly above it — global toggle + "always show completed" + `isScoreRevealableStatus(detail.entry?.status)` — with the placeholder being the reveal control alone, no stand-in digits. Its reveal is **independent per instance**: clicking the score's control does not reveal rank, and vice versa.

`ScoreValue` cannot be reused as-is: it hardcodes `value.toFixed(2)` (`components/ScoreValue.tsx:45`), which does not fit `#123`. The reveal/re-hide mechanics are extracted into a shared hook rather than reimplemented, so rank inherits the exact pathname-identity behaviour — re-hide on navigating to another anime, drop on the global toggle going back on, never persisted across a reload. See design D1/D2 for the shape and the rejected alternative.

**`Popularity` (`:620-623`) is untouched** — it is member-count-based, not derived from the community score.

### 4. The series page's "Highest MAL score" box is gated on series settledness

The per-entry gate (`SeriesPage.tsx:1158`, `isScoreRevealableStatus(entry.entry?.status)` deciding title+link+score vs a "Not yet watched" placeholder at `:1170`) is replaced by **one whole-box gate**, applied only while the hide-scores toggle is on: `malGroupRevealed(series.mainLine, series)` — the identical call already made at `:948` for the main-series MAL average chip, over the identical population, since `highestMalScoreAnimeIds` is computed over the same whole unfiltered main line (`:775-781`).

- **Hide-scores toggle off** → the box always renders, settled or not. The settledness gate governs only while scores are hidden (D5). **This is a deliberate loosening of today's behaviour**, decided by the user on 2026-09-18: the per-entry placeholder being replaced is unconditional on the toggle (`series-page/spec.md:818`, "regardless of whether the hide-scores toggle is on or off"). The trade is that the toggle becomes the single switch governing all four parts of this change, rather than part 4 answering to a rule of its own.
- **Hiding on, settled** → the box renders exactly as today: title, link and score for every tied entry, each digit still following the normal `ScoreValue` rule for its own status.
- **Hiding on, not settled** → the list is replaced by the same eye-icon reveal control a hidden score renders everywhere else in the app (D7), which reveals the list for that page view only. No text label: the `<dt>` above it already reads "Highest MAL score", so the box reads exactly as a hidden score does.
- The reveal is **non-persistent**, like every other reveal in the app: component state, not `localStorage`, dropped on navigating away and back, to another series, or on reload — using the same render-time identity comparison `ScoreValue.tsx:33-40` uses, not a `useEffect`.
- The settled check is **recomputed every render, never cached in state**, so a newly `currently_airing` entry arriving from a metadata refresh re-hides an auto-shown box on the next render with no separate code path.

## Capabilities

### New Capabilities

None. No new surface, endpoint or stat — this changes when four existing ones are rendered.

### Modified Capabilities

The request expected parts 1–3 to need no spec wording. **Re-reading the specs found otherwise**: each part contradicts a specific existing sentence, so all four need a delta. Details and exact sentences in the spec deltas; the conflicts are:

- `profile-stats`: **Opinion divergence lists** (`spec.md:958`) says at `:970` "Neither list SHALL be capped: **every anime that qualifies SHALL be present**, reachable by scrolling its box." Dropping unsettled rows while hiding is on makes that literally false. The delta scopes that sentence to membership/ordering and adds the hidden-state filter, leaving the qualification rules, the ordering and the normalisation population untouched.
- `list-recaps`: **Hot takes** (`spec.md:924`) says at `:936` "When fewer than five included entries qualify, the recap SHALL show those that do; when none qualify … it SHALL say so". After this change a recap can show fewer than qualify, and can say "no hot takes for this period" while entries did qualify. The delta adds the hidden-state filter as a rendering rule distinct from qualification, and amends the two affected scenarios. **Not modified:** "Hot take rows read as columns" (`:974`) and its scenario "A hidden MAL score does not shift the row" (`:986-988`) — still reachable, because a *settled* take with "always show completed" off still renders concealed.
- `anime-detail`: **Single anime detail layout** (`spec.md:6`) says at `:7` the box shows "rank and MAL score (**MAL score** respecting the hide/unhide toggle)", singling out the score as the one that does; and the scenario at `:56-58` says the score's line differs from the rank and popularity lines "only in the colour of its number". The delta extends the toggle to rank, keeps popularity explicitly out, and adds the independent-reveal rule.
- `series-page`: **Series stats** (`spec.md:683`) owns the scenario at `:816-818`, "An unsettled entry's title is withheld from Highest MAL score", which this change removes outright — the gate is no longer per entry, and no longer holds "regardless of whether the hide-scores toggle is on or off". The delta replaces it with the whole-box settled gate, conditional on the toggle, and its eye-icon reveal. The sibling scenarios at `:808-814` (a completed entry not blurred; a dropped entry shown) survive only in their settled form and are amended to say so. **This delta loosens an existing protection as well as tightening others** — see D5.
- `score-visibility`: gains one requirement covering the category this whole change closes — that hiding a score also withholds what would let it be inferred, and that a whole-stat reveal follows the same non-persistence rule as a per-score one. Parts 1–3's placeholder behaviour needs no change to the existing "no stand-in characters" rule (`spec.md:15-17`) — dropping a row is stricter than that rule, not an exception to it.

## Impact

- **Frontend only** (under `frontend/src/`):
  - `hooks/useScoreReveal.ts` — **new**. The reveal-that-lasts-one-page-view mechanic lifted verbatim out of `ScoreValue` (D2), used by all three of `ScoreValue`, the rank line and the series box. No parameter: all three want the identical reset.
  - `components/RevealControl.tsx` + `.css` — **new**. The eye button, lifted verbatim out of `ScoreValue` (D3), so rank and the score share one control rather than two copies.
  - `components/ScoreValue.tsx`, `.css` — rewired onto the hook and the control. **Public API unchanged**, so all 17 `<ScoreValue>` call sites across 10 files are untouched.
  - `pages/ProfilePage.tsx` — the filter in `DivergenceList`.
  - `pages/RecapPage.tsx` — the filter in `renderHotTakes`.
  - `pages/AnimeDetailPage.tsx`, `.css` — the rank line becomes a small local component.
  - `pages/SeriesPage.tsx`, `.css` — the box gate, the reveal button, and the removal of `.series-page__tie-list-placeholder` (used only there, `SeriesPage.css:353-360` / `SeriesPage.tsx:1170`).
- **Backend, API, database: no change.** No DTO gains or loses a field; `IsCompleted` and `MalRevealed` already carry exactly the flag each part needs. No migration.
- **`HotTakeCount = 5` stays as it is** (`RecapStatsBuilder.cs:10`). Deliberate — see part 2.
- **The frontend has no test runner.** `frontend/package.json` has no test script and no test dependency (scripts: `dev`, `build`, `lint`, `preview`), and `frontend/src` holds no `*.test.*` or `*.spec.*` file. The 16 test cases in the request are carried into `tasks.md` item for item as a manual walkthrough, none dropped — the shape every previous frontend change in `openspec/changes/archive/` used. Adding vitest + React Testing Library would be a change of its own and is not proposed here.
- **Verification** is `npm run build` (which is `tsc -b && vite build`) and `npm run lint`, both under nvm's Node 22 since the default `node` is v16, plus the manual walkthrough.
- **Docs:** `docs/ISSUE_TRIAGE.md` (local, gitignored) — PF6's entry at `:179`, the Phase 8 row at `:264-266`, and the two summary rows at `:376` and `:430` move to resolved.
- **Out of scope:**
  - Any change to how `highestMalScoreAnimeIds` / `myHighestScoreAnimeIds` / `mostRewatchedAnimeIds` are computed server-side. This changes only when and how the client renders the first of the three.
  - The residual "which entry is currently tied-highest among what I've watched" fact once the box *is* revealed. That is the stat's purpose.
  - `Popularity` on the anime detail page.
  - Recap's "Top 10" MAL-vs-mine basis toggle, and My List / season / year sort-by-score. Explicit user-initiated actions; PF6 does not address them.
  - The series page's "My favourite" stat — ranked by my own score, not MAL's — and "Most rewatched".
  - Backfilling hot takes past the server's five (part 2).
  - Adding a frontend test harness.
