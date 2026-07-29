## Context

All four problems live in the frontend's shared card/progress primitives, so a handful of small components carry the whole change:

- `AnimeCard.tsx` renders picture + title + `children` **inside** a `<Link>`, with an `actions` slot absolutely positioned top-right outside it. The carousel passes `<ProgressBar>` as `children`, which is exactly why the bar and count navigate on click. `MyListPage` already keeps its progress cell as a sibling of the row link — so the fix is to make the card match the pattern my list already uses.
- `ProgressBar.tsx` renders a static `<span className="progress-bar__label">{watched}/{total ?? '?'}</span>` plus an optional `IncrementButton`. Its three consumers (carousel, my list, detail page) are precisely the three surfaces that need the editable count.
- `.carousel__track` is `overflow-x: auto` with no padding, and `.increment-button:hover` is `transform: scale(1.2)`. On a 20px button that is 2px of growth per side, and the last card's button sits flush against the track's right scroll boundary — hence the clipping. Any hover treatment on the cards themselves would clip the same way, vertically too, since `overflow-x: auto` computes `overflow-y` to `auto`.
- Every increment site routes through `useEpisodeIncrement()` (`CompletionPromptContext`), which hardcodes `episodesWatched + 1` and owns the completion-score prompt. A direct "set to N" edit must not bypass it, or typing the final episode number would silently skip the prompt that the "+" button raises.

`UserAnimeEntryEditService.ApplyEpisodesWatched` already rejects negatives, applies the started-date rule, and auto-completes on reaching the total. Its original ceiling was `TotalEpisodes` alone — a gap surfaced once the editable count shipped: a still-airing show with a known total (e.g. `4/13`, 4 aired) could be set to any value up to 13, not just the 4 actually out yet. Decision 7 closes this using the same `IEpisodeScheduleService.EpisodesAiredAsOf` the current-season and detail DTOs already call.

## Goals / Non-Goals

**Goals:**

- Card navigation limited to picture/title; progress controls inert for navigation.
- No hover state clipped by the carousel's scroll box, in either scroll direction.
- One editable-count control, defined once in `ProgressBar`, live on all three surfaces.
- The editable count's ceiling reflects what's actually aired, not always the eventual total, and is enforced live as it's typed as well as server-side.
- Completion prompt fires identically for "+" and for a typed count.
- One consistent, clearly visible hover language for cards, anime rows, and nav controls, that holds up regardless of a card's own poster artwork.

**Non-Goals:**

- No backend business-rule changes beyond tightening the episodes-watched ceiling to what's actually aired (decision 7) — no other validation, sync, or completion behavior changes.
- No change to `AiringProgressBar` (the current-season aired/total bar) — it is passive display with no controls, and stays inside the card link.
- No editable count in the `EntryEditorOverlay` (it already has a number field) or in browse-only cards (search, season) where no entry may exist.
- No new design tokens; the hover language is built from the existing `--accent-bg` / `--accent-border` / `--shadow` variables.

## Decisions

### 1. New `footer` slot on `AnimeCard`, rendered outside the link

`AnimeCard` gains a `footer?: ReactNode` prop rendered as a sibling after the `<Link>`, joining the existing `children` (inside link) and `actions` (absolute, top-right). The carousel moves its `<ProgressBar>` and next-episode countdown from `children` to `footer`.

*Why:* it directly encodes the spec rule ("interactive progress controls sit outside the link") in the shared component, so every future card gets the distinction for free. *Alternatives:* (a) keep the bar inside and `preventDefault()` on the row — rejected, it leaves a real anchor under the pointer with link affordances (cursor, drag-to-link, middle-click opens a tab) and would need re-doing at each call site; (b) drop the wrapping `<Link>` for an `onClick` on the card — rejected, it loses real anchor semantics (middle-click, open-in-new-tab, keyboard).

Card visuals must not shift: `.anime-card__link` currently owns the flex column and 6px gap. The card root takes over the column layout so the link and footer sit in the same flow with the same gap.

### 2. Carousel track gets inner padding sized to the largest hover overflow

`.carousel__track` gets symmetric padding (≈6px) and its `max-width` grows by that horizontal padding, so five cards still fit exactly (`5 * 160px + 4 * 16px + 2 * padding`). End padding inside a scroll container is honoured by current Chrome/Firefox/Safari, so the padding remains scrollable space at both ends.

*Why:* it fixes the clipping for *any* hover treatment (button scale today, card highlight next) rather than only the current 2px. *Alternatives:* (a) replace `scale(1.2)` on the "+" with an inset ring — rejected, it changes a deliberate interaction and still leaves the card hover to clip; (b) `overflow: visible` on the track — impossible, the row must scroll.

