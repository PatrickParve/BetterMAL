## Context

The updates feed has one data path and two renderings of it. `MainDashboardService` asks `IAnimeUpdateService.GetRecentAsync(now - 30d)` and hands the result to the Home page inside `MainDashboardDto`; `UpdatesSection` lays those out as a horizontally scrolling row of fixed 280px cards, and its History button opens `UpdatesHistoryOverlay`, which fetches the whole log from `/api/updates/history` and renders it as fixed 78px rows. The two share `buildHeadline`/`formatShortDate` so they can never describe the same update differently — that sharing is worth keeping.

Everything that squeezes the cards comes from the two containers being sized before their content:

- The Home row is horizontal, so a card cannot be taller than its neighbours; its picture is a 64px-wide `aspect-ratio: 2/3` box with `object-fit: cover`, which centre-crops anything that isn't a 2:3 poster. `artwork-selection` already lets a chosen picture be landscape or square, and the picker itself is specified to draw every option whole at its own proportions — the feed is the surface that contradicts that.
- The history rows are `min-height: 78px` with `overflow: hidden`, a single-line ellipsis on both the headline and the affiliation, and a `57 × 78` `object-fit: cover` thumbnail. A broadcast-slot card spells out two slots ("Moved to Saturdays 23:30, was Mondays 22:00") and does not fit in one clipped line. The list's `max-height` is literally `calc(5 * (78px + 2px) + 4 * 6px)` — five rows of a height the rows are assumed to have.

Separately, the card text describes one fact as two: `describeKind` returns `Episode count revealed` for `EpisodeCountReleased`, and the meta line beneath independently prints `12 episodes` from `totalEpisodes`. The two are the same news, split because the headline is built from kinds and the meta line from the anime's current record.

The navbar is the natural home for the feed: it is mounted once in `AppShell` outside `<Routes>`, so it is present on every page and survives navigation, and its right group already holds the app's personal, stateful controls (score toggle, Profile, Settings).

## Goals / Non-Goals

**Goals:**
- The last 30 days of news reachable from anywhere, with a visible signal when something in it is new.
- A card whose shape follows its content: fixed width, height from the picture and the text, nothing cropped and nothing clipped.
- One fact reads as one line, whatever the kind that produced it.
- The menu and the history keep describing an update identically — one card, one text builder, two variants.
- No new backend state: everything the indicator needs is already in the update rows.

**Non-Goals:**
- No change to detection, eligibility, the 30-day window itself, or the history's search and date filters.
- No server-side "seen" state and no new columns. The marker is a per-browser preference, like the app's other `localStorage` toggles.
- No redesign of the history overlay's filters or its modal shell — only its rows.
- No change to what `AnimeUpdateDto` carries. Every field the new layout renders is already on it (including `previousEpisodeDate`, which nothing renders today).

## Decisions

### D1. The menu owns its own fetch; the dashboard stops carrying updates

A new `GET /api/updates/recent` returns the same eligible last-30-days list `MainDashboardService` embeds today. The 30-day constant moves onto `AnimeUpdateService` (it is a rule of `anime-updates`, not of the dashboard) and `IAnimeUpdateService` gains a `GetRecentAsync(ct)` overload that applies it. `MainDashboardDto` loses its `updates` field and the dashboard service loses the dependency.

*Why not keep it on the dashboard payload and share it through a context:* the navbar is mounted on every page and the dashboard is fetched only on Home. The menu would be empty — and its indicator blank — until the user visited the page the feature just moved off.

*Why remove the field rather than leave it unread:* it costs a full eligibility resolution (a relation-edge lookup per distinct anime) on every Home load, and an unread DTO field is exactly the kind of thing that grows a second consumer later.

### D2. "Unseen" is one timestamp in `localStorage`

`bettermal.updatesSeenAt` holds the ISO instant of the newest update the user has been shown. The indicator is on while any update in the loaded recent set has `detectedAt` greater than it. Opening the menu sets it to the newest `detectedAt` in that set — after the in-flight fetch resolves, so opening mid-load cannot mark a set seen that had not arrived yet. With no marker stored, everything recent counts as unseen.

Both sides of the comparison are server-issued `detectedAt` values, so browser clock skew is irrelevant.

*Why a timestamp and not a set of seen ids:* the feed is strictly ordered by detection and never back-fills; one high-water mark answers the question exactly, and cannot grow without bound.

*Why not a column on the server:* it needs a migration, a write endpoint, and a decision about what "seen" means across devices — for a single-user local app whose other per-browser preferences (`bettermal.scoresHidden`, `bettermal.hideNsfw`) already live in `localStorage`.

*Why a dot and not a count:* the ask was to show that something is unseen. A count needs a cap rule ("9+"), goes stale between polls, and says nothing the list itself won't say a click later.

