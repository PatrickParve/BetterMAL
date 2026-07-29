## MODIFIED Requirements

### Requirement: All-anime score distribution
The system SHALL show a count of anime per score/rating value plus the overall mean score, rendered as a bar per score value alongside its count. Each bar's length SHALL represent that score's share of all my rated anime — the count for that score divided by the total number of anime I have rated — not a size relative to whichever score has the highest count.

#### Scenario: Rendering the distribution
- **WHEN** the profile page loads
- **THEN** it shows how many anime have each score value and the overall mean score

#### Scenario: Bar length is a share of all rated anime
- **WHEN** one score accounts for 30% of all the anime I've rated
- **THEN** that score's bar fills 30% of the track width, regardless of how the counts for other scores compare to it

## ADDED Requirements

### Requirement: Poster-tile hover in My top anime and Most rewatched
The system SHALL give each poster tile in the "My top anime" and "Most rewatched" strips a hover/focus state distinct from the border-and-background language used elsewhere in the app: the tile SHALL scale up slightly in place, with no border, ring, or background change drawn over or around the poster. The strip SHALL reserve enough space that a scaled-up edge tile is not clipped by the strip's scroll boundary, and the hovered tile SHALL render above its neighbours rather than being overlapped as it grows into the gap beside them.

#### Scenario: Hovering a poster tile
- **WHEN** I move the pointer over a tile in the "My top anime" or "Most rewatched" strip
- **THEN** that tile scales up slightly in place and no border, ring, or background appears

#### Scenario: Scaled edge tile is not clipped
- **WHEN** the strip is scrolled fully to one end and I hover the tile at that end
- **THEN** the scaled-up tile is fully visible, with no part of it cut off by the strip's edge

#### Scenario: Hovered tile is not overlapped by its neighbours
- **WHEN** a tile scales up and grows into the gap beside an adjacent tile
- **THEN** the hovered tile renders above its neighbours rather than being partly covered by them
