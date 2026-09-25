## MODIFIED Requirements

### Requirement: Score prompt on completion via the increment button
The system SHALL open a score prompt overlay whenever an episode-count change made from the progress row takes an entry into Completed status — whether from the "+" button or from editing the count in place — from every place that row appears (the main dashboard's currently-watching carousel, my list rows, the anime detail page, and any later addition), except where "Completion prompt fires only on entering Completed" rules it out. The overlay SHALL show the anime's picture, large and whole, on the left, and beside it a score dropdown offering "No score" and the values 1 through 10, pre-selected with the entry's current score. Its layout is governed by "The completion prompt is laid out around a large, whole picture".

The overlay SHALL offer three actions: **cancel**, save, and **save and rank**. Cancel skips scoring only. The entry is already Completed by the time the prompt opens, and Cancel SHALL NOT undo that. Save and rank SHALL save the chosen score and then open the ranking editor focused on that anime, so an anime can be finished, scored, and placed in one pass. It SHALL be offered only while the chosen score would leave the entry hand-orderable — with "No score" selected, or for a dropped or short-form anime, saving alone is the only save action offered.

Every completion reachable from the progress row now concerns an anime that has aired in full and carries a finish date, so no finish-date condition is placed on the prompt. In particular the prompt SHALL open for the final episode of a run MyAnimeList still reports as currently airing, since that is a genuine finish. Reaching the aired-so-far count of an anime with episodes still to come completes nothing and SHALL NOT open this prompt.

#### Scenario: Final episode watched from any increment site
- **WHEN** I press the "+" button on an entry whose episodes-watched thereby reaches the anime's total episode count
- **THEN** a score prompt overlay opens showing that anime's picture and a score dropdown

#### Scenario: Final episode of a run with a stale airing status
- **WHEN** I press the "+" button to reach the total of an anime whose episodes have all aired but which MyAnimeList still reports as currently airing
- **THEN** the score prompt opens, because the entry entered Completed

#### Scenario: Count typed straight to the total
- **WHEN** I edit an entry's count in place and set it to the anime's total episode count
- **THEN** the same score prompt overlay opens, just as it would from the "+" button

#### Scenario: Prompt reflects an existing score
- **WHEN** the score prompt opens for an entry that already has a score
- **THEN** the dropdown is pre-selected with that score rather than "No score"

#### Scenario: The prompt offers to rank
- **WHEN** I pick a score of 1 through 10 in the completion prompt
- **THEN** a save-and-rank action is offered alongside save and cancel

#### Scenario: No rank action without a score
- **WHEN** "No score" is selected in the completion prompt
- **THEN** no save-and-rank action is offered

#### Scenario: The dismiss action reads Cancel
- **WHEN** the completion prompt opens
- **THEN** its action for closing without saving is labelled "Cancel"

#### Scenario: Non-final increments do not prompt
- **WHEN** I press the "+" button and episodes-watched stays below the anime's total episode count
- **THEN** no score prompt opens

#### Scenario: Catching up on a run still to come does not prompt
- **WHEN** I press the "+" button and episodes-watched thereby reaches the aired-so-far count of an anime whose total is higher
- **THEN** the entry stays Watching and no score prompt opens

## ADDED Requirements

### Requirement: The completion prompt is laid out around a large, whole picture
The completion score prompt SHALL present the anime's picture as its main visual, far larger than a list-row thumbnail, and SHALL draw it **whole, at its own proportions**, with nothing cut off.

A picture that is not landscape SHALL be drawn at one fixed width and take whatever height its own proportions give it there. This covers a poster, an upright picture and a square picture. A **landscape** picture, strictly wider than it is tall, SHALL be drawn at one wider fixed width, again at its own height, so a wide, short picture is not reduced to a strip. The picture's width SHALL depend on that landscape test alone. It SHALL take one of those two widths, never a width computed per picture. The prompt's picture SHALL NOT be drawn under the row-slot rule the app's list rows use.

Until the picture has loaded, the prompt SHALL reserve a portrait box of the fixed width, so the prompt does not collapse and then expand as the picture arrives. When the anime has no picture, a placeholder SHALL occupy that portrait box. A picture that is slow to arrive SHALL fade in over the box's placeholder surface, under the same timing rules as the pictures `artwork-presentation` governs. A picture the browser already holds, or one that arrives within about 150 ms, SHALL show at once. Under reduced motion it SHALL appear without animating. A picture that fails to load SHALL leave the placeholder surface showing.

The anime's title, the prompt's hint and the score dropdown SHALL sit beside the picture, aligned to its top. The prompt SHALL be wide enough for that text to read comfortably beside a landscape picture as well as beside a portrait one.

The actions SHALL sit in a row of their own beneath the picture and the text, spanning the prompt's full width:

- Cancel SHALL sit at the row's start.
- Save SHALL sit at the row's end.
- Save and rank, when it is offered, SHALL sit immediately before Save.

Save SHALL therefore not move when Save and rank appears or disappears as the chosen score changes. Every action SHALL be drawn at one shared height, with its label on a single line. No action's label SHALL wrap inside the action. Where the row is too narrow for all of them, a whole action SHALL move onto a new line instead.

On a viewport too narrow for the picture and the text to sit side by side, the picture SHALL move above the text, centred, and the action row SHALL follow beneath the text.

This requirement governs presentation only. The actions offered, their labels, when Save and rank is offered, and what each action does SHALL stay as "Score prompt on completion via the increment button" and "Saving or dismissing the completion score prompt" define them.

#### Scenario: A poster is shown large and whole
- **WHEN** the completion prompt opens for an anime whose picture is a 2:3 poster
- **THEN** the poster is drawn whole at 2:3, at the prompt's fixed picture width, and far larger than a list-row thumbnail

#### Scenario: A landscape picture is shown wider
- **WHEN** the completion prompt opens for an anime whose picture is wider than it is tall
- **THEN** the picture is drawn whole at the wider fixed width, at its own height, and the title, hint and score dropdown still sit beside it

#### Scenario: A square picture keeps the portrait width
- **WHEN** the completion prompt opens for an anime whose picture is exactly square
- **THEN** it is drawn whole and square at the portrait width, not at the landscape width

#### Scenario: The actions stay on one line
- **WHEN** the completion prompt opens and I pick a score of 1 through 10, so Save and rank is offered
- **THEN** Cancel, Save and rank, and Save each show their label on a single line and are drawn at the same height, in a row beneath the picture and the text

#### Scenario: Save does not move
- **WHEN** I change the chosen score from 8 to "No score", so Save and rank is withdrawn
- **THEN** Save stays at the end of the action row where it was, and only Save and rank disappears

#### Scenario: A narrow screen stacks the prompt
- **WHEN** the completion prompt opens on a phone-width viewport
- **THEN** the picture sits centred above the title, hint and score dropdown, the action row follows beneath them, and no action's label wraps

#### Scenario: No picture
- **WHEN** the completion prompt opens for an anime that has no picture
- **THEN** a placeholder occupies the portrait picture box, and the rest of the prompt is laid out as it is with a picture

#### Scenario: A slow picture fades in
- **WHEN** the prompt's picture finishes downloading more than about 150 ms after the prompt opened
- **THEN** the picture fades in over the reserved box rather than appearing abruptly or being drawn part-way