### D3. The menu refetches on mount, on open, and on a visible-tab interval

Mount and open are the two moments the data is about to be looked at. The interval — 10 minutes, matching `MetadataRefreshBackgroundService`, the job that produces most updates — exists only so the indicator is honest in a tab left open all day, and is skipped while `document.visibilityState !== 'visible'`.

*Why poll at all:* an indicator that only appears on reload is not an indicator. The precedent is `ConnectionStatusNotice`, which polls `getHealth()` while unreachable.

*Why not something cheaper, like a "newest detectedAt" endpoint:* it is a second endpoint over the same eligibility computation, and the full response is a few dozen rows at most. If the poll ever shows up in profiling, that is the change to make.

### D4. A card's picture is bounded on both axes and drawn at its own proportions

The media box holds the picture with `width: auto; height: auto`, capped by a maximum height (the portrait band) and a maximum width (a share of the card), with `object-fit: contain` as the pathological-panorama guard. The card's own width is fixed; its height follows its content, with the media box top-aligned and the text column taking the rest (`flex: 1 1 auto; min-width: 0`).

That one rule covers every shape without asking what shape the picture is:

| Picture | Drawn as | What sets the card's height |
|---|---|---|
| 2:3 poster | tall and narrow, height-bound | the picture |
| square | as wide as it is tall | the picture |
| 16:9 | short and wide, width-bound | usually the text |
| extreme panorama | short, width-bound | the text |

*Why bound the width too, when `artwork-selection`'s picker bounds only the height:* the picker lays options out in a free-flowing grid where a wide option can simply take more room. Here the card width is fixed, so an unbounded landscape picture would leave the text column a few characters wide.

*Why not switch layout on the aspect ratio* (landscape → a banner across the top, text beneath): it needs `naturalWidth` after the image loads, so the card visibly re-lays-out when the picture arrives, and it gives one list two different card shapes to scan. The picture stays on the left for every card, as asked.

*Why not a fixed box with `object-fit: contain`:* nothing is cropped, but a landscape picture is then drawn small inside a portrait frame with dead space either side — the "letterboxed inside a box of a different shape" that `artwork-selection` already rejects for the picker.

A card with no `pictureUrl` keeps a 2:3 placeholder at the media box's full height, so it doesn't collapse into a text-only row.

### D5. The dropdown's opening height is measured from the three newest cards, not assumed

The scroll container's `max-height` is computed from the first three rendered cards' heights plus the two gaps between them, through a `ResizeObserver` on the list, and re-applied whenever those heights change. Fewer than three cards means no cap at all. The computed value is additionally clamped to what is left below the button (`window.innerHeight` minus the list's top minus a margin).

*Why measured:* with heights that follow their content there is no constant to multiply. The multiply-a-row-height approach is precisely what the history overlay does today (`calc(5 * (78px + 2px) + 4 * 6px)`) and what forces its rows to be a fixed height in the first place.

*Why a `ResizeObserver` rather than one measurement on open:* pictures are remote and their intrinsic size is unknown until they load, so a measurement taken on the first frame is wrong for every card whose image has not arrived. The observer re-settles the cap as they do.

*Why clamp to the viewport:* three cards of a tall franchise poster each can exceed the window; without the clamp the fourth card is unreachable because the list itself runs off the bottom of the screen.

### D6. One card component, two variants

`UpdateCard` renders from `AnimeUpdateDto` and one shared text builder, so the menu and the history cannot drift apart — the reason `buildHeadline` is shared today. The variants differ in exactly two things: the menu clamps the title to one line (keeping the existing `TruncatedTitle` hover tooltip, which is what makes a clipped title recoverable), and the history shows the title in full plus the detection timestamp.

Everything else — the picture rule, the news lines, the affiliation line — is identical, and in the history nothing is clipped: no fixed row height, no `overflow: hidden`, no single-line ellipsis. The history list keeps an inner scroll area so the filters stay visible above it, sized against the viewport rather than against an assumed row height.

### D7. The news is a list of lines, and a kind that reveals a value states it

The text builder returns one line per kind, in the DTO's kind order:

| Kind | Line |
|---|---|
| `Announced` | `Announced` |
| `EpisodeCountReleased` | `Total episodes: 12` |
| `StartDateReleased` | `Premiere: 5 Oct 2026` |
| `StartDateChanged` | `Delayed to 12 Oct 2026 · was 5 Oct 2026` (or `Moved up to …`) |
| `BroadcastSlotChanged` | `Now Saturdays 23:30 · was Mondays 22:00` |
| `EpisodesMoved` | `Episode 7 moved to 12 Oct 2026 · was 5 Oct 2026` |

Then the current-value lines `anime-updates` requires — `Total episodes: N` and `Premiere: <date>`, each only where known — **except** where a kind above already stated that fact: `EpisodeCountReleased` suppresses the episode line, and `StartDateReleased` or `StartDateChanged` suppresses the premiere line. Then the affiliation line, unchanged.

*Why suppress by kind rather than by comparing the rendered strings:* the two come from different places — the kind lines from what was recorded, the fact lines from the anime's current record — and `anime-updates` deliberately keeps them different after a correction. String comparison would print both whenever they disagreed, which is the one case the suppression exists for.

*Why keep the fact lines at all:* for an announcement they are the substance of the card ("Announced / Total episodes: 12 / Premiere: 5 Oct 2026"), and the spec requires the current count and date be shown where known.

*Why the previous episode date now appears:* `previousEpisodeDate` has been on the DTO since the feed was built and nothing rendered it, while `anime-updates` requires an episodes-moved update to report "the dates it moved from and to". The card had no room; it does now.

*Why dates gain the year:* schedule news is routinely about next year, and `5 Oct` alone does not say which October. The full history is the surface where this matters most, and the two surfaces share the formatter.

### D8. The dropdown is a dropdown, not one of the app's overlays

It is absolutely positioned under the button, right-aligned to the navbar's edge so it cannot overflow the window, at `min(420px, calc(100vw - 24px))` wide. It closes on Escape (returning focus to the button), on a pointer-down outside it (`useClickOutside`, the search type-ahead's own mechanism), on following a card, and on opening the history. Its scroll container sets `overscroll-behavior: contain` so reaching its end does not chain the scroll to the page — the rule `navigation-and-search` already imposes on the app's other scrolling regions.

