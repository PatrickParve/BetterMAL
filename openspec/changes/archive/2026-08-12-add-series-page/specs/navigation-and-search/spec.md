## MODIFIED Requirements

### Requirement: Hover highlight on anime cards and rows
The system SHALL give every anime card a clearly visible hover state — a change in surface and border, not colour alone — so the card under the pointer is unmistakable. This SHALL apply wherever anime cards appear (the main dashboard's currently-watching carousel and current-season section, the season browser, and search results) and to the equivalent full-width anime rows on my list, top anime, the series page's main-series and More sections, and the profile page's latest-updates feed and opinion-divergence lists. The highlight SHALL appear on the whole card or row as one unit, SHALL be reachable by keyboard focus as well as pointer hover, and SHALL NOT shift surrounding layout or clip against a scroll container's edge. The highlight SHALL remain clearly visible regardless of the card's own poster artwork — it SHALL NOT rely on a thin ring drawn over the picture itself, which can wash out against bright or visually busy cover art. This requirement does not apply to the poster-tile strips in the profile page's "My top anime" and "Most rewatched" boxes, which use their own distinct hover treatment (see the `profile-stats` capability).

#### Scenario: Hovering a card
- **WHEN** I move the pointer over an anime card on any page
- **THEN** the whole card is clearly highlighted and the highlight clears when the pointer leaves

#### Scenario: Hovering a list row
- **WHEN** I move the pointer over a my-list, top-anime, series-page, or profile-page row
- **THEN** the whole row is clearly highlighted in the same way cards are

#### Scenario: Highlight does not move the layout
- **WHEN** a card or row is highlighted on hover
- **THEN** neighbouring cards and page content stay exactly where they were

#### Scenario: Highlight stays visible against busy poster art
- **WHEN** I hover an anime card whose poster art is bright or visually busy
- **THEN** the highlight is still clearly visible, since it does not depend on contrast against the poster image itself
