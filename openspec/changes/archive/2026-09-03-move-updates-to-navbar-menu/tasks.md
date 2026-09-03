## 1. The recent window gets its own endpoint (design D1)

- [x] 1.1 Move the 30-day window onto `Services/Updates/AnimeUpdateService.cs` as a private constant (it is a rule of `anime-updates`, not of the dashboard) and add `GetRecentAsync(CancellationToken)` to `IAnimeUpdateService` that applies it against `DateTimeOffset.UtcNow`. Keep the existing `GetRecentAsync(since, ct)` overload — it is what lets a test pin the boundary.
- [x] 1.2 Add `GET /api/updates/recent` to `Controllers/UpdatesController.cs`, alongside `history`, with a doc comment saying it backs the navbar menu's window and that eligibility is identical to the history's.
- [x] 1.3 Drop `updates` from `MainDashboardDto`, and from `Services/Dashboard/MainDashboardService.cs` drop the `IAnimeUpdateService` constructor parameter, the `UpdatesWindow` constant, the `GetRecentAsync` call and the now-unused `using`. Confirm nothing else reads the field.
- [x] 1.4 Check `Program.cs`: `IAnimeUpdateService` stays registered (the controller resolves it) — no DI change expected, but verify the dashboard registration does not name it.
- [x] 1.5 Move `AnimeTracker.Api.Tests/Services/Dashboard/MainDashboardServiceUpdatesTests.cs` to `Services/Updates/`, retargeting its two cases at `AnimeUpdateService.GetRecentAsync` directly (a 31-day-old update is excluded from the recent window but still returned by the history; a fresh one is returned by both). Keep the comment naming the requirement it covers, updated to say "the navbar menu's window" rather than "the dashboard payload".
- [x] 1.6 Fix `MainDashboardServiceReopenTests.cs`'s constructor call, and delete `FakeAnimeUpdateService` if nothing else uses it after that.
- [x] 1.7 Run the backend test suite. Building needs the `sdk:10.0` Docker image — the local SDK is 9.0 — and the bind-mount caveat under `~/Documents` applies.

## 2. Shared update text: one fact, one line (design D7)

- [x] 2.1 Create `frontend/src/components/Updates/updateText.ts` holding the formatters and the line builder, replacing `UpdatesSection.tsx`'s exported `buildHeadline`/`formatShortDate` as the shared source both surfaces read.
- [x] 2.2 `formatUpdateDate` renders a date as day, short month **and year** (`5 Oct 2026`) — schedule news is routinely about next year, and both surfaces share the formatter (design D7).
- [x] 2.3 `buildNewsLines(item): string[]` returns one line per kind, in the DTO's kind order: `Announced`; `Total episodes: 12`; `Premiere: 5 Oct 2026`; `Delayed to <new> · was <old>` / `Moved up to <new> · was <old>`; `Now <day>s <time> · was <day>s <time>`; `Episode 7 moved to <new> · was <old>`. Keep today's null-guards — a kind whose fields are missing contributes no line rather than a half-written one.
- [x] 2.4 Render `previousEpisodeDate` on the episodes-moved line. It has been on the DTO since the feed was built and nothing showed it, while `anime-updates` requires an episodes-moved update to report the dates it moved between.
- [x] 2.5 Add `buildFactLines(item): string[]` for the current-value lines (`Total episodes: N`, `Premiere: <date>`, each only where known), suppressing the episode line when `kinds` includes `EpisodeCountReleased` and the premiere line when it includes `StartDateReleased` or `StartDateChanged`. Comment why suppression keys off the **kinds** and not off comparing the rendered strings: the two are read from different places and are specified to differ after a correction, so a string test would print both in exactly the case the suppression exists for.
- [x] 2.6 Comment the module as the one place either surface derives an update's text, the role `buildHeadline` played.

## 3. One card component, two variants (design D4, D6)

