# settings-page Specification

## Purpose
The settings-page capability governs how the app's Settings page is organised and presented: the grouping of its controls into named groups ordered from cheapest to most expensive, the visual distinction between an instant preference and a control that starts a long-running background job, the shared shape a background job's progress and outcome are reported in, and the page's overall layout. It exists so that a page holding both a display toggle and a several-minute MAL re-sync reads as two different kinds of thing rather than as nine visually identical boxes, without changing what any setting, status figure, or action actually does.

## Requirements

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

### Requirement: Changes held for review are surfaced on the Settings page

Where any change is held for review — an unsent edit or a queued removal that was already waiting when the app started — the Settings page SHALL surface it in the **Sync** group, above the reconciliation diff, as a section naming what it is: changes from a previous session that have not been sent to MyAnimeList and are waiting on my decision.

Where **nothing** is held, the section SHALL NOT appear at all. It SHALL NOT render as an empty list, a zero count, or a collapsed heading. An item that turned out to have nothing left to send — MyAnimeList already holding the values it would have pushed — SHALL never appear in the section, so the page never offers a decision that would change nothing.

Each held item SHALL be listed as its own row, carrying the anime's picture and its name, whether it is an edit or a removal, what the unsent change was and when it was made, the values that would be sent, and MyAnimeList's current values for that anime where they could be read. A row whose MyAnimeList side could not be read SHALL say so rather than being omitted.

Each row SHALL carry its **own** accept and decline actions, so held items are decided one at a time. The section SHALL also offer an accept-all and a decline-all action over every item it lists.

While a decision is being applied its actions SHALL be disabled rather than pressable a second time, and a decision that fails SHALL leave the item listed and report the failure in the section, in the same way the reconciliation diff reports a failed accept.

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

### Requirement: The sync readout separates what is retrying from what is waiting on me

The Sync group's status readout SHALL report the number of entries pending and retrying **separately** from the number held for review, since one resolves itself and the other never will until I decide it. The pending/retrying figure SHALL NOT count held items.

The held figure SHALL agree with what the review below it actually lists. An item that cleared itself because MyAnimeList already held its values SHALL NOT be counted as awaiting my decision.

#### Scenario: The two counts are distinct
- **WHEN** two entries are held for review and one is retrying in the background
- **THEN** the readout reports one pending/retrying and two held for review, as separate figures

#### Scenario: Nothing held reads as it did before
- **WHEN** nothing is held for review
- **THEN** the readout reports the pending/retrying count and the last successful sync exactly as before

#### Scenario: The count matches the list
- **WHEN** one of three held items turns out to match MyAnimeList's current values and clears itself
- **THEN** the readout reports two held for review, and the section lists those same two

### Requirement: A preference is visibly not a job
The page SHALL make an instant preference visibly distinct from an action that starts work.

A **preference** SHALL be presented as a compact row carrying its control, its name, and its explanation, taking effect the moment it is changed with no confirmation step and no button to press.

An **action** SHALL be presented with its name, an explanation of what it does and roughly how long it takes, its current or last-known run state where the underlying operation reports one, and a button that starts it. An action that runs in the background and can take minutes SHALL say so in its explanation.

The two SHALL be distinguishable at a glance, without reading the explanations — a reader skimming the page SHALL be able to tell which entries change a display setting instantly and which start work.

#### Scenario: A preference row reads as a preference
- **WHEN** I look at the score-reveal and NSFW settings
- **THEN** each is a compact row with its toggle, name, and explanation, and changing it takes effect immediately with nothing to confirm

#### Scenario: An action reads as an action
- **WHEN** I look at the corrective re-sync, the airing-date refresh, and the build-all-series entries
- **THEN** each states what it does, that it runs in the background and can take a while, its last known run state, and offers a single button that starts it

#### Scenario: The two kinds are tellable apart without reading
- **WHEN** I skim the page without reading the explanations
- **THEN** the preference rows and the action entries are visibly different kinds of entry

### Requirement: Background jobs report progress the same way
Every background job the page can start — the corrective re-sync from MAL, the full airing-date refresh, and the build-all-series run — SHALL report its state in **one shared presentation**, so a reader learns to read it once.

While a job is running, the page SHALL show a proportional progress indicator alongside the processed-of-total counts the underlying operation reports, and SHALL keep both updating while the run is in flight. The job's button SHALL be disabled for the duration and SHALL say that the job is running rather than inviting a second press.