Consequence: the card hover treatment must stay inside that budget, which decision 4 enforces by using shadow/border rather than a transform.

### 3. Generalize the increment context to "set episodes watched"

`CompletionPromptContext` exposes `setEpisodesWatched(target, value)`; `increment(target)` becomes the `target.episodesWatched + 1` case of it. The prompt's existing trigger condition (`previousStatus !== 'Completed' && saved.status === 'Completed'`) is read off the server response, so it already covers a typed count landing on the total, and is untouched.

*Why:* one save path keeps the prompt impossible to forget at a new call site — the property the context was created for. *Alternative:* call `updateEntry` directly from the count field — rejected, it silently skips the prompt.

`IncrementTarget.episodesWatched` stays as the pre-change value (needed for the +1 case and unchanged for callers); the target value is a separate argument.

### 4. Editable count as an inline `<input>` inside `ProgressBar`

`ProgressBar` gains `onSetWatched?: (value: number) => void | Promise<void>` and `max?: number | null` (the total). When `onSetWatched` is given, the `watched` half of the label renders as a `<button>`-styled trigger that swaps to a controlled `<input type="text" inputMode="numeric">` on click/focus; `/{total}` stays static text beside it. Width is sized from the digit count so the row does not jump when the field opens.

Input handling, defined once here:

