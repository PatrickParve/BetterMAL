## MODIFIED Requirements

### Requirement: Settings are organised into named groups
The Settings page SHALL present its controls in **named groups** rather than as one flat stack of equally weighted boxes. Every control the page offers SHALL belong to exactly one group, and each group SHALL carry a name and a one-line explanation of what the controls inside it do.

The groups SHALL be, in this order:

1. **Preferences** — the settings that change how the app displays things for me and take effect immediately: the completed/dropped score-reveal setting and the NSFW content filter.
2. **Sync** — the state of the ongoing MyAnimeList sync (pending/retrying count, count held for review, last successful sync, and the weekly check) together with the actions that drive it (sync now, run full reconciliation), the MyAnimeList list import's report while it has one, the changes held for review when any exist, and the pending reconciliation diff when one exists.
3. **Data tools** — the long-running corrective and backfill jobs: the corrective re-sync from MAL, the full airing-date refresh, the build-all-series run, and the single-anime metadata force-refresh.
4. **Files** — the actions that produce or take a file, in this order: the list backup, the device-transfer export, and the device-transfer import. The group SHALL hold those three and nothing else. It SHALL NOT hold a sync action, a corrective or backfill job, or a preference.
5. **Account** — the MyAnimeList connection state (connected, lost, or not connected) and the re-authorize action.

Ordering SHALL run from the cheapest and most reversible to the most expensive:
- instant display preferences first
- the routine sync next
- the minutes-long jobs after that
- the file actions after those, since the import among them cannot be undone
- the account connection last

Grouping SHALL be presentational only. Every control, label, status figure, and action the page offers SHALL still be present, SHALL still call the same endpoint, and SHALL behave identically; the grouping itself SHALL NOT remove a setting, rename one in meaning, or move one behind an extra click.

#### Scenario: Groups are named and ordered
- **WHEN** I open the Settings page
- **THEN** its controls appear under the named groups Preferences, Sync, Data tools, Files, and Account, in that order, each group naming what it holds

#### Scenario: Files holds only the file actions
- **WHEN** I look at the Files group
- **THEN** it holds the list backup action, the export action and the import action, in that order, and no sync action, corrective job, or preference

#### Scenario: Every control survives the regrouping
- **WHEN** I compare the regrouped Settings page against what it offered before
- **THEN** every preference, status figure, and action is still present and still does the same thing

#### Scenario: The reconciliation diff appears in Sync
- **WHEN** a pending reconciliation diff exists
- **THEN** it is shown inside the Sync group with its review actions, and when none exists the group shows no diff section at all

#### Scenario: Held changes appear in Sync
- **WHEN** changes are held for review
- **THEN** they are shown inside the Sync group with their review actions, and when none are held the group shows no held-changes section at all

#### Scenario: The list import report appears in Sync
- **WHEN** the MyAnimeList list import has something to report
- **THEN** its report is shown inside the Sync group, and when it has nothing to report the group shows no import report at all

#### Scenario: The connection state appears in Account
- **WHEN** I look at the Account group
- **THEN** it says whether the MyAnimeList connection is connected, lost, or not connected, next to the re-authorize action

### Requirement: Changes held for review are surfaced on the Settings page

Where any change is held for review — an unsent edit or a queued removal that was already waiting when the app started — the Settings page SHALL surface it in the **Sync** group, above the reconciliation diff, as a section naming what it is: changes from a previous session that have not been sent to MyAnimeList and are waiting on my decision.

Where **nothing** is held, the section SHALL NOT appear at all. It SHALL NOT render as an empty list, a zero count, or a collapsed heading. An item that turned out to have nothing left to send — MyAnimeList already holding the values it would have pushed — SHALL never appear in the section, so the page never offers a decision that would change nothing.

Each held item SHALL be listed as its own row, carrying the anime's picture and its name, whether it is an edit or a removal, what the unsent change was and when it was made, the values that would be sent, and MyAnimeList's current values for that anime where they could be read. A row whose MyAnimeList side could not be read SHALL say so rather than being omitted.

Each row SHALL carry its **own** accept and decline actions, so held items are decided one at a time. The section SHALL also offer an accept-all and a decline-all action over every item it lists.

While a decision is being applied its actions SHALL be disabled rather than pressable a second time, and a decision that fails SHALL leave the item listed and report the failure in the section, in the same way the reconciliation diff reports a failed accept.

Accept-all and decline-all SHALL run in the background, as one job between them (see `mal-write-sync`, "Deciding every held change runs in the background").

While that job runs:
- the section SHALL show its progress, as the number decided out of the number held, in the shared presentation of "Background jobs report progress the same way"
- every accept and decline action in the section, the row actions included, SHALL be disabled
- this SHALL hold in every browser, since the state is read from the server

When the job ends with items it could not decide, the section SHALL say how many are still held, and those items SHALL stay listed.

