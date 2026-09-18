## Context

The proposal carries the evidence for each of the four leaks, the decisions already settled on 2026-09-18, and what stays out of scope. This document settles four things it deliberately left open: the concrete shape of the `ScoreValue` refactor (part 3 named two candidates and asked for a landing), the reveal-state mechanism shared by parts 3 and 4, how the series box's settled gate composes with its reveal click, and the shape of that box's reveal control.

Current state of the reveal mechanism, which everything here reuses rather than reimplements:

```tsx
// components/ScoreValue.tsx:33-40
const identity = `${pathname}|${hidden}`
const [renderedIdentity, setRenderedIdentity] = useState(identity)
const [revealed, setRevealed] = useState(false)

if (identity !== renderedIdentity) {
  setRenderedIdentity(identity)
  setRevealed(false)
}
```

Constraints this design works under:

- **The mechanism is load-bearing and its reasoning is already recorded.** `ScoreValue.tsx:23-32` explains every part: pathname rather than `location.key`, because a same-page filter writing to the URL must not drop a reveal; `hidden` in the identity, so switching the global toggle off and back on drops reveals; compared during render rather than in a `useEffect`, so a hidden score never paints revealed for one frame. Three spec requirements depend on it — `score-visibility/spec.md:42-49` ("Reveal is not persisted", including the two same-route-different-param scenarios at `:55-61`) and `:67-69`. Any reuse must be the same code, not a lookalike.
- **`ScoreValue` has 17 call sites across 10 files.** Its public API is `{ value, placeholder?, completed? }`. Widening it is a change to every one of them by construction, even when they compile unchanged.
- **The codebase resets derived state during render, not in an effect.** `hooks/usePageData.ts:62-66` and `hooks/useRestorableState.ts:20-23` both compare against a previous value and call a setter synchronously in the render body. `ScoreValue`'s own comment names them as the precedent it follows.
- **Single-page helper components live in the page file.** `DivergenceList` in `ProfilePage.tsx:265`, `MalScoreChip`/`MineScoreChip`/`isGroupCompleted`/`malGroupRevealed` in `SeriesPage.tsx:263-381`. Only genuinely shared pieces go in `components/`.
- **There is no frontend test runner.** `frontend/package.json` has no test script and no test dependency; `frontend/src` holds no test file. Verification is `npm run build`, `npm run lint` and a manual walkthrough — the shape every previous frontend change used.
- **Nothing here may reach the backend.** Both flags the filters need already exist and already mean the right thing: `OpinionDivergenceItemDto.IsCompleted` (`Services/Profile/ProfileService.cs:544`) and `RecapHotTakeDto.MalRevealed` (`Services/Recap/RecapStatsBuilder.cs:89`) are both `Status.IsScoreRevealable()`.

## Goals / Non-Goals

**Goals:**

- Close all four inference routes: list membership (parts 1–2), the direction label (part 2), rank as a score proxy (part 3), and the comparative "best so far" claim (part 4).
- Have rank and the series box reuse the *exact* reveal mechanics `ScoreValue` already has, so the three `score-visibility` requirements above hold for them for free rather than by re-derivation.
- Keep `ScoreValue`'s public API byte-identical, so its 17 call sites need no thought.
- Leave every behaviour with the hide toggle **off** exactly as it is today. Nothing needs hiding once scores are visible, so **all four parts are no-ops in that state** — part 4 included, per the user's call of 2026-09-18 (D5). The toggle is the one switch governing all of them.
- Keep the settled check for part 4 derived, never stored, so stale data cannot hold the box open.

**Non-Goals:**

- Any backend change (see the constraint above).
- Backfilling hot takes past the server's five. Settled in the proposal.
- A general "hideable value" abstraction. Two shapes exist — a two-decimal score and `#rank` — and D1 keeps them as two components over one shared hook rather than one parameterised component.
- Hiding `Popularity`, or touching "My favourite" and "Most rewatched" on the series page.
- Adding a frontend test harness.

## Decisions

### D1. Two components over one shared hook, not one component with a `format` prop

The request named two candidates: **(a)** generalise `ScoreValue` with a `format`/render prop so rank flows through it, or **(b)** extract the hidden/revealed/identity-reset state into a hook both call. **This design takes (b).**

(a) looks smaller until the rank's differences are enumerated, and there are four:

