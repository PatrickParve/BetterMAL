## Context

The updates feed (`anime-updates`) records `AnimeUpdate` rows through one write path, `AnimeUpdateRecorder.RecordAsync`. It reads them through one read path, `AnimeUpdateService`, which applies the eligibility filter to both the 30-day window (`GetRecentAsync`) and the full history (`GetHistoryAsync`).

"Seen" is a browser-side high-water mark today:
- `updatesSeen.ts` stores the newest `detectedAt` shown, under `bettermal.updatesSeenAt`.
- `UpdatesMenu.handleToggle` advances that mark over the whole window as soon as the dropdown's refresh lands.

**The pieces this change touches:**
- `UpdatesMenu` owns:
  - the bell and the open state
  - `useRecentUpdates`, which fetches on mount and on a visible-tab 10-minute poll
  - `useCappedCardHeight`, which measures the three newest `<li>` children through a callback ref and re-measures with a `ResizeObserver`
  - the dropdown markup
  - the History overlay's mount
- The dropdown's `<ul>` scrolls with its scrollbar hidden, `overscroll-behavior: contain`, and an inline `max-height` equal to the three newest cards plus two 8px gaps.
- `UpdatesHistoryOverlay` mounts on each open and fetches `getUpdatesHistory()` once. Its `<ul>` is sized by the same hook, inside `.updates-history__list-frame`, inside `.modal`. `.modal` is itself `overflow-y: auto` at `max-height: 85vh`.
- `UpdateCard` is one `<Link>`. The `menu` variant truncates the title through `TruncatedTitle` (`min-width: 0`, `overflow: hidden`, `nowrap`, ellipsis, tooltip shown only when `scrollWidth > clientWidth`). The `history` variant renders the title in a wrapping flex row with its timestamp.
- `client.ts`'s `fetchRaw` joins identical in-flight GETs and never collapses mutations. `performFetch` reports reachability for every call.

**Constraints:**
- Backend tests use the EF in-memory provider, which does not support `ExecuteUpdateAsync`.
- The backend compiles and tests only in the `mcr.microsoft.com/dotnet/sdk:10.0` image, from a copy under `/private/tmp`.
- The frontend has no test runner, builds with nvm's Node 22, and is verified by hand in the running app.
- `Program.cs:221` runs `db.Database.Migrate()` at startup, before the background services that record updates begin.

**Data today:** 8 update rows, all eligible and all inside the 30-day window.

## Goals / Non-Goals

**Goals:**
- One seen flag per update, on the server, read identically by every browser on this device.
- Seen only once a card has been looked at, in either surface.
- New cards marked in both surfaces, with that marking fixed for the length of a look.
- The bell derived from the flags, catching up on focus and visibility.
- Existing rows start seen, and the per-browser key is retired.

**Non-Goals:**
- Carrying seen state between devices, or into or out of the export file.
- Recording when an update was seen.
- A "mark all as seen" control, or un-marking an update.
- Changing the eligibility rule, the 30-day window, the 10-minute poll, or card content.
- Counting keyboard focus alone as "looked at" (see Open Questions).

## Decisions

### D1. A `Seen` boolean column on `AnimeUpdate`

`AnimeUpdate` gains `public bool Seen { get; set; }`. It is not null, and its database default is `false`. `AnimeUpdateRecorder` is unchanged: a new row is unseen because the CLR default is `false`. No index is added, because no query filters on it. Both reads load the window or the whole table and filter eligibility in memory.

*Alternatives:*
- **A separate `SeenUpdates` table.** Rejected: it would be one row per update, joined on every read. The flag's lifetime is exactly the row's, including its cascade delete with `AnimeMetadata`.
- **A `SeenAt` timestamp.** Rejected by the request, and nothing reads a time.
- **A server-side high-water mark.** Rejected: one mark can't say that card 3 was seen and card 2 wasn't, and saying that is the point of the change.

### D2. A migration that adds the column, then marks every existing row seen

A new migration (`AddUpdateSeenFlag`) runs `AddColumn<bool>("Seen", "AnimeUpdates", nullable: false, defaultValue: false)`, then `UPDATE "AnimeUpdates" SET "Seen" = true;`. `Down` drops the column. The model snapshot is updated with the column.

Migrations run at startup, before the recording services start, so no row recorded after this ships can be caught by the `UPDATE`.

*Alternative:* add the column with `defaultValue: true`, then change the default to `false`. Rejected: that takes two schema operations, and the column's lasting default would briefly be wrong.

### D3. The flag rides on the read DTO, and eligibility is untouched