- **Keystroke filter:** reject anything but digits (`value.replace(/\D/g, '')`) — this makes `-` unenterable, so the no-negatives rule holds by construction rather than by post-validation.
- **Live clamp:** on every keystroke, not only on commit, parse the filtered digits and clamp to `max` when it's non-null, replacing the field's value with the clamped number immediately. A value above the ceiling is never visible in the field even transiently, matching how the digit filter already makes `-` unenterable rather than merely rejected after the fact.
- **Commit** on Enter or blur; **cancel** on Escape (restore stored value, no save). Both close paths go through one `closeEditing(save)` function guarded by an `editingRef` (a ref mirroring the `editing` state, updated synchronously): a second close call for the same edit session — e.g. a blur that fires after Enter/Escape already closed the field — is a no-op rather than a stale flag silently swallowing the *next* legitimate close. (An earlier version used a one-shot "skip the next blur" ref instead; it assumed exactly one redundant blur always follows a programmatic close, and when that assumption didn't hold, the leftover flag ate a real subsequent blur, leaving the field stuck open with a cleared value instead of reverting.)
- **Clamp** again on commit to `[0, max]` when `max != null` (redundant with the live clamp above, kept as a guard); no upper clamp when neither the aired-so-far count nor the total is known.
- **No-op** when the committed value equals the stored count, or when the field is empty/invalid — revert, no request. This applies identically whether the field closes via Enter or via blur (clicking away).
- On failure, revert to the stored value (the parent's `onSaved` never fires, so the prop value is already the source of truth — reverting is just clearing local edit state).
- **Resting position matches `AiringProgressBar`'s static label:** the trigger/input have zero padding and margin at rest (box-sizing pinned to `content-box` so the browser's own `border-box` default for `<button>` can't squeeze the digit into an unreadably thin content area) — the count sits exactly where plain `watched/total` text would, and the click/edit affordance is a hover/focus-only background with no permanent footprint.

*Why `type="text"` + a digit filter over `type="number"`:* number inputs allow `-`, `e`, and `+` in their raw value, arrive as `''` for invalid input across browsers, and add spinners that would crowd a 160px card. *Why inside `ProgressBar` rather than a new component:* all three consumers already render `ProgressBar`, and the label is its own markup — a separate component would have to reach into progress-bar layout to keep the bar/count/plus on one line.

Pending state stays with each page (they already track `pendingIncrementId` / `incrementPending`); `ProgressBar` takes the existing `incrementPending` as the disable signal for both the field and the "+".

### 5. Call sites pass a save handler; the detail page gates on an existing entry

- **Carousel:** `setEpisodesWatched` with the same target it builds for `increment`, same `onSaved`/`onCompleted`.
- **My list:** same, reusing `handleSaved(item.animeId)` — which patches one row in state, so the list is not reloaded or re-sorted (spec requirement).
- **Detail page:** passes `onSetWatched` only when `detail.entry` exists, mirroring how `onIncrement` is already gated; without an entry there is no `previousStatus`/`currentScore` to build a target from, and adding to the list stays the overlay's job.
- All three pass `max={item.episodesAired ?? item.totalEpisodes}` (the detail page: `detail.episodesAired ?? detail.totalEpisodes`) — `episodesAired` already resolves to the total for a finished show (decision 7), so this one expression is the correct ceiling in both the airing and finished case without the call site needing to branch on airing status itself.

### 6. One hover language, applied per-surface

A shared visual recipe — `border-color: var(--accent-border)`, `background: var(--accent-bg)`, and the existing `--shadow` on cards — applied in each surface's own CSS file, matching the codebase's convention of per-component stylesheets with BEM-ish class names and no utility layer.

- Cards: a whole-card background "plate" behind the poster and title, not a ring drawn on the poster itself — see decision 8 for why and how.
- Anime rows (`.my-list-row`, `.top-anime-row`): accent background wash + accent border, matching `.airing-today__row`, which already does exactly this. `.my-list-row` carries a status-coloured 4px left border, so its hover sets the top/right/bottom border colours only and leaves `border-left-color` to the status modifier — otherwise hovering would erase the status stripe.
- Nav controls (`.navbar__link`, `.navbar__toggle`, `.navbar__settings`, `.carousel__arrow`, `.pagination__*`, `.season-page__nav button`, `.airing-page__nav button`): add `background: var(--accent-bg)` + `border-color: var(--accent-border)` to the existing colour-only hovers. `.navbar__link--active` keeps its own background and is ordered after the hover rule so the active page stays distinguishable while hovered.

*Alternative considered:* a global `.hoverable` utility class or a `:where()` blanket rule — rejected as out of step with a codebase where every component owns its stylesheet, and it would be hard to opt out of per surface.

### 7. Episode ceiling is episodes-aired-so-far, sourced from the existing schedule service

`EpisodeScheduleService.EpisodesAiredAsOf(anime, now)` — already used for `CurrentSeasonItemDto` and `AnimeDetailDto` — returns the anime's `TotalEpisodes` once it has finished airing, or an aired-so-far estimate (from a cached AniList schedule, or a weekly-cadence fallback) while still airing, or `null` when neither can be determined. `CurrentlyWatchingItemDto` and `MyListItemDto` gain an `EpisodesAired` field populated the same way (`MainDashboardService` already has `IEpisodeScheduleService` injected; `MyListService` gains it as a new dependency). The frontend's `max` prop becomes `episodesAired ?? totalEpisodes` everywhere, and `ProgressBar`'s "+" button disables at that same `max` (falling back to `total` only when the caller omits `max` entirely) instead of always at `total`, so typing and incrementing share one ceiling.

Server-side, `UserAnimeEntryEditService.ApplyEpisodesWatched` computes the identical ceiling (`scheduleService.EpisodesAiredAsOf(anime, now) ?? anime.TotalEpisodes`) and rejects a request above it, replacing the old total-only check.

*Why reuse `EpisodesAiredAsOf` rather than add new logic:* it already encodes exactly the two cases this needs (finished → total, airing → aired-so-far) for two other DTOs; duplicating that logic for the editable-count DTOs would drift from it over time. *Why enforce server-side too, not just clamp client-side:* the client clamp is UX (immediate feedback, no round-trip for an invalid value), but the server is the source of truth an API client could bypass — matching how the total-only cap was already enforced in both places before this decision.

### 8. Card hover is a whole-card background "plate", not a ring on the poster

`.anime-card` gains an absolutely-positioned `::before`, offset `-6px` on all sides (matching the carousel's existing hover-overflow padding budget from decision 2) with `z-index: -1` — a background/border/shadow wash that sits behind the card's own content and extends slightly beyond the poster and title, rather than a `box-shadow` ring drawn directly on the poster.

*Why:* a ring drawn over the poster only has as much contrast as the poster's own colours give it at that exact pixel — on a bright or busy cover image, a semi-transparent ring can wash out and become hard to notice. A plate rendered in the gutter around the card (against the plain page background, not the poster) and behind the title gives guaranteed, image-independent contrast from the theme's own tokens, and as a side effect highlights the title too, which a poster-only ring never did. *Why `-6px` and not larger:* it reuses the padding budget the carousel track already reserves for hover overflow (decision 2), so no further layout change is needed to keep it unclipped at the carousel's edges. `.anime-card` gets an explicit `z-index: 0` so the `::before`'s `z-index: -1` stays contained within the card's own stacking context instead of a bare `position: relative` letting it escape to compete with unrelated page content. *Superseded alternative:* the first implementation put `box-shadow: 0 0 0 2px var(--accent-border), var(--shadow)` directly on `.anime-card__picture`, matching the original decision 6 wording ("shadow + accent ring on the poster") — replaced once it proved hard to see against bright or busy cover art.

### 9. Profile page rows join the shared hover language; poster strips get their own

The profile page's `.profile-list-row` (used by both the "Latest updates" feed and the two opinion-divergence lists) is structurally the same "row wrapping a link, plus trailing content" shape as `.my-list-row`/`.top-anime-row`, so it takes the identical background-wash-plus-border treatment from decision 6 — no new visual language needed.

The "My top anime" and "Most rewatched" poster strips (`.top-anime-strip__item`/`.rewatched-strip__item`) went through two rounds before landing: first an inset `box-shadow` ring in the translucent `--accent-border` (drawn straight on the poster, so it inherited the exact washed-out-against-busy-art problem decision 8 exists to solve); then an opaque ring plus a dark scrim over the poster (fixed the visibility problem, but was a style the user didn't want on their own posters — a valid call, since darkening someone's cover art is a stronger visual statement than a background wash around a card). The setting settled on is a plain scale-up (`transform: scale(1.08)`), with no border, ring, or background drawn on or around the tile at all — deliberately outside the app's shared "background + border" hover language from decision 6, because a translucent or opaque addition on top of dense poster art reads differently than the same addition on the open gutter around an `AnimeCard`.

*Why this needs its own carve-out rather than reusing decision 6 or 8's language:* both prior approaches painted something on or immediately around the poster; a scale transform changes nothing about the poster's own pixels and needs no colour decision at all, so it sidesteps the "does this read well against any art" problem entirely instead of solving it. *Consequence:* `.top-anime-strip`/`.rewatched-strip` each gain an 8px padding (mirroring the carousel's decision 2) so a scaled-up edge tile doesn't clip against the strip's scroll boundary, and the hovered tile gets `z-index: 1` so it renders above — not behind — the neighbours it grows into.

### 10. Score-distribution bar length is share-of-total, not share-of-max

`ProfilePage.tsx` computed each bar's width as `bucket.count / maxBucketCount` — the tallest bar always fills the track, and every other bar is sized relative to it. This reads as "how does this score compare to my most common score," not "what fraction of my ratings are this score," which is what the box next to it (a raw count) already answers on its own. The fix sums all bucket counts once (`totalRated`) and sizes each bar as `bucket.count / totalRated`, so a bar's width is literally that score's percentage of every anime the user has rated — the same denominator `BuildScoreDistribution` (backend) already uses to fill every one of the 10 buckets, so no backend change was needed.

*Why this wasn't caught initially:* both readings look identical whenever one score's count happens to dominate the rest (a common real-world shape), and only diverge visibly with a flatter distribution — where "relative to the max" quietly overstates every bar's apparent share.

## Risks / Trade-offs

- **Trailing padding in a scroll container is a browser-behaviour dependency** → all three target engines honour it today; the visible symptom of a regression would be the old clipping, not a broken layout. Verified by scrolling the carousel to both ends.
- **A click that opens the count field could still bubble into a parent handler on a future card** → the field stops propagation and calls `preventDefault()`, the same guard `IncrementButton` and `ScoreValue` already use.
- **Clamping is visible immediately, as you type, not a surprise on commit** (typing `99` on a 12-episode show shows `12` before you've even confirmed) → this is a stronger guarantee than "clamps on save," not a weaker one; the value shown is always the value that would be saved.
- **The aired-so-far ceiling for a still-airing show is an estimate, not a guarantee** (`EpisodesAiredAsOf`'s cadence fallback can lag a real-world schedule change, e.g. a delayed episode) → the same estimate already drives the current-season and detail pages' aired counts, so this isn't a new source of drift, and the ceiling only ever *rejects* a too-high value, never forces the stored count down — a user who's genuinely caught up is never blocked from re-entering their current count.
- **Setting the count to the total auto-completes the entry**, which some users may not expect from a typed number → this is existing backend behaviour shared with the "+" button, and the completion prompt now fires here too, so the status change is surfaced rather than silent.
- **Blur-commits can fire on an accidental click-away** → Escape cancels, and the change is reversible by editing the count again; matching Enter-only would strand edits the user believes they made. The close path is now idempotent (decision 4), so this no longer risks a stuck-open field either.
- **Hover styling touches eight CSS files** → each is an additive rule on an existing selector; no shared file is restructured, so blast radius stays visual.
- **The poster-tile scale-up is a deliberate exception to the "one consistent hover language" goal** → a user preference call, not an oversight; the profile page's own list rows still use the shared language (decision 9), so the exception is scoped narrowly to the two poster strips rather than spreading.
- **A `transform: scale()` on hover can visually overlap a neighbouring tile mid-growth** → `z-index: 1` on the hovered tile keeps it drawn on top rather than partially covered, and the strip's added padding keeps the *edge* tiles from clipping against the scroll boundary the same way the carousel's "+" once did.
