# section-colour-language Specification

## Purpose
The section-colour-language capability defines the cross-page vocabulary of colour families a page section can be drawn in — the achromatic year family, the season family, the hot-take family, and the existing MAL/mine score roles reused rather than redefined — and the tinted-title treatment that carries a family onto a section's heading and, where a control selects the same subject, onto that control too. It exists so that what a section on the recap or profile page is about — a season, a year, a disagreement between my opinion and MAL's — is readable from colour alone, consistently, on both pages, without redefining or disturbing the podium medals, the score-board tiers, the status colours, or either score role that the colours are drawn from or sit alongside.

## Requirements

### Requirement: Named section colour families
The app SHALL define a small set of named **colour families**, each a pair of related colours, that a page section can be drawn in so what a section is about is readable before its text is:

- **year** — achromatic: a silver running to the theme's own extreme, black in the light theme and white in the dark one. The year family SHALL be the only family carrying no hue, so a year-level section is tellable from every other section by the absence of colour rather than by a colour of its own;
- **season** — an orange and a yellow;
- **hot take** — a fiery red running to an orange;
- **MAL** and **mine** — the two score colour roles the `score-presentation` capability already defines, reused here rather than redefined, so a section about MAL's opinion carries the same blue a MAL score carries and a section about mine carries the same purple.

Each family SHALL be defined as its own named colour pair rather than by reusing an existing token in place, so a family's colour can change without silently altering the podium medals, the score-board tiers, the status colours, or either score role.

Every family SHALL be legible against the page background in both the light and the dark theme, and the five families SHALL be tellable apart from one another in both.

The achromatic year family SHALL be kept clear of the two neutrals it could otherwise be confused with. It SHALL NOT open at the colour an untinted heading is drawn in, or a year-level title would read as a title nobody had tinted; and it SHALL be a true neutral rather than the cool slate the podium's silver medal and the score board's silver tier use, so the two are tellable apart even though families are never drawn on a card or a slot and medals are never drawn on a title. No family's colours SHALL be so close to the podium's gold, silver, or bronze, or to a score-board tier, that a section title reads as a rank.

A family SHALL be identifiable from the first of its two colours alone, since a title may cover only the opening portion of its ramp (see the next requirement).

#### Scenario: Families are distinguishable
- **WHEN** a page shows sections drawn in the year, season, hot-take, MAL, and mine families
- **THEN** each is tellable apart from the other four, in the light theme and in the dark theme alike

#### Scenario: A family is not a rank
- **WHEN** a section drawn in a family sits on the same page as the podium's medals or the score board's tiers
- **THEN** the family's colour is not mistakable for a medal or a tier colour

#### Scenario: The achromatic family still reads as tinted
- **WHEN** a year-family title sits on the same page as an untinted section title
- **THEN** the two are tellable apart, the year title opening at a silver plainly lighter than the colour the untinted heading is drawn in

#### Scenario: The achromatic family in both themes
- **WHEN** I view a year-family title in the light theme and again in the dark theme
- **THEN** it runs from silver toward black in the first and from silver toward white in the second, legible against the page background in both

#### Scenario: The score families are the score roles
- **WHEN** a section is drawn in the MAL family and a MAL score is shown on the same page
- **THEN** the two carry the same blue, and likewise the mine family and my own score carry the same purple

### Requirement: Tinted section titles
A section title drawn in a family SHALL be tinted by painting the title's own text with that family's two colours as a gradient, with no box, border, tint, or rule added around it. The gradient SHALL be measured across the full width of the section the title heads — the list, table, or block below it — rather than across the title's own text box, so the title's letters take the colours of the part of the ramp sitting over them and each section's title reads as the opening slice of one ramp spanning its content.

Tinting SHALL be decoration on top of a title that is otherwise unchanged: the title SHALL keep the size, weight, letter-spacing, margin, and position it has untinted, SHALL remain selectable text, and SHALL NOT gain or lose any box that would move the content beneath it. Tinting SHALL NOT vary with hover, focus, or any page state — it names what the section is, not what is happening to it.

Where the gradient-text technique is unavailable, or where the platform is rendering in a forced-colours mode, the title SHALL fall back to a plain legible colour rather than to transparent or invisible text.

A title's tint SHALL never be the only thing identifying its section: every tinted title SHALL keep the words it has today, so a reader who cannot distinguish the families loses nothing.

#### Scenario: The ramp spans the section, not the text
- **WHEN** a tinted title heads a wide section and its text occupies only part of that width
- **THEN** the title's letters carry the opening portion of the ramp, and the ramp is laid out across the section's full width rather than compressed into the title's own width

#### Scenario: Tinting moves nothing
- **WHEN** a title becomes tinted
- **THEN** its size, weight, spacing, and position are unchanged and nothing below it shifts

#### Scenario: Tinting does not respond to the pointer
- **WHEN** I move the pointer over a tinted title or over the section it heads
- **THEN** the title's colours do not change

#### Scenario: A title is never invisible
- **WHEN** the gradient-text treatment cannot be applied, including under a forced-colours mode
- **THEN** the title renders in a plain legible colour rather than disappearing

#### Scenario: The words still say it
- **WHEN** I read a tinted title without being able to tell its family's colours apart
- **THEN** the title's text still names the section exactly as it did untinted

### Requirement: A family's title and its control share its colours
Where a control selects the same subject a tinted title describes, the control SHALL carry that subject's family. The recap's **Yearly** recap-type tab SHALL carry the year family, matching the year-level ranking titles a yearly period produces, and its **Season** tab SHALL carry the season family, matching the season-level ranking titles — so the tab pressed and the sections it produces are visibly the same colour.

Carrying a family SHALL change a control's colours only. Every other property of the control — its size, its shape, its states, and how those states differ from one another — SHALL be exactly what it is without a family.

Where a state of the control fills it with the family's colours and prints a label on top, that label SHALL stay legible against the fill beneath it. A family whose colours are light in the current theme SHALL take a dark label there rather than the light label a dark-filled family takes, so the label's colour follows the fill it sits on rather than being fixed for every family alike. The achromatic year family makes this unavoidable: it fills toward black in the light theme and toward white in the dark one, so a single fixed label colour would be unreadable in one theme or the other.

#### Scenario: The tab matches the sections it produces
- **WHEN** I select the recap's **Season** tab and look at the season-level ranking titles below it
- **THEN** the tab and those titles carry the same family's colours

#### Scenario: A family changes colour and nothing else
- **WHEN** I compare a control carrying a family against one that does not
- **THEN** the two are the same size and shape and differ only in colour

#### Scenario: A filled label stays readable in both themes
- **WHEN** the **Yearly** tab is selected in the light theme and again in the dark theme
- **THEN** its label is legible against the family's fill in both, taking a light label over the light theme's dark fill and a dark label over the dark theme's light fill
