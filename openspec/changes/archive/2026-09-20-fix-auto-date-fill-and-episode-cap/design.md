## Context

`UserAnimeEntryEditService.UpdateEntryAsync` applies an edit in five ordered steps against one snapshot of "episodes aired so far": `ApplyEpisodesWatched`, `ApplyStatus`, `ApplyDates`, `ApplyScore`, `ApplyRewatchCount`. Two defects live in the first step.

**The invented start date.** `ApplyEpisodesWatched` fills `started_at` with today on the 0 → >0 transition whenever `started_at` is empty. `ApplyDates` runs two steps later and is the only step that sees the dates the request carries; it finishes by rejecting any entry whose finish date precedes its start date, testing the post-edit values. So step one writes a date, step three supplies the one it conflicts with, and the edit is rejected over a pair the user never entered. It surfaces two ways, both reproduced against the live database:

- the first episode of a rewatch, where the finish date is already stored (286 of 620 live entries have a finish date and no start date);
- backfilling an old watch, where the finish date arrives in the same request and the episode count is typed alongside it. Picking Completed *without* touching the episode field saves fine today, because `ApplyEpisodesWatched` then returns early — the same intent succeeds or fails depending on which field was touched.

**The ceiling above the total.** The cap is `airedSoFar ?? TotalEpisodes` — the aired count wins outright and the total never constrains it. Five anime in the live list have more stored AniList episode rows than MyAnimeList's published total (AniList grouping two MAL entries into one, or numbering a season continuously from an earlier one), so their entries can be set past their own total. Mushoku Tensei II saves as `13/12` and can then never complete, because completion requires reaching the total exactly.

Three ceilings exist today and disagree: the progress row caps at aired-so-far, the entry editor at the total alone, the server at aired-so-far. The editor's disagreement is its own small defect — it accepts counts the server rejects for a still-airing anime.

## Goals / Non-Goals

**Goals:**

- No edit is ever rejected because of a date the system itself filled in.
- A rewatch of an entry carrying a finish date records progress without touching either date.
- Backfilling a finished watch saves whether or not the episode count is typed in the same save.
- Episodes watched can never exceed the anime's own total, on any surface.
- One ceiling, computed in one place per side, used by the progress row, the entry editor and the server.

**Non-Goals:**

- Reconciling AniList's episode numbering with MyAnimeList's. That mismatch is real and stays; this change only stops it leaking into editing. It belongs to `episode-airing-data` if it is ever addressed.
- Per-rewatch date history. The entry keeps MyAnimeList's single date pair.
- Repairing stored data. No entry currently sits above its total and no stored date is rewritten.
- Changing the finish-date fill, which already matches the intended rule.

## Decisions

### D1: The started-date guard reads the finish date as it will stand after the edit

The fill's condition gains the finish date the entry will hold once the whole request is applied — the request's value where it supplies one, the stored value otherwise — rather than the stored value alone. Together with the existing "start date empty" test, that is the whole fix for both symptoms: a stored finish date and an incoming one suppress the fill identically.

*Alternatives considered.* **Move the fill after `ApplyDates`**, so it only ever sees final values. Cleaner in principle, and it would make "the automatic rules fill only what is still empty at the end of the save" literally true; rejected because detecting the 0 → >0 transition needs `previousEpisodesWatched`, which exists only inside `ApplyEpisodesWatched`, so the fill would have to carry state across steps to gain nothing the local test does not already give. **Relax the out-of-order date check** to ignore automatically filled dates; rejected outright — it would let genuinely contradictory pairs save, and the check is correct as written. The defect is the fill, not the validation.

### D2: A start date supplied by the same edit also suppresses the fill

Where the request carries a start date, `ApplyDates` overwrites whatever the fill wrote, so filling first is already a no-op in outcome. Making it an explicit condition states the rule as one sentence — *the system fills a date only where the user has said nothing about it* — instead of relying on a later step to undo it.

### D3: One ceiling helper per side, not a fix at each call site

`min(airedSoFar, total)` where both are known, otherwise whichever is known, otherwise none. The backend computes it in one place inside the edit service; the frontend gets one helper beside `hasAiredEpisodes` in `utils/anime.ts`, which all four call sites use — the three `ProgressBar` callers and the entry editor.

Patching the five sites independently is what produced three disagreeing ceilings in the first place. The two helpers stay honest across the language boundary the same way `AiredEpisodeGate` and `hasAiredEpisodes` already do: a comment on each naming the other, rather than a shared implementation.

### D4: The total wins over stored airing data, not the reverse

Where the two disagree, the published total is the ceiling. The total is already what every other rule measures the entry against — the completion target, the progress bar's denominator, what Completed fills the count to — so a count above it is incoherent with the rest of the entry no matter which source is right. This also restores the reasoning "Unknown total episodes cannot be completed" already depends on: that an entry standing at its total has necessarily watched only aired episodes.

The cost is that a genuinely extra episode cannot be recorded while MyAnimeList's total omits it. That is a metadata problem with a metadata fix (`metadata-refresh`), and `13/12` was never a coherent way to express it.

### D5: No data repair step

Verified against the live database: no entry currently exceeds its own total, so there is nothing to walk back. Were one to exist — arriving from MyAnimeList, say — the ceiling constrains new edits only; the stored value stands until edited, the plus control disables, and lowering it is always accepted. That matches how the aired-episode gate already treats values that predate it.

## Risks / Trade-offs

- **286 entries record no start date when rewatched** → Accepted deliberately. The entry's one date pair describes the original watch, which is MyAnimeList's own behaviour and what "Completed-date lifecycle" already commits to for the finish date. The start date stays editable by hand.
- **111 entries with no dates at all will have the rewatch's start date filled in as today**, so their pair describes the rewatch rather than the first watch → Accepted. Both dates being empty means nothing is being contradicted, and a recorded date beats none.
- **Backend and frontend ceilings drift apart again** → One helper per side rather than inline expressions, each carrying a comment naming its counterpart, following the existing `AiredEpisodeGate` / `hasAiredEpisodes` precedent.
- **A show whose real episode count exceeds MyAnimeList's total becomes unrecordable past that total** → Accepted per D4; the fix belongs in metadata, and the entry was already incapable of completing in that state.
- **Stored airing data above the total could still make a currently-airing anime look fully aired early**, via `AiredEpisodeGate.EverythingHasAired`, letting it complete before its finale → Pre-existing, untouched by this change, and verified not currently true for any followed anime. Out of scope, noted so it is not mistaken for something this change introduced.

## Migration Plan

None. No schema change, no data migration, no API contract change. The values pushed to MyAnimeList are the same ones the user sees, so sync is unaffected. Rollback is a code revert; nothing written while the change is live depends on it.

## Open Questions

None. The two behavioural choices this change forces — what happens to the 286 dateless-start entries and the 111 fully dateless ones — were settled before the proposal, and are recorded as trade-offs above.
