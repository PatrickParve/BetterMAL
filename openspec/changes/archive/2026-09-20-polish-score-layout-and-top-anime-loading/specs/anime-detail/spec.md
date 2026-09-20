## MODIFIED Requirements

### Requirement: The two score boxes share one size

The detail page's two score boxes — the MAL box (MAL score, rank, popularity) and my box (my score, and the rewatch count and finish date when it has them) — SHALL be drawn at one shared width: that of whichever of the two needs more room for its own content. Neither box SHALL be narrower than the other, whatever it happens to hold.

That shared width SHALL be the wider box's natural content width, so the pair stays sized to its content and is not stretched to fill the main column, per "Single anime detail layout". The two boxes SHALL also keep the shared height they have today, so the pair reads as one matched figure block beside the title.

Whichever box is wider SHALL be allowed to vary with the anime: my box grows as it gains a rewatch-count line and a finish-date line, and it SHALL take the pair with it when it becomes the wider of the two.

The pair's size SHALL depend on what the boxes hold, and never on whether what they hold is currently hidden or revealed. The MAL box carries two independently revealable figures — the MAL score and the rank — and it SHALL be drawn at one height across every combination of their states, from both hidden to both revealed, rather than growing as each is uncovered. Revealing either figure SHALL therefore move neither the other box nor anything below the pair.

Where my box is not shown at all — no score of my own — the MAL box SHALL render at its own content width, exactly as it does today.

Where the viewport is narrow enough that the two boxes stack, they SHALL keep one shared width in that stacked arrangement too.

The MAL box SHALL remain the leading box of the two, with my box following it, per the `score-presentation` capability's ordering of a MAL/mine score pair.

#### Scenario: My box is the wider one

- **WHEN** the detail page shows my score, a rewatch count, and a finish date beside the MAL box
- **THEN** both boxes are drawn at the width my box needs, and neither is narrower than the other

#### Scenario: The MAL box is the wider one

- **WHEN** my box holds my score alone and the MAL box's rank and popularity lines are longer
- **THEN** both boxes are drawn at the width the MAL box needs

#### Scenario: The pair stays content-sized

- **WHEN** either box is the wider one
- **THEN** the pair still occupies only the width its content needs, rather than each box taking half the main column

#### Scenario: The boxes stay level

- **WHEN** the two boxes hold a different number of lines
- **THEN** they are drawn at the same height as well as the same width

#### Scenario: Revealing the MAL score does not resize the box

- **WHEN** MAL scores are hidden and I use the MAL score's reveal control
- **THEN** the box is exactly as tall as it was before, and my box beside it and everything below the pair stay where they were

#### Scenario: Revealing the rank does not resize the box either

- **WHEN** MAL scores are hidden and I use the rank's own reveal control, with the MAL score left hidden
- **THEN** the box is again exactly as tall as it was, and revealing the score afterwards changes nothing further

#### Scenario: The box is at its full height before anything is revealed

- **WHEN** the detail page first renders with both the MAL score and the rank hidden
- **THEN** the box already stands at the height it will have with both revealed, rather than growing into it

#### Scenario: Only the MAL box is shown

- **WHEN** the anime has no score of mine and only the MAL box is drawn
- **THEN** that box is sized to its own content, unchanged from today

#### Scenario: Stacked on a narrow viewport

- **WHEN** the viewport is narrow enough that the two boxes stack
- **THEN** both boxes are still drawn at one shared width
