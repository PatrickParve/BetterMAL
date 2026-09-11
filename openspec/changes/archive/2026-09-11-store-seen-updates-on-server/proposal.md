## Why

The updates bell remembers what I've seen per browser, so reading the updates in one browser leaves the dot lit in another on the same device. And once the dropdown is open, nothing shows which cards are the new ones: opening it marks the whole window seen at once, whether or not I looked at anything.

**Verified against the code and the live database** (2026-09-11):

- **The record is one per-browser high-water mark.** `components/Updates/updatesSeen.ts` keeps the ISO `detectedAt` of the newest update shown under the localStorage key `bettermal.updatesSeenAt`. `hasUnseen` compares the window against it, and `markSeen` advances it.
- **Opening marks everything.** `UpdatesMenu.handleToggle` (`UpdatesMenu.tsx:42-54`) refreshes the list and then calls `markSeen` on the whole result. The dot is at `:81` and the accessible label at `:68`.
- **Nothing re-checks on focus.** `useRecentUpdates` fetches on mount and every 10 minutes while the tab is visible (`useRecentUpdates.ts:8`), and does nothing else.
- **The server has no seen state.** `AnimeUpdate` has no such column. Rows are created only by `AnimeUpdateRecorder.RecordAsync` (`:45`) and read only by `AnimeUpdateService`, whose `GetRecentAsync` (30-day window) and `GetHistoryAsync` both go through the eligibility filter before `ToDto` builds the DTO.
- **The data today.** There are 8 update rows, detected between 2026-08-26 and 2026-09-10. All 8 are eligible and all 8 are inside the 30-day window, so without a starting point every one of them would light the dot.
- **The export already leaves the feed out.** `device-transfer`, "The file holds only what exists nowhere but this app", excludes "the anime-updates feed" and "browser preferences". The transfer import never touches `AnimeUpdates`.

## What Changes

**Seen is stored on the server, per update**
- Each `AnimeUpdate` row gets a plain seen / not-seen flag. When it was seen isn't stored.
- New rows start unseen. A migration marks every row that exists when this ships as seen, so the current 8 don't all light up.
- `GET api/updates/recent` and `GET api/updates/history` carry the flag on each update.
- A new `POST api/updates/seen` takes a batch of update ids and marks them seen. It is idempotent, and ids that no longer exist are ignored.
- Every browser on this device reads the same flags. The flags stay on this device: the export file already excludes the updates feed, and that doesn't change.

**An update becomes seen when I've actually looked at it**
- This applies in the navbar dropdown and in the History overlay; either counts.
- A card counts as looked at when:
  - the whole card has been inside the list's visible area for about a second, while the tab is visible; or
  - a card taller than that area has filled it for about a second; or
  - I move the mouse over it, which counts at once; or
  - I follow it to its anime, by mouse, touch or keyboard, which also counts at once.
- Opening the dropdown by itself marks nothing.
- Cards seen close together are sent as one request. If a request fails, those cards stay unseen and are reported again the next time I look at them.

**New cards are marked**
- In both surfaces, an unseen card shows the same dot the bell uses, an accent edge, and the word "New". There's no divider between new and older cards.
- The marking is fixed for one look. A card that becomes seen while I'm looking keeps its New marking until the dropdown or History closes, and has lost it the next time either opens.
- A card that arrives from a poll while the dropdown is open shows as New and follows the same rule.
- The marking changes neither the dropdown's three-card height nor its one-line title truncation.

**The bell's dot**
- The dot shows while any shown update from the last 30 days is unseen, and clears as soon as the last of them is seen.
- Updates older than 30 days never raise it, as today, but an unseen one still shows as New in History.
- Updates hidden by the eligibility filter don't count. When a dropped entry is restored, its updates come back with the flag they had: an unseen one inside the window raises the dot again, and an older one shows as New in History.
- The bell re-checks when the window gets focus or the tab becomes visible, so reading the updates in one browser clears the dot in another as soon as I switch to it.

**Retired**
- `updatesSeen.ts` and the `bettermal.updatesSeenAt` key are removed. The key is also cleared from any browser that still holds it.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `anime-updates`:
  - **"The updates control signals unseen news"** is rewritten around the per-update flag: which updates count, what the dot shows, re-checking on focus, the starting point, and keeping the flags on this device.
  - A new requirement defines when an update becomes seen.
  - A new requirement defines how unseen cards are marked New.
  - **"The updates control is marked while its panel is open"** drops its claim that opening the dropdown clears the indicator.

## Impact

- **Backend:**
  - `Models/AnimeUpdate.cs`: gains a `Seen` flag.
  - A new migration adds the column and marks the existing rows seen; the model snapshot is updated.
  - `Services/Updates/AnimeUpdateDto.cs`: gains `Seen`.
  - `AnimeUpdateService` and `IAnimeUpdateService`: carry the flag on reads and gain `MarkSeenAsync`.
  - `Controllers/UpdatesController.cs`: gains `POST api/updates/seen`.
  - New tests next to `AnimeUpdateServiceTests`.
- **Frontend:**
  - `api/types.ts` and `api/client.ts`: the flag and the new call.
  - `components/Updates/`:
    - `updatesSeen.ts` is replaced by a shared seen store that batches reports.
    - A new hook tracks which cards have been looked at.
    - The dropdown panel moves into its own component, so each look starts fresh.
    - `UpdateCard` gains the New marking.
    - `useCappedCardHeight` also hands back the list element it measures.
    - `useRecentUpdates` also re-checks on focus.
    - `UpdatesHistoryOverlay` is wired to the store and the tracking hook.
- **Docs:** CODE_GUIDE's HTTP API table, `Services/Updates/` section, and frontend notes (which name the retired key).
- **No change:** `device-transfer` export and import, the eligibility rule, and the 10-minute poll.