*Why not the `Modal` shell:* `overlay-behaviour` requires every overlay to lock the page behind it and hold it still. Dimming and freezing the whole app to show three cards is heavier than the interaction deserves, and the closest existing thing — the navbar's own search type-ahead — is a plain dropdown. The history stays a `Modal`, unchanged.

The history overlay is rendered by the menu component rather than by the dropdown element, so closing the dropdown to open it does not unmount it.

### D9. The button sits between the score toggle and Profile

`navigation-and-search` defines the left group as page links and the right group as the search field plus the account/preference controls. The updates button opens a menu rather than navigating, and it carries per-user state, so it belongs in the right group — placed beside Profile and Settings, the other two personal controls, and after the score toggle so the search field keeps its position at the group's left edge.

It is a bell glyph in the same `--navbar-control-h` box the gear uses, with the unseen dot at its top-right corner. `aria-expanded` reflects the dropdown, and the accessible name states whether something is unseen, so the dot is not the only carrier of that fact.

## Risks / Trade-offs

- **Card heights settle as pictures load, so the dropdown's height changes on first open.** → The `ResizeObserver` keeps the cap correct as it settles, and the list is top-anchored, so the cards the reader is looking at do not move — only the bottom edge does. Subsequent opens hit the browser's image cache.

- **Three tall cards can exceed the window.** → The viewport clamp (D5) caps the height; past it the list scrolls, which it already does for the fourth card onwards.

- **The seen marker is per-browser, and clearing site data re-flags everything as unseen.** → Accepted, and identical to what already happens to the hide-scores and NSFW toggles. The failure mode is one extra dot, not lost data.

- **A 10-minute poll now runs on every page rather than a fetch per Home visit.** → One small request against an endpoint the menu already calls, skipped entirely while the tab is hidden. Net traffic is comparable to the dashboard fetch it replaces.

- **Dropping `updates` from `MainDashboardDto` is a breaking response change.** → Frontend and backend ship from this repo together and deploy together; there is no other consumer. The frontend type loses the field in the same change.

- **A panorama picture yields a short, wide image and a text-driven card.** → Accepted: nothing is cropped, and the width cap is what keeps the text beside it readable. This is the trade the picker already makes for the same reason.

- **The menu is only as fresh as its last fetch, so a card can be one poll interval stale.** → The updates it shows are themselves produced by background jobs on a 10-minute-or-slower cadence; a tighter poll would out-run the data.

## Migration Plan

1. Ship backend and frontend together: the new endpoint must exist before the frontend stops reading `dashboard.updates`, and the dashboard's field must go in the same deploy or the Home page carries a payload nothing reads.
2. No data migration. No schema change. The only new persisted state is a `localStorage` key, created on first open.
3. Verify after deploy: the button appears on every page with the dot lit (no marker stored yet), the dropdown opens to exactly three cards, the dot clears, a reload leaves it clear, and the history overlay shows full text and whole pictures.

Rollback is a code revert. The orphaned `localStorage` key is harmless and is re-adopted if the change is re-applied.

## Open Questions

None. The one judgement call — whether the indicator should account for updates older than 30 days — is settled by the window itself: the menu is the last 30 days, so "unseen" means unseen within it, and older news stays reachable through the history exactly as before.
