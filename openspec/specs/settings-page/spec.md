# settings-page Specification

## Purpose
The settings-page capability governs how the app's Settings page is organised and presented: the grouping of its controls into named groups ordered from cheapest to most expensive, the visual distinction between an instant preference and a control that starts a long-running background job, the shared shape a background job's progress and outcome are reported in, and the page's overall layout. It exists so that a page holding both a display toggle and a minutes-long airing-date refresh reads as two different kinds of thing rather than as nine visually identical boxes, without changing what any setting, status figure, or action actually does.

## Requirements

### Requirement: Settings are organised into named groups
The Settings page SHALL present its controls in **named groups** rather than as one flat stack of equally weighted boxes. Every control the page offers SHALL belong to exactly one group, and each group SHALL carry a name and a one-line explanation of what the controls inside it do.

The groups SHALL be, in this order:

1. **Preferences** — the settings that change how the app displays things for me and take effect immediately: the completed/dropped score-reveal setting and the NSFW content filter.
2. **Sync** — the state of the ongoing MyAnimeList sync (pending/retrying count, count held for review, last successful sync, and the weekly check) together with the actions that drive it (sync now, run full reconciliation), the MyAnimeList list import's report while it has one, the changes held for review when any exist, and the pending reconciliation diff when one exists.
3. **Data tools** — the long-running corrective and backfill jobs: the full airing-date refresh, the build-all-series run, and the single-anime metadata force-refresh.
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

### Requirement: The list backup action states what the file holds
The Files group SHALL present the list backup as its first action, in the shape "A preference is visibly not a job" gives every action: a name, an explanation and one button.

Its explanation SHALL be readable before the button is pressed, without hovering, opening or expanding anything. It SHALL state:
- that the file holds every anime in my list, with its status, progress, score, dates and rewatch count
- that it keeps Rewatching as Rewatching, which MyAnimeList cannot hold
- that it holds nothing else: not my ranking, pictures or edit history, and not my MyAnimeList connection
- that it is a copy to keep, which the app never imports, and not the file for my other device
- that producing it changes nothing in the app and sends nothing anywhere, and that the browser saves the file

While a backup is being produced, the button SHALL be disabled and SHALL say that the backup is in progress.

When the backup succeeds, the action SHALL name the file it handed over. When it fails, the action SHALL say that the backup could not be produced, no file SHALL be handed over, and the button SHALL be enabled again.

The backup action and the export action SHALL report separately: the outcome of one SHALL NOT be shown on the other.

#### Scenario: The explanation is on the page
- **WHEN** I open the Settings page and look at the Files group
- **THEN** the list backup action comes first, and its explanation already states what the file holds, that it keeps Rewatching, that the app never imports it, and that nothing is changed or sent, with nothing to hover over or expand

#### Scenario: A finished backup names its file
- **WHEN** I press the backup button and the backup succeeds
- **THEN** the browser saves the file, and the backup action names the file it saved

#### Scenario: A failed backup says so
- **WHEN** I press the backup button and the backend cannot produce the file
- **THEN** no file is saved, the backup action says the backup could not be produced, and the button can be pressed again

#### Scenario: A backup in progress cannot be started twice
- **WHEN** a backup is being produced
- **THEN** the backup button is disabled and says that the backup is in progress

#### Scenario: Backup and export report apart
- **WHEN** I produce a backup and then look at the export action
- **THEN** the export action shows no file name or error from the backup

### Requirement: The export action states what the file holds
The Files group SHALL present the export as an action, in the shape "A preference is visibly not a job" gives every action: a name, an explanation and one button.

Its explanation SHALL be readable before the button is pressed, without hovering, opening or expanding anything. It SHALL state:
- that the file holds my ranking, my chosen anime pictures, my chosen series titles and pictures, and my edit history
- that the file holds nothing that comes from MyAnimeList, not my MyAnimeList connection, and not my display preferences
- that producing it changes nothing in the app and sends nothing anywhere
- that the browser saves the file, and getting it to my other device is up to me

While an export is being produced, the button SHALL be disabled and SHALL say that the export is in progress.

When the export succeeds, the action SHALL name the file it handed over. When it fails, the action SHALL say that the export could not be produced, no file SHALL be handed over, and the button SHALL be enabled again.

#### Scenario: The explanation is on the page
- **WHEN** I open the Settings page and look at the Files group
- **THEN** the export action's explanation already states what the file holds, what it leaves out, and that nothing is changed or sent, with nothing to hover over or expand

