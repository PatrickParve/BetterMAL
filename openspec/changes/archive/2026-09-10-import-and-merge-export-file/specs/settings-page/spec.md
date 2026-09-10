## MODIFIED Requirements

### Requirement: Settings are organised into named groups
The Settings page SHALL present its controls in **named groups** rather than as one flat stack of equally weighted boxes. Every control the page offers SHALL belong to exactly one group, and each group SHALL carry a name and a one-line explanation of what the controls inside it do.

The groups SHALL be, in this order:

1. **Preferences** — the settings that change how the app displays things for me and take effect immediately: the completed/dropped score-reveal setting and the NSFW content filter.
2. **Sync** — the state of the ongoing MyAnimeList sync (pending/retrying count, count held for review, last successful sync) together with the actions that drive it (resync now, run full reconciliation), the changes held for review when any exist, and the pending reconciliation diff when one exists.
3. **Data tools** — the long-running corrective and backfill jobs: the corrective re-sync from MAL, the full airing-date refresh, the build-all-series run, and the single-anime metadata force-refresh.
4. **Transfer** — moving what exists only in this app between my devices as a file: the export action and the import action. The group SHALL hold the transfer between devices and nothing else. It SHALL NOT hold a sync action, a corrective or backfill job, or a preference.
5. **Account** — the MyAnimeList connection state and the re-authorize action.

Ordering SHALL run from the cheapest and most reversible to the most expensive:
- instant display preferences first
- the routine sync next
- the minutes-long jobs after that
- the transfer between devices after those
- the account connection last

Grouping SHALL be presentational only. Every control, label, status figure, and action the page offers SHALL still be present, SHALL still call the same endpoint, and SHALL behave identically; the grouping itself SHALL NOT remove a setting, rename one in meaning, or move one behind an extra click.

#### Scenario: Groups are named and ordered
- **WHEN** I open the Settings page
- **THEN** its controls appear under the named groups Preferences, Sync, Data tools, Transfer, and Account, in that order, each group naming what it holds

#### Scenario: Transfer holds only the transfer
- **WHEN** I look at the Transfer group
- **THEN** it holds the export action and the import action, and no sync action, corrective job, or preference

#### Scenario: Every control survives the regrouping
- **WHEN** I compare the regrouped Settings page against what it offered before
- **THEN** every preference, status figure, and action is still present and still does the same thing

#### Scenario: The reconciliation diff appears in Sync
- **WHEN** a pending reconciliation diff exists
- **THEN** it is shown inside the Sync group with its review actions, and when none exists the group shows no diff section at all

#### Scenario: Held changes appear in Sync
- **WHEN** changes are held for review
- **THEN** they are shown inside the Sync group with their review actions, and when none are held the group shows no held-changes section at all

### Requirement: Background jobs report progress the same way
Every background job the page can start SHALL report its state in **one shared presentation**, so that a reader learns to read it once. The jobs are:
- the corrective re-sync from MAL
- the full airing-date refresh
- the build-all-series run
- the import from a file

While a job is running, the page SHALL show a proportional progress indicator alongside the processed-of-total counts the underlying operation reports, and SHALL keep both updating while the run is in flight. The job's button SHALL be disabled for the duration and SHALL say that the job is running rather than inviting a second press.

When a job is not running, the page SHALL show its last known outcome where one exists, and SHALL leave the button enabled. The outcome is either:
- completed, with its final counts
- failed, with the counts reached and a pointer to where the failure is recorded

A job that has never run SHALL show no state rather than a zeroed-out one.

The import's outcome SHALL also carry its report (see `device-transfer`, "The import reports what it did"). The report SHALL be shown beneath the shared presentation when the import completes. When the import fails, the page SHALL show the reason in place of the pointer, and state that nothing from the file was applied.

A failed run SHALL be visibly distinct from a completed one rather than differing only in wording.

#### Scenario: A running job shows proportional progress
- **WHEN** a background job is in flight
- **THEN** the page shows a progress indicator filled in proportion to the processed-of-total counts, with those counts beside it, both refreshing while the run continues

#### Scenario: All four jobs report alike
- **WHEN** I compare the corrective re-sync, the airing refresh, the series build, and an import while each is running
- **THEN** all four present their progress in the same form, differing only in wording and figures

#### Scenario: A running job cannot be started twice
- **WHEN** a job is running
- **THEN** its button is disabled and says the job is running

#### Scenario: A finished run keeps its result visible
- **WHEN** a job has finished
- **THEN** its final counts remain visible until another run starts, and its button is enabled again

#### Scenario: A finished import shows its report
- **WHEN** an import has finished
- **THEN** its report is shown beneath its progress presentation, and stays until another import starts or the app restarts

#### Scenario: A failed run is marked as failed
- **WHEN** a job's last run failed
- **THEN** the page marks it as a failure rather than reporting it like a completed run, states how far it got, and points at where the failure is recorded

#### Scenario: A failed import says nothing was applied
- **WHEN** an import's last run failed
- **THEN** the page marks it as a failure, gives the reason, and states that nothing from the file was applied

#### Scenario: A job that never ran shows nothing
- **WHEN** a job has never been started
- **THEN** the page shows no run state for it rather than an empty or zeroed one

## ADDED Requirements

### Requirement: The import action states what it does
The Transfer group SHALL present the import as an action, beside the export, in the shape "A preference is visibly not a job" gives every action.

Its explanation SHALL be readable before a file is given, without hovering, opening or expanding anything. It SHALL state:
- that it merges a file exported on my other device into this one
- how each section merges:
  - the edit history is combined
  - each chosen picture and title goes to whichever device changed it last
  - the ranking is replaced whole by whichever device arranged it last
- that it asks nothing before applying and cannot be undone, and that exporting this device first keeps a copy of what this device holds
- that it runs in the background, and can take minutes when anime have to be fetched from MyAnimeList

The action SHALL accept a file chosen with a file picker, and a file dropped onto it. While a file is held over the action, it SHALL show that it will take a dropped file.

While an import runs, the action SHALL NOT accept another file, and its button SHALL say that an import is running.

A refused file SHALL be reported in the action, in the words of the refusal. The action SHALL then accept a file again.

When the import finishes, its report SHALL be shown in the action, together with the device name and export time of the file it is for.

#### Scenario: The explanation is on the page
- **WHEN** I open the Settings page and look at the Transfer group
- **THEN** the import action's explanation already states what it merges and how, that it cannot be undone, that exporting first keeps a copy, and that it can take minutes, with nothing to hover over or expand

#### Scenario: Choosing a file starts an import
- **WHEN** I choose an export file with the import action's file picker
- **THEN** the import starts, and the action shows its progress

#### Scenario: Dropping a file starts an import
- **WHEN** I drag an export file over the import action
- **THEN** the action shows that it will take the file, and dropping it starts the import

#### Scenario: A refused file says why
- **WHEN** I give the import action a file that is refused
- **THEN** the action shows the refusal's reason, nothing starts, and I can give it another file

#### Scenario: A running import takes no second file
- **WHEN** an import is running
- **THEN** the import button is disabled and says an import is running, and a dropped file is not taken

#### Scenario: The report is shown when the import finishes
- **WHEN** an import finishes
- **THEN** the action shows its report and names the device and export time of the file it came from
