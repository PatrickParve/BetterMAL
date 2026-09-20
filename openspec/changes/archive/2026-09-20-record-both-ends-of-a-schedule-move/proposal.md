## Why

An update row stores only the value a schedule change moved **from**. The value it moved **to** is not stored at all — it is re-read from the anime's current record every time a card renders. `AnimeUpdateService` passes `anime.AiredFrom` (live) beside `update.PreviousStartDate` (recorded), and does the same for the broadcast slot, converting the anime's *current* JST day and time for the "now" half of the line.

That works while an anime's schedule moves once. The second time it moves, the older card's "to" half is quietly replaced by the newer value: a card recorded for `8 Oct → 1 Oct` starts reading `… to 20 Nov · was 8 Oct`, describing a move it never recorded and duplicating one the newer card already reports. The direction word goes with it — `Moved earlier` becomes `Delayed` — so the wording fix in `polish-updates-panel` is only as reliable as the date it compares against.

`anime-updates` already forbids this in two places. "These stored values SHALL NOT be re-derived from the anime's current record, which by then holds only the new value", and the scenario **"A later correction does not rewrite an earlier update"**, which no test can satisfy today. The table itself shows the intent was always both ends: `EpisodesMoved` stores `PreviousEpisodeDate` *and* `NewEpisodeDate` and is correct. Premiere dates and broadcast slots are the two kinds that were left half-recorded.

Nothing is visibly wrong yet — all 6 recorded premiere changes in the live database are one-per-anime, and there are no broadcast-slot changes at all. That is also why the backfill is exact **now** and not later: while each anime has had exactly one recorded move, its current premiere date *is* that move's destination. The first time MAL moves one of those six again, what the row recorded is gone for good.

## What Changes

**What an update stores**

- `AnimeUpdate` gains `NewStartDate`, `NewBroadcastDayOfWeek` and `NewBroadcastTime`, mirroring the `Previous*` trio beside them and the `PreviousEpisodeDate`/`NewEpisodeDate` pair that already does this correctly. The broadcast pair is stored in JST, as its `Previous*` counterpart is, and converted to local time on the way out.
- `ScheduleMoveDetails` carries the matching values from detector to recorder. It already carries `NewEpisodeDate`, so this finishes a widening that is half-done rather than introducing a new idea.
- The detector fills them where it already computes the move — both ends are in hand at that moment, since it holds the before-snapshot and the just-written anime.

**What a card reads**

- A movement line reads **only** recorded values. `AnimeUpdateService` stops reading the anime's live premiere date and live broadcast slot for the `StartDateChanged` and `BroadcastSlotChanged` lines.
- The fact lines — `Total episodes:` and `Premiere:` — keep reading the anime's current record exactly as they do now. That rule exists so a corrected episode count reaches every card that mentions it, and it stays. What ends is its accidental application to the "to" half of a movement, where the news *is* the movement.
- Where a row holds no recorded "to" value — a row written before this change and not covered by the backfill — the card falls back to the live value, as it does today. The equal-dates guard added by `polish-updates-panel` therefore stays permanently rather than becoming dead code.

**The rows that already exist**

- The migration stamps `NewStartDate` on existing `StartDateChanged` rows from their anime's current premiere date. This is correct by verification rather than by assumption: every such row is the only one recorded for its anime, so the anime's current date is still that move's destination. The statement is written so it can only touch rows meeting that condition.
- Nothing to backfill for broadcast slots — no such row exists — and nothing for episode moves, which have always stored both ends.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `anime-updates`: "A schedule change is recorded with what it moved from" becomes a requirement to record **both** ends of every schedule move — premiere date and broadcast slot as well as moved episodes — and the display rules state that a movement line's "to" value is read from the update rather than from the anime, with a fallback named for rows recorded before that was stored.

## Impact

- **Backend** — `Models/AnimeUpdate.cs` (three columns), one new migration with a data statement, `Services/Updates/ScheduleMoveDetails.cs`, `Services/Updates/AnimeMetadataChangeDetector.cs` (fills them), `Services/Updates/AnimeUpdateRecorder.cs` (writes them), `Services/Updates/AnimeUpdateDto.cs` (carries them), `Services/Updates/AnimeUpdateService.cs` (`ToDto` reads the row, not the anime).
- **Frontend** — `api/types.ts` and `components/Updates/updateText.ts`: prefer the recorded values, fall back to the live ones where null.
- **Tests** — `AnimeUpdateRecorderTests` and `AnimeMetadataChangeDetectorTests` for what is stored; `AnimeUpdateServiceTests` for what is reported, including the "A later correction does not rewrite an earlier update" scenario that cannot be written today.
- **Documentation in code** — `AnimeUpdate`'s class comment and `AnimeUpdateDto`'s summary both currently describe the moved-from values as the only thing stored; both are corrected, or the next reader inherits exactly the confusion this change exists to remove.
- **Not changing** — what counts as an update and when it is recorded; the relevance gate; the airing-status gate; how episode moves are stored; the live reading behind the episode-count and premiere fact lines; seen state; any API route or response shape beyond the three added fields.
- **Sequencing** — lands after `polish-updates-panel`, which edits the same `StartDateChanged` branch of `updateText.ts`.
