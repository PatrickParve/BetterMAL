## MODIFIED Requirements

### Requirement: Score styling never changes score visibility
The colour and form of a score SHALL be presentation only. The system SHALL NOT change, through this convention, whether a MAL score is hidden, whether it offers a per-score reveal control, or whether the "Always show MAL scores for completed shows" setting applies to it — all of which continue to follow the `score-visibility` capability.

A hidden MAL score SHALL keep its reveal control inside whichever form it is rendered in, and SHALL still keep its value out of the rendered output. That control SHALL carry the MAL colour role, so a hidden score still reads as a MAL score rather than as an empty slot — in a chip, in a bare coloured value, and anywhere else a MAL score is rendered.

A hidden score SHALL NOT change the geometry of the form it sits in: a chip holding a hidden score SHALL be the same size as the same chip holding a value, and a coloured value's slot SHALL be the same width either way, per the `score-visibility` capability's slot rule.

#### Scenario: A hidden score inside a chip
- **WHEN** the hide-scores toggle is on and I look at a MAL score chip
- **THEN** the chip shows its reveal control in the MAL colour, the value is absent from the rendered output, and the chip still reads as the MAL slot

#### Scenario: A hidden chip is the size of a filled one
- **WHEN** the hide-scores toggle is on and a row of chips holds both a hidden MAL score and a shown score of mine
- **THEN** the two chips are the same size and share the row evenly, exactly as they do when both show values

#### Scenario: Revealing a restyled score
- **WHEN** I use the reveal control on a restyled MAL score
- **THEN** only that score is revealed, in place, exactly as before

#### Scenario: Completed-score setting is unaffected
- **WHEN** "Always show MAL scores for completed shows" is on and I look at a completed anime's MAL score anywhere it is rendered
- **THEN** it is shown in full with no reveal control, exactly as before the restyle
