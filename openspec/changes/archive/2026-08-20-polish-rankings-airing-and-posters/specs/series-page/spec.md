## ADDED Requirements

### Requirement: Landscape artwork is shown whole on the series page
An entry whose picture is landscape — its intrinsic width greater than its intrinsic height — SHALL have that picture shown whole wherever the series page renders it: the page header's picture, a main-line timeline card's picture, and a More tile's picture. No part of a landscape picture SHALL be cropped away to fill a portrait box.

In the page header, the picture SHALL keep the width it already has and take whatever height its own proportions give it at that width, so the title, status pill, personal badge, year span, links, score averages, and progress beside it keep their existing positions.

On a timeline card and on a More tile, the card's picture area SHALL keep the height it has for portrait artwork, so every card in a row still lines its picture, title, and footer up with its neighbours. The landscape picture SHALL be fitted whole inside that area rather than cropped to fill it, and the card carrying it MAY be wider than its portrait neighbours so that the fitted picture is shown at a useful size rather than reduced to a sliver of the card's height.

A card's extra width SHALL be a consequence of its artwork's orientation alone. It SHALL NOT vary with how long the entry ran, how long the wait before it was, its episode count, its scores, or my progress on it, and SHALL take only one widened size rather than a size computed per image, so a wider card can never be read as a duration or magnitude signal.

This treatment SHALL apply only to landscape artwork. A portrait picture, a square picture, and the placeholder shown when an entry has no picture SHALL keep their existing boxes and their existing card widths unchanged.

Because a picture's proportions are not known until the image itself has loaded, the page SHALL render the existing portrait boxes until then and adopt the landscape treatment once a picture is known to be landscape. The page SHALL NOT request, store, or wait on any additional data to make this decision.

#### Scenario: A landscape header picture is not cropped
- **WHEN** I open a series whose root entry's picture is wider than it is tall
- **THEN** the header shows that whole picture at its own proportions, and the title, pill, badge, year span, links, averages, and progress beside it are positioned exactly as on any other series page

#### Scenario: A landscape timeline card shows its whole picture
- **WHEN** a main-line entry's picture is wider than it is tall
- **THEN** its timeline card shows the whole picture, the card is wider than its portrait neighbours, and its picture area, title, chips, and footer still line up with theirs

#### Scenario: A landscape More tile shows its whole picture
- **WHEN** an extra's picture is wider than it is tall
- **THEN** its More tile shows the whole picture and is wider than the portrait tiles in its group, while its rows stay aligned with them

#### Scenario: Card width still says nothing about duration
- **WHEN** a series has one entry that ran a single cour and another that ran for several years, both with portrait pictures
- **THEN** both cards render at the same width, and the only cards that differ in width anywhere on the page are those whose own artwork is landscape

#### Scenario: Portrait artwork is untouched
- **WHEN** I open a series in which every picture is taller than it is wide
- **THEN** the header picture, every timeline card, and every More tile render exactly as they do today

## MODIFIED Requirements

### Requirement: Series timeline ribbon
The series page SHALL present the main line as one chronological list of cards, one per entry, in watch order — including an entry with no air date yet, such as an announced but unscheduled next season, shown inline in its correct sequence position rather than set apart from the dated entries around it. This section SHALL be the page's only presentation of the main line — there SHALL NOT be a separate, non-chronological list of main-line entries elsewhere on the page.

Every card SHALL be the same fixed size regardless of how long that entry ran, and every pair of adjacent cards SHALL be separated by the same fixed spacing regardless of how long the real wait between them was — a variable-width, aspect-ratio-locked poster reads as inconsistent image sizing rather than as a duration or gap signal, so neither a card's width nor the space around it varies with real elapsed time. The single exception SHALL be a card whose own artwork is landscape, which the landscape-artwork requirement widens to one alternative size for that reason alone; every card of a given orientation SHALL still be the same size as every other card of that orientation, whatever their durations.

