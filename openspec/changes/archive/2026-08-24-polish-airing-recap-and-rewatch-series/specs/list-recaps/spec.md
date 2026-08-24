## MODIFIED Requirements

### Requirement: Season recap links to its season page
A season recap SHALL offer a control that opens the season browser on that same season and year, so the anime that aired that season — not only the ones in my list — are one step away. The control SHALL NOT be offered on a yearly or multi-year recap, whose period is not a single season.

The control SHALL be presented as a button rather than as inline text: it SHALL be the same height as the period controls it sits beside, and SHALL take the same hover and keyboard-focus treatment the app's other controls take. It SHALL remain a real link — openable in a new tab or window by the means the browser normally offers for links — despite being presented as a button.

The control SHALL take the **season** colour family on hover and on keyboard focus, matching the treatment the recap's own Season tab takes when hovered, rather than the app's generic accent. Its resting state SHALL stay neutral, matching the period controls beside it, so the colour appears in response to the pointer or focus rather than being worn all the time.

#### Scenario: Opening the season page from a recap
- **WHEN** a fall 2019 season recap is shown and I follow its season-page control
- **THEN** the season browser opens on fall 2019

#### Scenario: The control reads as a button
- **WHEN** a season recap is shown
- **THEN** its season-page control is presented as a button matching the height of the controls beside it, and highlights on hover and on keyboard focus as they do

#### Scenario: The control is still a link
- **WHEN** I open the season-page control in a new tab the way I would any link
- **THEN** the season browser opens in a new tab on that season and year

#### Scenario: Hovering wears the season colour
- **WHEN** I hover the season-page control
- **THEN** it highlights in the season family's colour, the same one the Season tab highlights in when hovered, rather than in the app's generic accent

#### Scenario: Focus wears the season colour
- **WHEN** I reach the season-page control by keyboard
- **THEN** its focus ring is drawn in the season family's colour, as the Season tab's is

#### Scenario: The resting control is neutral
- **WHEN** a season recap is shown and I am not pointing at or focused on the season-page control
- **THEN** it is drawn neutrally, matching the period controls beside it

#### Scenario: Not offered outside season mode
- **WHEN** a yearly or multi-year recap is shown
- **THEN** no season-page control is offered

### Requirement: Mid-page recap controls hold the scroll position
Changing the top 10's **ranking basis**, its **media-type narrowing**, or the recap's **period** SHALL leave the page at the scroll position it was at, so the section being read stays where it is on screen instead of jumping away. None of these changes what the recap is about: the first two sit within the top 10's own header partway down the page, and the third swaps the data for another period of the same kind.

Holding the position on a period change SHALL apply to every control that selects a period without changing the recap's kind: the year select and its stepper arrows in yearly mode, the season select, the year select, and both stepper arrows in season mode, and the from-year and to-year selects in multi-year mode.

The controls that do change the recap's kind or its membership rule — the recap-type tabs and the time filter — SHALL continue to return to the top of the page, as SHALL following any link away from the recap.

When a period change lands on a period where the selected time filter has nothing to show, the automatic fall-back to the other filter SHALL preserve the held position rather than undoing it, since the user changed the period and not the filter.

A held scroll position SHALL be remembered for the entry it belongs to: after changing the ranking basis, the media type, or the period, following a link away from the recap, and returning with back, the recap SHALL be restored at the position it was held at rather than at the top.

Every other page's scroll behaviour SHALL be unchanged, including pages that deliberately return to the top when a control in the page URL changes.

#### Scenario: Switching ranking basis
- **WHEN** I scroll down to the top 10 and switch it from my score to MAL's
- **THEN** the ten re-rank in place and the page stays exactly where it was

#### Scenario: Narrowing by media type
- **WHEN** I narrow the top 10 to films
- **THEN** the list narrows in place and the page stays exactly where it was

#### Scenario: Stepping to the next year
- **WHEN** I scroll partway down a yearly recap and press its next-year arrow
- **THEN** the year's data is replaced in place and the page stays exactly where it was

#### Scenario: Choosing a year from the select
- **WHEN** I scroll partway down a yearly recap and choose another year from its year select
- **THEN** the page stays exactly where it was

#### Scenario: Stepping to the next season
- **WHEN** I scroll partway down a season recap and press its next-season arrow
- **THEN** the page stays exactly where it was, including when the step rolls over into the following year

#### Scenario: Changing a multi-year range
- **WHEN** I scroll partway down a multi-year recap and change its from-year or to-year select
- **THEN** the page stays exactly where it was

#### Scenario: A period change that also changes the filter
- **WHEN** I step to a year that has nothing completed or dropped in it, so the recap falls back from the watched filter to the aired one
- **THEN** the page stays where it was rather than being returned to the top by the fall-back

#### Scenario: Switching the recap type still returns to the top
- **WHEN** I switch from a yearly recap to a season recap, or change the time filter myself
- **THEN** the page returns to the top, as it does today

#### Scenario: Coming back to a held position
- **WHEN** I step the period partway down a recap, open an anime from the top 10, and press back
- **THEN** the recap is restored at the position I stepped it at, not at the top

#### Scenario: Back undoes the switch
- **WHEN** I switch the ranking basis and press back
- **THEN** the previous basis is restored, as it is today

#### Scenario: Back undoes a period step
- **WHEN** I step the recap to another year and press back
- **THEN** the previous year's recap is shown, as it is today