- [x] 3.1 Create `frontend/src/components/Updates/UpdateCard.tsx`: takes an `AnimeUpdateDto`, a `variant` of `'menu' | 'history'`, and an `onNavigate` callback; renders the whole card as a `Link` to `/anime/{animeId}`, with the picture on the left and title → news lines → fact lines → reason stacked beside it.
- [x] 3.2 `variant="menu"` clamps the title with `TruncatedTitle lines={1}` (its hover tooltip is what makes a clipped title recoverable); `variant="history"` renders the title in full and appends the detection timestamp via `formatTimestamp`.
- [x] 3.3 `UpdateCard.css`: card is `display: flex`, fixed width from its container, `align-items: flex-start`; the text column is `flex: 1 1 auto; min-width: 0` and every line wraps (no `white-space: nowrap`, no ellipsis outside the menu title).
- [x] 3.4 The picture: `width: auto; height: auto; max-height: var(--update-card-media-h); max-width: <share of the card>; object-fit: contain`. Comment the rule with the table from design D4 — portrait is height-bound, landscape width-bound — and why both axes are bounded here when the `artwork-selection` picker bounds only the height (a fixed-width card, so an unbounded landscape starves the text column).
- [x] 3.5 The no-picture placeholder keeps a 2:3 box at the media box's full height, so a card without artwork does not collapse to a text row.
- [x] 3.6 Keep the app's card hover/focus treatment (surface + border, per `navigation-and-search`), and `:focus-visible` outline as `UpdatesSection` has today.

## 4. The navbar menu (design D2, D3, D5, D8, D9)