`AnimeUpdateDto` gains `bool Seen` as its last member, set from the row in `ToDto`. The eligibility filter is not changed, so:
- A hidden update never reaches the client and can't count toward the dot.
- Restoring a dropped entry brings its updates back with the flag they already had. Hiding writes nothing.

The dot and New marking are computed from what the endpoints return. They inherit "every view applies the same rule" for free.

### D4. `POST api/updates/seen` marks a batch of ids through tracked entities

The new endpoint:
- **Body:** `{ "ids": [72, 71] }`, sent to `UpdatesController.MarkSeen`, which calls `IAnimeUpdateService.MarkSeenAsync(IReadOnlyCollection<long> ids, ct)`.
- **Success:** returns `204`.
- **Bad request:** a missing body or `ids` returns `400`, as does a batch over 500 ids (`MaxSeenBatch`). The client never comes near that: it reports only cards that were on screen or hovered within one batching window.
- **Ids:** an empty list is a `204` no-op. Duplicates are harmless, and ids with no row (deleted by cascade) are ignored.

`MarkSeenAsync` loads `db.AnimeUpdates.Where(u => ids.Contains(u.Id) && !u.Seen)` tracked, sets `Seen = true`, and saves.

- **Why not `ExecuteUpdateAsync`:** the in-memory test provider throws on it. Batches are a handful of rows, and `EpisodeAiringRepository`'s `ExecuteDeleteAsync` is the only precedent, which is untested for exactly this reason.
- **Races:** two browsers reporting the same id both write `true`, and `AnimeUpdate` has no concurrency token, so the race is harmless.
- **Where it lives:** the method goes on `AnimeUpdateService`, and the interface summary changes from "the read side" to "the read side and the seen flag". A separate service was rejected, because it would be one method on the same table feeding the same DTO.

### D5. One seen store per page, shared by the bell and both surfaces

`components/Updates/updatesSeenStore.ts` replaces `updatesSeen.ts`. It is module-level, because the History overlay's reports must clear the bell's dot, and the bell, dropdown and History are separate components. It holds:

- **`locallySeen: Set<number>`:** ids reported as seen in this page's lifetime, whether in flight or confirmed. `isSeen(item)` is `item.seen || locallySeen.has(item.id)`. Components subscribe with `useSyncExternalStore`.
- **Optimism:** an id is added to `locallySeen` the moment it's reported, not when the server answers. The dot clears as the last card is seen (spec: "without waiting for the dropdown to close"), and the tracker never reports the same card twice.
- **Batching:** the first id queued starts a 500 ms timer, ids queued meanwhile join the batch, and the timer sends one `POST`. The timer doesn't slide, so a long run of cards seen one after another still reaches the server at least every half second. That keeps other browsers able to catch up while I'm looking.
- **Flushing:** `flushSeenReports()` sends immediately. It is called when the dropdown or History unmounts, and on `pagehide`. The `pagehide` flush uses `fetch(..., { keepalive: true })` so it survives the page going away.
- **Failure:** the batch's ids are removed from `locallySeen` and subscribers are notified. The card is unseen again locally, and the dot comes back if it was the last one. The tracker treats it as reportable again. Nothing is announced. `performFetch` still reports an unreachable backend through the connection-status notice, as it does for every call.

`locallySeen` is never pruned. Once a refresh brings `seen: true` the entry is redundant, and the set holds at most the ids seen in one page lifetime.

*Alternatives:*
- **One request per card.** Rejected by the request.
- **Report only on close.** Rejected: the other browser couldn't catch up while this one is open, and a crash loses the whole look.
- **`navigator.sendBeacon`.** Rejected: it sends `text/plain` unless given a `Blob`, which fights the controller's JSON `[FromBody]`. A `keepalive` fetch carries the ordinary JSON header.

### D6. A look's New set is accumulated, never recomputed, and scoped by mounting

`useUpdateLook(items, isSeen)` returns the set of ids to mark New for this look. It keeps two refs:
- the ids already shown in this look;
- the ids marked New.

On each render, any id in `items` not yet shown is recorded as shown and, if `!isSeen(item)`, added to the New set. Nothing is ever removed. This satisfies every rule in "The marking is fixed for one look":
- a refresh can't remove or add a marking on a card already shown;
- a card arriving mid-look is judged when it first appears;
- a card seen during the look stays in the set.

Accumulating during render is idempotent (a second render adds nothing), so StrictMode's double render is safe.

