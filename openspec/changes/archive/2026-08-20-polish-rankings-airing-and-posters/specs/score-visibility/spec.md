## MODIFIED Requirements

### Requirement: A hidden score occupies the same slot as a shown score
A hidden MAL score SHALL occupy the same horizontal space its value would occupy if shown, with its reveal control placed inside that space where the value itself would sit. Hiding or revealing a score SHALL NOT change the width of the score's slot, and SHALL therefore not reflow, shift, or re-align the text, controls, or tiles around it.

The reserved slot SHALL be sized to the score format the app renders (a two-decimal MAL average) rather than to each individual value, so that a column of hidden and shown scores stays aligned with itself.

Where the value is centred within its slot, the control SHALL be centred; where the value is aligned to the start of its slot — as in a labelled chip whose label and value both begin at the chip's leading edge — the control SHALL sit at the start; and where the value is aligned to the end of its slot — as in a right-aligned score column or a score pinned to the trailing side of a card — the control SHALL sit at the end. The control SHALL therefore begin, or end, where the number it replaces begins or ends, and SHALL line up with any unscored `—` placeholder shown for my score in the same layout.

This rule SHALL hold in both score densities — inside a score chip and as a bare coloured value in a dense row — and wherever a hidden score appears: My List rows, the Top anime page, the anime detail page, series-page entry rows, timeline and extra tiles, and the profile page's divergence lists and top-series chips.

#### Scenario: Revealing a score shifts nothing
- **WHEN** the hide toggle is on and I use one row's reveal control
- **THEN** the value appears in the space the control occupied, and no other text or control in that row or the rows around it moves

#### Scenario: A column of scores stays aligned
- **WHEN** the hide toggle is on, "Always show MAL scores for completed and dropped shows" is on, and a list contains completed and dropped rows showing a value alongside rows showing the reveal control
- **THEN** the score slots of all those rows line up in the same column with the same width

#### Scenario: Trailing text is not pushed out
- **WHEN** the hide toggle is on and I look at a profile divergence row, which reads `Me <my score> · MAL <MAL score>`
- **THEN** the row's trailing text ends at the same place it would if the MAL score were shown, rather than being pushed further by the hidden state

#### Scenario: The control is centred where the value is centred
- **WHEN** the hide toggle is on and I look at a compact MAL score chip, such as a profile top-series tile's chip pair, whose value is centred
- **THEN** the reveal control sits centred within the chip, in the position the value would have occupied

#### Scenario: The control starts where a labelled chip's value starts
- **WHEN** the hide toggle is on and I look at a Top anime showcase card's "MAL" chip, whose label and value both begin at the chip's leading edge
- **THEN** the reveal control sits at that leading edge, starting where the score would start and lining up with the `—` shown in the "My score" chip beside it when I have not scored that anime

#### Scenario: The control ends where a trailing value ends
- **WHEN** the hide toggle is on and I look at a Top anime ranks-4-to-10 card, whose my-score sits at the card's leading edge and whose MAL score sits at its trailing edge
- **THEN** the reveal control ends at that trailing edge, where the score would end, so it is inset from its edge by the same amount the my-score value is inset from the opposite edge
