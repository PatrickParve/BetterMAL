## MODIFIED Requirements

### Requirement: Settings are organised into named groups
The Settings page SHALL present its controls in **named groups** rather than as one flat stack of equally weighted boxes. Every control the page offers SHALL belong to exactly one group, and each group SHALL carry a name and a one-line explanation of what the controls inside it do.

The groups SHALL be, in this order:

1. **Preferences** — the settings that change how the app displays things for me and take effect immediately: the completed/dropped score-reveal setting and the NSFW content filter.
2. **Sync** — the state of the ongoing MyAnimeList sync (pending/retrying count, count held for review, last successful sync) together with the actions that drive it (resync now, run full reconciliation), the changes held for review when any exist, and the pending reconciliation diff when one exists.
3. **Data tools** — the long-running corrective and backfill jobs: the corrective re-sync from MAL, the full airing-date refresh, the build-all-series run, and the single-anime metadata force-refresh.
4. **Files** — the actions that produce or take a file, in this order: the list backup, the device-transfer export, and the device-transfer import. The group SHALL hold those three and nothing else. It SHALL NOT hold a sync action, a corrective or backfill job, or a preference.
5. **Account** — the MyAnimeList connection state and the re-authorize action.

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

## ADDED Requirements

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
