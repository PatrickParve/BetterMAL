## MODIFIED Requirements

### Requirement: Settings are organised into named groups
The Settings page SHALL present its controls in **named groups** rather than as one flat stack of equally weighted boxes. Every control the page offers SHALL belong to exactly one group, and each group SHALL carry a name and a one-line explanation of what the controls inside it do.

The groups SHALL be, in this order:

1. **Preferences** — the settings that change how the app displays things for me and take effect immediately: the completed/dropped score-reveal setting and the NSFW content filter.
2. **Sync** — the state of the ongoing MyAnimeList sync (pending/retrying count, count held for review, last successful sync) together with the actions that drive it (resync now, run full reconciliation), the changes held for review when any exist, and the pending reconciliation diff when one exists.
3. **Data tools** — the long-running corrective and backfill jobs: the corrective re-sync from MAL, the full airing-date refresh, the build-all-series run, and the single-anime metadata force-refresh.
4. **Transfer** — moving what exists only in this app between my devices as a file: the export action. The group SHALL hold the transfer between devices and nothing else. It SHALL NOT hold a sync action, a corrective or backfill job, or a preference.
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
- **THEN** it holds the export action and no sync action, corrective job, or preference

#### Scenario: Every control survives the regrouping
- **WHEN** I compare the regrouped Settings page against what it offered before
- **THEN** every preference, status figure, and action is still present and still does the same thing

#### Scenario: The reconciliation diff appears in Sync
- **WHEN** a pending reconciliation diff exists
- **THEN** it is shown inside the Sync group with its review actions, and when none exists the group shows no diff section at all

#### Scenario: Held changes appear in Sync
- **WHEN** changes are held for review
- **THEN** they are shown inside the Sync group with their review actions, and when none are held the group shows no held-changes section at all

## ADDED Requirements

### Requirement: The export action states what the file holds
The Transfer group SHALL present the export as an action, in the shape "A preference is visibly not a job" gives every action: a name, an explanation and one button.

Its explanation SHALL be readable before the button is pressed, without hovering, opening or expanding anything. It SHALL state:
- that the file holds my ranking, my chosen anime pictures, my chosen series titles and pictures, and my edit history
- that the file holds nothing that comes from MyAnimeList, not my MyAnimeList connection, and not my display preferences
- that producing it changes nothing in the app and sends nothing anywhere
- that the browser saves the file, and getting it to my other device is up to me

While an export is being produced, the button SHALL be disabled and SHALL say that the export is in progress.

When the export succeeds, the action SHALL name the file it handed over. When it fails, the action SHALL say that the export could not be produced, no file SHALL be handed over, and the button SHALL be enabled again.

#### Scenario: The explanation is on the page
- **WHEN** I open the Settings page and look at the Transfer group
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
