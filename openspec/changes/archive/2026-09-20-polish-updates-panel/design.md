## Context

Four independent defects in one surface, three of them in the frontend and one spanning both ends.

- **`updateText.ts`** builds every update's news lines for both surfaces. Its `StartDateChanged` branch compares `item.airedFrom` (the anime's *live* premiere date, per the spec's "read from the anime's current cached record") against `item.previousStartDate` (the date recorded on the update row) and prints `Delayed` or `Moved up`. Update 76 in the live database — anime 53913, `PreviousStartDate = 2026-10-08`, `AiredFrom = 2026-10-01` — is a move to an earlier date, and the code correctly took the "not delayed" branch. The wording is what is wrong, not the comparison.
- **Cursors.** The app has no base cursor rule. 41 stylesheets set `cursor: pointer` on their own controls; `.updates-menu__button`, `.updates-menu__history-button` and `.updates-history__date` do not. Every `cursor: default` / `cursor: not-allowed` override in the codebase (22 of them) sits on a `:disabled` selector, and no component uses `aria-disabled` or `role="button"` — so a base rule guarded on `:not(:disabled)` cannot collide with any of them, and the non-followable recap tiles the `list-recaps` spec keeps pointer-free are `<div>`s, not buttons.
- **Two scrolls.** `.modal` is `max-height: 85vh; overflow-y: auto`. Inside it, `useCappedCardHeight` caps `.updates-history__list` to its three rendered rows *or* to `window.innerHeight - listTop - 16`, whichever is smaller. That second bound is measured against the **window**, not against the room the modal has left after its 24px padding, title row and filter row — so on a shorter window the list is allowed to be taller than the space the modal can give it, and both boxes end up scrollable.
- **The reason line.** `AnimeUpdateService.TryDeriveAffiliateReasonAsync` composes `$"{HumanizeRelation(...)} {best.Title}"`, where `best` is a `ResolvedRelationEdge` and `Title` is, for an outgoing edge, the relation row's denormalised MAL title. `ResolvedRelationEdge` carries no English title at all, although `RelationResolver.GetEdgesAsync` already loads the far end's full `AnimeMetadata` row (it reads `PictureUrl`, `MediaType` and `AiredFrom` from it) — the field simply was never added. `anime-detail`'s More overlay solved the same problem in `RelatedAnimeDto` by carrying both titles; the resolved-edge path never got the same treatment, which is why the detail page's Prequel/Sequel tooltips are in romaji too.

## Goals / Non-Goals

**Goals:**

- A premiere move states its direction in words that cannot be read backwards, and states none when it has none.
- One place in the app decides that a clickable control shows the pointer cursor.
- The history overlay has exactly one scroll position, with the list filling the panel.
- An affiliated anime is named by the title the app displays for it, resolved from data already loaded, and the same fix covers every consumer of that resolved edge.

**Non-Goals:**

- **Storing the premiere date a change moved *to*.** `anime-updates` requires a schedule change to report both its values as recorded, but `AnimeUpdate` holds only `PreviousStartDate`; the "to" value shown is the anime's live date. Closing that needs a column, a migration and a backfill rule for existing rows. This change adds only the zero-cost part of it (D1's equal-dates guard) and leaves the column to a change of its own.
- **Removing the 41 per-component `cursor: pointer` rules.** They become redundant, not wrong. Deleting them is a large diff with no behavioural effect and a real chance of catching a selector that was doing something else.
- **The navbar dropdown.** Its three-card opening height, hidden scrollbar and overscroll containment are all untouched, and `useCappedCardHeight` survives for it.
- **What gets recorded, and when.** No detector, recorder, relevance-gate or DTO-shape change beyond the one new field on the resolved edge.

## Decisions

### D1 — "Moved earlier" / "Delayed", decided in `updateText.ts`

The earlier-date verb becomes **`Moved earlier`**; the later-date verb stays **`Delayed`**. Both words name the thing that moved (the premiere, in time) rather than a direction on an axis the reader has to guess the orientation of.

