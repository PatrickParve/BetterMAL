## MODIFIED Requirements

### Requirement: Started-date on first progress
The system SHALL set `started_at` to today when episodes-watched is set on an anime that was previously at 0 episodes or not yet in my list, and only when all of the following hold:

- `started_at` is currently empty;
- the same edit is not itself supplying a start date — a supplied value stands on its own rather than being preceded by a filled-in one; and
- the entry's finish date is empty **once the edit has been applied in full** — that is, empty both before the edit and after it. A finish date already stored SHALL suppress the fill, and so SHALL one supplied by the same edit.

The finish-date condition is tested against the outcome of the edit rather than the stored value alone because the two would otherwise contradict each other inside a single save: an invented start date of today sitting beside a finish date in the past is an out-of-order pair, which "Start and finish dates are editable in the editor" rejects — failing an edit whose own values were consistent, and naming a date the user never entered. A stored finish date carries the same meaning: it records a run already finished, so the first episode after it begins a rewatch rather than a first start, and the entry's single date pair continues to describe the original watch, as "Completed-date lifecycle" keeps the finish date across rewatches.

An entry left with no start date by this rule SHALL NOT be treated as an error state: a finish date with no start date is a valid pair per "Start and finish dates are editable in the editor", and the start date remains editable by hand at any time.

#### Scenario: First episode watched
- **WHEN** I set episodes-watched above 0 on an anime that had 0 (or was not in my list) and has no start date
- **THEN** `started_at` is set to today

#### Scenario: Subsequent increments do not overwrite start date
- **WHEN** I increment episodes on an anime that already has a `started_at`
- **THEN** `started_at` is left unchanged

#### Scenario: First episode of a rewatch fills no start date
- **WHEN** I watch the first episode of a Rewatching entry that carries a finish date from its original watch and has no start date
- **THEN** the episode count is saved, `started_at` stays empty, and the edit is not rejected for having dates out of order

#### Scenario: Backfilling an old watch with the episode count in the same save
- **WHEN** I set an entry with no dates to Completed, type its episode count, and enter a finish date from two years ago in one save
- **THEN** the entry saves with that finish date, no start date, and no rejection

#### Scenario: A start date supplied in the same save stands on its own
- **WHEN** I set episodes-watched from 0 to above 0 and enter a start date in the same save
- **THEN** the start date I entered is stored, and today's date is never written in its place

### Requirement: Manually set dates are never overwritten
The system SHALL treat a date the user supplies as authoritative — whether it is already stored on the entry or is arriving in the same edit — and SHALL fill a date automatically only where doing so contradicts nothing the user has said. The automatic started-on-first-progress and finished-on-completion rules SHALL only fill a date that is empty, SHALL NOT overwrite an existing value regardless of how it was set, and SHALL NOT fill a date that the same edit's own values rule out. Clearing a date manually SHALL make it eligible to be filled automatically again by those rules.

An automatic fill SHALL never be the cause of a rejection: where filling a date would put the entry's two dates out of order, the fill SHALL be skipped rather than applied and then reported as an error.

#### Scenario: Automatic rule leaves a manual start date alone
- **WHEN** I set a start date manually and then set episodes-watched above 0 on that entry
- **THEN** the start date I set is kept, not replaced with today

#### Scenario: Automatic rule leaves a manual finish date alone
- **WHEN** I set a finish date manually and then mark the entry Completed
- **THEN** the finish date I set is kept, not replaced with today

#### Scenario: Cleared date can be auto-filled again
- **WHEN** I clear an entry's start date and later set episodes-watched from 0 to above 0
- **THEN** the start date is set to today by the automatic rule

#### Scenario: An automatic fill never causes a rejection
- **WHEN** an edit would have a date filled in automatically that lands out of order against a date the same edit supplies
- **THEN** the automatic fill is skipped and the edit saves, rather than being rejected over a date the user did not enter

### Requirement: Inline editable episode count
The system SHALL make the `watched` half of the `watched/total` count directly editable in place wherever the count appears next to a plus control — the main dashboard's currently-watching carousel, my list rows, and the anime detail page. Clicking (or keyboard-focusing) the count SHALL turn it into a text field pre-filled with the current value and select its contents, so typing replaces the count rather than appending to it. At rest, the count SHALL occupy no more space than the plain `watched/total` text would, so it lines up with the equivalent static count shown elsewhere (e.g. the current-season aired/total bar).

