## Why

The Updates feed lives in a full-width row on the Home page, which is the wrong shape for it twice over. It is only reachable by going Home — news about my franchises is app-wide, not a dashboard statistic — and its horizontal row forces every card into one fixed 280×~100px box, so titles clip, headlines clip, and every poster is centre-cropped into a 64px 2:3 slot whatever shape the picture actually is.

The cards also read as two half-sentences: an `Episode count revealed` headline with `12 episodes` on the line beneath it, when the news is one fact and should be one line — `Total episodes: 12`.

The history overlay inherits the same squeeze from a different direction: fixed 78px rows with `overflow: hidden`, single-line ellipsis on the headline and the reason, and a 57×78 `object-fit: cover` thumbnail. For a card carrying a broadcast-slot move (two slots, both spelled out) the text simply does not fit, and the picture is cropped exactly as on Home.

## What Changes

**The feed moves from the Home page into a navbar menu**

- The Home page's Updates section is removed. It is replaced by an **Updates button in the navbar's right group**, available from every page, which opens a dropdown listing the same last-30-days window.
- The dropdown lists cards **vertically, newest at the top**, and scrolls. Its height is set by the **three newest cards as actually rendered** — no fourth card peeking, no card cut mid-height — so the opening view adapts to however tall those three happen to be.
- The **History** control moves to the top of the dropdown, where it stays reachable even when the window is empty. The history overlay it opens is unchanged in purpose and filters.
- The button carries an **unseen indicator**: a dot shown while a recent update is newer than the newest one the user has already been shown. Opening the dropdown clears it.
- **BREAKING (internal API):** `GET /api/dashboard` no longer returns `updates`. The window moves to a new `GET /api/updates/recent`, so the menu is not tied to a Home-page fetch.

**Cards stop cropping pictures and stop clipping text**

- A card's picture is drawn **whole, at its own proportions**, inside a bounded box — never cropped, never letterboxed into a shape that isn't its own. Card width is fixed; card height follows its content, so a portrait poster makes a taller card than a landscape one. This is the rule the `artwork-selection` picker already applies to its options, bounded on both axes here so a panorama cannot crowd the text out of a fixed-width card.
- Layout is picture on the left, and beside it: title, then the news, then the affiliation line that is there today. Only the dropdown's title truncates (one line, with the existing hover tooltip); everything else wraps.

**One fact reads as one line**

- A kind whose news *is* a value states that value in its own line — `Total episodes: 12`, `Premiere: 5 Oct 2026` — instead of a label followed by a separate value line. The current-value lines are still shown for facts no kind on that card already stated, so an announcement still reports the count and date it arrived with.

**The history overlay shows everything each row holds**

- Rows grow to fit: no fixed row height, no single-line ellipsis on the headline or the affiliation, and the whole title. The picture is shown whole there too, by the same rule as the dropdown.

## Capabilities

### New Capabilities

None. The feed, its window, its eligibility rules and its history are all `anime-updates` already; this changes where they are surfaced and how a card is laid out.

### Modified Capabilities

- `anime-updates`: the last-30-days window moves from a Home-page section to a navbar menu (vertical, newest first, three-card opening height, History at its top); the button gains an unseen indicator; an update's presentation gains the whole-picture rule, the fixed-width/variable-height card shape, and the one-fact-one-line phrasing; the history's rows are required to show everything they carry rather than clipping it.
- `main-dashboard`: the "Updates section placement on the main page" requirement is removed, and the section-divider requirement no longer names Updates.
- `navigation-and-search`: the navbar's right group gains the Updates button, between the score toggle and Profile.

## Impact

**Backend**

- `Controllers/UpdatesController.cs` — new `GET /api/updates/recent`.
- `Services/Updates/AnimeUpdateService.cs` — owns the 30-day window constant (moved off the dashboard service); `GetRecentAsync` gains a parameterless overload on `IAnimeUpdateService`.
- `Services/Dashboard/MainDashboardService.cs` + `MainDashboardDto` — drop the `updates` field and the `IAnimeUpdateService` dependency.

**Frontend**

- New `components/Updates/` holding the menu, the shared card, and the update-text helpers; `UpdatesHistoryOverlay` moves in beside them.
- `components/Navbar/Navbar.tsx` + `.css` — the new button and its dropdown anchor.
- `components/UpdatesSection.tsx` / `.css` — deleted; `pages/HomePage.tsx` and `api/types.ts` lose the `updates` field.
- A `localStorage` seen-marker alongside the existing `bettermal.*` keys.

**Not covered**

- No change to what is recorded as an update, to eligibility, or to the history's search and date filters.
- No new backend state for "seen". The marker is a single client-side timestamp, like the app's other per-browser preferences; a multi-device seen state is not something a single-user local app needs.
- The dropdown is a dropdown, not one of the app's overlays: it does not lock the page behind it, matching the search type-ahead rather than the modal family.