*Alternatives:* `Moved down` (what was asked for) states the direction only if the reader pictures a calendar running downwards — it is as ambiguous as `Moved up`, in the opposite direction, and the pair `Moved down` / `Delayed` is asymmetric on top of that. A symmetric `Moved down` / `Moved up` pair is worse again: it re-uses `Moved up` for exactly the case it currently misdescribes, so anyone reading it with the ordinary idiom in mind gets the wrong answer. `Brought forward` is unambiguous but two words where one does, and reads as scheduling jargon beside `Delayed`.

**Equal-dates guard.** Because the "to" date is live and the "was" date is recorded, the two can coincide (a premiere that moved and moved back, or a correction that landed on the original date). `delayed = to > was` would then print `Moved earlier to 1 Oct · was 1 Oct`. Where the two dates are equal, the line becomes `Premiere moved to <date>` — it reports the move, which did happen, and claims no direction. This is a frontend guard on rendering, not an attempt to fix the stored data (see Non-Goals).

The verb stays in `updateText.ts` rather than moving to the DTO: that module is already the single place both surfaces derive an update's text from, and the comparison needs both a recorded value and a live one, which only the assembled DTO has.

### D2 — One base cursor rule in `index.css`

```css
button:not(:disabled),
[role="button"]:not([aria-disabled="true"]) { cursor: pointer; }
input[type="date"] { cursor: pointer; }
```

Anchors — every `<Link>` in the app — already show the pointer natively, so buttons, `role="button"` elements and date fields are the whole gap.

*Why a base rule over four component rules:* the four reported controls are a symptom of there being no rule at all; patching them leaves the next control to be missed the same way, which is how these three were missed.

*Specificity, checked rather than assumed:* `button:not(:disabled)` is (0,1,1), which outranks a bare `.class { cursor: default }` at (0,1,0). That would matter if any component suppressed the pointer on an **enabled** button — none does. Every suppression is on a `:disabled` selector, at (0,2,0) or more, and `:not(:disabled)` already excludes those elements anyway; the one non-disabled `cursor: default` in the app is on `.top-anime-strip--fits`, a `<div>`. The `[role="button"]` half is future-proofing: the app uses neither `role="button"` nor `aria-disabled` today.

`input[type="date"]` takes the pointer on the whole field rather than only on `::-webkit-calendar-picker-indicator`, because a date field in this app is used by picking, not by typing — and the indicator-only rule would leave most of the control still showing an I-beam, which is the complaint.

### D3 — The history overlay becomes a non-scrolling column; the list is the only scroll region

`.modal` keeps `max-height: 85vh`. The history passes a second class alongside `modal--wide` — a new **`.modal--column`** — that turns the box into `display: flex; flex-direction: column; overflow: hidden`. This is the treatment `.modal--rank` already carries for exactly this reason ("laid out as a column so only its own anime list scrolls"), now expressed as a modifier any overlay can take rather than baked into one overlay's width class. `.modal--rank` itself is left exactly as it is: rewriting a working overlay to compose the two classes buys nothing a user can see and risks the ranking editor's layout, the same call made about the 41 redundant cursor rules.

Inside it:

- `.updates-history` becomes the flex column that fills the box (`min-height: 0`, so a flex child may shrink below its content height — without it the list cannot scroll and the box overflows instead).
- The title row and the filter row stay as they are, at their natural height.
- `.updates-history__list-frame` becomes `flex: 1 1 auto; min-height: 0; display: flex`, and the list inside it `flex: 1 1 auto; min-height: 0`, keeping its existing hidden-scrollbar rules untouched.

Because the box is bounded by `max-height` rather than given a fixed height, a two-row history still shrink-wraps to a short panel, and a long one grows to 85vh and scrolls once. **`useCappedCardHeight` is dropped from this surface** — the three-row cap was what made the list's height independent of the space the panel had, and a height measured against the window is precisely the bug.

*Alternatives:* keeping the cap and only stopping the modal from scrolling still wastes most of a tall panel and keeps a window-measured height inside a box-bounded parent. Giving the panel a fixed `height: 85vh` gives one scroll region but strands a two-row history in an empty panel.

