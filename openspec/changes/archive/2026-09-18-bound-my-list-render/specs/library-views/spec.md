## ADDED Requirements

### Requirement: My list reveals its rows as the page scrolls

My list SHALL be drawn with continuous (infinite) scroll rather than all at once: an initial bounded number of entry rows renders immediately and further rows are appended automatically as the user scrolls toward the end of what is drawn, with no pagination controls anywhere on the page. Reveal SHALL stop once every entry matching the active filters is drawn, and SHALL never draw an entry twice.

The reveal budget SHALL be **one count shared by the whole page**, not one per status group. In grouped view the budget SHALL be consumed in the order the groups are shown: each group draws as many of its entries as the remaining budget allows and passes any remainder to the next group, so a group whose entries fit entirely draws all of them, and once the budget is spent the groups after it draw no rows yet.

Every status group's header SHALL report that group's true entry count under the active filters, whether or not all of its rows are drawn yet, and the results line SHALL likewise keep reporting how many entries match and how many the status selection holds — neither SHALL report how many rows happen to be drawn.

Changing what the list shows or the order it shows it in SHALL reset the reveal to the initial bounded number: the find-in-list text, the status tabs, the type, airing-status and score filters, the sort key, the sort direction, the tiebreaker, the airing-status-first choice, and the grouped/flat choice each SHALL reset it. Narrowing by find-in-list text SHALL take effect as the user types, without an imposed delay.

How much of the list has been revealed SHALL be restored on a back or forward navigation, alongside the filter and sort controls the page already restores, so returning to the list lands on the same entries at the same scroll position.

#### Scenario: Only a first batch is drawn on arrival
- **WHEN** I open my list in grouped view and it holds far more entries than one batch
- **THEN** only the first batch of rows is drawn, and the entries beyond it are not rendered yet

#### Scenario: A flat list is bounded the same way
- **WHEN** I choose Single list and my list holds far more entries than one batch
- **THEN** only the first batch of rows is drawn, in the active sort's order

#### Scenario: Scrolling reveals more
- **WHEN** I scroll toward the end of the drawn rows
- **THEN** a further batch of rows is appended automatically, with no control to press

#### Scenario: Reveal stops at the end of the list
- **WHEN** I keep scrolling until every entry matching the active filters is drawn
- **THEN** nothing further is appended, and no entry appears twice

#### Scenario: One budget spans the status groups
- **WHEN** grouped view's first group alone holds more entries than the initial batch
- **THEN** that group draws as much of itself as the batch allows and the groups after it draw no rows yet

#### Scenario: Small groups are all drawn
- **WHEN** grouped view's groups together hold no more entries than the initial batch
- **THEN** every group draws all of its entries, with none held back

#### Scenario: Headers count entries, not drawn rows
- **WHEN** a group holds more entries than are currently drawn for it
- **THEN** its header still reports the group's true count, and the results line still reports the matched and total counts

#### Scenario: Typing narrows the list as I type
- **WHEN** I type into the find-in-list field
- **THEN** the drawn rows narrow to the matching entries on each character, with no imposed delay before the list responds

#### Scenario: Typing resets the reveal
- **WHEN** I have scrolled well into the list and then type into the find-in-list field
- **THEN** the reveal returns to the initial batch rather than keeping the larger amount revealed by my scrolling

#### Scenario: Changing a filter, the sort, or the grouping resets the reveal
- **WHEN** I have scrolled well into the list and then change a status tab, the type, airing-status or score filter, the sort key, the sort direction, the tiebreaker, the airing-status-first choice, or the grouped/flat choice
- **THEN** the reveal returns to the initial batch

#### Scenario: Coming back restores how much was revealed
- **WHEN** I scroll well into my list, open an entry, and go back
- **THEN** the same amount of the list is drawn as when I left, with the filters and sort restored as they already are, and I land at the same scroll position

#### Scenario: A fresh visit starts from the first batch
- **WHEN** I reach my list from the navbar rather than by going back
- **THEN** the reveal starts at the initial batch, as every other control starts at its default
