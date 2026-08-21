## ADDED Requirements

### Requirement: Settings are organised into named groups
The Settings page SHALL present its controls in **named groups** rather than as one flat stack of equally weighted boxes. Every control the page offers SHALL belong to exactly one group, and each group SHALL carry a name and a one-line explanation of what the controls inside it do.

The groups SHALL be, in this order:

1. **Preferences** — the settings that change how the app displays things for me and take effect immediately: the completed/dropped score-reveal setting and the NSFW content filter.
2. **Sync** — the state of the ongoing MyAnimeList sync (pending/retrying count, last successful sync) together with the actions that drive it (resync now, run full reconciliation), and the pending reconciliation diff when one exists.
3. **Data tools** — the long-running corrective and backfill jobs: the corrective re-sync from MAL, the full airing-date refresh, the build-all-series run, and the single-anime metadata force-refresh.
4. **Account** — the MyAnimeList connection state and the re-authorize action.

Ordering SHALL run from the cheapest and most reversible to the most expensive: instant display preferences first, the routine sync next, the minutes-long jobs after that, and the account connection last.

Grouping SHALL be presentational only. Every control, label, status figure, and action the page offers today SHALL still be present, SHALL still call the same endpoint, and SHALL behave identically; no setting SHALL be added, removed, renamed in meaning, or moved behind an extra click.

#### Scenario: Groups are named and ordered
- **WHEN** I open the Settings page
- **THEN** its controls appear under the named groups Preferences, Sync, Data tools, and Account, in that order, each group naming what it holds

#### Scenario: Every control survives the regrouping
- **WHEN** I compare the regrouped Settings page against what it offered before
- **THEN** every preference, status figure, and action is still present and still does the same thing

#### Scenario: The reconciliation diff appears in Sync
- **WHEN** a pending reconciliation diff exists
- **THEN** it is shown inside the Sync group with its review actions, and when none exists the group shows no diff section at all

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
