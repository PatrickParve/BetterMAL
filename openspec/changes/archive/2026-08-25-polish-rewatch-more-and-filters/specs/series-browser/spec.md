## ADDED Requirements

### Requirement: A Rewatching entry counts as fully watched in a card's figures
Wherever a series card counts **my watched main-line episodes**, a main-line entry marked **Rewatching** SHALL count as fully watched — as the greater of its own episodes-watched figure and its aired-episode figure — rather than as its current in-progress count. Entering Rewatching resets episodes-watched to zero, so without this rule a franchise the user has seen in full and is part-way through watching again reads as barely started.

This SHALL govern both the card's progress badge and the **My progress** sort, whose key is main-line episodes watched divided by main-line episodes aired so far, so a series being rewatched sorts with the series that are finished rather than with the ones never started.

It SHALL govern only the watched side of each pair: the card's main-line episode total, its aired figure, and its entry count describe the anime rather than the user, and SHALL be unchanged by a rewatch in progress.

The rule SHALL match the `series-page` capability's own rule exactly, so a card and the series page it opens can never disagree about the same series.

#### Scenario: A rewatching series does not sort as unwatched
- **WHEN** I sort by My progress and a series whose main line I have completed in full has its first season marked Rewatching with two episodes watched
- **THEN** it sorts among the fully-watched series, not among those with nothing watched

#### Scenario: A card's episode total is unmoved by a rewatch
- **WHEN** a main-line entry of a listed series is marked Rewatching
- **THEN** the card's main-line episode total and entry count are exactly what they were before the rewatch began

## MODIFIED Requirements

### Requirement: A card's progress badge follows the series page's precedence
The system SHALL choose each card's progress badge by the same precedence the `series-page` capability defines for its header's personal badge, evaluated over the series' aired main-line entries in release order:

1. `Completed` — every member of the series has finished airing, at least one main-line entry has finished airing, and every main-line entry that has finished airing is marked Completed or Rewatching in my list.
2. `Dropped` — among main-line entries that have aired (finished airing or currently airing), at least one is marked Dropped in my list, and no aired main-line entry released after the most recently aired such drop has ever been watched at all. A drop I later watched past does not count.
3. `Caught up` — I have watched at least one main-line episode, and the total I have watched across the aired main line meets the total that has actually broadcast across it.
4. `N behind` — I have watched at least one main-line episode, but fewer than have broadcast across the aired main line. N SHALL be the total broadcast main-line episodes minus the total I have watched, summed across every aired main-line entry — not only a currently-airing one.
5. `Unwatched` — at least one main-line entry has aired, I have watched none of the main line at all, and rule (2) does not already apply.
6. No badge — when nothing in the main line has aired yet, or when a currently-airing main-line entry's broadcast episode count is unknown and rules (3)/(4) cannot otherwise be resolved.

A main-line entry marked **Rewatching** SHALL count as fully watched throughout this precedence, per "A Rewatching entry counts as fully watched in a card's figures", and SHALL satisfy rule (1) alongside `Completed`.

Rules (2) and (5) SHALL be decided without needing any entry's broadcast episode count, so an unknown broadcast count SHALL only ever be able to produce no badge once evaluation reaches rules (3)/(4).

A main-line entry that is not in my list SHALL count as zero episodes watched. An entry that has not aired at all SHALL NOT count against the badge.

`Completed` SHALL carry the colour the app already uses for a Completed watch status. `Dropped` SHALL carry the colour the app already uses for a Dropped watch status. `Unwatched` SHALL carry the colour the app already uses for a Plan-to-watch status. `Caught up` and `N behind` SHALL keep their existing colours.

A card's badge and the series page's header badge for the same series SHALL agree.

#### Scenario: A finished series I have completed
- **WHEN** every member of a listed series has finished airing and every main-line entry is marked Completed in my list
- **THEN** the card shows a "Completed" badge in the app's Completed-status colour

#### Scenario: A finished series I am rewatching keeps its Completed badge
- **WHEN** every member of a listed series has finished airing, every main-line entry is marked Completed, and I then mark one of them Rewatching with two episodes watched
- **THEN** the card still shows "Completed", not a behind count

#### Scenario: Behind on an airing season
- **WHEN** I have completed every finished-airing main-line entry of a listed series and its currently-airing season has broadcast 8 episodes of which I have watched 5
- **THEN** the card shows a "3 behind" badge

#### Scenario: An airing season not in my list at all
- **WHEN** I have completed every earlier main-line entry and the currently-airing season, with 8 episodes broadcast, is not in my list
- **THEN** the card shows an "8 behind" badge

#### Scenario: Caught up while the next entry is unaired
- **WHEN** every main-line entry that has aired is completed and one main-line entry has not yet aired
- **THEN** the card shows a "Caught up" badge

#### Scenario: A partially watched finished entry shows a behind count
- **WHEN** a listed series' only main-line entry has finished airing with 12 episodes, I have watched 5, and I have neither completed nor dropped it
- **THEN** the card shows a "7 behind" badge, not no badge

#### Scenario: A dropped entry with nothing watched after it
- **WHEN** a listed series has one main-line entry marked Dropped and no main-line entry released after it has ever been watched
- **THEN** the card shows a "Dropped" badge, in the app's Dropped-status colour

#### Scenario: A dropped entry later resumed does not read as Dropped
- **WHEN** a listed series has an early main-line entry marked Dropped but a later main-line entry has watched episodes
- **THEN** the card does not show "Dropped"; it shows whatever "Caught up"/"N behind" the combined figures produce

#### Scenario: Nothing watched at all
- **WHEN** at least one main-line entry of a listed series has aired and I have watched none of the main line, with no entry marked Dropped
- **THEN** the card shows an "Unwatched" badge, in the app's Plan-to-watch colour

#### Scenario: A rewatching entry does not read as unwatched
- **WHEN** a listed series' only aired main-line entry is marked Rewatching with zero episodes watched
- **THEN** the card does not show "Unwatched"

#### Scenario: An unknown broadcast count means no badge, once Dropped/Unwatched are ruled out
- **WHEN** a currently-airing main-line entry of a listed series has no known count of episodes broadcast so far, and the series is not "Dropped" or "Unwatched"
- **THEN** the card shows no progress badge, rather than "Caught up"