1. **The accessible name.** `aria-label="Reveal score"` (`ScoreValue.tsx:60`) is wrong for rank, so (a) needs a `label` prop too.
2. **The reserved slot.** `.score-value { min-width: 4ch }` (`ScoreValue.css:5`) is sized for a two-decimal MAL average, as `score-visibility/spec.md:125` requires ("sized to the score format the app renders … rather than to each individual value"). `#12` and `#12345` are not that format, so (a) needs the width parameterised as well.
3. **The colour role.** Callers wrap `ScoreValue` in `<span className="score--mal">`; rank is ordinary text and takes no score colour. Not a prop, but it means the component is no longer "a score".
4. **The name.** With `format` and `label` both passed in, `ScoreValue` is a generic revealable value wearing a score's name, which is the kind of drift that makes the next reader check 17 call sites to find out what it actually is.

At three props and a misleading name, (a) has become (b) with worse naming. (b) splits along the real seam instead: **the mechanism is shared, the presentation is not.**

```
hooks/useScoreReveal.ts       ← the identity-reset reveal state (D2)
components/RevealControl.tsx  ← the eye button (D3)
components/ScoreValue.tsx     ← score presentation; API unchanged
pages/AnimeDetailPage.tsx     ← RankValue, a local component (D4)
pages/SeriesPage.tsx          ← the box gate and its reveal control (D5–D7)
```

The cost is two new small files. The benefit is that `ScoreValue`'s 17 call sites are untouched, each piece keeps its own label and slot, and the mechanism has exactly one home.

### D2. `useScoreReveal()` — one hook, no parameter, serving all three callers

The reveal state moves out of `ScoreValue` verbatim, including its comment, which is the record of why it is shaped this way:

```ts
// hooks/useScoreReveal.ts
export function useScoreReveal(): [boolean, () => void] {
  const { hidden } = useScoreVisibility()
  const { pathname } = useLocation()
  const identity = `${pathname}|${hidden}`
  const [renderedIdentity, setRenderedIdentity] = useState(identity)
  const [revealed, setRevealed] = useState(false)

  if (identity !== renderedIdentity) {
    setRenderedIdentity(identity)
    setRevealed(false)
  }

  return [revealed, () => setRevealed(true)]
}
```

**No parameter.** An earlier draft took an `extraIdentity` so the series box could opt out of the `hidden` half of the identity — it needed to, while its gate still held with the toggle off. The user's call of 2026-09-18 made the toggle govern part 4 too (D5), so all three callers now want the identical identity, and a parameter every caller passes the same value to is dead generality. The hook reads `useScoreVisibility()` itself instead:

| Caller | Call | Drops the reveal when |
| --- | --- | --- |
| `ScoreValue` | `useScoreReveal()` | pathname changes, **or** the global toggle flips |
| `RankValue` (detail page) | `useScoreReveal()` | same |
| Series highest-MAL box | `useScoreReveal()` | same |

This is identity-preserving for `ScoreValue`: the body above is character-for-character what `ScoreValue.tsx:33-40` runs today, `hidden` included. **Not a behaviour change, a move.**

`ScoreValue` and `RankValue` also call `useScoreVisibility()` in their own bodies, for `alwaysShowCompletedScores`; the hook calling it again is a second `useContext` on the same provider, which costs nothing and keeps the hook self-contained rather than making every caller thread `hidden` in.

**Independence is free.** Each `useScoreReveal` call is one component instance's own `useState` pair, so the rank's reveal and the score's reveal cannot cascade into one another — the requirement part 3 states, satisfied by construction rather than by a guard.

*Alternative rejected:* a `useHiddenReveal(completed)` that also folds in the `!hidden || revealed || (completed && alwaysShow)` decision. It would suit `ScoreValue` and `RankValue` but not the series box, whose gate is `malGroupRevealed`, not the global toggle — so it would need a second hook or an escape hatch. Keeping the hook to the reveal state alone lets one hook serve all three, with each caller composing its own gate.

### D3. `RevealControl` — the eye button as a shared component

The button (`ScoreValue.tsx:48-64`) moves to `components/RevealControl.tsx` with props `{ onReveal, label }`. Its `preventDefault` + `stopPropagation` (`:53-57`) moves with it, comment included: the control often sits inside a clickable `AnimeCard` link, and swallowing the click is what stops a reveal from navigating. `RankValue` does not sit in a link, but a control that behaves identically everywhere is worth more than a prop to switch it off.

