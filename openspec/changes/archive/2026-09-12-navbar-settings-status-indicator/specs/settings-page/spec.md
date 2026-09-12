## ADDED Requirements

### Requirement: The page reports the outcomes it shows as seen

The Settings page SHALL report a job's ended outcome as seen **because it is showing it** (see `background-jobs`, "A job's outcome can be reported as seen"):

- when the page opens, every outcome already on it;
- while the page is open, each further outcome as the job ends.

It SHALL do the same for a failed weekly check, whose reason it shows in the "Weekly check" line.

Reporting SHALL change nothing about what the page shows: an outcome reported as seen SHALL stay on the page with its counts, reason and report, until its job runs again or the application restarts.

The page SHALL report each outcome **once**, rather than repeating the report while it keeps showing it. A report that fails SHALL be left for the page's next read to try again, and SHALL NOT be announced.

This is what clears the navbar's indicator for an ended job, in this browser and in every other on the device (see `navbar-settings-status`).

#### Scenario: Opening the page reports what is already on it
- **WHEN** a job has failed unseen and I open the Settings page
- **THEN** its failure and reason are shown, and its outcome is reported as seen

#### Scenario: A job that ends while I watch is reported at once
- **WHEN** I start a job from the Settings page and stay there until it ends
- **THEN** its outcome is reported as seen as the page shows it, and the navbar's indicator never appears for it

#### Scenario: A failed weekly check is reported
- **WHEN** I open the Settings page while the "Weekly check" line reports a failure that has not been seen
- **THEN** that outcome is reported as seen, and the line still reports the failure and its reason

#### Scenario: The outcome stays on the page
- **WHEN** an outcome has been reported as seen and I stay on the page
- **THEN** the job's counts, reason and report are still shown

#### Scenario: A failed report is silent
- **WHEN** the report cannot reach the server
- **THEN** nothing is said, the outcome stays shown, and the report is tried again on the page's next read

## MODIFIED Requirements

### Requirement: The sync readout separates what is retrying from what is waiting on me

The Sync group's status readout SHALL report the number of entries pending and retrying **separately** from the number held for review, since one resolves itself and the other never will until I decide it. The pending/retrying figure SHALL NOT count held items.

The held figure SHALL agree with what the review below it actually lists. An item that cleared itself because MyAnimeList already held its values SHALL NOT be counted as awaiting my decision.

The pending/retrying figure and the last successful sync SHALL come from the same shared read the page takes every job's state from (see `background-jobs`, "Job and connection state are read together"), rather than from a read of their own. They SHALL therefore keep up with that read while the page is open, including edits made elsewhere in the app, without needing a job to end or the page to be reopened. Until that read has arrived, the page SHALL show its loading state rather than a readout it cannot fill.

#### Scenario: The two counts are distinct
- **WHEN** two entries are held for review and one is retrying in the background
- **THEN** the readout reports one pending/retrying and two held for review, as separate figures

#### Scenario: Nothing held reads as it did before
- **WHEN** nothing is held for review
- **THEN** the readout reports the pending/retrying count and the last successful sync exactly as before

#### Scenario: The count matches the list
- **WHEN** one of three held items turns out to match MyAnimeList's current values and clears itself
- **THEN** the readout reports two held for review, and the section lists those same two

#### Scenario: An edit made elsewhere shows up
- **WHEN** I edit an entry on another page and come back to Settings with the edit still waiting to be sent
- **THEN** the pending/retrying figure counts it, without my reloading the page