The field SHALL accept only non-negative whole numbers: non-numeric characters SHALL be rejected, and negative values SHALL NOT be enterable or submittable, filtered as each character is typed. The enterable ceiling is the number of episodes actually available, and never more than the anime's own total: where an aired-so-far count and a total episode count are both known, the ceiling SHALL be the **lower of the two**. For an anime that has finished airing that resolves to the total; for an anime still airing it resolves to the aired-so-far count, which may be lower than the total. Where only one of the two figures is known, that figure is the ceiling. When neither an aired-so-far count nor a total is known, no upper cap applies. Stored airing data reporting **more** episodes than the published total SHALL NOT raise the ceiling above the total — this happens where AniList groups into one entry what MyAnimeList splits into two, or numbers a season continuously from an earlier one, and it is a difference between sources rather than episodes the user can watch against this entry. The field SHALL clamp to this ceiling live, as the value is typed, so a value above it is never shown even momentarily — not corrected only once confirmed. The plus control SHALL disable at this same ceiling, so it cannot increment past what the field would allow.

This ceiling SHALL be the single one the whole system enforces. The entry editor's own episodes-watched field SHALL use it rather than the total alone, so the editor never accepts a count the server will reject, and the server SHALL apply it to every edit path so no client can save above it. Keeping the ceiling at or below the total also keeps "An entry is Completed only while its progress covers everything available" reachable: a count that could exceed the total would sit permanently past the completion target and never complete.

Confirming with Enter or by moving focus away (including clicking elsewhere on the page) SHALL save the value; Escape SHALL cancel and restore the previous count without saving. An empty or otherwise invalid entry SHALL revert to the stored value rather than saving, regardless of which of these three ways the field is closed. Confirming a value equal to the stored count SHALL NOT issue a save. A save SHALL go through the same entry-edit path as the plus control, so the started-date rule, activity log, debounced MAL sync, and completion prompt all apply, and the progress bar's fill SHALL update together with the count. If the save fails, the displayed count SHALL revert to the stored value.

While a save is in flight, the field and the plus control SHALL be disabled so the same entry cannot be edited twice concurrently.

#### Scenario: Setting a specific episode number
- **WHEN** I click the count on an entry at `3/12`, type `7`, and press Enter
- **THEN** episodes-watched is saved as 7, the count reads `7/12`, and the bar's fill grows to match

#### Scenario: Capped at the total once finished airing
- **WHEN** I try to enter a value above the total for an anime that has finished airing
- **THEN** the value is capped at the total rather than saved as entered

#### Scenario: Capped at episodes aired so far while still airing
- **WHEN** I try to enter a value above the number of episodes aired so far for an anime that is still airing
- **THEN** the value is capped at the aired-so-far count, even when the eventual total (once known) is higher

#### Scenario: Stored airing data above the total does not raise the ceiling
- **WHEN** an anime publishes a total of 12 episodes while stored airing data holds 13 aired episodes, and I try to enter 13
- **THEN** the value is capped at 12, and reaching 12 completes the entry as it would for any other finished anime

#### Scenario: The entry editor shares the same ceiling
- **WHEN** I open the entry editor for a still-airing anime with 3 episodes aired of a published total of 12 and try to enter 12
- **THEN** the editor caps the value at 3, rather than accepting a count the server would reject

#### Scenario: Plus control disables at the aired-so-far ceiling
- **WHEN** an entry's episodes-watched already equals the number of episodes aired so far for a still-airing anime
- **THEN** the plus control is disabled, the same as it would be at the total for a finished show

#### Scenario: Value clamps live while typing
- **WHEN** I type a number above the applicable ceiling into the count field
- **THEN** the displayed value snaps down to the ceiling as I type it, rather than only being corrected once I confirm

#### Scenario: No cap when neither an aired count nor a total is known
- **WHEN** the count reads `watched/?` and no aired-so-far estimate is available either
- **THEN** the value is saved as entered, since no ceiling is known to cap against

#### Scenario: Negatives and non-numbers rejected
- **WHEN** I try to type a negative number or non-numeric text into the count field
- **THEN** the input is rejected and no negative or non-numeric value is ever saved

#### Scenario: Cancelling an edit
- **WHEN** I have typed a new value into the count field and press Escape
- **THEN** the field closes and the previous count is restored without saving

#### Scenario: Empty entry reverts on confirm or on clicking away
- **WHEN** I clear the count field and either press Enter or click elsewhere on the page
- **THEN** the stored value is restored and no save is issued, the same way in both cases

#### Scenario: Unchanged value issues no save
- **WHEN** I open the count field and confirm without changing the number
- **THEN** no save request is issued and the count stays as it was

#### Scenario: Failed save reverts
- **WHEN** I confirm a new count and the save request fails
- **THEN** the displayed count reverts to the stored value
