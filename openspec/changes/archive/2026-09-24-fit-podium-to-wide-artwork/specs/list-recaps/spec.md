## MODIFIED Requirements

### Requirement: The top five are presented as a podium
The recap SHALL present the five highest-ranked anime of its top 10 as a podium of cards rather than as rows, so the best of a period is identifiable at a glance rather than only by reading its numeral.

The podium SHALL be laid out as a descending row: the cards SHALL sit side by side in rank order, the first-ranked card the largest and leading the row, each following card no larger than the one before it, and the fourth- and fifth-ranked cards SHALL be visibly smaller than the third. Every card's lower edge SHALL sit on one line, so the row's silhouette steps down from first to fifth. The podium SHALL occupy the full width available to the top-10 section rather than being capped short of it, so the section holds no empty band beside the cards.

Each card SHALL carry, at minimum:

- its rank, in a badge coloured for that rank — gold for first, silver for second, bronze for third, and for fourth and fifth a neutral badge visibly subordinate to the three medals and distinct from all of them, in both the light and the dark theme;
- the anime's picture at a size that reads as artwork rather than as a thumbnail, with a placeholder of the same size when the anime has no picture. A poster fills the card's poster-sized picture area. A picture that is not a poster is drawn whole and centred in that same area, so every card keeps its size whatever its picture's shape, with no blurred fill and no box behind it, only the card's own surface around it;
- the anime's title, up to two lines, with the full title available on hover when it is truncated;
- its score on the currently selected ranking basis, in the app's existing score language for that basis.

Every card's score SHALL sit in a box of one shared size — the same box on the fifth card's smaller card as on the first card's larger one — wide enough for the widest value either ranking basis can print, which is a two-digit score under my scores and a two-decimal figure under MAL's, and set in tabular figures so the five scores read as a column.

The whole card SHALL be one link to the anime's page, and following it SHALL open the same page the equivalent row would have.

The five cards SHALL appear in rank order in the page's reading and keyboard order, first-ranked first. Where the display is too narrow for five cards side by side, the podium SHALL reflow onto fewer cards per line — and at the narrowest into a single column, each card at the full width of the column — always in rank order, filling each line before starting the next. Once the cards no longer share one line the descending sizes MAY be dropped, since cards on different lines cannot be compared by size.

The podium SHALL show only as many cards as the period has entries: two entries SHALL produce two cards and one entry a single card, with no placeholder or empty card standing in for a missing rank.

#### Scenario: The best of a period stands out
- **WHEN** a recap with ten or more entries is shown
- **THEN** its top five appear as cards in one descending row, the first-ranked card largest and leading, the fourth and fifth smaller than the third, and every card's lower edge on one line

#### Scenario: A landscape picture does not change the card
- **WHEN** the podium's first-ranked anime has a landscape picture and the others hold posters
- **THEN** the first card is exactly the height it would be with a poster, is still the widest card and leads the row, and the whole picture is drawn centred in its picture area with no blurred fill and no box behind it, and the row still steps down from first to third, with the fourth and fifth the same height

#### Scenario: A square picture is drawn whole
- **WHEN** a podium card's anime has a square picture
- **THEN** the whole picture is drawn across the card's picture width, centred, with no blurred fill and no box behind it, and the card's height, badge, title and score box are unchanged

#### Scenario: An upright picture is drawn whole
- **WHEN** a podium card's anime has an upright picture, such as a 4:5 picture
- **THEN** the card is the same size it would be with a poster, and the whole picture is drawn centred in its picture area with no blurred fill and no box behind it

#### Scenario: The podium fills its section
- **WHEN** the top-10 section is wider than the podium's cards need
- **THEN** the five cards spread to fill that width rather than leaving an empty band beside them

#### Scenario: Rank is colour-coded
- **WHEN** I look at the podium
- **THEN** the first, second, and third cards carry gold, silver, and bronze rank badges, the fourth and fifth carry a neutral badge subordinate to those three, and each is legible in both the light and the dark theme

#### Scenario: Every card's score box is the same size
- **WHEN** the podium shows five cards of different widths
- **THEN** the five score boxes are identical in size and aligned as a column, and a score of `10` fits its box without wrapping or clipping

#### Scenario: A MAL score fits the same box
- **WHEN** I switch the ranking basis to MAL's scores
- **THEN** each card's two-decimal MAL figure fits the same box the my-score figures used, with no card's box resizing

#### Scenario: A card opens its anime
- **WHEN** I follow any podium card
- **THEN** that anime's page opens, exactly as following its row would have

#### Scenario: Reading order follows rank
- **WHEN** I move through the podium by keyboard or with a screen reader
- **THEN** I reach the first-ranked card first, then the second, and so on to the fifth

#### Scenario: Narrow displays reflow the podium
- **WHEN** the display is too narrow for five cards side by side
- **THEN** the cards reflow onto fewer per line in rank order, filling each line before the next

#### Scenario: The narrowest display stacks the podium
- **WHEN** the display is too narrow for even two cards side by side
- **THEN** the cards stack in one column in rank order, first-ranked at the top, each at the full width of the column

#### Scenario: A period with two entries
- **WHEN** a recap's included set holds only two anime
- **THEN** two cards are shown and no empty third, fourth, or fifth card appears

#### Scenario: A long title does not change the card
- **WHEN** one card's title needs two lines and another's needs one
- **THEN** both cards are the same height, and the truncated title is available in full on hover
