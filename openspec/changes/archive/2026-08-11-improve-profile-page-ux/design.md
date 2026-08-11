## Context

Two unrelated bodies of work travel together here because the user reported them together.

**Profile page polish.** `ProfilePage.tsx` / `ProfilePage.css` and `EditHistoryOverlay.tsx` / `.css` have a handful of layout defects: the top-anime strip uses `flex: 1 1 0` so tiles shrink as the list grows, both strips inherit an accidental vertical scroll axis, the stats box is a `1fr` grid column that widens with the window, the distribution bars divide by the total (so no bar ever approaches the track's end), the activity feed stops at a hard `max-height: 320px` inside a stretched box, overlay scrollbars float over the rows, and the history overlay renders `item.animeTitle` raw — ignoring `animeEnglishTitle` and `pictureUrl`, both of which the DTO already carries.

**Cross-page navigation.** Every page follows the same shape: local `useState` for data, a `useEffect` that fetches on mount, and local `useState` for filters. React Router unmounts the page on navigation, so going back re-runs the whole cycle — empty render, network round-trip, filters reset to defaults, scroll at the top. The fetch layer (`api/client.ts`) de-duplicates *in-flight* GETs but deliberately caches nothing, so there is no layer today that can answer a repeat read instantly.

Constraints: React 19 + React Router 7 with `BrowserRouter` + `<Routes>` (not a data router, so `<ScrollRestoration>` is unavailable); no state-management or data-fetching library in `package.json`, and none is being added; `#root` is a flex column and the **document** scrolls, so scroll restoration targets `window`.

## Goals / Non-Goals

**Goals:**

- Back/forward navigation restores a page's data, view-control selections, and scroll position, with a silent background refresh — for every routed page, via shared hooks rather than per-page bespoke code.
- Keep the "fresh visit opens on defaults" behaviour that the existing specs mandate, distinguishing it from a restore.
- Fix the profile page's strip sizing, stats width, bar scaling, feed height, scrollbar placement, and title truncation.
- Widen the latest-updates feed to include completions and score changes, with completions superseding the increment that caused them.

**Non-Goals:**

- No HTTP response cache. Restoration snapshots a page's *rendered state*, not `fetch` responses; `api/client.ts`'s "nothing is cached" contract is untouched.
- No persistence across reloads (no `sessionStorage`/`localStorage`) — an in-memory store only.
- Not restoring horizontal scroll offsets inside the poster strips, nor overlay open/closed state.
- No route-transition animations. "Smooth" here means *no teardown flash*, not a crossfade.
- No new backend endpoint, DTO field, or migration.

## Decisions

### D1 — Snapshot per history entry, keyed by `location.key`, held in a module-level store

A `pageStateStore` module owns `Map<string, PageSnapshot>` where `PageSnapshot = { data: Map<string, unknown>, view: Map<string, unknown>, scrollY: number }`, keyed by React Router's `location.key`.

*Why `location.key` over `pathname`:* the key is unique per history entry, so two entries for the same path (`/my-list` filtered to Watching, then later `/my-list` fresh from the navbar) hold separate snapshots and cannot leak into each other. Keying by pathname would make a fresh navbar visit inherit the old visit's filters, which directly contradicts the "filter resets on revisit" requirements this change is preserving.

*Why a module-level `Map` over React context state:* writes happen on every keystroke-level state change and must not re-render the tree. The store is a plain mutable object; only a thin context carries the current key and restore flag.

Bounded by an insertion-ordered cap (30 entries, oldest evicted). Long browsing sessions accumulate history entries and each snapshot pins a page's worth of DTOs; 30 comfortably covers realistic back-navigation depth.

*Alternative rejected:* keeping every page mounted and toggling visibility. It would restore everything for free but multiplies live effects, timers, and DOM nodes across eight pages, and the Settings page's polling intervals would keep running while hidden.

### D2 — A restore is `navigationType === 'POP'` **and** a snapshot exists

`useNavigationType()` returns `'POP'` for back/forward — but also for the very first render of a session, and it returns `'POP'` for a history entry that was never rendered (e.g. reload then back). Requiring a snapshot under the current `location.key` collapses all those cases to "fresh visit" without extra bookkeeping, which is exactly what the spec asks for.

### D3 — `usePageData` and `useRestorableState`, mirroring the hooks they replace

```ts
// One call per resource a page loads. `key` names the resource within the
// page and must include any route/query parameter the data depends on.
usePageData<T>(key: string, load: () => Promise<T>): {
  data: T | null
  loading: boolean               // true only on a fresh load with nothing restored
  setData: Dispatch<SetStateAction<T | null>>
  reload: () => Promise<void>
}

// Drop-in for useState on any view control that should survive a back nav.
useRestorableState<T>(key: string, initial: T): [T, Dispatch<SetStateAction<T>>]
```

Both write through to the snapshot on every state change, under the location key captured at mount. Writing eagerly — rather than trying to save on unmount or on a `beforeunload`-style hook — avoids the fragile part of this problem entirely: by the time a component unmounts, the location has already changed, so there is no reliable moment at which "the entry I am leaving" is still current.

`setData` keeps pages' existing local-patch paths working (HomePage patching `episodesWatched`, MyListPage patching an edited entry): a patch updates state and the snapshot together, so going back shows the patched value immediately.

*On mount:* if restoring and the snapshot holds this key, seed state from it and fire `load()` in the background without touching `loading`; the result replaces state on success and is discarded on failure. Otherwise set `loading` and load normally.

### D4 — Scroll restoration in one app-level hook, with a bounded retry

`main.tsx` sets `history.scrollRestoration = 'manual'` so the browser stops competing. A `useScrollRestoration()` hook mounted in `AppShell`:

- Records `window.scrollY` into the current snapshot on scroll, throttled with `requestAnimationFrame`. Recording continuously (rather than at navigation time) means the value is already correct whenever an entry is left, by any means.
- On location change: `PUSH`/`REPLACE` → `scrollTo(0, 0)`. `POP` with a snapshot → apply `snapshot.scrollY` in a layout effect; if `document.documentElement.scrollHeight` is still too short for the target, retry on subsequent animation frames until it fits or a ~500 ms budget expires. A restored page renders from its snapshot synchronously, so the first attempt normally succeeds; the retry only covers pages whose height depends on a completing fetch.

### D5 — Pages already using `useSearchParams` need no view-state hook

Season, Airing, and Search keep their view controls in the URL, and the URL is part of the history entry — those selections already survive back/forward. Only React-state view controls need `useRestorableState`: Profile's two media-type filters, My list's status filter / sort / airing-status-first, Top anime's page number, Search's `visibleCount`.

Settings is a special case: its data is polled on intervals and its state is transient action flags, so it takes scroll restoration only (which it gets for free from D4) and no `usePageData`. That satisfies "every routed page participates" — participation for a continuously-polled page means scroll, because there is nothing stale to show.

### D6 — Activity feed: completions are never skipped; increments collapse against the last emitted progress item

`ProfileService.BuildActivityFeed` walks a most-recent-first window. The new rule:

- Accept `Added`, `Completed`, `ScoreChanged`, and `EpisodeIncremented` that pass the existing `IsGenuineIncrease` check. Reject everything else.
- Classify `EpisodeIncremented` and `Completed` as *progress* events. Skip an `EpisodeIncremented` when the last item already emitted is a progress event for the same anime. `Completed` is never skipped.

The completion log row is written after the increment that triggered it, so in most-recent-first order the completion comes first, is emitted, and then swallows the whole run of increments behind it — the feed reads "Completed", not "Episode 12". A completion reached by a status change has no increment to absorb and behaves identically. A later rewatch's increments sit above the old completion and both are shown, which is the honest history.

Consecutive score changes to the same anime are **not** collapsed — no requirement asks for it, and two deliberate re-scorings are two events.

`RecentActivityFetchWindow` stays at 200: accepting more change types can only make the window yield its 20 items sooner.

### D7 — Bars scale to the largest bucket; the percentage carries the exact share

`width = maxCount > 0 ? count / maxCount * 100 : 0`, with the row's trailing text becoming `count (share%)` where `share = count / totalRated * 100`, rounded to a whole number and rendered as `<1%` when a non-zero count would otherwise round to `0%`. With `totalRated === 0` every bar is empty and no share is rendered. The trailing column widens to fit `120 (40%)` and keeps `tabular-nums` so the numbers stay in a column.

Both the score label (`.score-distribution__label`) and the trailing count/share column (`.score-distribution__count`) use a fixed character width (`2ch`, `9ch`) with `white-space: nowrap`, rather than sizing to content. Two bugs came from *not* doing this: a content-sized count column let a row's wider `"NNN (NN%)"` string steal space from its own `flex: 1 1 auto` bar-track, so tracks — and the bars meant to be compared against each other — ended up different widths row to row; and a content-sized 16px label with no `nowrap` let the two-digit `"10"` row wrap its `"0"` onto its own line instead of sitting beside the `"1"` and lining up with the single-digit rows. Fixed widths make every row's track (and label) identical regardless of what text a given row happens to contain.

### D8 — One `TruncatedTitle` component for both truncation shapes

A single component takes the text, a `lines` prop (1 → `white-space: nowrap` + `text-overflow: ellipsis`; 2 → `-webkit-line-clamp`), and renders the span plus, while hovered and only when actually overflowing, a tooltip in a `createPortal` to `document.body`.

- Overflow test on pointer-enter: `scrollWidth > clientWidth` for one line, `scrollHeight > clientHeight` for the clamped case. This is what makes "a title that fits shows no tooltip" testable.
- The tooltip is `position: fixed`, offset below the pointer, `pointer-events: none`, updated on `mousemove`, and clamped to the viewport so a title near the right edge doesn't push the page wide.
- Portalling to `body` keeps the tooltip out of the strips' and feeds' `overflow: hidden`/`auto` containers, where it would otherwise be clipped.
- Every call site drops its `title={…}` attribute, satisfying "no duplicate native tooltip".
- The tooltip's vertical position is floored at `max(pointer.y + offset, elementBottom + offset)`, not just `pointer.y + offset`. A two-line-clamped title is taller than the pointer offset, so anchoring purely to the pointer let the tooltip land on top of the title's own second line whenever the pointer entered near the top of the row — visually reading as a second, redundant box stacked on the first rather than a tooltip clearly below it. Flooring at the element's own bottom edge guarantees the tooltip clears the whole title regardless of where within it the pointer is, while still tracking the pointer for anything shorter than the offset (the common single-line case).

### D9 — Strips: fixed tile basis plus an explicit `overflow-y: hidden`

`.top-anime-strip__item` takes the same basis the rewatched strip already uses — `flex: 0 0 calc((100% - 9 * 10px) / 10)` — so ten tiles span the strip at any width and an eleventh scrolls. The vertical-drift bug is CSS's own rule that `overflow-x: auto` with `overflow-y: visible` computes the latter to `auto`; setting `overflow-y: hidden` explicitly on both strips removes the second axis. The strips' existing 8 px padding still absorbs the 1.08 hover scale, so clipping at the padding-box edge does not cut a scaled tile.

### D10 — A shared scroll-container utility rather than per-list scrollbar CSS

macOS overlay scrollbars float over content and `scrollbar-gutter: stable` alone does not reserve space for them. A `.scroll-y` utility in `index.css` combines `overflow-y: auto`, `scrollbar-gutter: stable`, `scrollbar-width: thin`, and a `::-webkit-scrollbar { width: 8px }` rule — which is what forces WebKit/Blink out of overlay mode so the reserved gutter is actually used. `CurrentlyWatchingCarousel.css` already styles `::-webkit-scrollbar`, so this follows existing precedent. Applied to the activity feed and the history list.

### D11 — Latest-updates list fills its box under a cap

Replace the list's fixed `max-height: 320px` with `flex: 1 1 auto; min-height: 0; max-height: 340px`. `.profile-box` is already a flex column stretched by the grid row, so the list grows to the row's height when a sibling box is taller (no dead space at the bottom) and stops at the cap when its own content would otherwise drive the row taller.

*Correction after implementation:* the cap was first set to 460px on the assumption that a sibling box would usually be the row's tallest member. In practice the rating-distribution box (ten bars, its own tallest natural sibling) tops out around 427px total — a 460px feed cap only made the *feed* the tallest member, and CSS Grid's `align-items: stretch` then stretched the two shorter, fixed-content siblings (stats, rating distribution) to match it, leaving each with roughly 100–200px of dead space at the bottom. 340px is what the feed's own list needs so the whole "Latest updates" box lands at that same ~427px the other two already reach on their own — all three boxes end up at their natural height, with no forced stretch and no dead space in any of them.

### D12 — Stats box narrowed at two levels

The top row becomes `grid-template-columns: clamp(200px, 18%, 260px) minmax(0, 1fr) minmax(0, 1.4fr)`, and `.profile-stats` itself takes `max-width: 300px`. The second constraint is what keeps the box narrow below the 1024 px breakpoint, where the grid collapses to a single full-width column — a `clamp` on the column alone would not survive that collapse.

*Tuned after implementation:* the third column narrowed further to `minmax(0, 1.15fr)` — the "Latest updates" box read as disproportionately wide next to the other two once the stats column had already been narrowed by the first constraint above.

### D13 — Full history compacts a run of episode-watched entries into a range

Unlike the "Latest updates" feed (D6), which keeps only the newest item in a run of same-anime episode increases and drops the rest, the full history is meant to show everything — silently dropping the earlier episodes would lose real history, not just declutter it. `ProfileService.GetActivityHistoryAsync` instead collapses a consecutive run of same-anime genuine `EpisodeIncremented` entries into a single row reporting the range (`"Episodes 4-8"`) rather than one row per episode. A run of exactly one keeps its original `"Episode N"` wording; any other change type, a different anime, or a gap in the log ends the run. This is a read-time transform over `ActivityLog`, like D6 — no new column, no migration.

### D14 — Media-type tab switches keep the previous section on screen until the new one loads

Switching `mediaType` or `rewatchedMediaType` selects a new `usePageData` key (`` `top-anime:${mediaType}` ``), and per D3, `usePageData` clears `data` to `null` on a key change unless the navigation is a `POP` restore under an existing snapshot (D2) — correct for a page navigation, but between tabs on the same page it read as the strip visibly collapsing and reopening on every switch. `ProfilePage` keeps a ref to the last non-null section per box (`topAnimeDisplayRef` / `rewatchedDisplayRef`, updated in-render whenever the hook's data is non-null) and renders that ref's value, falling back to the hook's live value once it resolves. The strip's height — and anything gated on the section's presence, like the top-anime box's "Edit order" control, which is also switched to read the ref rather than the live value — stays stable across the switch instead of flashing empty. This is scoped to `ProfilePage`'s own render logic; it does not change `usePageData` itself, so every other page's key-change behavior (D3) is unaffected.

## Risks / Trade-offs

- **A restored page shows stale data for one round-trip** → Unavoidable and intended; the background refresh corrects it in place, and the alternative (a loading state) is the very thing being fixed. Mutations that regroup content are the visible case: the item appears in its old group briefly.
- **`location.key` is not stable across a reload, so a snapshot can never be reused after one** → Accepted; the spec makes reload-discards-state explicit.
- **Eager snapshot writes on every state change** → Writes are `Map.set` on a plain object with no subscribers, so they cost nothing and cannot re-render. The risk is retaining large DTO graphs; the 30-entry cap bounds it.
- **Scroll-restore retry could fight a user who scrolls during the retry window** → The retry loop aborts as soon as the target is reached or a user scroll event is observed, whichever comes first.
- **`::-webkit-scrollbar` opts out of macOS overlay scrollbars, so the scrollbar is always visible on those two lists** → That is the requested behaviour (a gutter beside the rows) and it is confined to the `.scroll-y` utility, not applied globally.
- **Completions superseding increments changes what an existing feed shows for past history** → The feed is derived from `ActivityLog` on every read, so old rows re-render under the new rule with no migration; a previously-shown "Episode 12" simply becomes "Completed".
- **Eight pages adopt new hooks in one change** → Each page's adoption is mechanical and independent (swap `useState`+`useEffect` for `usePageData`), so a page that misbehaves can be reverted to its current pattern on its own.
