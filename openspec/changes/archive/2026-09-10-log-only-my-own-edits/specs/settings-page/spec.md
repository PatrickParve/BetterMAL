## MODIFIED Requirements

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
