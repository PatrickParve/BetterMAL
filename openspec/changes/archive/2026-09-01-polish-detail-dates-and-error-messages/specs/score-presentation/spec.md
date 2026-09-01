## ADDED Requirements

### Requirement: Paired score chips put their values on one line
Where two score chips are shown side by side — a MAL score beside my score — their values SHALL sit on the same line as each other, so the pair reads as one row of figures rather than as two figures at different heights.

This SHALL hold regardless of which form each value takes: my score is a plain number, while a MAL score is a hide/reveal-capable value that may render as a number or as its reveal control. Neither form SHALL sit higher or lower than the other, and swapping between a hidden MAL score and a revealed one SHALL NOT move either value.

Alignment SHALL be a property of the shared chip rather than of any one page, so every paired chip in the app — the Top anime page's rank 1–3 showcase cards, the series page's average-score chips, and the compact pairs on series timeline cards and extra tiles — agrees without each restating it. The chips' existing size, tint, border, labels, and colour roles SHALL be unchanged.

#### Scenario: The showcase cards' two scores align
- **WHEN** I open the Top anime page and look at a rank 1–3 showcase card
- **THEN** its MAL score and my score sit on the same line, with neither lower than the other

#### Scenario: Revealing a hidden score does not move it
- **WHEN** MAL scores are hidden and I reveal the MAL score on a showcase card
- **THEN** the revealed number sits exactly where the reveal control sat, still level with my score beside it

#### Scenario: The same holds in the compact form
- **WHEN** I look at a series timeline card's or extra tile's pair of compact chips
- **THEN** their two values sit on one line as well

#### Scenario: Nothing else about the chips changes
- **WHEN** I compare a chip before and after this alignment
- **THEN** its size, tint, border, label, and colour role are the same