`EyeIcon` moves alongside it. The CSS rule `.score-value__reveal` (`ScoreValue.css:13-31`) moves to `components/RevealControl.css` as `.reveal-control`, unchanged except for the name — the rename is confined to the two files that touch it, since no other file references the old class (checked).

`label` becomes the `aria-label`: `"Reveal score"` from `ScoreValue` (unchanged from today), `"Reveal rank"` from `RankValue`, `"Reveal the series' highest MAL score"` from the series box (D7).

### D4. Rank renders through a local `RankValue`, with a slot sized to its own value

`RankValue` lives in `AnimeDetailPage.tsx` beside the page's other single-use helpers, per the constraint above. Shape:

```tsx
function RankValue({ rank, completed }: { rank: number | null; completed: boolean }) {
  const { hidden, alwaysShowCompletedScores } = useScoreVisibility()
  const [revealed, reveal] = useScoreReveal()

  if (rank == null) return <>—</>
  if (!hidden || revealed || (completed && alwaysShowCompletedScores)) return <>#{rank}</>

  return (
    <span className="anime-detail-page__rank-slot" style={{ minWidth: `${String(rank).length + 1}ch` }}>
      <RevealControl onReveal={reveal} label="Reveal rank" />
    </span>
  )
}
```

Called as `<RankValue rank={detail.rank} completed={isScoreRevealableStatus(detail.entry?.status)} />` — the same `completed` expression the MAL score on the line above already passes (`:616`), so the two lines cannot disagree about the entry's status.

The visibility condition is copied from `ScoreValue.tsx:44` rather than shared, which is the one duplication this design accepts. It is a single boolean expression; hoisting it into the hook is the alternative D2 rejects, and hoisting it into a third helper would cost more indirection than it saves.

**The slot.** `score-visibility/spec.md:123` requires a hidden score to occupy the space its value would occupy, so revealing shifts nothing. Rank is not a MAL score and the rule does not formally bind it, but the same guarantee is cheap here and this page has a specific reason to want it: `.anime-detail-page__score-boxes` is `width: fit-content` with `grid-auto-columns: 1fr` (`AnimeDetailPage.css:220-226`), so the pair of boxes is sized to its widest line and the two boxes share that width (`anime-detail/spec.md:127`). A rank that collapsed to an icon could, in principle, resize both boxes on reveal.

