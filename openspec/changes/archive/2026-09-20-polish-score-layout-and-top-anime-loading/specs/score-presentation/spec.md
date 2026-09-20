## ADDED Requirements

### Requirement: A MAL/mine score pair leads with MAL

Wherever one anime's MAL score and my own score are shown together, MAL's score SHALL come first in reading order and mine SHALL follow it. The rule SHALL hold in both densities and in every arrangement the app uses for a pair: two chips side by side, two coloured values in a row's cells, two labelled lines in stacked boxes, and a single line of running text naming both.

The order SHALL be a property of the pair rather than of the page, so every surface showing both figures agrees and the reader never has to check a label to know which side is which. A surface SHALL NOT re-order the pair for its own layout reasons.

This SHALL apply to the series browser's cards, the series page's average-score chips and its entry rows, series timeline cards and extra tiles, the anime detail page's two score boxes, My List rows, all three tiers of the Top anime page, the profile page's Top series tiles and both opinion-divergence lists, and the recap page's hot takes. Any later surface showing both figures SHALL follow it too.

Ordering SHALL change position only. Each figure keeps the colour role, density, label, and hide/reveal behaviour it already has, and a pair that is a rank-ordered column of cells keeps the column's own alignment — a MAL score aligned to the trailing edge of its cell stays aligned there, now in the leading cell of the two.

Where only one of the two figures is shown, the rule SHALL have nothing to say: a surface showing a MAL score alone — the season, year, and search browse cards — is unaffected, and so is one showing my score alone.

#### Scenario: A pair of chips puts MAL first

- **WHEN** I look at any two score chips shown side by side — a Top anime showcase card, a series page average, a timeline card, an extra tile, a profile top-series tile
- **THEN** the MAL chip is the leading one and my score's chip follows it

#### Scenario: A dense row's score cells put MAL first

- **WHEN** I look at a My List row, a Top anime flat row, or a Top anime ranks-4-to-10 card
- **THEN** the MAL score cell comes before my own score's cell in reading order

#### Scenario: A line of running text names MAL first

- **WHEN** I look at a profile opinion-divergence row or a recap hot take, which name both scores in one line
- **THEN** the line names MAL's score first and mine second

#### Scenario: Every surface agrees

- **WHEN** I move between the series browser, a series page, My List, Top anime, the profile page, the recap page, and an anime's detail page
- **THEN** MAL's score sits on the same side of my own on all of them

#### Scenario: Ordering changes nothing else

- **WHEN** I compare a score pair before and after this ordering
- **THEN** both figures carry the same colour role, density, labels, and hide/reveal behaviour they had, and the surface's own column alignment is unchanged

#### Scenario: A lone score is unaffected

- **WHEN** I look at a season, year, or search browse card, which shows a MAL score and no score of mine
- **THEN** that card is unchanged