- [x] 4.1 `frontend/src/components/Updates/useRecentUpdates.ts`: fetches `getRecentUpdates()` on mount, exposes `items` + `refresh()`, and re-fetches on a 10-minute interval skipped while `document.visibilityState !== 'visible'`. Comment the cadence choice (it matches `MetadataRefreshBackgroundService`, the job that produces most updates) and name `ConnectionStatusNotice` as the polling precedent.
- [x] 4.2 Add `getRecentUpdates()` to `api/client.ts` beside `getUpdatesHistory()`, and drop `updates` from `MainDashboardDto` in `api/types.ts`.
- [x] 4.3 `frontend/src/components/Updates/updatesSeen.ts`: read/write `bettermal.updatesSeenAt` (an ISO instant) via `localStorage`, matching the `bettermal.*` keys `ScoreVisibilityContext` and `ContentFilterContext` use; expose `hasUnseen(items, marker)` and `markSeen(items)`. No marker stored means everything is unseen.
- [x] 4.4 `UpdatesMenu.tsx`: the navbar button (bell glyph, `--navbar-control-h` box like the gear), the unseen dot at its top-right, `aria-expanded`, and an accessible name that states whether something is unseen so the dot is not the only carrier of that fact.
- [x] 4.5 The dropdown: a header row holding the **History** button (always present, including when the list is empty), then the card list, then the empty-state text in place of the list when the window is empty.
- [x] 4.6 Mark seen on open — after the in-flight fetch resolves, so opening mid-load cannot mark a set seen that had not arrived (design D2).
- [x] 4.7 `useCappedCardHeight` (renamed from the original `useFiveCardHeight` when the cap changed to three): a `ResizeObserver` over the list computes the first three cards' heights plus the two gaps and applies it as the scroll container's `max-height`, re-applying as heights change; fewer than three cards means no cap; the value is clamped to `window.innerHeight` minus the list's top minus a margin. Comment why it is measured rather than multiplied (design D5) and why the observer is needed (remote pictures have no known size until they load).
- [x] 4.8 Close on Escape (returning focus to the button), on pointer-down outside via `useClickOutside` (the search type-ahead's own mechanism), on following a card, and on opening the history.
- [x] 4.9 Render `UpdatesHistoryOverlay` from the menu component rather than from inside the dropdown element, so closing the dropdown to open it does not unmount it.
- [x] 4.10 `UpdatesMenu.css`: `position: absolute` under the button, right-aligned to the navbar edge, `width: min(420px, calc(100vw - 24px))`, `overflow-y: auto` with `overscroll-behavior: contain` on the scroll container, and a `z-index` above page content but below `.modal-backdrop`'s 100. Confirm no navbar ancestor clips it (`overflow`) or creates a containing block for the modal's `position: fixed` (`transform`/`filter`).

## 5. Navbar wiring (design D9)

- [x] 5.1 Mount `UpdatesMenu` in `Navbar.tsx`'s `navbar__controls`, between the score switch and the Profile link, so the right group reads search field, score toggle, Updates, Profile, Settings.
- [x] 5.2 Check the `max-width: 900px` wrap rule still gives the search field its own row and keeps the four controls together as one unit, and that the dropdown stays within the window when the group has wrapped.

## 6. The history overlay stops clipping (design D6)

- [x] 6.1 Move `UpdatesHistoryOverlay.tsx`/`.css` into `components/Updates/` and render each row through `UpdateCard variant="history"`, dropping its own picture/title/detail/reason markup.
- [x] 6.2 Remove from `UpdatesHistoryOverlay.css`: `--history-row-h`, the row `min-height`, `overflow: hidden`, and the `white-space: nowrap` / `text-overflow: ellipsis` on `__detail-text` and `__reason`. Rows size to their own content.
- [x] 6.3 Replace the list's `max-height: calc(5 * (var(--history-row-h) + 2px) + 4 * 6px)` with a viewport-relative bound, so the filters stay in view above a scrolling list without assuming a row height.
- [x] 6.4 Keep the search, the date range, the Clear control, the three distinct empty states, and the `Modal` shell exactly as they are.
- [x] 6.5 Verify the overlay behaves the same opened from the navbar as it did from the Home page: backdrop covers the viewport, `useScrollLock` still holds the page, Escape and backdrop-click still close, and navigating from a row closes it.

## 7. Retire the Home page section

- [x] 7.1 Delete `components/UpdatesSection.tsx` and `UpdatesSection.css`; remove the `<UpdatesSection>` element and its import from `pages/HomePage.tsx`.
- [x] 7.2 Confirm no leftover references to `updates-section`, `buildHeadline`, `formatShortDate` or `dashboard.updates` anywhere in `frontend/src`.
- [x] 7.3 Check `HomePage.css` and `index.css` for rules that existed only for the section, and confirm the remaining sections' spacing reads correctly with it gone.
- [x] 7.4 `npm run lint` and `npm run build` (`nvm use 22` — the default Node is v16).

## 8. Docs and verification

- [x] 8.1 Update `CODE_GUIDE.md`: the endpoint table (§2) gains `GET /api/updates/recent` and loses `updates` from the `/api/dashboard` row; §3's dashboard/updates notes drop "the dashboard's 30-day window"; §4's component list replaces the `UpdatesSection` entry with the `Updates/` folder (menu, card, text helpers, history overlay) and notes the new `bettermal.updatesSeenAt` key beside the other `localStorage` preferences; the `HomePage` row in the pages table drops the fourth section.
- [x] 8.2 Verify in the running app: the button appears on every page; with no marker stored the dot is lit; opening shows exactly three cards with no fourth peeking; the dot clears and stays clear across a reload; the dropdown scrolls without moving the page; Escape, outside-click and following a card all close it.
- [x] 8.3 Verify the card layout against a portrait poster, a square picture and a landscape picture (use `artwork-selection`'s picker to set one), confirming nothing is cropped, card widths match, and heights differ.
- [x] 8.4 Verify a broadcast-slot-change card and an episodes-moved card read as one line each and show both ends of the move, in the dropdown and in the history.
- [x] 8.5 Verify the Home page renders correctly with the section gone and `/api/dashboard` no longer carrying `updates`.
- [ ] 8.6 Update `openspec/specs/` via the archive step (`/opsx:archive`), not by hand.
