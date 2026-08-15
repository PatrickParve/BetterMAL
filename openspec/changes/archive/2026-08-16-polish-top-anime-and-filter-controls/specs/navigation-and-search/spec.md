## ADDED Requirements

### Requirement: The search field's clear control reads as clickable
The system SHALL present the clear ("×") control inside a search field as an interactive control: hovering it SHALL show the pointer cursor, the same cursor every other clickable control in the app shows, rather than the default text or arrow cursor. This SHALL hold for every search field the app renders — the navbar search bar and the Settings page's refresh picker — and SHALL NOT change the control's behaviour, position, or visibility, nor the field's own focus indication.

Where the browser renders no clear control of its own, this requirement is satisfied vacuously; it constrains the cursor over the control, not whether the control exists.

#### Scenario: Hovering the clear control
- **WHEN** I have typed into the navbar search field and move the pointer over its clear ("×") control
- **THEN** the cursor changes to the pointer, marking it as clickable

#### Scenario: Clearing still works
- **WHEN** I click that clear control
- **THEN** the field is cleared exactly as before, with no change to the dropdown, the focus ring, or the magnifier beside it

#### Scenario: Every search field behaves the same
- **WHEN** I use the Settings page's anime-refresh search field
- **THEN** its clear control shows the same pointer cursor as the navbar's

## MODIFIED Requirements

### Requirement: Hover highlight on anime cards and rows
The system SHALL give every anime card a clearly visible hover state — a change in surface and border, not colour alone — so the card under the pointer is unmistakable. This SHALL apply wherever anime cards appear (the main dashboard's currently-watching carousel and current-season section, the season browser, search results, and the Top anime page's rank 1–3 showcase cards and rank 4–10 card row) and to the equivalent full-width anime rows on my list, top anime, the series page's main-series and More sections, and the profile page's latest-updates feed and opinion-divergence lists. The highlight SHALL appear on the whole card or row as one unit, SHALL be reachable by keyboard focus as well as pointer hover, and SHALL NOT shift surrounding layout or clip against a scroll container's edge. The highlight SHALL remain clearly visible regardless of the card's own poster artwork — it SHALL NOT rely on a thin ring drawn over the picture itself, which can wash out against bright or visually busy cover art. This requirement does not apply to the poster-tile strips in the profile page's "My top anime" and "Most rewatched" boxes, which use their own distinct hover treatment (see the `profile-stats` capability).

#### Scenario: Hovering a card
- **WHEN** I move the pointer over an anime card on any page
- **THEN** the whole card is clearly highlighted and the highlight clears when the pointer leaves

#### Scenario: Hovering a top-anime card
- **WHEN** I move the pointer over one of the Top anime page's rank 1–3 showcase cards or rank 4–10 cards
- **THEN** the whole card is highlighted the same way anime cards are highlighted everywhere else

#### Scenario: Hovering a list row
- **WHEN** I move the pointer over a my-list, top-anime, series-page, or profile-page row
- **THEN** the whole row is clearly highlighted in the same way cards are

#### Scenario: Highlight does not move the layout
- **WHEN** a card or row is highlighted on hover
- **THEN** neighbouring cards and page content stay exactly where they were

#### Scenario: Highlight stays visible against busy poster art
- **WHEN** I hover an anime card whose poster art is bright or visually busy
- **THEN** the highlight is still clearly visible, since it does not depend on contrast against the poster image itself