#### Scenario: Held changes are listed with their context
- **WHEN** I open Settings with two changes held from a previous session
- **THEN** the Sync group lists both, each naming its anime, what changed and when, what would be sent, and what MyAnimeList currently holds

#### Scenario: Nothing held shows nothing
- **WHEN** I open Settings with nothing held
- **THEN** the Sync group shows no held-changes section at all

#### Scenario: Items are decided individually
- **WHEN** I accept one held item
- **THEN** only that item is acted on and the rest stay listed

#### Scenario: A failed decision keeps the item
- **WHEN** a decision on a held item fails
- **THEN** the item is still listed and the section reports that the decision could not be applied

#### Scenario: The last decision closes the section
- **WHEN** I decide the last held item
- **THEN** the held-changes section disappears from the page

#### Scenario: Accept all shows its progress
- **WHEN** I press accept all with five items held
- **THEN** the section shows a progress bar counting the items decided out of five, and every accept and decline action in it is disabled until the run ends

#### Scenario: Accept all is disabled in another browser too
- **WHEN** accept all is running and I open Settings in another browser
- **THEN** that browser shows the same progress, with every accept and decline action disabled

#### Scenario: Items that stayed held are reported
- **WHEN** decline all ends with two items it could not decide
- **THEN** the section says two are still held, and those two stay listed

### Requirement: Background jobs report progress the same way
Every background job the page shows SHALL report its state in **one shared presentation**, so that a reader learns to read it once. The jobs are:
- the MyAnimeList list import, while it has something to report (see "The MyAnimeList list import is reported while it has something to report")
- sync now
- run full reconciliation
- accepting or declining every held change
- the corrective re-sync from MAL
- the full airing-date refresh
- the build-all-series run
- the import from a file

While a job is running, the page SHALL show a proportional progress indicator alongside the processed-of-total counts the underlying operation reports, and SHALL keep both updating while the run is in flight. The job's button, where it has one, SHALL be disabled for the duration and SHALL say that the job is running rather than inviting a second press.

Where a running job does not yet know its total:
- the indicator SHALL move continuously rather than being filled in proportion
- the counts SHALL show what the job has counted so far, where it counts anything, and otherwise that it is starting
- it SHALL NOT show a zero total or an empty bar that looks like no progress

Where motion is reduced by the reader's system setting, the indicator SHALL stay still while remaining distinct from a proportional one.

The page SHALL take every job's state from the server, so that:
- leaving and returning shows the same run
- another browser shows the same run
- a job started from elsewhere, or by the app itself, appears without a reload

When a job is not running, the page SHALL show its last known outcome where one exists, and SHALL leave the button enabled. The outcome is either:
- completed, with its final counts
- failed, with the counts reached and the job's reason; where the job knows no reason, it points to where the failure is recorded

A job that has never run SHALL show no state rather than a zeroed-out one.

The import's outcome SHALL also carry its report (see `device-transfer`, "The import reports what it did"). The report SHALL be shown beneath the shared presentation when the import completes. When the import fails, the page SHALL show the reason and state that nothing from the file was applied.

A failed run SHALL be visibly distinct from a completed one rather than differing only in wording.

#### Scenario: A running job shows proportional progress
- **WHEN** a background job with a known total is in flight
- **THEN** the page shows a progress indicator filled in proportion to the processed-of-total counts, with those counts beside it, both refreshing while the run continues

#### Scenario: A job without a total shows a moving bar
- **WHEN** run full reconciliation is reading my MyAnimeList list
- **THEN** the page shows a moving indicator and the number of anime read so far, and no total

#### Scenario: A job that is starting says so
- **WHEN** a job has started but has counted nothing and knows no total
- **THEN** the page shows a moving indicator and says it is starting

#### Scenario: All jobs report alike
- **WHEN** I compare any two of the jobs while each is running
- **THEN** they present their progress in the same form, differing only in wording and figures

#### Scenario: A running job cannot be started twice
- **WHEN** a job is running
- **THEN** its button is disabled and says the job is running

#### Scenario: Another browser shows the same run
- **WHEN** a job is running and I open Settings in another browser
- **THEN** that browser shows the same progress and its button is disabled too

#### Scenario: A finished run keeps its result visible
- **WHEN** a job has finished
- **THEN** its final counts remain visible until another run starts, and its button is enabled again

#### Scenario: A finished import shows its report
- **WHEN** an import has finished
- **THEN** its report is shown beneath its progress presentation, and stays until another import starts or the app restarts

#### Scenario: A failed run is marked as failed
- **WHEN** a job's last run failed
- **THEN** the page marks it as a failure rather than reporting it like a completed run, states how far it got, and gives the job's reason

#### Scenario: A failed import says nothing was applied
- **WHEN** an import's last run failed
- **THEN** the page marks it as a failure, gives the reason, and states that nothing from the file was applied

