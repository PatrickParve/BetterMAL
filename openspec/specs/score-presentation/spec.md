# score-presentation Specification

## Purpose
The score-presentation capability defines the app-wide colour roles a score is drawn in — MAL's community score and my own score, always in the same two colours — and their two densities, with a paired MAL/mine chip always keeping both values on one line. Any control that sets or selects a score carries that score's role colour, and the styling never changes whether a score is shown. Whether a MAL score shows at all is score-visibility's; section-colour-language reuses these two roles but doesn't redefine them.

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

### Requirement: Two densities of the same score language
The system SHALL render a score in one of two forms, both carrying the colour role from the requirement above:

- as a **chip** — a tinted, bordered block carrying the value and, where one applies, a label naming what the score is — wherever a score stands on its own as a figure;
- as a **coloured value** — the value alone, colour-keyed and using tabular figures, with no block around it — wherever scores appear as cells in a dense list of rows, and wherever a score sits as one labelled line among other labelled lines.

Which form a surface uses SHALL be a property of that surface's density, not of the score, and both forms SHALL always agree on the colour role.

The series page's average-score figures, the compact score pairs on series timeline cards, series extra tiles, and the profile page's top-series tiles, and the Top anime page's rank 1–3 showcase cards SHALL use the chip form: each is a figure standing on its own. The Top anime page's rank 4–10 cards and its flat rows SHALL NOT — they are a dense grid and a dense list, where a pair of chips per entry would crowd the card and break the rows' column alignment; those SHALL use the coloured-value form. The anime detail page's MAL score and my score SHALL NOT either — they sit inside boxes of labelled lines (rank, popularity, rewatch count, completed date), so a chip there would raise the score out of a rhythm it belongs to; those two SHALL use the coloured-value form on a labelled line, per the `anime-detail` capability.

Adopting the chip form SHALL NOT change a MAL score's hide/reveal behaviour: a hidden MAL score inside a chip SHALL stay hidden, revealing on its own control exactly as it does in the coloured-value form.

#### Scenario: A standalone score is a chip
- **WHEN** I open a series page, where each average score stands on its own as a figure
- **THEN** those scores appear as colour-keyed chips

#### Scenario: The top-3 showcase cards use chips
- **WHEN** I open the Top anime page and look at the rank 1–3 showcase cards
- **THEN** each card's MAL score and my score appear as labelled, colour-keyed chips, so which figure is MAL's and which is mine is readable from the label as well as the colour

#### Scenario: The rest of the top-anime ranking stays dense
- **WHEN** I look at the same page's rank 4–10 cards and its rows from rank 11 down
- **THEN** their scores appear as colour-keyed values with no chip around them, and the rows keep their existing height and column alignment

#### Scenario: A dense list uses coloured values
- **WHEN** I open My List, where scores are one column among several across many rows
- **THEN** the scores appear as colour-keyed values without a chip around each one, and the rows keep their existing height and column alignment

#### Scenario: A labelled line uses a coloured value
- **WHEN** I open the anime detail page, where the MAL score and my score sit among other labelled lines inside their boxes
- **THEN** each score is a colour-keyed number on its own labelled line, with no chip around it

#### Scenario: Both forms agree on colour
- **WHEN** I compare the detail page's MAL score to a series page's MAL score chip
- **THEN** both carry the same blue role, differing only in the form around the number

#### Scenario: Hiding scores still works inside a chip
- **WHEN** MAL scores are hidden and I open the Top anime page
- **THEN** the showcase cards' MAL chips show the reveal control instead of the value, and revealing one shows that score alone

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

### Requirement: A score-role control carries its role's colour
Where a control's options select between the two score roles — which score a list is ranked by, which score a view shows — each option SHALL carry the colour of the role it selects: the option choosing MAL's score in the MAL role's blue, the option choosing my score in the mine role's purple. A control that switches to somebody else's opinion SHALL NOT light up in the colour this app uses for my own.

This SHALL apply to the recap page's top-10 ranking-basis toggle and the profile page's Top series ranking-basis control, and to any later control offering the same choice.

The colour SHALL be carried through the control's states as the surface's own control styling defines them — at minimum its selected state and its hover state — rather than appearing in one state and reverting to the page accent in another. Carrying a role's colour SHALL change the control's colour only: its size, shape, and the set of states it distinguishes SHALL be unchanged, so a cluster it sits in SHALL NOT reflow.

Controls that select something other than a score role — a media type, a time filter, a period — SHALL NOT take a score role's colour, so the blue and the purple keep meaning "MAL's opinion" and "mine" rather than merely "selected".

#### Scenario: Choosing MAL's score is blue
- **WHEN** I select the **MAL score** option on the recap's top 10 or on the profile's Top series
- **THEN** that option is drawn in the MAL role's blue, matching the scores the list then ranks on

#### Scenario: Choosing my score is purple
- **WHEN** I select the **My score** option on either control
- **THEN** that option is drawn in the mine role's purple

#### Scenario: The colour holds across the control's states
- **WHEN** I hover the **MAL score** option and then select it
- **THEN** both states are drawn in the MAL role's blue rather than one of them reverting to the page's accent

#### Scenario: A non-score control keeps the accent
- **WHEN** I select a media type on the profile page or a time filter on the recap page
- **THEN** the selected option is drawn in the page's accent, not in either score role's colour

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