Sizing is per-value (`#` + the value's digits), **not** the fixed slot `ScoreValue` uses, because rank has no fixed format to reserve for: MAL ranks run from `#1` to five digits, so one shared width would either clip or pad. The spec's "rather than to each individual value" reasoning is about keeping a *column* of scores aligned with itself; rank is a single line in one box with no column to align to, so the reasoning does not carry over. `ch` is a good proxy because the box already renders digits in the page's body font; `#` is not a digit and may differ slightly in width, which is accepted — it is one character of slack on a line whose own label (`Rank: `) is fixed.

In practice the boxes will not resize either way: `Popularity: #1234` is longer than `Rank: #123` for any realistic pair of values, because `Popularity: ` is six characters longer than `Rank: `, so the popularity line sets the box width regardless. The slot makes the guarantee unconditional rather than incidental — which is the point, since a rank line that happened to be the longest would otherwise produce a visible reflow on reveal.

### D5. The series box gate is `!hidden || settled || revealed`, with `settled` recomputed every render

```tsx
const { hidden } = useScoreVisibility()
const highestMalSettled = malGroupRevealed(series.mainLine, series)
const [highestMalRevealed, revealHighestMal] = useScoreReveal()
```

and in the box:

```tsx
{!hidden || highestMalSettled || highestMalRevealed ? <TieList … /> : <RevealControl … />}
```

**`!hidden` is the user's call of 2026-09-18**, and it changes what the request asked for, so it is worth stating plainly: with the hide-scores toggle **off**, the stat always renders, settled or not. The settledness gate governs only while scores are hidden.

This is a deliberate loosening of behaviour that exists **today**. The per-entry "Not yet watched" placeholder this replaces is unconditional on the toggle — `series-page/spec.md:818` says "regardless of whether the hide-scores toggle is on or off", and `SeriesPage.css:353-355` records the reasoning: a spoiler concern rather than a score-privacy one. Under the new rule a user with scores shown sees the tied-highest entry of a franchise they are midway through, which they do not today. That is the intended trade: the toggle becomes the single switch governing all four parts of this change, rather than part 4 answering to a rule of its own. Turning scores off is how a user asks for the protection.

`malGroupRevealed(series.mainLine, series)` is the identical call already at `:948`. Two properties matter and both fall out of the shape rather than needing their own code path:

- **`settled` is never stored.** It is a `const` recomputed from `series` on every render. When a metadata refresh surfaces a newly `currently_airing` entry, `malGroupRevealed` returns false on the next render and an auto-shown box hides itself — no effect, no invalidation, no cached flag.
- **The click is never consulted for settledness.** `highestMalRevealed` records only "the user asked to see this on this page view". It cannot make an unsettled series read as settled anywhere else, because nothing else reads it.

**Confirmed 2026-09-18.** The request's "goes back to hidden/button on the next render **regardless of the earlier click**" admitted two readings; the user settled it: **a click holds for as long as the page view does.** So:

- **A click is never undone by data.** Once revealed on this page view, the stat stays revealed — including across a refresh that leaves the series unsettled. The click is an explicit request to see the stat, and content closing under the reader because a background refresh landed would read as a bug.
- **A page view is ended by leaving or reloading, and by nothing else.** `useScoreReveal()` keys on pathname, and the series route is `/series/:animeId` (`AppShell.tsx:62`) with `SeriesPage` using `useParams` alone — no `useSearchParams`, no `navigate(`, so every in-page control (the route pick, the More filters, an in-place row edit) keeps the pathname. The reveal therefore survives all of them and is dropped by exactly three things: navigating away, switching to another series, and a reload.
- **The "regardless of the earlier click" case still holds where it was aimed.** A series that was *settled* showed no button, so no click exists to survive; when it stops being settled, `revealed` is false and the box hides on the next render. That is the request's own test, and it passes.

In short, `settled || revealed` is the whole rule: `settled` is data, recomputed every render and never stored; `revealed` is this page view's answer to the button.

### D6. The per-entry placeholder is removed, not kept as a second gate

`isScoreRevealableStatus(entry.entry?.status)` at `:1158` and the "Not yet watched" placeholder at `:1170` both go, along with `.series-page__tie-list-placeholder` (`SeriesPage.css:353-360`), which nothing else uses (checked). The whole-box gate strictly subsumes the old one for the case it was written for — an unsettled entry is withheld while the series is unsettled, and now the rest of the box is withheld with it, which is the change — so keeping both would leave a "Not yet watched" row inside an already-gated box for no added protection.

One residual case is accepted, and it applies only while scores are hidden (with them shown, D5's `!hidden` renders the whole box anyway): a main-line entry that is `not_yet_aired` while `malGroupRevealed` is true — possible via rule 1 (`mainLineCompletedByMe` counts only `finished_airing` members, `SeriesPage.tsx:264-265`) — would now show its title and link where it previously showed the placeholder. It requires an unaired entry to hold the series' highest MAL score, which means MAL published a community average for something that has not aired. Vanishingly rare, and the failure mode is naming an entry the user already knows exists from the timeline above.

Inside the list, each entry's digit keeps the ordinary per-score rule: `<ScoreValue value={entry.malScore} completed={isScoreRevealableStatus(entry.entry?.status)} />`. The current code passes `completed={revealed}` where `revealed` was the per-entry gate — the same expression by a different name — so this is a rename at the call site, not a behaviour change to the digit.

### D7. The control is the same eye icon every other reveal uses — no text label

The box's reveal control is `<RevealControl>` (D3), the identical eye button a hidden score renders, with `aria-label="Reveal the series' highest MAL score"` as its only text. Not a labelled text button.

The request's draft suggested wording ("Reveal current highest MAL-rated season") and left the exact phrasing open; **the user's call is the icon**, which resolves the question by removing it. It is the better answer on three counts:

- **It is what the app already does.** Every other withheld value in the app renders as this icon alone. A user who has learned that an eye means "there is something here I can reveal" reads the series box without being taught a second convention.
- **It satisfies the placeholder rule rather than sitting beside it.** `score-visibility/spec.md:7` requires a hidden value's placeholder to be "the score's reveal control alone — no stand-in digits, dots, or other characters representing the value". A text button saying "Reveal current highest MAL score" is not a stand-in *for the value*, so it was not a violation — but the icon needs no such argument, and the stat now behaves like every other hidden thing on the page.
- **It removes a wording decision from the change.** The draft's "season" was wrong for a main line holding movies, and any singular noun is wrong for a tie list naming two entries. The icon is count-neutral and media-type-neutral for free.

The `<dt>` directly above it already reads "Highest MAL score", so the box reads as `Highest MAL score → 👁`, exactly as a hidden score reads elsewhere. The `aria-label` carries the full phrasing for anyone not seeing that pairing.

It gets a small wrapper class, `series-page__tie-list-reveal`, for alignment within the `<dd>` — the `.series-page__tie-list-placeholder` rule it replaces. No new button styling: `RevealControl` brings its own.

### D8. Both filters are applied at the render boundary, not in the fetch or the derived data

Part 1 filters inside `DivergenceList`, part 2 inside `renderHotTakes` — in each case at the point the list is rendered, not where the DTO is received or memoised. Two reasons:

- **The hide toggle is live.** `useScoreVisibility()` re-renders every consumer when the switch flips, so a filter at the render boundary follows the toggle without invalidating or refetching anything. A filter applied at fetch time would need the toggle in its dependencies and would not survive a toggle while the page is open.
- **The filtered-out rows are still needed.** Turning the toggle off must show them again immediately, from data already in hand.

Neither filter needs `useMemo`. `DivergenceList` renders at most ten rows at rest (`profile-stats/spec.md:402`) and hot takes at most five, and `.filter()` over arrays that size is far below the cost of the JSX either way.

Both fall back to the *existing* empty state rather than a new "some rows are hidden" message. Stating that rows were withheld would itself be a weak signal about the hidden scores — smaller than the one being closed, but in the same category, and the empty states already read correctly.

## Risks / Trade-offs

- **A recap can say "No hot takes for this period" while the server did send some.** → Accepted and settled on 2026-09-18. The alternative is backfilling from the sixth-most-divergent settled entry, which needs the hide state in the request and makes the server's ranking depend on client UI state. The message is also not false in a useful sense: with hiding on, no hot take is showable.
- **The two divergence lists can look emptier than a user expects**, since neither is capped and a list of Watching entries can vanish entirely with the toggle on. → The toggle is one click away and restores them instantly; the `profile-stats` delta records the behaviour so the next reader is not surprised by it.
- **`ScoreValue` is rewired, and it is the most-used component in this area.** → Its public API, its visibility condition, its identity string and its markup are unchanged; only where the pieces live moves. The manual walkthrough covers a score on my list, the detail page and a series row, and `npm run build` catches any call-site type drift across all 17 sites.
- **The `.score-value__reveal` → `.reveal-control` rename could strand a selector.** → Checked: the class is referenced only in `ScoreValue.css` and `ScoreValue.tsx`. A grep for the old name after the change is a task item.
- **Part 4 trades a narrow unconditional guard for a wide conditional one.** The per-entry "Not yet watched" placeholder (design.md decision 7 of the original series-page change) holds whether or not scores are hidden; its replacement is wider while hiding is on — the whole box, not one row — and absent while hiding is off. → The user's explicit call (D5), and the toggle is one click away. The cost is real and named rather than hidden: a user browsing with scores shown gets no spoiler protection on this stat, where today they get some. Worth revisiting if that turns out to bite in use; it is one `!hidden` to remove.
- **`#` may not be exactly `1ch`, so a hidden rank's slot can be a fraction off.** → One character of slack on a line whose box width is set by the popularity line anyway (D4). The alternative, measuring the rendered text, is far more machinery than the problem.

## Migration Plan

No data migration, no API version, no persisted state. All five `localStorage` keys in play (`bettermal.scoresHidden`, `bettermal.alwaysShowCompletedScores`) keep their meanings and are neither read nor written differently. Rollback is reverting the commit; nothing is written that a previous build would misread.

## Open Questions

None. Both were settled on 2026-09-18:

- ~~D5's interpretation call~~ — a reveal clicked while unsettled **holds for the page view**, including across a data refresh; it is dropped by leaving the page, switching series, or reloading, and by nothing else. See D5.
- ~~D7's button wording~~ — the control is the same eye icon as every other reveal, with no text label, so there is no wording to decide. See D7.
