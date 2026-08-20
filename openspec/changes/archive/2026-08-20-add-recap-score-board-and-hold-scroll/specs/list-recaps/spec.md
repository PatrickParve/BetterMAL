## ADDED Requirements

### Requirement: The rating distribution opens a score board
The recap's rating distribution SHALL offer a control, beside its heading, that opens a **score board** — an overlay naming the anime behind the distribution's counts. The control SHALL sit in the distribution section's own header, alongside the heading rather than below or inside the block, so the block's rows keep the alignment and drill-through the existing requirements give them.

Opening the board SHALL NOT change the recap's period, time filter, ranking basis, or media-type narrowing, SHALL NOT be recorded in the page URL, and SHALL NOT add a history entry — pressing back from a recap with the board open SHALL leave the recap, as it does with the board closed.

When the period holds no entry carrying one of my scores, the control SHALL still be shown, but disabled and carrying an explanation of why, rather than being removed — so the section header holds its shape as the period and time filter change.

#### Scenario: Opening the board
- **WHEN** I use the control beside the **Rating distribution** heading
- **THEN** the score board opens over the recap, with the recap's period, time filter, ranking basis, and media-type narrowing all unchanged behind it

#### Scenario: The board is not a history entry
- **WHEN** I open the score board and then press back
- **THEN** I leave the recap, exactly as I would have with the board closed

#### Scenario: A period with nothing scored
- **WHEN** the period's included entries carry none of my scores
- **THEN** the control is shown disabled with an explanation, and the distribution block beside it renders its empty tracks as before

### Requirement: The score board lays a period out by score
The score board SHALL show ten slots, one per score from 10 down to 1 in that order, each holding the poster art of every included anime I gave that score. Each slot SHALL carry its score numeral and how many anime it holds, so the score of a slot is never conveyed by its colour alone.

The board SHALL cover exactly the set the rating distribution covers: every entry the selected period and time filter include that carries one of my scores, unnarrowed by the top 10's media-type control, and recomputed whenever the period or time filter changes. The number of posters in a slot and the count the matching distribution row reports SHALL always agree.

Within a slot, anime SHALL be ordered by title so the order is stable across reloads and across a refresh of the same period. Anime with no score of mine SHALL NOT appear in any slot.

All ten slots SHALL be shown even when a slot holds nothing — an empty slot reports the same "nothing scored this" the distribution's empty track reports, and keeping the ten fixed makes two periods comparable. An empty slot SHALL be identifiable as empty rather than appearing to be still loading.

An anime with no poster art SHALL occupy a placeholder of the same size in its slot, so a slot's count and its number of tiles agree whatever art is available.

#### Scenario: Seeing the tens of a period
- **WHEN** I open the score board for a period in which I scored four anime 10
- **THEN** the 10 slot shows those four anime's posters, and reports a count of four

#### Scenario: The board and the distribution agree
- **WHEN** a distribution row reports 12 anime scored 8
- **THEN** the board's 8 slot holds exactly 12 tiles

#### Scenario: The media-type control does not narrow the board
- **WHEN** I narrow the top 10 to films only and then open the score board
- **THEN** every included anime of the period is still placed in its slot, films and everything else alike

#### Scenario: The board follows the time filter
- **WHEN** I switch a yearly recap's time filter and open the score board
- **THEN** the slots hold the newly included entries, matching the recomputed distribution

#### Scenario: Unscored anime are not on the board
- **WHEN** the period includes anime I have not scored
- **THEN** they appear in no slot, and no slot's count includes them

#### Scenario: A slot nothing was scored
- **WHEN** no included anime carries a score of 3
- **THEN** the 3 slot is still shown, identifiably empty rather than missing or apparently loading

#### Scenario: An anime with no poster
- **WHEN** an included anime has no poster art
- **THEN** its slot holds a placeholder of the same size in its place, and the slot's count still matches the distribution

### Requirement: Score slots are coloured by tier
Each slot of the score board SHALL be coloured by the tier its score belongs to, so the tier is readable before the numeral is:

- **10** SHALL carry a purple-and-white mixed treatment belonging to no other slot, marking it as the top of the ladder;
- **9** SHALL be red, **8** blue, and **7** gold;
- **6** and **5** SHALL share silver;
- **4**, **3**, **2**, and **1** SHALL share bronze.

Every tier colour SHALL be legible against the overlay's background in both the light and the dark theme, and the six tiers SHALL be distinguishable from one another. The 10 slot's treatment SHALL be distinguishable from the app's ordinary accent-coloured selected controls, and the 8 slot's blue SHALL be distinguishable from the blue the app uses for MAL scores, so neither tier reads as a control or as somebody else's opinion.

Tier colour SHALL be decoration on top of the numeral and count each slot already carries, never the only thing that identifies a slot.

#### Scenario: The tens are marked out
- **WHEN** I open the score board
- **THEN** the 10 slot carries a purple-and-white treatment that no other slot carries

