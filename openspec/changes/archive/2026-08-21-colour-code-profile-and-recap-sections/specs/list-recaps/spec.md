## ADDED Requirements

### Requirement: Recap section titles are tinted by family
The recap page SHALL tint the titles of the sections whose subject a colour family names, using the tinted-title treatment the `section-colour-language` capability defines:

- **Biggest Hot takes** SHALL carry the hot-take family;
- **Season ranking** and **Seasons by time watched** SHALL carry the season family;
- **Year ranking** and **Years by time watched** SHALL carry the year family.

A season-level ranking and a year-level ranking SHALL therefore be tellable apart by colour alone, which matters most on a multi-year recap where the two sit side by side in adjacent columns saying nearly the same words.

The page's remaining section titles — **Top N**, **Stats**, and **Rating distribution** — SHALL stay untinted: none of them is about a season, a year, or a disagreement, and tinting every title would leave the tinted ones saying nothing.

Tinting a ranking's title SHALL NOT change anything else about that ranking: its rows, its shared row height, its posters, its "See all" control and overlay, and the two-column grouping of the four rankings SHALL be exactly as they are untinted.

#### Scenario: A season ranking and a year ranking are tellable apart
- **WHEN** a multi-year recap shows a season ranking beside a year ranking
- **THEN** the two titles carry different families' colours, so which column ranks seasons and which ranks years is readable before either title is read

#### Scenario: Both of a level's rankings share a family
- **WHEN** a multi-year recap shows **Season ranking** and **Seasons by time watched** in one column
- **THEN** both titles carry the season family

#### Scenario: Hot takes carry their own family
- **WHEN** a recap shows its **Biggest Hot takes** section
- **THEN** its title carries the hot-take family, distinct from every other family on the page

#### Scenario: Untinted titles stay untinted
- **WHEN** a recap renders its **Top N**, **Stats**, and **Rating distribution** headings
- **THEN** each is drawn in the page's ordinary heading colour

#### Scenario: A tinted ranking behaves identically
- **WHEN** a ranking whose title is tinted holds more rows than it shows
- **THEN** its "See all" control opens the same overlay with the same rows in the same order as it does untinted

## MODIFIED Requirements

### Requirement: The recap's segmented controls show every state
Every segmented control on the recap page — the recap-type tabs, the time filter, and the ranking-basis toggle — SHALL make each of its states visually distinct from every other:

- **unselected at rest**, and **unselected under the pointer**, which SHALL differ from it;
- **selected**, which SHALL be unmistakable as the current choice next to its unselected siblings;
- **selected under the pointer**, which SHALL differ visibly from selected at rest, so moving the pointer onto the option that is already chosen still gives feedback rather than appearing inert;
- **focused by keyboard**, which SHALL show a visible focus indicator distinct from the hover treatment;
- **disabled**, which SHALL read as unavailable and SHALL show neither the hover nor the focus treatment.

All three groups SHALL use the same state *treatments* as one another — the same fill for a selected option, the same tint for a hovered one, the same kind of focus indicator — so the six states are learned once and read the same way in every group.

An individual option MAY carry a colour family (per the `section-colour-language` capability) in place of the page's default accent, in which case every one of its states SHALL be drawn in that family's colours through the treatments above, and no state SHALL be dropped, weakened, or merged with another because of the family it carries. Specifically, the **Yearly** and **Season** recap-type tabs SHALL carry the year and season families; the **Multi-year** tab and both time-filter options SHALL keep the page's accent; and the ranking-basis toggle's options SHALL carry the score-role colours per the "Top 10 ranking basis control" requirement.

None of these states SHALL change the control's height or width, so the cluster a control sits in SHALL NOT reflow as the pointer moves across it, and the controls SHALL remain the same height as the selects and other controls beside them.

#### Scenario: Hovering the already-selected option
- **WHEN** I move the pointer over the recap-type tab that is already selected
- **THEN** it changes visibly from its resting selected appearance

#### Scenario: Hovering an unselected option
- **WHEN** I move the pointer over an unselected option
- **THEN** it changes visibly from its resting appearance, and still reads as unselected next to the selected one

#### Scenario: Keyboard focus is visible
- **WHEN** I reach one of these controls with the keyboard
- **THEN** a visible focus indicator is shown, distinct from the hover treatment

#### Scenario: A disabled filter reads as unavailable
- **WHEN** a time filter option is unavailable because the period holds nothing for it and I move the pointer over it
- **THEN** it reads as unavailable and takes neither the hover nor the focus treatment

