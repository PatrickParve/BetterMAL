## MODIFIED Requirements

### Requirement: Opinion divergence lists
The system SHALL show two opinion-divergence lists comparing my score against MAL's score for anime I have rated: "They liked it, I didn't" and "I liked it, they didn't".

Membership SHALL be decided on *standardized* scores rather than the raw difference between them. MAL's community averages occupy a far narrower band than personal 1–10 scores, so an identical raw gap on both sides can never populate both lists; each score SHALL therefore be measured in standard deviations from the mean of its own scale. The population for both scales SHALL be the anime I have rated that also carry a MAL average — the same population both lists draw from — and the mean and standard deviation SHALL be recomputed from that population whenever the profile is built, so the rules track my list as it grows rather than sitting on fixed constants.

For each anime in that population, its divergence SHALL be how many standard deviations MAL's score sits above its own mean minus how many my score sits above mine. An anime SHALL qualify for a list when its divergence reaches 1.0 in that list's direction **and** it passes that list's label gate, which keeps the heading's claim honest about both parties:

- "They liked it, I didn't" — MAL's score is at least 7.5 and my score is at most 5.
- "I liked it, they didn't" — my score is at least 8 and MAL's score is at most 7.5.

The 5 and 8 boundaries are MAL's own score labels: 5 is "Average" and below it lies everything worse, 8 is "Very Good" and above it everything better. The scores between them — 6 ("Fine") and 7 ("Good") — are a neutral band, and an anime I scored in that band SHALL fall in neither list however far MAL's average sits from it. The 7.5 boundary splits MAL's community scale between its "Good" and "Very Good" labels.

Each list SHALL be ordered by divergence, strongest first, with ties broken by title case-insensitively. Neither list SHALL be capped: every anime that qualifies SHALL be present, reachable by scrolling its box.

Divergence needs a population to normalize against. When fewer than 10 anime carry both my score and a MAL average, or when either scale's standard deviation across that population is zero, both lists SHALL be empty rather than ranking on a spread that does not exist.

#### Scenario: They liked it, I didn't
- **WHEN** I rated an anime 3 and its MAL average is 8.70, more than 1.0 standard deviation apart once each score is standardized against its own scale
- **THEN** it appears in the "They liked it, I didn't" list

#### Scenario: I liked it, they didn't
- **WHEN** I rated an anime 9 and its MAL average is 6.31, more than 1.0 standard deviation apart once each score is standardized against its own scale
- **THEN** it appears in the "I liked it, they didn't" list

#### Scenario: Both lists populate
- **WHEN** my scores run lower on average and spread wider than the MAL averages of the same anime
- **THEN** neither list is starved by that difference in scale — each holds every anime that diverges by the same standardized amount in its own direction

#### Scenario: A score I did not dislike stays out
- **WHEN** an anime's MAL average diverges upward from my score by more than 1.0 standard deviation but I scored it 6
- **THEN** it does not appear in "They liked it, I didn't", because the list claims I didn't like it and a 6 does not say that

#### Scenario: A community score that is not a dislike stays out
- **WHEN** I scored an anime 10 and it diverges downward by more than 1.0 standard deviation, but its MAL average is 7.8
- **THEN** it does not appear in "I liked it, they didn't", because the list claims they didn't like it and a 7.8 average does not say that

#### Scenario: Strongest disagreement first
- **WHEN** either list renders
- **THEN** its rows run from the largest standardized divergence to the smallest

#### Scenario: Too little to normalize against
- **WHEN** fewer than 10 of my rated anime carry a MAL average
- **THEN** both lists are empty and each box shows its empty state

### Requirement: Poster strips keep a fixed tile size and scroll horizontally only
The "My top anime", "Top series", and "Most rewatched" strips SHALL size their poster tiles as a fixed fraction of the strip's width — ten tiles across the visible width — regardless of how many entries the strip contains. Tiles SHALL NOT shrink to fit additional entries; entries beyond the tenth SHALL be reached by scrolling the strip horizontally.

Ten tiles SHALL actually fit: the tile's full rendered width, including any border it carries, SHALL be what the ten-across computation divides the strip's visible width into, so that a strip holding exactly ten entries shows all ten whole — the tenth SHALL NOT be clipped at the strip's trailing edge, and this SHALL hold for a strip that cannot be scrolled as much as for one that can.

Each strip SHALL scroll along the horizontal axis only. Vertical scrolling within a strip SHALL be impossible: a trackpad or wheel gesture SHALL NOT be able to displace the strip's contents up or down, including where a hovered tile's scaled-up size exceeds the strip's height.

A strip holding no more entries than fit across its visible width SHALL NOT be scrollable at all: no wheel gesture and no drag SHALL displace its contents by any amount, and the strip SHALL NOT present itself as scrollable — including the grab cursor, which SHALL appear only on a strip that can actually be scrolled. Sub-pixel differences between the tiles' total width and the strip's width SHALL NOT make a strip that fits behave as a scrollable one.

#### Scenario: Exactly ten entries are all whole
- **WHEN** a strip holds exactly ten entries
- **THEN** all ten tiles are fully visible, with the tenth's trailing edge inside the strip rather than cut off by it

#### Scenario: The tenth tile of a longer strip
- **WHEN** a strip holds more than ten entries and sits at its starting scroll position
- **THEN** the tenth tile is whole and the eleventh is the first one reached by scrolling

