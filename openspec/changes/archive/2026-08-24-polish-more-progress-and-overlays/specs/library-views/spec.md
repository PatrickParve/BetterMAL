## ADDED Requirements

### Requirement: Broadcast progress on my-list rows

While an anime is currently airing, its my-list row's progress bar SHALL render broadcast progress — episodes aired out of the anime's total episode count — as a fill behind my watched fill, in the same blue used by the home page's airing-progress bar, the dashboard's currently-watching cards, and the anime detail page's bar. My watched progress SHALL keep the site's purple accent, layered on top within the same track.

This SHALL be an addition to the row's existing bar, not a second bar and not a replacement: the row SHALL keep exactly one progress cell, its `watched/total` label, its in-place editable count, and its increment control, and the row's height SHALL be unchanged.

The aired fill SHALL be drawn only while MAL reports the anime as currently airing. For any other airing status the bar SHALL render with no aired fill — a finished run's aired count and total are the same figure, and an unaired show has broadcast nothing.

When the total episode count is known, the aired fill SHALL span episodes aired out of that total. When the total is unknown and the aired count is known, the aired fill SHALL span exactly half the track as a fixed "progress so far, end unknown" marker, and my watched fill SHALL be measured within that extent against the aired count, so being caught up on everything aired covers the aired extent exactly and my fill can never exceed it. When the aired count is not known, no aired fill SHALL be drawn. Both fills SHALL be clamped so neither can exceed the track.

The bar's label SHALL remain `watched/total`, or `watched/?` when the total is unknown, and SHALL NOT restate the aired count.

A row that has no progress cell at all — an anime that has aired no episode, per "My list rows offer no progress or score control before the first episode" — SHALL be unaffected: it gains no bar from this requirement.

When my episodes watched changes — by the increment control or by editing the count in place — my fill SHALL update in place without a reload or a re-sort, and the aired fill SHALL be unaffected.

#### Scenario: An airing row shows broadcast progress

- **WHEN** a my-list row renders for a currently airing anime with 12 total episodes, 5 aired, and 3 watched
- **THEN** a blue fill spans 5/12 of the track, a purple fill spans 3/12 on top of it, and the label reads `3/12`

#### Scenario: Caught up with the broadcast

- **WHEN** a my-list row renders for a currently airing anime where I have watched every episode that has aired
- **THEN** the purple fill covers the full extent of the blue fill and neither extends past the aired portion of the track

#### Scenario: Finished airing keeps the plain bar

- **WHEN** a my-list row renders for an anime that has finished airing
- **THEN** no aired fill is drawn and the bar shows only my watched progress against the total, as it did before

#### Scenario: Unknown total on an airing anime

- **WHEN** a my-list row renders for a currently airing anime whose total episode count is unknown and whose aired count is known
- **THEN** the aired fill spans half the track, my fill is measured within that extent, and the label reads `watched/?`

#### Scenario: Incrementing an airing row

- **WHEN** I press "+" on a my-list row for a currently airing anime
- **THEN** my purple fill and the count advance in place, the blue aired fill is unchanged, and the row keeps its position in the list

#### Scenario: A row with no progress cell gains nothing

- **WHEN** a my-list row's anime has aired no episode
- **THEN** the row still shows no progress cell at all
