# score-presentation Specification

## Purpose
TBD - created by change add-top-series-and-unify-score-styling. Update Purpose after archive.

## Requirements

### Requirement: App-wide MAL/mine score colour roles
The system SHALL use one colour convention for scores across every view: a MAL score SHALL carry the app's blue — the same colour the airing-progress bar fills with for episodes aired — and my own score SHALL carry the app's purple accent. The convention SHALL hold wherever either score is rendered, so which figure is "the world's" and which is "mine" is readable without reading a label.

The two roles SHALL be defined as their own named colour roles rather than by reusing the status colours directly, so that "MAL score" and "Completed status" can be given different colours later without one change silently altering the other.

Green SHALL NOT be used for either score. Green marks that something is on air right now and nothing else.

#### Scenario: The same two scores look the same on every page
- **WHEN** I look at a MAL score and my score on the anime detail page, in My List, on the Top anime page, on the profile page, and on a series page
- **THEN** every MAL score carries the blue role and every score of mine carries the purple role, on all of them

#### Scenario: Score colours match the progress colours
- **WHEN** I compare a MAL score to the aired fill of a progress bar, and my score to the watched fill
- **THEN** the MAL score matches the aired fill's colour and my score matches the watched fill's colour

#### Scenario: Green is never a score colour
- **WHEN** a view shows both an on-air indicator and scores
- **THEN** the on-air indicator is green while both scores stay blue and purple

### Requirement: Two densities of the same score language
The system SHALL render a score in one of two forms, both carrying the colour role from the requirement above:

- as a **chip** — a tinted, bordered block carrying the value and, where one applies, a label naming what the score is — wherever a score stands on its own as a figure;
- as a **coloured value** — the value alone, colour-keyed and using tabular figures, with no block around it — wherever scores appear as cells in a dense list of rows.

Which form a surface uses SHALL be a property of that surface's density, not of the score, and both forms SHALL always agree on the colour role.

#### Scenario: A standalone score is a chip
- **WHEN** I open the anime detail page or a series page, where each score is presented as its own figure
- **THEN** those scores appear as colour-keyed chips

#### Scenario: A dense list uses coloured values
- **WHEN** I open My List or the Top anime page, where scores are one column among several across many rows
- **THEN** the scores appear as colour-keyed values without a chip around each one, and the rows keep their existing height and column alignment

### Requirement: Score controls match the score they set
The system SHALL apply the "mine" colour role to the controls that set my score — the inline score control on a My List row and the score control in the entry editor — so that setting a score looks like the score it sets. Those controls SHALL remain standard form controls with unchanged keyboard, pointer, and mobile behaviour.

A control showing no score SHALL render neutrally rather than in the "mine" colour, since there is no score to colour.

#### Scenario: Setting a score looks like the score
- **WHEN** I look at the score control on a My List row for an anime I have scored
- **THEN** the control carries the same purple role as the score itself

#### Scenario: An unscored entry's control is neutral
- **WHEN** I look at the score control for an anime I have not scored
- **THEN** the control renders neutrally rather than in the score colour

#### Scenario: The control still behaves like a form control
- **WHEN** I open and use the score control by keyboard or by pointer
- **THEN** it behaves exactly as it did before, including on mobile

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
