## MODIFIED Requirements

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

#### Scenario: Sync now says it only pushes

- **WHEN** I read the sync-now explanation
- **THEN** it states that it pushes my own unsent edits to MyAnimeList and pulls nothing back

#### Scenario: Explanations are visible without interaction

- **WHEN** I open the Settings page and look at the Sync group
- **THEN** each control's explanation is already on the page, with nothing to hover or expand to read it

#### Scenario: Accepting a diff says it leaves no history

- **WHEN** I read the explanation of accepting a reconciliation diff
- **THEN** it says that nothing it applies is recorded in Latest updates or the full edit history

#### Scenario: Declining a held change says it is recorded

- **WHEN** I read the explanation of the changes held for review
- **THEN** it says that what declining applies is recorded in the edit history, and no explanation on the page describes a change as marked as coming from MyAnimeList

#### Scenario: Nothing about the controls themselves changes

- **WHEN** I compare each sync control against what it did before the explanations were added
- **THEN** each still calls the same operation, with no confirmation step added and no control moved or hidden