Elapsed time on a card SHALL be stated in words rather than implied by position on a scale the cards do not have. There SHALL NOT be a year ruler above the cards, and there SHALL NOT be a connector or any other element between cards stating the wait between them. Instead, each dated card SHALL state its own air range — the month and year it started and the month and year it ended — abbreviating to a single date for an entry that aired on one day. A dated entry that is still broadcasting SHALL state its start month and year and SHALL be marked as still running by its airing indicator rather than by an invented end date.

A calendar year in which no main-line entry aired SHALL never be presented anywhere on the timeline.

Each card SHALL show its picture, title, media type, air range (or an explicit no-date indicator for an entry with none), episode count, my list status, and SHALL link to that anime's detail page and offer an edit control that opens the app's shared entry editor. A card's title SHALL reserve the same vertical space regardless of whether it wraps to one line or two, and a card's air range and episode count SHALL each occupy the same reserved space on every card whatever their content, so no card's layout falls out of alignment with its row neighbours. A card for a currently-airing entry SHALL carry a distinct "airing" indicator rather than restating in text how many episodes have broadcast so far, since that count is already shown as a graphical fill on the card. A card for an entry I have started but not completed SHALL additionally state my watched episode count against that entry's total. A card SHALL show its entry's rewatch count when it is greater than zero.

The airing indicator SHALL name the state in words — a label reading "airing" — rather than relying on chrome alone to carry the meaning, and SHALL be drawn in the page's airing colour rather than in its broadcast colour, so a currently-airing card can never read as a card that is merely selected or focused. It SHALL sit inline in the card's air-range line, in the same position an undated card's no-date indicator occupies, and SHALL NOT be drawn over the poster art — a mark over the artwork can be camouflaged by a poster of a similar colour, while card chrome cannot. Because the indicator is real text, it SHALL be announced by assistive technology without a separate visually-hidden equivalent.

The currently-airing card SHALL additionally be marked at card level by rendering its existing border in the page's airing colour. That card-level mark SHALL NOT surround the card with a glow, SHALL NOT pulse or otherwise animate, and SHALL NOT change the card's size or shift its contents relative to any other card — so it needs no extra room outside the card, and the timeline's own scrolling container cannot clip it. The airing indicator itself SHALL remain visible while the card is hovered or focused; the card-level border MAY take the same hover and focus treatment as any other card, since the indicator and not the border carries the meaning.

Timeline cards SHALL NOT carry a status-coloured edge or border; my list status on a card SHALL be conveyed by its footer text alone. Card chrome SHALL therefore vary only to mark an entry as currently airing or as having no air date, so a coloured card reads unambiguously as one of those two things rather than as one of several list statuses.

Each card SHALL be filled to show how much of that entry I have watched, and SHALL show broadcast progress behind my own fill while that entry is airing.

Each card SHALL carry a paired score readout — MAL's score for that entry and mine — rendered as numeric values in the same colour convention and visual treatment the More section's tiles already use for their score chips (blue for MAL, purple for mine), rather than as an independently height-scaled graphical bar per entry. The MAL side SHALL follow the same hide-scores behaviour as every other MAL score on the page — blurred with a reveal control while the hide-scores toggle is on, shown in full when the entry is one I have completed and scored and the "always show completed scores" setting is on; my own score SHALL always be shown.

Main-line entries with no air date SHALL be shown in their correct watch-order position among the dated cards rather than set apart in their own lane, carrying an explicit no-date indicator so it reads unambiguously as dateless rather than as a dated card with missing information.

The timeline SHALL scroll within its own container when it does not fit, rather than making the page scroll sideways, and SHALL do so without showing a scrollbar — the row SHALL remain scrollable by drag, wheel, or trackpad, but SHALL NOT display a scrollbar track or thumb.

#### Scenario: Main line in watch order
- **WHEN** I open a series with four main-line entries
- **THEN** they are shown in watch order on the timeline, each the same size, showing its picture, title, type, air range, episodes, MAL score, my score, and my status