The look's lifetime comes from mounting:
- `UpdatesHistoryOverlay` already mounts per open.
- The dropdown panel moves out of `UpdatesMenu` into a new `UpdatesDropdown` component, rendered only while `open`. Its look, its tracker and its height measurement then start fresh on every open by construction.
- Opening History closes the dropdown, so the dropdown's look ends and History's begins. A card seen in the dropdown is not New in History, as the spec requires.

History's search and date filters narrow what is shown. A card first shown after a filter change is judged at that moment, which is the same rule.

*Alternative:* keep the panel inline and reset the look in an effect keyed on `open`. Rejected: the first render after opening would use the previous look's set until the effect ran.

### D7. Visibility is measured geometrically, only for cards still to report

`useSeenTracking(listNode, items, isSeen, report)` runs in both surfaces:

- **Which cards.** Every `<li>` carries `data-update-id`. The hook measures only cards whose update is unseen and not yet reported. That is usually a handful, and it shrinks as they are seen.
- **The visible area.** The list's bounding rect is intersected with every ancestor whose computed `overflow` clips, and with the window. For History that is `.modal`, which scrolls at 85vh. For the dropdown it is only the window, since the list itself is capped to it. The scrollbar is hidden, so the rect needs no scrollbar gutter subtracted.
- **Qualifying.** A card qualifies when it is whole: `top ≥ area.top − 1` and `bottom ≤ area.bottom + 1`. A card taller than the area qualifies when it fills it: `top ≤ area.top + 1` and `bottom ≥ area.bottom − 1`. The 1px tolerance absorbs subpixel rounding: `useCappedCardHeight` sets `max-height` to a fractional sum, so the third card's bottom lands on the list's bottom only to within rounding.
- **The dwell.** Each qualifying card gets a 1000 ms timer (`SEEN_DWELL_MS`). The timer is cancelled when the card stops qualifying, when `document.visibilityState` becomes `hidden`, and on unmount. When it fires, it calls `report(id)`. A card that re-qualifies starts a fresh second.
- **When to re-evaluate.** The hook re-evaluates:
  - on the list's `scroll` (passive, coalesced to one pass per animation frame)
  - when the list or any card is resized, via a `ResizeObserver` (pictures loading move cards)
  - on window `resize`
  - on `visibilitychange`
  - on any change to `items`
  - on History's modal scroll, via a capture-phase `scroll` listener on `document`

*Alternative:* `IntersectionObserver` with thresholds. Rejected: a ratio threshold can't express "fills the area" for a card taller than the root, because the ratio at which it fills depends on the card's own height. So geometry would be needed anyway, and for a handful of cards it is the simpler of the two.

**Sharing the list node.** `useCappedCardHeight` already owns the `<ul>` through a callback ref. It now also returns the node (`listNode`), and both hooks use it. That avoids composing two refs.

### D8. Hover counts on real mouse movement; following a card counts at once

The tracker listens for delegated `pointermove` events on the list. An event counts when `pointerType === 'mouse'` and `movementX` or `movementY` is non-zero. It then finds `closest('li[data-update-id]')` and, if that update is unseen and unreported, reports it at once.

- **Why `pointermove` with movement, not `pointerenter`:** after a scroll, browsers re-run hover under a still pointer by sending boundary events. `pointerenter` would count every card wheel-scrolled past the cursor. Requiring movement means the card I stop on counts the moment I nudge the mouse.
- **Why hover works with the hidden scrollbar:** the list clips its overflow, so a card's scrolled-out part can't receive pointer events. Any hit is on a visible part.
- **Touch** is excluded: it has no hover, and a touch that scrolls the list isn't looking at a card.

**Following a card.** A delegated `click` listener on the list, plus `auxclick` for a middle-click into a new tab, finds `closest('li[data-update-id]')` and reports the card at once. `click` fires for a mouse click, a touch tap, and Enter on the focused link, so one listener covers:
- **touch**, which has no hover;
- **keyboard**, which has no hover;
- **a still mouse**, where the card was scrolled under the pointer and clicked without the pointer moving.

**Getting the report out.** The listener is a native one on the `<ul>`, so it runs during bubbling before React's root-level handler runs the card's `onNavigate`. The id is therefore queued before `onNavigate` closes the dropdown or History. That close unmounts the surface, whose unmount calls `flushSeenReports()` (D5), so the report goes out at once rather than waiting for the batch timer. Navigation stays inside the single-page app, so the request is not cut off.

### D9. The New marking is an inset edge plus a badge on the title's line

`UpdateCard` gains an `isNew` prop:

- **The accent edge.** `update-card--new` adds `box-shadow: inset 3px 0 0 var(--accent)`. It is drawn inside the border box, so width, padding and wrapping are identical to an unmarked card, and hover's tinted background and accent border still read as hover on top of it.
  - *Rejected:* a tinted background, which is hover's own treatment; and a thicker left border, which would move the content 2px and could re-wrap text.
- **The badge.** `<span class="update-card__new"><span class="update-card__new-dot" aria-hidden="true"></span>New</span>` goes at the start of the title's line:
  - **The dot:** 8px in `--accent`, the same size and colour as `.updates-menu__dot`, without the bell's ring and absolute position.
  - **The word:** 11px, weight 600, in `--text-h`. Accent-coloured text that small would be low-contrast.
  - The link's accessible name then starts with "New".
- **Menu variant.** The title becomes a nowrap flex row: the badge with `flex: none`, then `TruncatedTitle` with `flex: 1 1 auto`. `TruncatedTitle`'s class already has `min-width: 0` and the ellipsis. The ellipsis and its `scrollWidth > clientWidth` tooltip check therefore still apply to the title span alone, and the badge is never cut. The badge's line box is no taller than the 14px title's, so the card's height is unchanged.
- **History variant.** The badge is the first item in the existing wrapping `update-card__title--full` row.
- **Stable heights.** A look's marking never changes while the look lasts (D6), so heights never shift under the tracker or under `useCappedCardHeight` mid-look. The hook's `ResizeObserver` would re-measure anyway.

### D10. The bell re-checks on focus, and the old key is retired

`useRecentUpdates` also calls `refresh()` on window `focus`, and on `visibilitychange` when the tab becomes visible. They often fire together, and `fetchRaw` joins identical in-flight GETs, so that costs one request, with no throttle of its own. The poll is unchanged.

`UpdatesMenu` computes `unseen = items.some((item) => !isSeen(item))`, subscribed to the store. The accessible label (`'Updates, new'`) and the dot markup are unchanged. `handleToggle` still refreshes on open, but no longer marks anything.

`updatesSeen.ts` is deleted. When the store module loads, it runs `localStorage.removeItem('bettermal.updatesSeenAt')` once, in a `try` because storage can throw. The comment on that line names this change, so the line can be dropped once no browser holds the key.

## Risks / Trade-offs

- **[A card seen in another browser stays New here until I close]** → Intended (spec, "A refresh does not change the look"). The dot still clears, because it reads the flags and not the look.
- **[Two browsers visible side by side, neither gaining focus]** → Neither gets a focus event, so the second catches up at its next poll or its next focus. Accepted: the request is about switching between browsers.
- **[A report is lost when the tab dies mid-batch]** → The `pagehide` flush uses `keepalive`. At worst the cards stay unseen and are reported on the next look, which the request accepts.
- **[Browsers differ in synthesising hover after a scroll]** → The movement check (D8) ignores synthesised events. At worst a card counts by hover a moment early.
- **[Subpixel rounding keeps the third card from counting as whole]** → A 1px tolerance on both edges (D7).
- **[A restored franchise brings back many unseen cards in History]** → Only unseen, unreported cards are measured, and the set shrinks as they are seen.
- **[The 8 existing rows are marked seen, though not every one was looked at]** → Requested. Otherwise all of them would light up at once.
- **[A failed report is silent]** → Requested. The card stays New next time, and the connection-status notice still covers an unreachable backend.
- **[`updatesSeenStore` is module state]** → The bell, the dropdown and History live in one page and must agree. The store holds no user data beyond ids already on screen.

## Migration Plan

1. Deploy the backend and frontend together (`docker compose build`). On startup, `AddUpdateSeenFlag` adds `Seen` and marks every existing row seen before any recording service runs.
2. Old frontend against the new backend: it ignores `seen`, and nothing sends reports. New frontend against an old backend: `POST /seen` returns 404, reports fail, and `seen` is missing, so every card would read as unseen. Hence deploying together.
3. The first load of the new frontend clears `bettermal.updatesSeenAt`.
4. **Rollback:** `Down` drops the column. The old frontend then finds no `bettermal.updatesSeenAt` and raises its dot once, which its own spec allowed ("losing it SHALL do no more than raise the indicator once").

## Open Questions

None blocking. One thing is left out on purpose and would be a one-line addition to the tracker if wanted later:
- **Keyboard focus alone.** Tabbing onto a card without opening it doesn't count like hovering. Focus scrolls the card into view, where the one-second rule counts it. Pressing Enter on it counts at once (D8).