#### Scenario: The control cluster does not reflow
- **WHEN** I move the pointer along a row of these controls
- **THEN** none of them changes size and nothing beside them moves

#### Scenario: The groups agree on their treatments
- **WHEN** a recap shows both its type tabs and its time filter
- **THEN** the selected option in each is drawn with the same treatment as the selected option in the other, differing only where an option carries its own colour family

#### Scenario: A family-coloured tab keeps every state
- **WHEN** I look at the **Season** tab unselected, hover it, select it, hover it while selected, and reach it with the keyboard
- **THEN** all five states are as distinct from one another as the **Multi-year** tab's are, drawn in the season family's colours instead of the accent

#### Scenario: Selected is unmistakable whichever family a tab carries
- **WHEN** the **Yearly** tab is selected beside the unselected **Multi-year** and **Season** tabs
- **THEN** it reads as the current choice even though the three tabs carry three different colours

### Requirement: Top 10 of the period
Every recap SHALL show the ten highest-ranked anime of the included set, ranked by the selected ranking basis, with ties broken by title case-insensitively so the order is stable across reloads. Entries with no score on the selected basis SHALL be ranked below every scored entry rather than treated as zero. When the included set holds fewer than ten entries, the recap SHALL show all of them.

The ten SHALL be presented in two forms: the first five as the podium described in "The top five are presented as a podium", and the remaining ranks as rows below it. The split SHALL be presentational only — the same ten anime in the same order are shown either way — and the rows SHALL continue the podium's numbering rather than restarting, both in the rank each row shows and in the list semantics exposed to assistive technology.

The score a row shows SHALL carry the same score colour role the podium's cards carry: my score in the mine role, MAL's score in the MAL role, per the `score-presentation` capability. Falling below the podium SHALL change a score's size and its surround, never whose opinion it is — a row's score SHALL NOT revert to neutral text.

#### Scenario: Ten of many
- **WHEN** the included set holds 40 anime
- **THEN** the ten highest-ranked are shown in descending order

#### Scenario: Fewer than ten
- **WHEN** the included set holds six anime
- **THEN** all six are shown

#### Scenario: The ten split into a podium and rows
- **WHEN** a recap shows ten anime
- **THEN** the first five are shown as podium cards and ranks six through ten as rows beneath them, numbered six through ten

#### Scenario: Five or fewer entries
- **WHEN** the included set holds five or fewer anime
- **THEN** they are all shown on the podium and no row list is shown

#### Scenario: Stable tie order
- **WHEN** several anime share the same score at the cut line
- **THEN** they are ordered by title, and the same order is shown on every reload

#### Scenario: Unscored entries rank last
- **WHEN** the included set holds both scored and unscored anime on the selected basis
- **THEN** every scored anime is ranked above every unscored one

#### Scenario: The score colour survives the cut line
- **WHEN** a recap ranked by my score shows rank five on the podium and rank six as a row
- **THEN** both scores carry the mine role's colour, the row's differing only in the size and surround its density calls for

#### Scenario: A row's score follows the basis
- **WHEN** the top 10 is switched to MAL's score
- **THEN** the scores on ranks six through ten carry the MAL role's colour rather than the mine role's

### Requirement: Top 10 ranking basis control
The recap SHALL let the top 10 be ranked by my score or by MAL's community score whenever the included set is an aired-in-period selection — that is, on a season recap, and on a multi-year or yearly recap whose time filter is **What aired**. Under the **What I watched** filter the control SHALL NOT be offered and the ranking SHALL be by my score.

The control's two options SHALL carry the colour of the score role each selects: **My score** the mine role, **MAL score** the MAL role, per the `score-presentation` capability's requirement that a score-role control carries its role's colour. The option carrying the MAL role SHALL NOT be drawn in the purple this app reserves for my own score.

MAL scores shown in the recap SHALL follow the app's existing MAL-score visibility rules, so a hidden score stays hidden here as it does elsewhere.

#### Scenario: Switching to MAL's ranking
- **WHEN** a season recap's top 10 is switched to MAL's score
- **THEN** the ten are re-ranked by MAL's community score

#### Scenario: Basis control hidden under watch history
- **WHEN** a yearly recap's time filter is **What I watched**
- **THEN** no ranking-basis control is offered and the top 10 is ranked by my score

#### Scenario: Basis control available on a season recap
- **WHEN** a season recap is shown
- **THEN** the ranking-basis control is offered, since a season recap always selects on what aired

