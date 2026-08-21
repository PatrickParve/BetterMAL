## ADDED Requirements

### Requirement: Profile section titles are tinted by family
The profile page SHALL tint the titles of the sections whose subject a colour family names, using the tinted-title treatment the `section-colour-language` capability defines:

- **They liked it, I didn't** SHALL carry the MAL family and **I liked it, they didn't** the mine family — the same two colours the recap's "MAL liked it more" and "I liked it more" labels carry, so the pair of lists reads as one claim stated in two directions, and which list is about whose opinion is readable before either title is;
- **Favourite years** SHALL carry the year family and **Favourite seasons** the season family, matching the families the recap page's year-level and season-level rankings carry, so the same ranking of the same groups is the same colour on both pages.

The page's remaining section titles SHALL stay untinted, since none of them is about a season, a year, or a disagreement.

Tinting SHALL change nothing else about these four sections: the divergence lists' membership, ordering, scrolling, and empty states, and the favourites rankings' rows, posters, five-row cap, "See all" overlay, and side-by-side layout, SHALL be exactly as they are untinted.

#### Scenario: The divergence pair is colour-coded by whose opinion it is
- **WHEN** the profile page shows its two opinion-divergence lists
- **THEN** "They liked it, I didn't" carries the MAL family's blue and "I liked it, they didn't" the mine family's purple

#### Scenario: The divergence titles match the recap's labels
- **WHEN** I compare the profile's "They liked it, I didn't" title against a recap hot take labelled "MAL liked it more"
- **THEN** the two carry the same colour

#### Scenario: Favourites match the recap's rankings
- **WHEN** I compare the profile's **Favourite years** and **Favourite seasons** titles against a recap's **Year ranking** and **Season ranking** titles
- **THEN** the year-level titles carry one family on both pages and the season-level titles the other

#### Scenario: A tinted section behaves identically
- **WHEN** more than five years qualify for **Favourite years**
- **THEN** its "See all" control opens the same overlay with the same rows in the same order as it does untinted

## MODIFIED Requirements

### Requirement: Top series ranking basis
Top series SHALL be ranked by a **main-series average**, and SHALL offer a control that switches which average ranks it: **my score** or **MAL's score**. The control SHALL default to my score.

The control's two options SHALL carry the colour of the score role each selects: **My score** the mine role, **MAL score** the MAL role, per the `score-presentation` capability's requirement that a score-role control carries its role's colour. The option carrying the MAL role SHALL NOT be drawn in the purple this app reserves for my own score, and the colour SHALL apply to the option's selected and hovered states alike rather than only one of them.

Carrying those colours SHALL be scoped to this control. The media-type tabs on **My top anime** and **Most rewatched**, which share the same control form, SHALL keep the page's accent — they select a media type, not a score role.

Ranking SHALL be by the selected average in descending order. Ties SHALL be broken by the number of scored main-line entries the average was computed over, descending — so an average earned across more entries places higher — and then by title, case-insensitively.

Switching the basis SHALL reorder the strip without reloading the page's data and without the strip collapsing or changing height.

Both averages SHALL remain visible on every tile regardless of which basis is selected; the basis chooses the ordering, not what is shown.

#### Scenario: Default ranking
- **WHEN** I open the profile page without having changed the control
- **THEN** Top series is ranked by my main-series average, highest first

#### Scenario: Ranking by MAL's score
- **WHEN** I switch the control to MAL's score
- **THEN** the strip reorders by MAL's main-series average, highest first, and both averages are still shown on every tile

#### Scenario: An average earned over more entries wins a tie
- **WHEN** two series have the same average under the selected basis, one computed over five scored entries and one over two
- **THEN** the one computed over five places first

#### Scenario: Switching the basis is immediate
- **WHEN** I switch the ranking basis
- **THEN** the strip reorders immediately without a visible reload and without collapsing

#### Scenario: The basis control wears its role's colour
- **WHEN** I select **MAL score** and then **My score**
- **THEN** the selected option is blue in the first case and purple in the second, matching the colour the tiles' own MAL and my-score figures carry

#### Scenario: The media-type tabs are unaffected
- **WHEN** I select a media type on **My top anime** or **Most rewatched**
- **THEN** the selected tab is drawn in the page's accent exactly as it is today
