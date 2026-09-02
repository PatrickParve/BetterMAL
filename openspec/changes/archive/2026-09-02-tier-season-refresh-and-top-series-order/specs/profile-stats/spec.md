## MODIFIED Requirements

### Requirement: Top series ranking basis
Top series SHALL be ranked by a **main-series average**, and SHALL offer a control that switches which average ranks it: **my score** or **MAL's score**. The control SHALL default to my score.

The control's two options SHALL carry the colour of the score role each selects: **My score** the mine role, **MAL score** the MAL role, per the `score-presentation` capability's requirement that a score-role control carries its role's colour. The option carrying the MAL role SHALL NOT be drawn in the purple this app reserves for my own score, and the colour SHALL apply to the option's selected and hovered states alike rather than only one of them.

Carrying those colours SHALL be scoped to this control. The media-type tabs on **My top anime** and **Most rewatched**, which share the same control form, SHALL keep the page's accent — they select a media type, not a score role.

Ranking SHALL be by the selected average in descending order. **The two bases break their ties differently**, because only one of them has a second opinion of mine to consult.

**Under my score**, ties SHALL be broken by the identical chain the `series-browser` capability's **My average** sort uses, so the two surfaces can never order the same two franchises differently:

1. **the average position the series' main-line entries hold in my rankings**, ascending — the series whose entries sit nearer the top of my rankings first. The figure SHALL be the mean rank over the series' main-line entries **that hold a rank**, taken over the whole main line. An entry that holds no rank SHALL be left out of the mean entirely — not counted as a worst rank, not as a zero, and not as a member the mean is divided by — so an entry I have not scored can neither raise nor lower its series' average position. A series **none** of whose main-line entries holds a rank SHALL be placed after every tied series that has such a figure.
2. **the count of main-line episodes aired so far**, descending — the larger franchise first;
3. **display title**, ascending.

Both figures SHALL be the same figures the Series page's cards carry, computed the same way over the same member sets: the average position over the whole main line, matching the average it breaks a tie in; the aired-episode count over the main line's default combination of version alternatives, as every episode figure on a card without a version picker is.

**Under MAL's score**, ties SHALL be broken by the number of scored main-line entries the average was computed over, descending — so an average earned across more entries places higher — and then by title, case-insensitively. My rankings SHALL NOT enter this ordering: they are an opinion of mine, and the MAL basis is deliberately an ordering that says nothing about what I think.

Switching the basis SHALL reorder the strip without reloading the page's data and without the strip collapsing or changing height.

Both averages SHALL remain visible on every tile regardless of which basis is selected; the basis chooses the ordering, not what is shown.

#### Scenario: Default ranking
- **WHEN** I open the profile page without having changed the control
- **THEN** Top series is ranked by my main-series average, highest first

#### Scenario: Ranking by MAL's score
- **WHEN** I switch the control to MAL's score
- **THEN** the strip reorders by MAL's main-series average, highest first, and both averages are still shown on every tile

#### Scenario: Tied my-averages fall to my rankings
- **WHEN** the basis is my score and two series both average 8.00, the main-line entries of one sitting at ranks 3 and 7 in my rankings while the other's sit at ranks 40 and 60
- **THEN** the first is placed above the second

#### Scenario: Only ranked entries count toward the average position
- **WHEN** a tied series has three main-line entries of which only one is ranked
- **THEN** its average position is that one entry's rank, not a figure penalised for the two without one

#### Scenario: An unscored entry does not drag its series down
- **WHEN** two series are tied on my average, one having four main-line entries of which I have scored the two sitting at ranks 4 and 6, and the other having exactly those two entries and nothing else
- **THEN** both have the same average position, because the two unscored entries hold no rank and are left out of the mean rather than counted against it

#### Scenario: A tied series with nothing ranked sorts after those with ranks
- **WHEN** the basis is my score, two series are tied on my average, and none of one series' main-line entries appears in my rankings
- **THEN** that series is placed after the tied series that does have an average position

#### Scenario: Still tied falls to episodes aired
- **WHEN** two series are tied on my average and on their average position in my rankings, and one has 62 main-line episodes aired against the other's 24
- **THEN** the 62-episode series is placed first

#### Scenario: Still tied falls to the title
- **WHEN** two series are tied on my average, on average position, and on episodes aired
- **THEN** they are ordered by display title ascending

#### Scenario: The strip and the Series page agree
- **WHEN** two series appear tied on my average on both the profile's Top series strip and the Series page sorted by My average
- **THEN** the two surfaces place them in the same order, because both read the same average position and the same aired-episode count

#### Scenario: An average earned over more entries wins a tie under MAL's score
- **WHEN** the basis is MAL's score and two series have the same MAL average, one computed over five scored entries and one over two
- **THEN** the one computed over five places first

#### Scenario: My rankings do not order the MAL basis
- **WHEN** the basis is MAL's score and two series are tied on MAL average and on scored main-line count, with very different average positions in my rankings
- **THEN** they are ordered by title, not by my rankings

#### Scenario: Switching the basis is immediate
- **WHEN** I switch the ranking basis
- **THEN** the strip reorders immediately without a visible reload and without collapsing

#### Scenario: The basis control wears its role's colour
- **WHEN** I select **MAL score** and then **My score**
- **THEN** the selected option is blue in the first case and purple in the second, matching the colour the tiles' own MAL and my-score figures carry

#### Scenario: The media-type tabs are unaffected
- **WHEN** I select a media type on **My top anime** or **Most rewatched**
- **THEN** the selected tab is drawn in the page's accent exactly as it is today

## ADDED Requirements

### Requirement: The Top series read carries its tie-break figures
The Top series read endpoint SHALL carry, for every series it returns, the two figures the my-score ordering breaks its ties on: the average position the series' main-line entries hold in my rankings (absent when no main-line entry holds a rank), and the count of main-line episodes aired so far.

Both SHALL be derived at read time from the entries and members already stored — neither SHALL be persisted, so a score edit that moves an entry in my rankings changes the ordering on the next read with no rebuild.

The response SHALL already be ordered by the full my-score chain, so a client that never touches the ranking-basis control shows the same order as one that does. Adding these figures SHALL NOT change which series the endpoint returns: eligibility — membership in my list, and the two-aired-main-line-entries coverage rule — is unchanged, and no control switches either off.

#### Scenario: The figures arrive with the section
- **WHEN** the profile page reads its Top series section
- **THEN** each series carries its average ranking position and its main-line aired-episode count alongside its two averages

#### Scenario: A series with nothing ranked carries no position
- **WHEN** none of a series' main-line entries appears in my rankings
- **THEN** that series carries no average ranking position at all, rather than a zero or a worst-case value

#### Scenario: A score edit moves a series without a rebuild
- **WHEN** I change my score on an entry so it moves in my rankings, and then reload the profile page
- **THEN** its series' average position reflects the new rank, with no series rebuild

#### Scenario: The wire order matches the page's default order
- **WHEN** the Top series read is returned
- **THEN** its series are already in my-score order with the full tie-break chain applied

#### Scenario: Eligibility is unchanged
- **WHEN** the section is read
- **THEN** exactly the series that were listed before carry the new figures, and none is added or dropped