#### Scenario: A finished export names its file
- **WHEN** I press the export button and the export succeeds
- **THEN** the browser saves the file, and the action names the file it saved

#### Scenario: A failed export says so
- **WHEN** I press the export button and the backend cannot produce the file
- **THEN** no file is saved, the action says the export could not be produced, and the button can be pressed again

#### Scenario: An export in progress cannot be started twice
- **WHEN** an export is being produced
- **THEN** the export button is disabled and says that the export is in progress

### Requirement: The import action states what it does
The Files group SHALL present the import as an action, beside the export, in the shape "A preference is visibly not a job" gives every action.

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
- **WHEN** I open the Settings page and look at the Files group
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

### Requirement: A preference is visibly not a job
The page SHALL make an instant preference visibly distinct from an action that starts work.

A **preference** SHALL be presented as a compact row carrying its control, its name, and its explanation, taking effect the moment it is changed with no confirmation step and no button to press.

An **action** SHALL be presented with its name, an explanation of what it does and roughly how long it takes, its current or last-known run state where the underlying operation reports one, and a button that starts it. An action that runs in the background and can take minutes SHALL say so in its explanation.

The two SHALL be distinguishable at a glance, without reading the explanations — a reader skimming the page SHALL be able to tell which entries change a display setting instantly and which start work.

#### Scenario: A preference row reads as a preference
- **WHEN** I look at the score-reveal and NSFW settings
- **THEN** each is a compact row with its toggle, name, and explanation, and changing it takes effect immediately with nothing to confirm

#### Scenario: An action reads as an action
- **WHEN** I look at the airing-date refresh and the build-all-series entries
- **THEN** each states what it does, that it runs in the background and can take a while, its last known run state, and offers a single button that starts it

#### Scenario: The two kinds are tellable apart without reading
- **WHEN** I skim the page without reading the explanations
- **THEN** the preference rows and the action entries are visibly different kinds of entry

### Requirement: Background jobs report progress the same way
Every background job the page shows SHALL report its state in **one shared presentation**, so that a reader learns to read it once. The jobs are:
- the MyAnimeList list import, while it has something to report (see "The MyAnimeList list import is reported while it has something to report")
- sync now
- run full reconciliation
- accepting or declining every held change
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

### Requirement: A job starts on the first press

Pressing a job's button once SHALL be enough to start it and show it. The page SHALL:
- show the job's progress presentation as soon as the start is answered, without a second press
- keep it updating from then on

This covers every job with a button:
- sync now
- run full reconciliation
- accept all
- decline all
- the full airing-date refresh ("Airing dates")
- build all series
- the import from a file

While the start request is waiting for its answer, the button SHALL already be disabled.

A press that arrives while the job is starting or running — from this page or another — SHALL start nothing (see `background-jobs`, "A job is started once").

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

### Requirement: Every sync control states what it will do

Each control on the Settings page that touches my list SHALL carry an explanation of what pressing it does, readable **before** it is pressed and without opening, hovering, or expanding anything. The controls this covers SHALL be: sync now, run full reconciliation, the accept and decline actions on a pending reconciliation diff, and the accept and decline actions on a change held for review.

Each explanation SHALL state, in plain terms, what the control fetches or compares, **whether it shows me the differences for review before applying anything or applies them immediately**, and what it writes when it does apply. Where a control creates list entries for anime not tracked locally, its explanation SHALL say so.

Specifically:

- **Sync now** SHALL say that it pushes my own unsent edits to MyAnimeList straight away instead of waiting, and that it sends nothing else and changes nothing locally. Where anything is held for review, it SHALL also say that held changes are not among what it pushes and are waiting on my decision.
- **Run full reconciliation** SHALL say that it fetches my current MyAnimeList list, compares it against what is stored locally, and presents the differences for me to accept or decline — changing nothing until I do.
- **Accept** SHALL say that it applies exactly the differences shown and nothing else on my list is touched. **Decline** SHALL say that it discards them, applying nothing.
- **Accepting a held change** SHALL say that it sends that anime's stored values to MyAnimeList now, overwriting what MyAnimeList holds for it. **Declining a held change** SHALL say that it discards the unsent change and takes MyAnimeList's current value for that anime instead; where MyAnimeList holds no entry for that anime, it SHALL say instead that declining removes the anime from my list locally.

