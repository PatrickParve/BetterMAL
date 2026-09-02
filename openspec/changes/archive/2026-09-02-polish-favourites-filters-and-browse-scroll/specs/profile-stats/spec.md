## ADDED Requirements

### Requirement: Favourites rankings can be ranked by how many of a score they hold
Each of the profile page's two favourites rankings — **Favourite years** and **Favourite seasons** — SHALL offer a control that changes what the ranking ranks on, so a year or season can be found by how many anime of a given score it holds rather than only by its weighted average.

The control SHALL sit directly beneath its ranking's title and above its rows, and SHALL belong to that ranking alone: the two rankings SHALL hold independent selections, and changing one SHALL NOT change the other. It SHALL read as **All**, followed by the words **With most:** and one button per score, ordered **10 down to 1**.

A score SHALL be offered only when at least one group in that ranking holds at least one anime I scored it, so no offered button can produce an empty ranking. **All** SHALL always be offered and SHALL be the selection on a fresh visit; under it the ranking is exactly what it is today — the Bayesian weighted average, its tie-break sequence, its five-row cap, its "See all" overlay, and its posters, all unchanged.

Selecting a score N SHALL re-rank that ranking by **how many anime I scored N** each group holds, most first, and SHALL omit every group holding none. Groups tied on that count SHALL be ordered by the identical sequence the unfiltered ranking applies — the weighted score at full precision, then the number of scored anime, then a score-by-score comparison from 10 down to 1, then the newer group ahead of the older — so the order is stable across reloads and never arbitrary. Ranks SHALL be numbered from one down the re-ranked order rather than carrying over the positions the groups held under **All**.

Everything else about a ranked row SHALL be unchanged under a score selection: its name, its posters, its row height, and where following it leads. Its supporting figures SHALL name the count it was ranked on alongside how many of my anime the group was computed over, so the row states why it placed where it did.

The five-row cap and the "See all" overlay SHALL apply to the re-ranked ranking: at most five rows are shown, the overlay lists every group the selection keeps, in the selected order, and the overlay SHALL name the selection it is showing. A selection that leaves five or fewer groups SHALL offer no overlay control, exactly as an unfiltered ranking of that size does not.

Each score button SHALL wear the colour its score carries in the recap page's rating distribution — the same score-tier colours, so a score is the same colour wherever it is shown — on pointer hover, on keyboard focus, and while it is the selection. At rest and unselected it SHALL be neutral. The **All** button SHALL wear its section's own colour family — the year family in Favourite years, the season family in Favourite seasons — rather than a score tier, since it names no score. No state SHALL change a button's size, so the control row SHALL NOT reflow as the pointer moves along it.

A selection SHALL be part of the profile page's restorable state per the `page-state-restoration` capability: it SHALL be restored on a back/forward navigation and SHALL open on **All** on a fresh visit. A restored selection naming a score the reloaded ranking no longer offers SHALL fall back to **All** rather than showing an empty ranking.

#### Scenario: The control is offered under each title
- **WHEN** the profile page shows Favourite years and Favourite seasons
- **THEN** each ranking shows an All button and a "With most:" row of score buttons between its title and its rows

#### Scenario: All is the default and changes nothing
- **WHEN** I open the profile page without touching the control
- **THEN** All is selected and both rankings are ranked exactly as they are by weighted average

#### Scenario: Ranking by how many 10s
- **WHEN** I select 10 in Favourite years
- **THEN** the years are re-ranked with the year holding the most anime I scored 10 first, numbered from one

#### Scenario: Groups holding none of the score are dropped
- **WHEN** I select 9 and one of the years shown under All holds no anime I scored 9
- **THEN** that year is not shown in the ranking or in its "See all" overlay

#### Scenario: A tie on the count falls back to the ranking's own order
- **WHEN** two seasons each hold three anime I scored 8 and 8 is selected
- **THEN** they are ordered by weighted score, then scored count, then score-by-score, then the newer first — the same sequence All uses

#### Scenario: Only scores that exist are offered
- **WHEN** nothing in my list is scored 1
- **THEN** no 1 button is offered in either ranking

#### Scenario: The two rankings are independent
- **WHEN** I select 10 in Favourite years
- **THEN** Favourite seasons is still on All

#### Scenario: A row states what it was ranked on
- **WHEN** a year is shown under a selection of 10
- **THEN** its row states how many anime I scored 10 it holds, alongside how many of my anime it was computed over

#### Scenario: The overlay follows the selection
- **WHEN** I select 8 in Favourite seasons and open its "See all" overlay
- **THEN** the overlay lists every season holding an 8, in the selected order, and names the selection it is showing

#### Scenario: No overlay when the selection leaves few groups
- **WHEN** a selected score is held by only three years
- **THEN** all three are shown and no "See all" control is offered

#### Scenario: Buttons wear their score's colour
- **WHEN** I hover the 10 button, then the 9 button, then the 8 button
- **THEN** each highlights in the colour its score carries in the recap page's rating distribution

#### Scenario: The selected button keeps its colour
- **WHEN** 9 is selected and the pointer is elsewhere
- **THEN** the 9 button still reads as the selection, drawn in its score's colour

#### Scenario: All wears its section's family
- **WHEN** I hover the All button in Favourite seasons
- **THEN** it highlights in the season family's colour rather than in a score tier's

#### Scenario: The control row does not reflow
- **WHEN** I move the pointer along a ranking's score buttons
- **THEN** none of them changes size and nothing beside them moves

#### Scenario: A selection survives back-navigation
- **WHEN** I select 10 in Favourite years, follow a row into a recap, and go back
- **THEN** 10 is still selected and the ranking is still ranked by it

#### Scenario: A fresh visit opens on All
- **WHEN** I select 10 and then reach the profile page again from the navigation bar
- **THEN** both rankings are back on All

### Requirement: The episode-progress figure is set as a headline figure
The `watched / total` figure on the profile page's episode-progress bar SHALL be set at a larger type size than the same figure carries on cards and list rows, so the summary of a whole list reads as the headline of its section rather than as row furniture. It SHALL remain the shared episode progress-bar treatment in every other respect — the same filled track, the same label content, the same read-only behaviour — and no other episode progress bar in the app SHALL change size.

The larger size SHALL NOT change the section's layout beyond the label's own height: the bar SHALL still span the section's width and the unresolved-entries note beneath it SHALL be unmoved.

#### Scenario: The profile's figure is larger
- **WHEN** I look at the profile page's Episode progress section
- **THEN** its watched-against-total figure is set noticeably larger than the figure on a currently-watching card

#### Scenario: Other progress bars are unchanged
- **WHEN** I look at a currently-watching card, a my-list row, or an anime detail page after this change
- **THEN** its episode progress figure is the size it has always been

#### Scenario: The section does not otherwise change
- **WHEN** the profile page's episode-progress section is shown with unresolved entries
- **THEN** the bar spans the section as before and the unresolved note sits where it did