#### Scenario: Cards are the same size and evenly spaced regardless of duration
- **WHEN** a series has one entry that ran for a single cour and another that ran continuously for several years
- **THEN** both cards render at the same size, and the spacing between every pair of cards on the timeline is the same

#### Scenario: A card states its own air range
- **WHEN** a main-line entry aired from April 2013 to June 2013
- **THEN** its card states that range rather than a bare start year

#### Scenario: A still-airing card states its start and is marked as airing
- **WHEN** a main-line entry started in April 2026 and is still broadcasting
- **THEN** its card's air-range line states April 2026 followed by the airing indicator, with no invented end date

#### Scenario: A multi-year gap is never presented as labelled empty years
- **WHEN** a series has a two-year stretch with nothing airing between two seasons
- **THEN** neither of those years appears anywhere on the timeline

#### Scenario: An airing card is labelled rather than badged over its poster
- **WHEN** a main-line entry is currently airing
- **THEN** its card carries an "airing" label in the page's airing colour within its air-range line, no airing badge is drawn over its poster, and its episode count reads as its total rather than restating in text how many have broadcast so far

#### Scenario: The airing card is not made to look selected
- **WHEN** a main-line entry is currently airing and no card is hovered, focused, or otherwise selected
- **THEN** its card carries no glow around it and nothing on it animates, and its card-level mark is its own border drawn in the airing colour rather than in the page's broadcast colour

#### Scenario: The airing indicator survives hover
- **WHEN** I hover a currently-airing card
- **THEN** its airing label is still visible, and neither the card's size nor the position of its contents changes

#### Scenario: The airing indicator is read as text
- **WHEN** a currently-airing card is read by a screen reader
- **THEN** it is announced as airing from the card's own visible text, with no separate visually-hidden duplicate of that announcement

#### Scenario: The airing indicator is never clipped
- **WHEN** a currently-airing entry's card is the first or last card in the row
- **THEN** its airing label and its coloured border both render in full, uncut by the timeline's own scrolling container

#### Scenario: A long timeline scrolls without a visible scrollbar
- **WHEN** a series has enough main-line entries that the row overflows its container
- **THEN** the row can still be scrolled horizontally, but no scrollbar track is shown beneath it

#### Scenario: Cards carry no status colour
- **WHEN** I open a series with entries I am watching, have completed, and have dropped
- **THEN** none of those cards carries a status-coloured edge, and each states its status in its footer text

#### Scenario: A partly-watched card states my position
- **WHEN** I have watched 5 episodes of a 24-episode entry and have not completed it
- **THEN** its card states my 5 against that entry's 24

#### Scenario: My progress on each card
- **WHEN** I have completed the first season, watched half the second, and not started the third
- **THEN** the first card is fully filled, the second half filled, and the third empty

#### Scenario: Scores shown the same way as the More section
- **WHEN** I open a series where MAL rates the third season lowest and I rate it highest
- **THEN** that card shows both scores as numeric chips in the app's blue-for-MAL/purple-for-mine convention, the same treatment the More section's tiles use

#### Scenario: A MAL score hides and reveals like everywhere else
- **WHEN** the hide-scores toggle is on and a card's entry is not one I have completed and scored
- **THEN** that card's MAL chip is blurred with a reveal control, the same as a MAL score anywhere else on the page

#### Scenario: An unreleased next season is shown in sequence
- **WHEN** a main-line entry has been announced with no air date yet
- **THEN** its card appears in its correct watch-order position among the dated cards, carrying a no-date indicator, rather than being set apart from them

#### Scenario: A rewatched main-line entry shows its count
- **WHEN** a main-line entry has a rewatch count of 2
- **THEN** its card shows a rewatch indicator reading 2

#### Scenario: Two landscape cards are the same width as each other
- **WHEN** a series has two main-line entries with landscape artwork, one that ran for one cour and one that ran for several years
- **THEN** both of their cards render at the same widened size as each other, and every portrait card on the timeline renders at the usual size