#### Scenario: Hidden MAL scores stay hidden
- **WHEN** MAL scores are hidden by the global toggle and a recap row carries one
- **THEN** that score is hidden in the recap under the same rules as on every other page

#### Scenario: The basis control wears its role's colour
- **WHEN** I select **MAL score** and then **My score**
- **THEN** the selected option is blue in the first case and purple in the second, matching the scores the ranking then shows

### Requirement: Recap rating distribution
Every recap SHALL show, below its stat block, how many of the period's anime I gave each score from 10 down to 1, rendered as the same bar-per-score block the profile page's all-anime score distribution uses: a row per score value carrying that score's bar, its count, and its share of the period's scored anime.

The block SHALL be computed over the entries the selected period and time filter include — the same set every other stat is computed over — and SHALL recompute whenever either changes. It SHALL NOT be narrowed by the top 10's media-type control, which is scoped to the top 10 alone.

Each bar's length SHALL be that score's count relative to the largest count across the period's score values, so the period's most-common score fills the full width of its track. A score no included anime received SHALL render an empty track. Shares SHALL be computed against the period's scored anime rather than my whole list, and a share that rounds to zero from a non-zero count SHALL be shown as less than one percent.

Each row's bar and its score numeral SHALL be drawn in the tier colour that score carries on the score board, exactly as the "Score slots are coloured by tier" requirement assigns them — 10 the apex treatment, 9 red, 8 blue, 7 gold, 6 and 5 silver, 4 through 1 bronze — so the distribution and the board the button beside it opens describe one ladder in one set of colours. The tier colours SHALL be read from the same definition the board reads, not restated, so the two can never disagree. Each row's count and share SHALL stay in the block's ordinary text colour, so the row's colour is carried by the score and its bar rather than smeared across every cell.

Tier colour SHALL be decoration on top of a row that is otherwise unchanged: every row SHALL keep its numeral, its count, and its share, the bar lengths SHALL still be proportional as above, and an empty track SHALL still read as empty rather than as a bar of that tier's colour.

This colouring is the recap's. The profile page's all-anime distribution SHALL keep its single-colour bars, so the two pages' blocks stay the same block drawn at two densities rather than diverging in structure.

The recap's block SHALL NOT repeat the mean score, which the stat block directly above it already reports for the same set. It MAY be rendered at a smaller size than the profile page's to suit the narrower column it occupies, provided it uses the same row structure and column alignment.

#### Scenario: Distribution of a period's scores
- **WHEN** a recap renders for a period whose included anime I have scored
- **THEN** it shows a bar, a count, and a share for each score from 10 down to 1, computed over that period's included entries

#### Scenario: The period's most-common score fills its track
- **WHEN** one score has more of the period's anime than any other
- **THEN** that score's bar fills the full width of its track and the others are drawn in proportion to it

#### Scenario: Shares are of the period, not the whole list
- **WHEN** a period holds 20 scored anime of which 5 scored 8
- **THEN** the 8 row reports a 25% share, whatever proportion 8s make up across my whole list

#### Scenario: The distribution follows the time filter
- **WHEN** I switch a yearly recap's time filter
- **THEN** the distribution recomputes for the newly included entries alongside the rest of the stats

#### Scenario: The media-type control does not narrow it
- **WHEN** I narrow the top 10 to films only
- **THEN** the distribution continues to cover every included anime of the period

#### Scenario: No mean line on the recap
- **WHEN** a recap renders its distribution
- **THEN** the block carries no mean-score line of its own, the stat block above it having already reported the period's mean

#### Scenario: Nothing scored in the period
- **WHEN** no included entry of the period has a score
- **THEN** every track renders empty and no share is shown as an invalid number

#### Scenario: The distribution and the board agree on colour
- **WHEN** I look at the distribution's 9 row and then open the score board and look at its 9 slot
- **THEN** the row's bar and numeral carry the same red the slot carries, and likewise for every other score

#### Scenario: Counts and shares stay neutral
- **WHEN** a distribution row is drawn in its tier colour
- **THEN** its count and its share are drawn in the block's ordinary text colour

#### Scenario: An empty track is still empty
- **WHEN** no included anime carries a given score
- **THEN** that row's track renders empty rather than filled with the score's tier colour

#### Scenario: The profile page's distribution is unchanged
- **WHEN** I open the profile page's all-anime score distribution
- **THEN** its bars are drawn in the single colour they use today, not in the score board's tiers