#### Scenario: More than ten top anime
- **WHEN** I have more than ten anime scored 10
- **THEN** the tiles stay the same size as when there were exactly ten, and the rest are reached by scrolling the strip

#### Scenario: Exactly ten entries do not scroll
- **WHEN** a media-type filter leaves a strip with exactly ten entries
- **THEN** the strip cannot be scrolled or dragged in either direction, and its cursor does not offer to drag it

#### Scenario: Switching to a filter that fits stops the scrolling
- **WHEN** I switch from a filter whose strip scrolls to one whose entries all fit
- **THEN** the strip stops being scrollable, rather than keeping a few pixels of travel from the wider list

#### Scenario: Tile size matches between the strips
- **WHEN** two of the strips are shown at the same window width
- **THEN** their tiles are the same size

#### Scenario: No vertical drift
- **WHEN** I make a vertical scroll gesture with the pointer over any strip
- **THEN** the strip's contents do not move up or down

### Requirement: Profile list-row posters fill the row
The system SHALL render the poster in every profile list row — "Latest updates" rows, full edit-history rows, and both opinion-divergence lists' rows — flush with the row's top, bottom, and leading edges, filling the row's full height with no padding between the poster and those edges. The poster's leading corners SHALL follow the row's own corner radius, and a row SHALL take its height from its list rather than from the poster's natural aspect ratio — the poster SHALL never inflate a row to its own intrinsic size. A row whose anime has no picture SHALL render its placeholder at the same full-height size.

A poster SHALL keep the proportions of the poster art at whatever height its row has: where a list sets its own row height, the poster's width SHALL follow that height rather than staying at a width fixed for some other row height.

#### Scenario: Poster fills a latest-updates row
- **WHEN** the Latest updates box renders a row
- **THEN** that row's poster touches the row's top, bottom, and leading edges with no gap

#### Scenario: Poster fills a history row
- **WHEN** the full edit-history overlay renders a row
- **THEN** that row's poster touches the row's top, bottom, and leading edges with no gap

#### Scenario: Poster fills a divergence row
- **WHEN** either opinion-divergence list renders a row
- **THEN** that row's poster touches the row's top, bottom, and leading edges with no gap

#### Scenario: Poster follows its row's height
- **WHEN** the Latest updates rows are taller than the divergence and history rows
- **THEN** their posters are correspondingly wider, keeping the poster proportions rather than rendering a narrow crop

#### Scenario: A poster never sets the row height
- **WHEN** any of these rows renders its poster
- **THEN** the row occupies the height its list gives it, and no box grows to accommodate the image's intrinsic size

### Requirement: Scrollbars sit beside scrollable content
Every scrollable list on the profile page and in its overlays — the "Latest updates" feed, both opinion-divergence lists, and the full edit-history list — SHALL lay out its scrollbar beside the rows rather than over them, so no row's right-hand edge, border, or content is covered by the scrollbar.

#### Scenario: Feed scrollbar does not cover rows
- **WHEN** the "Latest updates" feed has more rows than fit and shows a scrollbar
- **THEN** the scrollbar sits beside the rows and no row is drawn underneath it

#### Scenario: Divergence scrollbar does not cover rows
- **WHEN** an opinion-divergence list has more rows than fit and shows a scrollbar
- **THEN** the scrollbar sits beside the rows and no row is drawn underneath it

#### Scenario: History scrollbar does not cover rows
- **WHEN** the full edit-history overlay shows a scrollbar
- **THEN** the scrollbar sits beside the rows and no row is drawn underneath it

## ADDED Requirements

### Requirement: Profile lists show whole rows only
The "Latest updates" feed SHALL show exactly five rows at rest, with no part of a sixth row visible beneath them. Each opinion-divergence list SHALL show at most ten rows at rest, with no part of an eleventh visible beneath them; a list with fewer than ten rows SHALL show what it has without reserving space for the rest.

The feed SHALL reconcile this with filling its box: rather than stopping at a fixed height and leaving dead space, its rows SHALL take their height from the height the box gives the feed, so that five of them fill it exactly. Row height SHALL therefore follow the box — a taller box makes taller rows, not four and a fraction — and the feed SHALL keep a floor below which it does not shrink, so it cannot collapse when nothing else in the row of boxes is tall.

Rows partly scrolled out of view while scrolling are not covered by this: the requirement is about what the lists show before and after a scroll gesture, not during one.

#### Scenario: No sliver of a sixth update
- **WHEN** the profile page loads with more than five entries in the Latest updates feed
- **THEN** five rows are visible in full and no part of the sixth is shown

#### Scenario: Whole rows at any window width
- **WHEN** the window width changes enough to change the height of the profile page's top row of boxes
- **THEN** the feed still shows exactly five whole rows, resized to fill the new height

#### Scenario: Feed still fills its box
- **WHEN** the top row of boxes is taller than the feed's floor height
- **THEN** the feed's five rows extend to the bottom of the box, leaving no empty gap beneath them

#### Scenario: The rest is still reachable
- **WHEN** the feed holds more than five rows
- **THEN** the remainder is reached by scrolling the feed

#### Scenario: No sliver of an eleventh divergence row
- **WHEN** an opinion-divergence list holds more than ten anime
- **THEN** ten rows are visible in full, no part of the eleventh is shown, and the rest is reached by scrolling the list

#### Scenario: A short divergence list
- **WHEN** an opinion-divergence list holds three anime
- **THEN** its box shows those three rows and does not reserve the height of ten
