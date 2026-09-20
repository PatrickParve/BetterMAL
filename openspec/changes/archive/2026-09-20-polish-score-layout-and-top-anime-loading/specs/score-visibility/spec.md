## MODIFIED Requirements

### Requirement: A hidden score occupies the same slot as a shown score
A hidden MAL score SHALL occupy the same space its value would occupy if shown — the same width **and the same height** — with its reveal control placed inside that space where the value itself would sit. Hiding or revealing a score SHALL NOT change the size of the score's slot, and SHALL therefore not reflow, shift, or re-align the text, controls, or tiles around it, nor grow or shrink the line, row, card, or box the slot sits in.

The reserved slot SHALL be sized to the score format the app renders (a two-decimal MAL average) rather than to each individual value, so that a column of hidden and shown scores stays aligned with itself. Its reserved height SHALL be one line of the text the slot sits among, which is what a shown value occupies, rather than the reveal control's own smaller natural height — the control is the shorter of the two forms, so a slot sized to it would grow when the value replaced it.

Where the value is centred within its slot, the control SHALL be centred; where the value is aligned to the start of its slot — as in a labelled chip whose label and value both begin at the chip's leading edge, or a score pinned to the leading side of a card — the control SHALL sit at the start; and where the value is aligned to the end of its slot — as in a right-aligned score column — the control SHALL sit at the end. The control SHALL therefore begin, or end, where the number it replaces begins or ends, and SHALL line up with any unscored `—` placeholder shown for my score in the same layout. Vertically, the control SHALL sit where the value's own line sits, so revealing moves neither the control's replacement nor anything beside it.

This rule SHALL hold in both score densities — inside a score chip and as a bare coloured value in a dense row — and wherever a hidden score appears: My List rows, the Top anime page, the anime detail page, series-page entry rows, timeline and extra tiles, and the profile page's divergence lists and top-series chips. Where a surface shows more than one independently revealable figure — the anime detail page's box, which holds a MAL score and a rank that reveal separately — the box SHALL hold one height across every combination of their revealed and hidden states, so revealing either one, or both, moves nothing.

The season and search pages' browse cards are the one exception: there a hidden MAL score SHALL be omitted from the card entirely — no value, no placeholder, and no reveal control — rather than keeping a slot. Those grids show many cards at once, each carrying a single score at the end of its meta line with nothing after it, so an omitted score cannot pull any other text out of position, while a grid of reveal controls would read as a wall of eye icons on pages the user is browsing rather than reading their own scores from. The type and episode count at the start of that meta line SHALL stay where they are whether or not a score is drawn beside them.

#### Scenario: Revealing a score shifts nothing
- **WHEN** the hide toggle is on and I use one row's reveal control
- **THEN** the value appears in the space the control occupied, and no other text or control in that row or the rows around it moves

#### Scenario: Revealing a score does not grow its box
- **WHEN** the hide toggle is on and I reveal a score that sits on a labelled line inside a box — the anime detail page's MAL score or its rank
- **THEN** the box is exactly as tall as it was, and everything below it stays where it was

#### Scenario: A box with two revealable figures holds one height
- **WHEN** I reveal the anime detail page's MAL score, then its rank, then reload with both hidden
- **THEN** the box holding them is the same height in all four combinations of the two states

#### Scenario: A column of scores stays aligned
- **WHEN** the hide toggle is on, "Always show MAL scores for completed and dropped shows" is on, and a list contains completed and dropped rows showing a value alongside rows showing the reveal control
- **THEN** the score slots of all those rows line up in the same column with the same width

#### Scenario: Trailing text is not pushed out
- **WHEN** the hide toggle is on and I look at a profile divergence row, which reads `MAL <MAL score> · Me <my score>`
- **THEN** the row's trailing text ends at the same place it would if the MAL score were shown, rather than being pushed further by the hidden state

#### Scenario: The control is centred where the value is centred
- **WHEN** the hide toggle is on and I look at a compact MAL score chip, such as a profile top-series tile's chip pair, whose value is centred
- **THEN** the reveal control sits centred within the chip, in the position the value would have occupied

#### Scenario: The control starts where a labelled chip's value starts
- **WHEN** the hide toggle is on and I look at a Top anime showcase card's "MAL" chip, whose label and value both begin at the chip's leading edge
- **THEN** the reveal control sits at that leading edge, starting where the score would start and lining up with the `—` shown in the "My score" chip beside it when I have not scored that anime

#### Scenario: The control ends where a right-aligned column's value ends
- **WHEN** the hide toggle is on and I look at a Top anime flat row (rank 11 and beyond), whose MAL score column is aligned to the end of its cell
- **THEN** the reveal control ends where the score would end, so the column's hidden and shown rows line up with each other

#### Scenario: The control starts where a leading-edge value starts
- **WHEN** the hide toggle is on and I look at a Top anime ranks-4-to-10 card, whose MAL score sits at the card's leading edge and whose my-score sits at its trailing edge
- **THEN** the reveal control starts at that leading edge, where the score would start, so it is inset from its edge by the same amount the my-score value is inset from the opposite edge

#### Scenario: A browse card drops its score rather than reserving a slot
- **WHEN** the hide toggle is on and I look at the season page or the search results page
- **THEN** no card shows a score, a placeholder, or a reveal control, and each card's type and episode count sit exactly where they do when scores are shown