#### Scenario: The tiers descend
- **WHEN** I look down the board from 10 to 1
- **THEN** 9 is red, 8 is blue, 7 is gold, 6 and 5 are silver, and 4 through 1 are bronze

#### Scenario: Both themes
- **WHEN** I view the board in the light theme and again in the dark theme
- **THEN** every slot's colour is legible against the overlay's background and tellable apart from the other tiers in both

#### Scenario: Colour is not the only signal
- **WHEN** two slots share a tier colour, as 6 and 5 do
- **THEN** each still carries its own score numeral and its own count

### Requirement: Board posters answer to the pointer and the keyboard
Each poster on the score board SHALL respond when the pointer is over it or when it is reached by keyboard: it SHALL lift out of its slot and a card SHALL appear naming the anime, the score whose slot it sits in, and its media type.

The card SHALL be fully visible wherever the poster sits — including in the board's first and last slots and at the overlay's left and right edges — rather than being clipped by the overlay or running off the viewport. The card SHALL NOT intercept the pointer, so moving across a slot moves cleanly from poster to poster.

Each poster SHALL be a link to that anime's page, carrying the anime's title as its accessible name, and following it SHALL close the board and open that page. Every poster SHALL be reachable by keyboard, and a poster reached by keyboard SHALL show the same card it shows on hover.

Under a reduced-motion preference the posters SHALL NOT animate, and the card SHALL still appear on hover and on focus.

#### Scenario: Pointing at a poster
- **WHEN** I move the pointer over a poster in the 9 slot
- **THEN** it lifts and a card appears naming the anime, its score of 9, and its media type

#### Scenario: A poster at the edge of the board
- **WHEN** I point at a poster at the far right of a slot, or in the board's topmost or bottommost slot
- **THEN** the whole card is visible rather than being cut off by the overlay or the window

#### Scenario: Opening an anime from the board
- **WHEN** I follow a poster
- **THEN** the board closes and that anime's page opens

#### Scenario: Reaching a poster by keyboard
- **WHEN** I move through the board with the keyboard
- **THEN** each poster can be focused and followed, and the focused poster shows the same card hovering it would

#### Scenario: Reduced motion
- **WHEN** I have asked my system for reduced motion
- **THEN** no poster animates, and hovering or focusing one still shows its card

### Requirement: The score board is dismissible like the recap's other overlays
The score board SHALL be dismissible by the Escape key, by clicking outside it, and by an explicit close control, in the same way the recap's "see all" ranking overlay is. It SHALL be scrollable within itself when it holds more than fits, leaving the recap behind it in place, and each slot's score numeral and count SHALL remain identifiable while scrolling through that slot.

Closing the board SHALL return the recap exactly as it was left, at the same scroll position.

#### Scenario: Closing with the keyboard
- **WHEN** I press Escape with the board open
- **THEN** the board closes and the recap is where I left it

#### Scenario: Closing by clicking away
- **WHEN** I click outside the board
- **THEN** it closes, as the ranking overlay does

#### Scenario: A period with hundreds of scored anime
- **WHEN** the board holds more than fits on screen
- **THEN** it scrolls within itself, the recap behind it does not move, and the slot being scrolled through stays identifiable

### Requirement: Mid-page recap controls hold the scroll position
Changing the top 10's **ranking basis** or its **media-type narrowing** SHALL leave the page at the scroll position it was at, so the section being compared stays where it is on screen instead of jumping away. Both controls sit within the top 10's own header partway down the page, and neither changes what the recap is about.

The controls that do change the recap's subject — the recap-type tabs, the period stepper and selects, and the time filter — SHALL continue to return to the top of the page, as SHALL following any link away from the recap.

A held scroll position SHALL be remembered for the entry it belongs to: after changing the ranking basis or the media type, following a link away from the recap, and returning with back, the recap SHALL be restored at the position it was held at rather than at the top.

Every other page's scroll behaviour SHALL be unchanged, including pages that deliberately return to the top when a control in the page URL changes.

#### Scenario: Switching ranking basis
- **WHEN** I scroll down to the top 10 and switch it from my score to MAL's
- **THEN** the ten re-rank in place and the page stays exactly where it was

#### Scenario: Narrowing by media type
- **WHEN** I narrow the top 10 to films
- **THEN** the list narrows in place and the page stays exactly where it was

#### Scenario: Changing the period still returns to the top
- **WHEN** I step the recap to another year, switch the recap type, or change the time filter
- **THEN** the page returns to the top, as it does today

#### Scenario: Coming back to a held position
- **WHEN** I switch the ranking basis partway down a recap, open an anime from the top 10, and press back
- **THEN** the recap is restored at the position I switched it at, not at the top

#### Scenario: Back undoes the switch
- **WHEN** I switch the ranking basis and press back
- **THEN** the previous basis is restored, as it is today