Each explanation of a control that applies changes SHALL also say whether what it applies is recorded in my history. The explanation of accepting a reconciliation diff SHALL say that nothing it applies is recorded in Latest updates or the full edit history. The explanation of the held-change review SHALL say that what declining applies is recorded in the edit history, like any other change I make in the app. No explanation SHALL describe a change as marked as coming from MyAnimeList.

An explanation SHALL describe what the control does today rather than what it is expected to do: a control that applies changes without review SHALL NOT be described in terms that suggest a review step exists.

These explanations SHALL be presentational. No control SHALL gain a confirmation step, change what it does, move group, or be placed behind an extra click.

#### Scenario: The review actions say what they apply

- **WHEN** a pending reconciliation diff is shown with its accept and decline actions
- **THEN** the page states that accepting applies exactly the differences listed and touches nothing else, and that declining applies none of them

#### Scenario: Sync now is not confused with a re-sync

- **WHEN** I read the sync-now explanation
- **THEN** it states that it pushes my own unsent edits to MyAnimeList and pulls nothing back

#### Scenario: Explanations are visible without interaction

- **WHEN** I open the Settings page and look at the Sync group
- **THEN** each control's explanation is already on the page, with nothing to hover or expand to read it

#### Scenario: The sync paths say they leave no history

- **WHEN** I read the explanation of accepting a reconciliation diff
- **THEN** it says that nothing it applies is recorded in Latest updates or the full edit history

#### Scenario: Declining a held change says it is recorded

- **WHEN** I read the explanation of the changes held for review
- **THEN** it says that what declining applies is recorded in the edit history, and no explanation on the page describes a change as marked as coming from MyAnimeList

#### Scenario: Nothing about the controls themselves changes

- **WHEN** I compare each sync control against what it did before the explanations were added
- **THEN** each still calls the same operation, with no confirmation step added and no control moved or hidden

### Requirement: The force-refresh picker names anime in English

The Settings page's force-refresh anime picker SHALL name every anime by the same rule the rest of the app uses: the English title where one is known, falling back to the stored MyAnimeList title where none is. No surface of this control SHALL show a romaji title while the same anime is named in English everywhere else.

This SHALL cover every place the control names an anime: each row of its search dropdown, the text it writes into the search field when a row is picked, and the messages it shows after a refresh succeeds or fails.

The control's behaviour SHALL be unchanged. It SHALL still search the same way, still offer only anime rows (never series), and still refresh the anime by its id — the title it displays is a label, never what identifies the anime being refreshed.

#### Scenario: A dropdown row is named in English
- **WHEN** I type into the force-refresh picker and a matching anime has a known English title
- **THEN** its row shows that English title rather than the romaji one

#### Scenario: Picking a row fills the field in English
- **WHEN** I pick that row
- **THEN** the search field shows the same English title the row showed

#### Scenario: The result message uses the same name
- **WHEN** the refresh completes, or fails
- **THEN** the message names the anime exactly as the row and the field did

#### Scenario: An anime with no English title
- **WHEN** a matching anime has no English title
- **THEN** it is shown under its stored MyAnimeList title, in the row, the field, and the message alike

#### Scenario: The refresh itself is unchanged
- **WHEN** I press Refresh after picking a row
- **THEN** the same anime is refreshed as before, identified by its id rather than by the displayed title

### Requirement: Settings layout
The Settings page SHALL be laid out as a single readable column, wide enough that an action's explanation, its run state, and its button sit comfortably without the explanation collapsing to a narrow ribbon of text, and capped so that a line of explanation never runs the full width of a wide window.

The page SHALL remain usable at narrow window widths: no group, action row, or status readout SHALL overflow horizontally or force the page to scroll sideways, and where a row's control cannot share a line with its label the control SHALL wrap beneath it rather than shrink out of reach.

The page SHALL keep the standalone header block the `page-header-design` capability requires of it.

#### Scenario: A comfortable reading width
- **WHEN** I open Settings in a wide window
- **THEN** its groups sit in one column of comfortable reading width rather than stretching across the whole window

#### Scenario: Narrow windows do not break the page
- **WHEN** I narrow the window
- **THEN** every group, row, and status readout stays inside the page with no sideways scrolling, controls wrapping beneath their labels where needed

#### Scenario: The page header is unchanged
- **WHEN** the Settings page renders
- **THEN** its title still appears as the standalone header block with its subtitle, exactly as before the reorganisation
