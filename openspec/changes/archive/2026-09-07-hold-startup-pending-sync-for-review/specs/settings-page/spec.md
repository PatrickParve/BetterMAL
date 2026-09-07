## ADDED Requirements

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

## MODIFIED Requirements

### Requirement: Settings are organised into named groups
The Settings page SHALL present its controls in **named groups** rather than as one flat stack of equally weighted boxes. Every control the page offers SHALL belong to exactly one group, and each group SHALL carry a name and a one-line explanation of what the controls inside it do.

The groups SHALL be, in this order:

1. **Preferences** — the settings that change how the app displays things for me and take effect immediately: the completed/dropped score-reveal setting and the NSFW content filter.
2. **Sync** — the state of the ongoing MyAnimeList sync (pending/retrying count, count held for review, last successful sync) together with the actions that drive it (resync now, run full reconciliation), the changes held for review when any exist, and the pending reconciliation diff when one exists.
3. **Data tools** — the long-running corrective and backfill jobs: the corrective re-sync from MAL, the full airing-date refresh, the build-all-series run, and the single-anime metadata force-refresh.
4. **Account** — the MyAnimeList connection state and the re-authorize action.

Ordering SHALL run from the cheapest and most reversible to the most expensive: instant display preferences first, the routine sync next, the minutes-long jobs after that, and the account connection last.

Grouping SHALL be presentational only. Every control, label, status figure, and action the page offers SHALL still be present, SHALL still call the same endpoint, and SHALL behave identically; the grouping itself SHALL NOT remove a setting, rename one in meaning, or move one behind an extra click.

#### Scenario: Groups are named and ordered
- **WHEN** I open the Settings page
- **THEN** its controls appear under the named groups Preferences, Sync, Data tools, and Account, in that order, each group naming what it holds

#### Scenario: Every control survives the regrouping
- **WHEN** I compare the regrouped Settings page against what it offered before
- **THEN** every preference, status figure, and action is still present and still does the same thing

#### Scenario: The reconciliation diff appears in Sync
- **WHEN** a pending reconciliation diff exists
- **THEN** it is shown inside the Sync group with its review actions, and when none exists the group shows no diff section at all

#### Scenario: Held changes appear in Sync
- **WHEN** changes are held for review
- **THEN** they are shown inside the Sync group with their review actions, and when none are held the group shows no held-changes section at all

### Requirement: Every sync control states what it will do

Each control on the Settings page that touches my list SHALL carry an explanation of what pressing it does, readable **before** it is pressed and without opening, hovering, or expanding anything. The controls this covers SHALL be: sync now, run full reconciliation, the accept and decline actions on a pending reconciliation diff, the accept and decline actions on a change held for review, and the corrective re-sync.

Each explanation SHALL state, in plain terms, what the control fetches or compares, **whether it shows me the differences for review before applying anything or applies them immediately**, and what it writes when it does apply. Where a control creates list entries for anime not tracked locally, its explanation SHALL say so.

Specifically:

- **Sync now** SHALL say that it pushes my own unsent edits to MyAnimeList straight away instead of waiting, and that it sends nothing else and changes nothing locally. Where anything is held for review, it SHALL also say that held changes are not among what it pushes and are waiting on my decision.
- **Run full reconciliation** SHALL say that it fetches my current MyAnimeList list, compares it against what is stored locally, and presents the differences for me to accept or decline — changing nothing until I do.
- **Accept** SHALL say that it applies exactly the differences shown and nothing else on my list is touched. **Decline** SHALL say that it discards them, applying nothing.
- **Accepting a held change** SHALL say that it sends that anime's stored values to MyAnimeList now, overwriting what MyAnimeList holds for it. **Declining a held change** SHALL say that it discards the unsent change and takes MyAnimeList's current value for that anime instead; where MyAnimeList holds no entry for that anime, it SHALL say instead that declining removes the anime from my list locally.
- **The corrective re-sync** SHALL say that it re-fetches my whole MyAnimeList list and full anime details, and **immediately overwrites** the local status, episodes watched, score, and dates for every anime, **with no review step**, creating entries for anime not tracked locally; and that entries with unsent local edits are left alone.

Each explanation SHALL also say that the changes the control applies are recorded in Latest updates and the full edit history, marked as coming from MyAnimeList.

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

#### Scenario: Explanations name where the changes are recorded

- **WHEN** I read the explanation of any control that applies changes from MyAnimeList
- **THEN** it says those changes appear in Latest updates and the edit history, marked as coming from MyAnimeList

#### Scenario: Nothing about the controls themselves changes

- **WHEN** I compare each sync control against what it did before the explanations were added
- **THEN** each still calls the same operation, with no confirmation step added and no control moved or hidden

#### Scenario: The held-change actions say which way the data flows

- **WHEN** a change held for review is shown with its accept and decline actions
- **THEN** the page states that accepting sends my stored values to MyAnimeList, and that declining discards them and takes MyAnimeList's current value instead

#### Scenario: A destructive decline says so before it is pressed

- **WHEN** a held change is shown for an anime MyAnimeList holds no entry for
- **THEN** its decline action states that declining removes that anime from my list locally

#### Scenario: Sync now says what it leaves behind

- **WHEN** I read the sync-now explanation while changes are held for review
- **THEN** it states that held changes are not pushed by it and are waiting on my decision
