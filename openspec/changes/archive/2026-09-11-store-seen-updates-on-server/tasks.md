## 1. Backend: the seen flag

- [x] 1.1 Add `public bool Seen { get; set; }` to `Models/AnimeUpdate.cs`, with a comment saying:
  - it is a plain flag, and when it was seen is not stored
  - new rows start unseen through the CLR default, so `AnimeUpdateRecorder` needs no change (design D1)
- [x] 1.2 Generate the `AddUpdateSeenFlag` migration with `dotnet ef migrations add` in the `mcr.microsoft.com/dotnet/sdk:10.0` image, from a copy under `/private/tmp`. It needs the Designer file and the snapshot update. Then:
  - check it adds `Seen` as `boolean NOT NULL DEFAULT false`
  - add `migrationBuilder.Sql("UPDATE \"AnimeUpdates\" SET \"Seen\" = true;")` after the `AddColumn`, with a comment naming the starting-point rule (design D2)
  - make `Down` only drop the column
- [x] 1.3 Add `bool Seen` as the last member of `Services/Updates/AnimeUpdateDto.cs`, and pass `update.Seen` from `AnimeUpdateService.ToDto` (design D3).
- [x] 1.4 Add `Task MarkSeenAsync(IReadOnlyCollection<long> ids, CancellationToken ct = default)` to `IAnimeUpdateService`, and change its summary to "the read side and the seen flag". Implement it in `AnimeUpdateService`:
  - load `Where(u => ids.Contains(u.Id) && !u.Seen)` tracked
  - set `Seen = true` and save
  - return without a query for an empty list (design D4)
- [x] 1.5 Add `POST api/updates/seen` to `UpdatesController`. It takes a `MarkUpdatesSeenRequest(IReadOnlyList<long>? Ids)` body and:
  - returns `400` with an `{ error }` body when `Ids` is null or holds more than `MaxSeenBatch` (500)
  - otherwise calls `MarkSeenAsync` and returns `204`

## 2. Backend tests

- [x] 2.1 Add `AnimeUpdateServiceSeenTests` beside `AnimeUpdateServiceTests`, using the in-memory provider and the same `CreateService`. Cover:
  - `GetRecentAsync` and `GetHistoryAsync` carry each row's `Seen`
  - `MarkSeenAsync`:
    - sets exactly the given ids
    - leaves the others unseen
    - ignores ids with no row
    - is a no-op for an empty list
    - is harmless when repeated
  - an unseen update on a Dropped standalone entry is absent from `GetRecentAsync`
  - setting that entry back to Watching brings it back still unseen, and a seen sibling still seen
- [x] 2.2 Add a scenario to `AnimeUpdateRecorderTests`: a recorded row has `Seen == false`.
- [x] 2.3 Add `UpdatesControllerTests` with a recording fake `IAnimeUpdateService`, in the style of `ListBackupControllerTests`. Cover:
  - a batch returns `NoContentResult` and forwards the ids
  - null `Ids` returns `400`
  - 501 ids returns `400` without calling the service
  - an empty list returns `204`
- [x] 2.4 Build and run the whole backend test project in the `sdk:10.0` image, from a fresh `/private/tmp` copy. Record the pass count. (1173 passed, 0 failed)

## 3. Frontend: data and the shared seen store

- [x] 3.1 Add `seen: boolean` to `AnimeUpdateDto` in `api/types.ts`. Add `markUpdatesSeen(ids: number[], options?: { keepalive?: boolean }): Promise<void>` to `api/client.ts`, a JSON `POST` through `fetchVoid` to `/api/updates/seen`.
- [x] 3.2 Create `components/Updates/updatesSeenStore.ts` (design D5):
  - **state:** a module-level `locallySeen` set and `isSeen(item)`
  - **`reportSeen(id)`:** adds the id to `locallySeen` at once and notifies subscribers; the first queued id starts a non-sliding 500 ms timer that sends one batch
  - **`flushSeenReports({ keepalive })`:** sends at once
  - **`pagehide`:** a listener flushes with `keepalive: true`
  - **failure:** removes the batch's ids from `locallySeen` and notifies, with no notice
  - **subscribing:** `subscribe`/`getSnapshot` (a version counter) for `useSyncExternalStore`, wrapped as `useUpdatesSeen()`
  - **retiring the key:** a one-time `localStorage.removeItem('bettermal.updatesSeenAt')` in a `try`, commented with this change's name (design D10)
- [x] 3.3 Delete `components/Updates/updatesSeen.ts` and remove every import of it. (`UpdatesMenu.tsx`'s import is removed in 6.2's rewrite)

## 4. Frontend: tracking a look

- [x] 4.1 Make `useCappedCardHeight` also return `listNode`, the element its callback ref holds, without changing how it measures (design D7, "Sharing the list node").
- [x] 4.2 Create `useUpdateLook(items, isSeen)`. It returns the set of ids marked New for the life of the calling component. Ids are added when first shown and unseen, and are never removed; the accumulation must be idempotent across re-renders (design D6).
- [x] 4.3 Create `useSeenTracking(listNode, items, isSeen, report)` (design D7):
  - measure only unseen, unreported `li[data-update-id]` cards
  - take the visible area as the list's rect intersected with every clipping ancestor and the window
  - test whole and fills-the-area with a 1px tolerance
  - run a 1000 ms dwell timer per qualifying card, cancelled when the card stops qualifying, when the tab is hidden, or on unmount
  - re-evaluate:
    - on list `scroll`, coalesced per animation frame
    - on a capture-phase `scroll` on `document`
    - on `ResizeObserver` changes to the list and its cards
    - on window `resize`
    - on `visibilitychange`
    - when `items` change
