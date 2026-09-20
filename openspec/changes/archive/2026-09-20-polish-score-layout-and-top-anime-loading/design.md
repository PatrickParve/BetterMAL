## Context

Three independent pieces of work that happen to converge on the same handful of files.

**The detail box grows on reveal.** `AnimeDetailPage`'s MAL box is three `<p>` lines of running text (`AnimeDetailPage.tsx:633-647`). Two of them hold a hide/reveal-capable figure: the MAL score inside `<ScoreValue>`, and the rank inside `RankValue`'s own `anime-detail-page__rank-slot`. Both hidden branches render an inline-flex box whose only child is the `RevealControl` button — 13px icon plus 2px padding, ~17px tall — while the revealed branch renders text occupying a full line (~19px at the box's 16px font). Both slots carry `vertical-align: middle`, which positions an inline-flex box relative to the baseline rather than letting the line's own strut govern, so the difference between the two heights reaches the line box and the box around it. `ScoreChip` already fixed exactly this for the chip form with `min-height: 1lh` on `.score-chip__value` (`ScoreChip.css:26-38`); `.score-value` itself never got a height floor, only `min-width: 4ch`.

Tracing that, the same bug exists a second time in the opposite direction: `.score-chip--compact .score-chip__value` overrides the font to 11px, where one line (~13px) is *shorter* than the 17px reveal control, so a compact chip shrinks by a few pixels on reveal instead of growing. The floor `ScoreChip` has does not help there, because the control is the taller of the two forms.

**Top anime blocks on MAL.** `TopAnimeService.GetRankingAsync` awaits `EnsureFreshAsync` before it reads a single row (`TopAnimeService.cs:28-52`), so the first visit of a new day holds the HTTP response open for the MAL round-trip even though a complete cached ranking is already in Postgres. The caching itself is sound and needs no change: `TopAnimeRankingEntry` rows are scoped by `RankingType` and `TopAnimeFetchLog` keeps a per-type `LastFetchedAt`, so all seven lists cache and expire independently. The same repo already solved this shape for seasons: `SeasonController` exposes a cache-only `GET` beside a `POST …/refresh`, and `SeasonPage` renders the cache, posts the refresh, and re-reads on a `fetched` outcome (`SeasonPage.tsx:319-340`).

**Score pairs disagree on which side MAL sits.** Six surfaces put MAL first, four put it second. Nothing in the specs ever stated an order, so both readings were locally defensible.

## Goals / Non-Goals

**Goals:**

- The anime detail page's MAL box holds one height across every combination of its two reveal states, and does so from first paint rather than settling into it.
- The Top anime page paints cached rows without waiting on MyAnimeList, and takes up the refreshed rows in place when they land.
- MAL's score leads my own on every surface that shows both, with nothing else about either figure changing.

**Non-Goals:**

- Changing the daily refresh cadence, the per-list clocks, single-flight behaviour, or the rule that an unvisited list is never fetched. Only *when the read answers* changes.
- Warming ranking lists the user has not selected, so a first-ever visit to a category still has nothing to paint. That rule is deliberate and stays.
- Giving Top anime a user-facing refresh control, a "last updated" line, or a scheduled job. The refresh stays visit-triggered and silent, as seasons' is.
- Re-theming, re-sizing, or re-labelling any score. Ordering changes position only.
- Touching the season, year, and search browse cards, which show a MAL score alone.

## Decisions

### D1 — Fix the reveal-height jump at the shared slot, not on the detail page

The detail page could be fixed alone by making its three `<p>` lines flex rows with a one-line floor. Rejected: the same slot is used across the app and the same jump follows it, so a page-local fix leaves the bug in place everywhere else and invites the next page to re-solve it. `ScoreChip` already establishes the precedent of solving this once, on the shared element.

The mechanism is two-sided, because the control is sometimes shorter than the line it replaces and sometimes taller:

- **Floor** — `.score-value` gains `min-height: 1lh`, and `anime-detail-page__rank-slot` the same. Where the control is the shorter form (the detail box's 16px lines, the default 15px chip), the slot now stands at one line whether it holds the control or the value.
- **Ceiling** — in the compact chip density, where 11px text makes one line shorter than the 17px control, the control is constrained to the line rather than the line stretched to the control, so a tile's chip row does not gain height purely from being hidden.

Both are scoped to the reveal control and the score slot. `RevealControl`'s icon size, padding, colour, hover, and focus ring are otherwise untouched, and no page-level CSS is involved.

*Alternative considered:* setting an explicit pixel height on the slot. Rejected — `1lh` tracks whatever font-size the surrounding density sets, which is precisely what varies between the three places this appears.

### D2 — Split the Top anime endpoint the way the season endpoint is split

`GET /api/top-anime?type=` becomes a cache-only read; a new `POST /api/top-anime/refresh?type=` runs the existing gated refresh and reports which of three outcomes it had (`fetched` / `skipped` / `failed`), exactly as `SeasonRefreshResultDto` does. `EnsureFreshAsync`'s body — the `RefreshGate` single-flight, the double-checked `IsFreshAsync`, the swallow-and-log on failure — moves behind the new entry point unchanged; only its caller moves.

*Alternative considered:* keep one endpoint and kick the refresh off as fire-and-forget inside the `GET`. Rejected on two counts. The service takes a request-scoped `AnimeTrackerDbContext`, which is disposed the moment the response completes, so a detached continuation would have to open its own DI scope and resolve a second context — real complexity for no gain. And the client would have no signal that the refresh had finished, so it could only poll or guess when to re-read. The two-call shape hands the client the outcome directly and is already the pattern a reader of this codebase knows.

Both routes reject an unrecognised `type` before touching the cache or MAL, as `GET` does today.

### D3 — The client refreshes a list once per session, on that list's first load

`TopAnimePage` caches each ranking list at module scope and fetches it at most once per app session (`TopAnimePage.tsx:31-53`), because a list's 500 rows are identical whatever page or list the URL names. The refresh rides along with that first load per type: read → render → post refresh → on `fetched`, re-read and replace the cached rows. Re-selecting a list already loaded this session still makes no request at all, so "Fast switching between ranking lists" is untouched and a list is refreshed at most once per session — the daily cadence then bounds it further, server-side.

A refresh whose list the reader has since navigated away from still writes its result into the module cache. The cache is the page's whole source of truth, so discarding the result would only mean re-reading it later; writing it costs nothing and keeps every list consistent.

### D4 — A landing refresh must not undo an optimistic membership edit

`setEntry` patches every cached list when the reader adds or edits an anime from a ranking row, so one add flips that anime to "Edit" in all seven lists (`TopAnimePage.tsx:128-141`). A re-read that lands afterwards would overwrite those patches with whatever the server said, and since `updateEntry` writes to Postgres before the optimistic update, the re-read is usually right — but not if it was already in flight when the edit committed.

The page therefore keeps the entries it has set this session in a module-scope map and re-applies them over any freshly read list before that list replaces the cached one. The map is the same data `setEntry` already computes, so this is a record of what it did rather than new state to keep in sync.

*Alternative considered:* skipping the re-read whenever any edit is pending. Rejected — it makes the refresh silently unreliable in exactly the session where the reader is most active on the page.

### D5 — Ordering is a DOM change plus three alignment rules

For most surfaces, putting MAL first is swapping two sibling elements: `MyListRow`'s two score cells, `TopAnimePage`'s three tiers, `ProfilePage`'s divergence line, `RecapPage`'s hot-take scores. Column widths and alignments are per-cell and travel with their cells, so my list and the recap hot takes need no CSS at all.

Three alignment rules do need attention, all of them consequences of `score-visibility`'s existing rule that a hidden score's control begins or ends where its value would:

- **Top anime ranks 4–10** — the card's score row is `justify-content: space-between`, so the two scores sit at opposite card edges. MAL moves from the trailing edge to the leading one, and `.top-anime-card__scores .score-value` flips from `justify-content: flex-end` to `flex-start` to follow it.
- **Top anime flat rows** — `.top-anime-row__mal-score` keeps `text-align: right` and its `flex-end` reveal alignment. It is no longer the row's last score, but the column stays aligned with itself down the page, which is what the rule is for.
- **Top anime showcase chips** — already start-aligned at the chip's leading edge and unaffected by which chip comes first.

The two `score-visibility` scenarios that named the ranks-4-to-10 card as the app's example of a trailing-aligned control are rewritten: that card now illustrates the leading-edge case, and the flat row takes over as the trailing-aligned example.

### D6 — The profile and recap lines are reworded, not just reordered

`Me 9 · MAL 7.21` becomes `MAL 7.21 · Me 9`. The labels move with their values rather than staying put, since the point of the line is that each label names the figure beside it.

While rewriting the Top anime spec's chip wording, `("My score", "MAL")` is corrected to `("MAL score", "My score")` — the MAL chip was relabelled "MAL score" in an earlier change and the spec sentence was not updated with it.

## Risks / Trade-offs

- **The compact-chip jump is a second instance found by tracing, not one the user reported.** → It is included because the height rule is stated generally and a fix that skipped it would leave the spec untrue. It is a few pixels on a tile grid; if the correction turns out to shrink the eye icon noticeably at 11px, the alternative is to raise the compact chip's line floor instead and accept a slightly taller chip row in both states. Both readings satisfy the requirement; the decision is visual and is settled by looking at it.
- **Reveal-height fixes are not unit-testable.** → Each is confirmed by eye in the running app: reveal the score and the rank on a detail page and check that nothing below the pair moves, then do the same on a series timeline card. Tasks call this out explicitly rather than leaving it to a reviewer to remember.
- **A refresh landing mid-read shifts rows under the reader.** → Accepted deliberately. The rows are a ranking, so a refresh typically moves a handful of entries by a place or two rather than rewriting the page, and the alternative — holding the new data back until the next visit — means the reader can never see a change on the day it happens. Pagination is client-side over the whole 500 rows, so the reader's page number stays valid whatever the refresh returns.
- **Two calls where there was one.** → The read is a plain cache query and the refresh is the same work the read used to do inline, so the page does not do more work overall; it just stops making the reader wait for the second half. A failed refresh is already swallowed and logged, and now cannot affect the read at all.
- **A first-ever visit to a category still waits.** → Unchanged and out of scope. Warming the other six lists would fetch rankings the user may never open, which the spec forbids for good reason.
- **Spec deltas touch four capabilities at once.** → They are genuinely separable concerns; the grouping is one round of polish, not one mechanism. Each delta stands on its own and could be archived independently if the change were split.

## Open Questions

None. The two decisions that were genuinely open — whether a landing refresh updates the view in place, and whether the recap hot takes are in scope — were put to the user and answered: swap in place, and yes.