#### Scenario: A job that never ran shows nothing
- **WHEN** a job has never been started
- **THEN** the page shows no run state for it rather than an empty or zeroed one

## ADDED Requirements

### Requirement: A job starts on the first press

Pressing a job's button once SHALL be enough to start it and show it. The page SHALL:
- show the job's progress presentation as soon as the start is answered, without a second press
- keep it updating from then on

This covers every job with a button:
- sync now
- run full reconciliation
- accept all
- decline all
- the corrective re-sync ("Correct imported data")
- the full airing-date refresh ("Airing dates")
- build all series
- the import from a file

While the start request is waiting for its answer, the button SHALL already be disabled.

A press that arrives while the job is starting or running — from this page or another — SHALL start nothing (see `background-jobs`, "A job is started once").

#### Scenario: Correct imported data shows its bar at once
- **WHEN** I press "Run corrective re-sync" once
- **THEN** its progress bar appears straight away, without a second press

#### Scenario: Airing dates shows its bar at once
- **WHEN** I press "Refresh all airing dates" once
- **THEN** its progress bar appears straight away, without a second press

#### Scenario: A quick second press starts nothing
- **WHEN** I press a job's button twice in quick succession
- **THEN** one run starts, and the second press has no effect

### Requirement: The MyAnimeList list import is reported while it has something to report

The Sync group SHALL show the MyAnimeList list import's report only while the import has something to report, as `initial-import`, "Visible import progress indicator" defines. While it has nothing, the page SHALL show nothing for it: no heading, no empty bar, no "complete" line.

Where it is shown, the report SHALL:
- name the import and say what it does: brings in anime on my MyAnimeList list that this device does not have yet, when the app starts and after re-authorizing
- show its progress in the shared presentation, counting anime, with a total that counts only the anime it is bringing in
- when it failed, give the reason, and say when it will try again, or that it will try again when the app next starts

It SHALL offer no button, since the import is started by the app and not by me.

#### Scenario: A start with nothing new shows nothing
- **WHEN** the app starts and my MyAnimeList list has nothing this device lacks
- **THEN** the Sync group shows nothing for the list import

#### Scenario: Anime added on my other device are shown coming in
- **WHEN** the app starts and my MyAnimeList list has four anime this device lacks
- **THEN** the Sync group shows the list import with a progress bar out of four, and then how it ended

#### Scenario: A failed import says when it tries again
- **WHEN** the list import ends with anime it could not fetch and another try is planned
- **THEN** the report marks it as failed, says how many could not be fetched, and says when it will try again

### Requirement: The weekly check is reported in one line

The Sync group's readout SHALL carry one line for the weekly MyAnimeList check, beside "Last successful sync". The line SHALL say when the check last ran and:
- that it found no problems, where that run succeeded
- that it failed, with its reason, where that run failed
- only when it ran, where that run's ending was not recorded
- that it has not run yet, where it never has

The line SHALL NOT show progress, and the weekly check SHALL never show a progress bar. What the check found is already shown as the pending reconciliation diff.

#### Scenario: A successful weekly check
- **WHEN** the weekly check last ran on Monday and succeeded
- **THEN** the readout's weekly line gives Monday's time and says there were no problems

#### Scenario: A failed weekly check
- **WHEN** the weekly check last failed because MyAnimeList could not be reached
- **THEN** the weekly line gives its time and says it failed because MyAnimeList couldn't be reached

#### Scenario: The weekly check never shows a bar
- **WHEN** the weekly check is running while I have Settings open
- **THEN** no progress bar appears for it, and "Run full reconciliation" shows no progress from it

### Requirement: The Account section explains a lost connection

Where the MyAnimeList connection is **lost** (see `mal-api-integration`), the Account group SHALL say so plainly, in place of "Connected". It SHALL state:
- that the connection to MyAnimeList was lost, and when that was noticed
- that my changes are not being sent to MyAnimeList
- that anime added on MyAnimeList elsewhere are not being brought in
- that I need to re-authorize to reconnect

The message SHALL be styled as a problem, not as a neutral status. The re-authorize action SHALL stay where it is.

Where the connection is **connected**, the group SHALL say "Connected."; where **not connected**, "Not connected.".

Re-authorizing SHALL bring the group back to "Connected." without any other step.

#### Scenario: A lost connection is explained
- **WHEN** MyAnimeList has refused the app's login and I open Settings
- **THEN** the Account group says the connection was lost and when, that my changes aren't being sent, and that I need to re-authorize, and it no longer says "Connected"

#### Scenario: An outage is not a lost connection
- **WHEN** MyAnimeList is unreachable but has not refused the login
- **THEN** the Account group still says "Connected."

#### Scenario: Re-authorizing clears the message
- **WHEN** I re-authorize after the connection was lost
- **THEN** the Account group says "Connected." again
