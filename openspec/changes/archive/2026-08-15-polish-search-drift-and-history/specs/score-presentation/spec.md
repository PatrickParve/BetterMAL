## MODIFIED Requirements

### Requirement: Two densities of the same score language
The system SHALL render a score in one of two forms, both carrying the colour role from the requirement above:

- as a **chip** — a tinted, bordered block carrying the value and, where one applies, a label naming what the score is — wherever a score stands on its own as a figure;
- as a **coloured value** — the value alone, colour-keyed and using tabular figures, with no block around it — wherever scores appear as cells in a dense list of rows, and wherever a score sits as one labelled line among other labelled lines.

Which form a surface uses SHALL be a property of that surface's density, not of the score, and both forms SHALL always agree on the colour role.

The series page's average-score figures, and the compact score pairs on series timeline cards, series extra tiles, and the profile page's top-series tiles, SHALL use the chip form: each is a figure standing on its own. The anime detail page's MAL score and my score SHALL NOT — they sit inside boxes of labelled lines (rank, popularity, rewatch count, completed date), so a chip there would raise the score out of a rhythm it belongs to; those two SHALL use the coloured-value form on a labelled line, per the `anime-detail` capability.

#### Scenario: A standalone score is a chip
- **WHEN** I open a series page, where each average score stands on its own as a figure
- **THEN** those scores appear as colour-keyed chips

#### Scenario: A dense list uses coloured values
- **WHEN** I open My List or the Top anime page, where scores are one column among several across many rows
- **THEN** the scores appear as colour-keyed values without a chip around each one, and the rows keep their existing height and column alignment

#### Scenario: A labelled line uses a coloured value
- **WHEN** I open the anime detail page, where the MAL score and my score sit among other labelled lines inside their boxes
- **THEN** each score is a colour-keyed number on its own labelled line, with no chip around it

#### Scenario: Both forms agree on colour
- **WHEN** I compare the detail page's MAL score to a series page's MAL score chip
- **THEN** both carry the same blue role, differing only in the form around the number
