## MODIFIED Requirements

### Requirement: Clickable anime cards everywhere
The system SHALL make anime cards clickable everywhere they appear, linking to that anime's detail page. The clickable region SHALL cover the card's picture, title, and passive metadata. Interactive progress controls — the watched/total bar, the episode count, and the plus control — SHALL sit outside the card's link, so clicking or dragging within that region never navigates.

#### Scenario: Clicking any card
- **WHEN** I click an anime card's picture or title on any page
- **THEN** I am taken to that anime's detail page

#### Scenario: Clicking a card's progress controls
- **WHEN** I click the progress bar, episode count, or plus control on an anime card
- **THEN** I stay on the current page and only that control reacts

## ADDED Requirements

### Requirement: Hover highlight on anime cards and rows
The system SHALL give every anime card a clearly visible hover state — a change in surface and border, not colour alone — so the card under the pointer is unmistakable. This SHALL apply wherever anime cards appear (the main dashboard's currently-watching carousel and current-season section, the season browser, and search results) and to the equivalent full-width anime rows on my list, top anime, and the profile page's latest-updates feed and opinion-divergence lists. The highlight SHALL appear on the whole card or row as one unit, SHALL be reachable by keyboard focus as well as pointer hover, and SHALL NOT shift surrounding layout or clip against a scroll container's edge. The highlight SHALL remain clearly visible regardless of the card's own poster artwork — it SHALL NOT rely on a thin ring drawn over the picture itself, which can wash out against bright or visually busy cover art. This requirement does not apply to the poster-tile strips in the profile page's "My top anime" and "Most rewatched" boxes, which use their own distinct hover treatment (see the `profile-stats` capability).

#### Scenario: Hovering a card
- **WHEN** I move the pointer over an anime card on any page
- **THEN** the whole card is clearly highlighted and the highlight clears when the pointer leaves

#### Scenario: Hovering a list row
- **WHEN** I move the pointer over a my-list, top-anime, or profile-page row
- **THEN** the whole row is clearly highlighted in the same way cards are

#### Scenario: Highlight does not move the layout
- **WHEN** a card or row is highlighted on hover
- **THEN** neighbouring cards and page content stay exactly where they were

#### Scenario: Highlight stays visible against busy poster art
- **WHEN** I hover an anime card whose poster art is bright or visually busy
- **THEN** the highlight is still clearly visible, since it does not depend on contrast against the poster image itself

### Requirement: Clearly visible hover state on navigation controls
The system SHALL give navigation controls a clearly visible hover state, consistent across the app: the navbar links, the score-visibility toggle, the settings link, the currently-watching carousel arrows, pagination controls, and the season and airing period arrows. The hover state SHALL be visible as more than a text-colour change, and SHALL leave the active/current state of a control still distinguishable while hovered.

#### Scenario: Hovering a navbar link
- **WHEN** I move the pointer over a navbar link
- **THEN** it is clearly highlighted rather than only changing text colour

#### Scenario: Hovering an arrow control
- **WHEN** I move the pointer over a carousel, pagination, season, or airing arrow
- **THEN** it is clearly highlighted in the same style as other navigation controls

#### Scenario: Active page stays distinguishable
- **WHEN** I hover the navbar link for the page I am already on
- **THEN** it shows the hover state while still reading as the active page