- [x] 4.4 In the same hook, add the two immediate rules (design D8):
  - **hover:** a delegated `pointermove` with `pointerType === 'mouse'` and non-zero `movementX`/`movementY`
  - **following a card:** delegated native `click` and `auxclick` listeners on the `<ul>`
  - both find `closest('li[data-update-id]')` and report at once
  - the listeners are attached with `addEventListener` on the list, so they run before React's `onNavigate` closes the surface

## 5. Frontend: the New marking

- [x] 5.1 Give `UpdateCard` an `isNew` prop (design D9):
  - add `update-card--new` to the link
  - render the `update-card__new` badge (an `aria-hidden` dot plus the word "New") at the start of the title's line
  - **menu variant:** wrap the badge and `TruncatedTitle` in a nowrap `update-card__title-row` flex row
  - **history variant:** make the badge the first item of `update-card__title--full`
- [x] 5.2 In `UpdateCard.css`:
  - `.update-card--new`: `box-shadow: inset 3px 0 0 var(--accent)`
  - `.update-card__title-row`: flex, nowrap, `min-width: 0`, a small gap
  - `.update-card__new`: `flex: none`, 11px, weight 600, `--text-h`, line box no taller than the title's
  - `.update-card__new-dot`: 8px circle in `--accent`, matching `.updates-menu__dot`'s size and colour
  - `TruncatedTitle` in the row gets `flex: 1 1 auto`

## 6. Frontend: wiring the two surfaces and the bell

- [x] 6.1 Extract the dropdown panel (header, History button, empty text, list) from `UpdatesMenu` into `UpdatesDropdown.tsx`, rendered only while open (design D6). It:
  - owns `useCappedCardHeight`, `useUpdateLook` and `useSeenTracking`
  - puts `data-update-id` on each `<li>` and passes `isNew` to each card
  - calls `flushSeenReports()` on unmount
  - keeps the `updates-menu__*` classes, so `UpdatesMenu.css` needs no change
- [x] 6.2 In `UpdatesMenu`:
  - drop `readSeenMarker`/`markSeen` and the `seenMarker` state
  - make `handleToggle` open and `refresh()` without marking anything
  - compute `unseen` as `items.some((item) => !isSeen(item))` through `useUpdatesSeen()`
  - keep the dot markup and the `'Updates, new'` label as they are
- [x] 6.3 In `useRecentUpdates`, also `refresh()` on window `focus` and on `visibilitychange` when the tab becomes visible, removing both listeners on unmount. Update the file's comments (design D10).
- [x] 6.4 In `UpdatesHistoryOverlay`:
  - apply `useUpdateLook` and `useSeenTracking` to `filteredHistory` with the store's `isSeen`/`reportSeen`
  - put `data-update-id` on each `<li>` and pass `isNew`
  - call `flushSeenReports()` on unmount
- [x] 6.5 Update the component comments that cite the old behaviour (`UpdatesMenu`'s "marked seen" note and `useRecentUpdates`' design D2/D3 references), so no comment describes marking the window on open.

## 7. Docs

- [x] 7.1 Update CODE_GUIDE:
  - add `POST /api/updates/seen` to the HTTP API table
  - add the seen flag and `MarkSeenAsync` to the `Services/Updates/` section
  - replace the `bettermal.updatesSeenAt` mentions in the frontend section (around lines 654 and 687-691) with the seen store, the two hooks and `UpdatesDropdown`

## 8. Verification

- [x] 8.1 Run `npm run build` and `npm run lint` in `frontend/` with nvm's Node 22. Both must pass with no new warnings.
- [x] 8.2 Rebuild and restart the stack (`docker compose build backend frontend && docker compose up -d`). Then confirm:
  - `__EFMigrationsHistory` lists `AddUpdateSeenFlag`
  - all 8 existing `AnimeUpdates` rows have `Seen = true`
  - the bell shows no dot on first load
  - `bettermal.updatesSeenAt` is gone from localStorage
- [x] 8.3 For the walkthrough, set 5 rows back to unseen on the dev database (`UPDATE "AnimeUpdates" SET "Seen" = false WHERE "Id" IN (…)`), noting the ids. Walk the spec's scenarios in the running app:
  - **on open:** opening and closing at once marks nothing; after a second, the three cards in view become seen and the fourth and fifth don't
  - **looking:**
    - a card cut off at the edge stays unseen
    - a hidden tab pauses the dwell
    - moving the mouse over a card counts at once
    - wheel-scrolling under a still pointer doesn't count
    - tapping a card (touch emulation in devtools) or pressing Enter on it before a second has passed counts, and the Network tab shows the `POST` as the dropdown closes
    - a card taller than the list counts once it fills it (narrow window or zoom)
  - **the marking:** New holds until close, and is gone on reopen and in History
  - **the bell:** the dot clears on the last unseen card while the dropdown is still open, and a second browser clears its dot on focus
  - **History** counts, and marks an unseen update older than 30 days as New (`UPDATE … SET "DetectedAt" = now() - interval '40 days'` on a copy of one row's date, restored afterwards)
  - **reports:** the Network tab shows cards seen together sent as one `POST`, and with the backend stopped a card stays unseen and is reported on the next look
  - **layout:** a long title stays one line with the badge in full and its tooltip on hover; the dropdown still opens exactly three cards tall
- [x] 8.4 Restore the dev database's rows to the state they had before 8.3.
- [x] 8.5 Run `openspec validate store-seen-updates-on-server`.