When a job is not running, the page SHALL show its last known outcome where one exists — completed with its final counts, or failed with the counts reached and a pointer to where the failure is recorded — and SHALL leave the button enabled. A job that has never run SHALL show no state rather than a zeroed-out one.

A failed run SHALL be visibly distinct from a completed one rather than differing only in wording.

#### Scenario: A running job shows proportional progress
- **WHEN** a background job is in flight
- **THEN** the page shows a progress indicator filled in proportion to the processed-of-total counts, with those counts beside it, both refreshing while the run continues

#### Scenario: All three jobs report alike
- **WHEN** I compare the corrective re-sync, the airing refresh, and the series build while each is running
- **THEN** all three present their progress in the same form, differing only in wording and figures

#### Scenario: A running job cannot be started twice
- **WHEN** a job is running
- **THEN** its button is disabled and says the job is running

#### Scenario: A finished run keeps its result visible
- **WHEN** a job has finished
- **THEN** its final counts remain visible until another run starts, and its button is enabled again

#### Scenario: A failed run is marked as failed
- **WHEN** a job's last run failed
- **THEN** the page marks it as a failure rather than reporting it like a completed run, states how far it got, and points at where the failure is recorded

#### Scenario: A job that never ran shows nothing
- **WHEN** a job has never been started
- **THEN** the page shows no run state for it rather than an empty or zeroed one

### Requirement: Every sync control states what it will do

Each control on the Settings page that touches my list SHALL carry an explanation of what pressing it does, readable **before** it is pressed and without opening, hovering, or expanding anything. The controls this covers SHALL be: sync now, run full reconciliation, the accept and decline actions on a pending reconciliation diff, the accept and decline actions on a change held for review, and the corrective re-sync.

Each explanation SHALL state, in plain terms, what the control fetches or compares, **whether it shows me the differences for review before applying anything or applies them immediately**, and what it writes when it does apply. Where a control creates list entries for anime not tracked locally, its explanation SHALL say so.

Specifically:

- **Sync now** SHALL say that it pushes my own unsent edits to MyAnimeList straight away instead of waiting, and that it sends nothing else and changes nothing locally. Where anything is held for review, it SHALL also say that held changes are not among what it pushes and are waiting on my decision.
- **Run full reconciliation** SHALL say that it fetches my current MyAnimeList list, compares it against what is stored locally, and presents the differences for me to accept or decline — changing nothing until I do.
- **Accept** SHALL say that it applies exactly the differences shown and nothing else on my list is touched. **Decline** SHALL say that it discards them, applying nothing.
- **Accepting a held change** SHALL say that it sends that anime's stored values to MyAnimeList now, overwriting what MyAnimeList holds for it. **Declining a held change** SHALL say that it discards the unsent change and takes MyAnimeList's current value for that anime instead; where MyAnimeList holds no entry for that anime, it SHALL say instead that declining removes the anime from my list locally.
- **The corrective re-sync** SHALL say that it re-fetches my whole MyAnimeList list and full anime details, and **immediately overwrites** the local status, episodes watched, score, and dates for every anime, **with no review step**, creating entries for anime not tracked locally; and that entries with unsent local edits are left alone.

Each explanation of a control that applies changes SHALL also say whether what it applies is recorded in my history. The explanations of accepting a reconciliation diff and of the corrective re-sync SHALL say that nothing they apply is recorded in Latest updates or the full edit history. The explanation of the held-change review SHALL say that what declining applies is recorded in the edit history, like any other change I make in the app. No explanation SHALL describe a change as marked as coming from MyAnimeList.

An explanation SHALL describe what the control does today rather than what it is expected to do: a control that applies changes without review SHALL NOT be described in terms that suggest a review step exists.

These explanations SHALL be presentational. No control SHALL gain a confirmation step, change what it does, move group, or be placed behind an extra click.

#### Scenario: The reviewable and the immediate are tellable apart

- **WHEN** I read the explanations of "Run full reconciliation" and the corrective re-sync
- **THEN** the first states that it shows me differences to accept or decline before anything changes, and the second states that it overwrites my entries immediately with no review step

#### Scenario: Silent entry creation is called out

- **WHEN** I read the corrective re-sync's explanation
- **THEN** it states that it creates entries for anime not yet tracked locally

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

- **WHEN** I read the explanation of accepting a reconciliation diff or of the corrective re-sync
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
