## ADDED Requirements

### Requirement: Profile section titles are banded by family
The profile page SHALL present the titles of the sections whose subject a colour family names as bands, using the banded-title treatment the `section-colour-language` capability defines:

- **They liked it, I didn't** SHALL carry the MAL family and **I liked it, they didn't** the mine family — the same two colours the recap's "MAL liked it more" and "I liked it more" labels carry, so the pair of lists reads as one claim stated in two directions, and which list is about whose opinion is readable before either title is;
- **Favourite years** SHALL carry the year family and **Favourite seasons** the season family, matching the families the recap page's year-level and season-level rankings carry, so the same ranking of the same groups is the same colour on both pages.

Each band SHALL span the full width of the list it heads, from that list's left edge to its right edge, with its title centred on it.

The page's remaining section titles SHALL stay plain and unbanded, since none of them is about a season, a year, or a disagreement.

Banding SHALL change nothing else about these four sections: the divergence lists' membership, ordering, scrolling, and empty states, and the favourites rankings' rows, posters, five-row cap, "See all" overlay, and side-by-side layout, SHALL be exactly as they are unbanded.

#### Scenario: The divergence pair is colour-coded by whose opinion it is
- **WHEN** the profile page shows its two opinion-divergence lists
- **THEN** "They liked it, I didn't" is headed by a band filled in the MAL family's blue and "I liked it, they didn't" by one filled in the mine family's purple

#### Scenario: A band spans its list
- **WHEN** I look at the **Favourite seasons** section
- **THEN** its band runs the full width of the ranking beneath it, with the title centred on the band

#### Scenario: The divergence bands match the recap's labels
- **WHEN** I compare the profile's "They liked it, I didn't" band against a recap hot take labelled "MAL liked it more"
- **THEN** the two carry the same colour

#### Scenario: Favourites match the recap's rankings
- **WHEN** I compare the profile's **Favourite years** and **Favourite seasons** bands against a recap's **Year ranking** and **Season ranking** bands
- **THEN** the year-level titles carry one family on both pages and the season-level titles the other

#### Scenario: Plain titles stay plain
- **WHEN** the profile renders its stats, rating-distribution, latest-updates, and top-anime headings
- **THEN** each is drawn as a plain heading with no band behind it

#### Scenario: A banded section behaves identically
- **WHEN** more than five years qualify for **Favourite years**
- **THEN** its "See all" control opens the same overlay with the same rows in the same order as it does unbanded

### Requirement: Profile rows highlight in their section's family
Every row of a profile section that carries a colour family SHALL take that family's colour in place of the app's accent in the standard hover treatment, on pointer hover and keyboard focus alike, per the `section-colour-language` capability:

- a row of **They liked it, I didn't** SHALL highlight in the MAL family's blue and a row of **I liked it, they didn't** in the mine family's purple, so a row's highlight says whose opinion the list it sits in is about;
- a row of **Favourite years** SHALL highlight in the year family and a row of **Favourite seasons** in the season family;
- the "See all" overlay a favourites ranking opens SHALL carry the same family as the ranking that opened it, banding its own title and highlighting its rows in that family.

Rows of the page's other lists — latest updates, top anime, and the rating distribution — SHALL keep the accent highlight they have today.

Only the highlight's colour SHALL change: a row's size, position, resting appearance, and behaviour on click SHALL be exactly what they are today.

#### Scenario: A divergence row highlights by whose opinion it is
- **WHEN** I hover a row of "They liked it, I didn't" and then a row of "I liked it, they didn't"
- **THEN** the first highlights blue and the second purple

#### Scenario: A favourites row highlights in its family
- **WHEN** I hover a row of **Favourite seasons**
- **THEN** it highlights in the season family's colour rather than the app's accent

#### Scenario: The overlay keeps the ranking's family
- **WHEN** I open the "See all" overlay from **Favourite years**
- **THEN** its title is banded in the year family and its rows highlight in that same family

#### Scenario: Unfamilied lists are unchanged
- **WHEN** I hover a row of the latest-updates feed
- **THEN** it takes the app's accent highlight exactly as it does today

## REMOVED Requirements

### Requirement: Profile section titles are tinted by family
**Reason**: Replaced by "Profile section titles are banded by family" — the same four sections in the same four families, presented as full-width bands rather than as gradient-painted title text.

**Migration**: No section gains or loses a family and no list's contents change; only the way a family is drawn on the title changes.
