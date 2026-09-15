## MODIFIED Requirements

### Requirement: A failed action is never silent
The system SHALL report every action it took on the user's behalf that did not take effect, so no control can be pressed, appear to do nothing, and leave the user to work out whether it worked.

This SHALL cover, at minimum, every edit made from a control outside the entry editor — the "+" episode-increment button wherever it appears (the progress bar, the currently-watching carousel, my list, and the detail page), the inline episode-count edit, the score control on my list, the detail page's add-to-list and add-to-watching actions, and the add action on the top anime page. It SHALL equally cover the other actions a page takes on my behalf: the detail page's refresh action, choosing or resetting a picture on a detail page or a series page, choosing a series title, and reordering tied favourites on a series page. An action whose failure the user is already told about in place — a save in the entry editor, which reports in the editor itself — SHALL NOT also raise this notice, so one failure is reported once.

The report SHALL say what did not happen and, where the server stated a reason, that reason. It SHALL name the anime or series the action was taken on, so a failure is attributable when the control that raised it is one of many on a page.

The system SHALL NOT silently roll an action back and say nothing: where the displayed value was not changed because the action failed, the notice is what accounts for it. Where the page showed the choice before the save completed and the save then failed, the notice SHALL still be raised, so the page is never the only account of whether a choice was kept.

#### Scenario: An increment the server refuses
- **WHEN** I press "+" on an anime and the server rejects the edit
- **THEN** a notice reports that the episode count was not raised, names the anime, and states the server's reason

#### Scenario: An increment that fails with no reason given
- **WHEN** a "+" press fails without the server stating a reason
- **THEN** a notice still reports that the episode count was not raised for that anime

#### Scenario: The count does not move
- **WHEN** an increment fails
- **THEN** the episode count shown is the count before the press, and the notice explains why it did not move

#### Scenario: One failure is reported once
- **WHEN** a save made in the entry editor is rejected
- **THEN** the editor reports it and no separate app-wide notice is raised for the same failure

#### Scenario: An add-to-list that fails
- **WHEN** I use "Add to list" on a detail page and the request fails
- **THEN** a notice reports that the anime was not added, and the page does not show it as in my list

#### Scenario: A score that does not save
- **WHEN** I pick a score for an anime on my list and the request fails
- **THEN** the score shown is the score before the pick, and a notice reports that the score was not updated and names the anime

#### Scenario: An add from the top anime page that fails
- **WHEN** I use "Add" on the top anime page and the request fails
- **THEN** the control still offers to add that anime, and a notice reports that it was not added and names it

#### Scenario: A refresh that fails
- **WHEN** I use the detail page's refresh action and the refresh request fails
- **THEN** the page keeps showing what it already had, and a notice reports that the anime was not refreshed and names it

#### Scenario: A picture choice that does not save
- **WHEN** I choose or reset the picture on a detail page or a series page and the save fails
- **THEN** a notice reports that the picture was not saved and names the anime or series

#### Scenario: A series title choice that does not save
- **WHEN** I choose a title for a series and the save fails
- **THEN** a notice reports that the title was not saved and names the series

#### Scenario: A favourite reorder that rolls back
- **WHEN** I move a tied favourite on a series page and the save fails
- **THEN** the favourites return to the stored order, and a notice reports that the move did not take effect and names the anime I moved
