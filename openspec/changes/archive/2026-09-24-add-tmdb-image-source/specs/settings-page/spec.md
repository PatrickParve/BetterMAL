## MODIFIED Requirements

### Requirement: Settings are organised into named groups
The Settings page SHALL present its controls in **named groups** rather than as one flat stack of equally weighted boxes. Every control the page offers SHALL belong to exactly one group, and each group SHALL carry a name and a one-line explanation of what it holds.

The groups SHALL be, in this order:

1. **Preferences** — the settings that change how the app displays things for me and take effect immediately: the completed/dropped score-reveal setting and the NSFW content filter.
2. **Sync** — the state of the ongoing MyAnimeList sync (pending/retrying count, count held for review, last successful sync, and the weekly check) together with the actions that drive it (sync now, run full reconciliation), the MyAnimeList list import's report while it has one, the changes held for review when any exist, and the pending reconciliation diff when one exists.
3. **Data tools** — the long-running corrective and backfill jobs: the full airing-date refresh, the build-all-series run, and the single-anime metadata force-refresh.
4. **Files** — the actions that produce or take a file, in this order: the list backup, the device-transfer export, and the device-transfer import. The group SHALL hold those three and nothing else. It SHALL NOT hold a sync action, a corrective or backfill job, or a preference.
5. **Account** — the MyAnimeList connection state (connected, lost, or not connected) and the re-authorize action.
6. **Credits** — the credit `tmdb-artwork` requires for TMDB: its logo and notice. The group holds no control.

Ordering SHALL run from the cheapest and most reversible to the most expensive:
- instant display preferences first
- the routine sync next
- the minutes-long jobs after that
- the file actions after those, since the import among them cannot be undone
- the account connection last among the groups that hold controls
- the Credits group after it, since it is information and not a control

Grouping SHALL be presentational only. Every control, label, status figure, and action the page offers SHALL still be present, SHALL still call the same endpoint, and SHALL behave identically; the grouping itself SHALL NOT remove a setting, rename one in meaning, or move one behind an extra click.

#### Scenario: Groups are named and ordered
- **WHEN** I open the Settings page
- **THEN** its controls appear under the named groups Preferences, Sync, Data tools, Files, and Account, in that order, each group naming what it holds, followed by the Credits group

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

#### Scenario: Credits closes the page
- **WHEN** I scroll to the end of the Settings page
- **THEN** the last group is Credits, it holds TMDB's logo and notice, and it holds no control