`useSeenTracking` needs the list's DOM node, which it currently gets from `useCappedCardHeight`'s callback ref. The history keeps a node-in-state callback ref of its own for that (the same reason the hook used one: the `<ul>` mounts after the items are loaded, so a plain `useRef` read in an effect sees nothing). Its `computeVisibleArea` walks clipping ancestors, and `.modal` is still clipping (`overflow: hidden` rather than `auto`), so the geometry it measures is unchanged in kind; the comment naming `.modal` as "scrolls at 85vh" is updated to say it clips at 85vh.

The filter row is laid out so search (`flex: 1 1 auto`), the date range and Clear sit on one line at the panel's width, with wrapping allowed only where the window is too narrow to fit them — the spec permits that and requires the list to keep filling what is left either way.

### D4 — `EnglishTitle` on `ResolvedRelationEdge`, filled from data already loaded

`ResolvedRelationEdge` gains `string? EnglishTitle` beside its existing `Title`. Its three construction sites in `RelationResolver` fill it from the far end's cached row — `farAnime?.EnglishTitle` for an outgoing edge, `info.EnglishTitle` for a reverse-derived one, `neighbour.Anime.EnglishTitle` for the series-neighbour fallback — all of which are already in hand. No new query, no new MAL call.

Then:

- `AnimeUpdateService` composes the reason from `best.EnglishTitle ?? best.Title`, in a small shared helper so the choice is expressed once.
- `AnimeUpdateRelevance.FindAffiliateAsync`'s tie-break (`.ThenBy(e => e.Title)`) switches to that same displayed title, so which affiliate gets named matches what is shown — the same displayed-title principle the sorting change in `bound-browse-range-and-sorting` established.
- `ResolvedRelationDto` carries `EnglishTitle` to the detail page, and `AnimeDetailPage`'s Prequel/Sequel tooltips run it through the existing `pickDisplayTitle`.

*Alternatives:* looking the affiliate's metadata up separately inside `AnimeUpdateService` would leave two places deciding what an edge's title is, and would add a query for a row the resolver had already loaded. Sending the reason to the client as structured parts (relation + both titles) so the frontend could compose it would move MAL's relation vocabulary and its humanisation into the client for no gain; `AnimeUpdateDto.Reason` stays one composed string, so nothing on the wire changes shape.

An outgoing edge whose far end has no cached metadata row keeps falling back to the relation row's denormalised title, with no English title — unchanged behaviour, and unreachable for an update's affiliate in any case, since a qualifying affiliate is a list entry of the user's own.

## Risks / Trade-offs

- **[The base cursor rule reaches every button in the app]** → Every existing suppression is on a `:disabled` selector, which the `:not(:disabled)` guard excludes and whose specificity wins regardless; the audit above found no enabled button anywhere that deliberately hides the pointer. A regression would show as a pointer over something unclickable, not as a broken control.
- **[`min-height: 0` is easy to get wrong in nested flex columns]** → Three boxes need it (the modal, `.updates-history`, the list's frame). Missing one shows up immediately as the panel overflowing or the list refusing to scroll, which is exactly what the change is verified against at several window heights.
- **[A taller list with no scrollbar gives no explicit scroll affordance]** → The scrollbar stays hidden on both surfaces, by decision: what marks that there is more to reach is the row at the list's bottom edge being cut partly off, which the panel-filling height produces naturally and which the spec delta states as the affordance. A list whose last row happens to end flush with the panel is the one case that reads as complete when it is not; the odds of a row ending exactly on the boundary are small, and a scrollbar is not wanted.
- **[The direction word can still be wrong for an old update]** → Only through the gap held open in Non-Goals: a premiere that moves twice rewrites the older card's "to" date, so its direction word describes the move as it now looks rather than as it was recorded. The equal-dates guard removes the nonsense case; the general case waits for the stored "to" date.
- **[`ResolvedRelationEdge` is a positional record]** → Adding a parameter breaks every construction site at compile time, which is the desired outcome: three sites in `RelationResolver`, no test constructs it directly. `AnimeUpdateServiceTests` asserts `"Sequel to Original Show"` and will need fixtures that state which title is which.
