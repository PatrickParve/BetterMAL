## MODIFIED Requirements

### Requirement: Top series section
The profile page SHALL show a **Top series** section that ranks my franchises, presented like My top anime and Most rewatched: a horizontally-scrolling, drag-scrollable strip of fixed-size poster tiles, uncapped, using the same tile size and hover behaviour as the other two strips.

A series SHALL be eligible for the section when at least one of its members — main line or extra — is in my list. A series with no member in my list SHALL NOT appear.

A series whose main line holds two or more entries that have started airing SHALL additionally require that **at least two of those aired main-line entries are in my list**. A series that clears the first condition but not this one SHALL NOT be listed, because its averages would rank a whole franchise on a single entry's worth of my viewing. An entry counts as in my list at **any** status — plan-to-watch included — and a main-line entry that has been announced but has not aired a single episode SHALL NOT count toward either total, so a series whose main line holds one aired entry is unaffected by this rule however many sequels are confirmed.

That exclusion SHALL be unconditional: no control SHALL switch it off, and it SHALL apply under either ranking basis. It SHALL be independent of the single-entry filter, which narrows the strip further when the user switches it on.

Each tile SHALL show the series' poster and **both** of its main-series averages: MAL's average across the main line, and my average across the main line. Those figures SHALL be the same figures that series' own page shows for `MAL · main series` and `Mine · main series`, computed the same way, so the two surfaces can never disagree. The two figures SHALL be distinguishable by the score colour roles (see the `score-presentation` capability).

Opening a tile SHALL navigate to that series' page, not to an anime's detail page.

Each tile SHALL make available, without requiring navigation, how many main-line entries each average was computed over — the same "N of M scored" figure the series page shows beside its chips.

#### Scenario: Ranking my franchises
- **WHEN** I open the profile page
- **THEN** a Top series section lists my series as poster tiles, each showing both its MAL main-series average and my main-series average

#### Scenario: Only series I have watched something of
- **WHEN** a stored series has no member in my list
- **THEN** it does not appear in Top series

#### Scenario: A franchise I have barely started
- **WHEN** a series' main line has three seasons that have aired and only one of them is in my list
- **THEN** the series is not listed, under either ranking basis

#### Scenario: Two of a franchise's seasons is enough
- **WHEN** a series' main line has three seasons that have aired and two of them are in my list
- **THEN** the series is listed and ranked normally

#### Scenario: An unaired sequel does not make a franchise barely-watched
- **WHEN** a series' main line has one aired season, which is in my list, plus a second season that is announced but has not aired an episode
- **THEN** the series is listed, because only one main-line entry has actually aired

#### Scenario: Plan-to-watch counts as coverage
- **WHEN** a series' main line has two aired seasons, one completed and one sitting in my list as plan-to-watch
- **THEN** the series is listed, because both aired main-line entries are in my list

#### Scenario: A series I only know through an extra
- **WHEN** the only member of a series in my list is one of its extras, and the series' main line has a single aired entry
- **THEN** the series is eligible, and its two averages are still computed over its main line only

#### Scenario: An extra is not coverage of a multi-season franchise
- **WHEN** the only member of a series in my list is one of its extras, and the series' main line has three aired entries
- **THEN** the series is not listed, because at most one of its aired main-line entries is in my list

#### Scenario: The exclusion cannot be switched off
- **WHEN** I look for a way to see the franchises this rule hides
- **THEN** the section offers none, and switching the single-entry filter or the ranking basis does not bring them back

#### Scenario: Figures match the series page
- **WHEN** I compare a tile's two averages with that series' page
- **THEN** they equal the page's `MAL · main series` and `Mine · main series` averages

#### Scenario: Opening a series from the strip
- **WHEN** I click a tile
- **THEN** I am taken to that series' page

#### Scenario: The strip scrolls rather than wrapping
- **WHEN** I have more series than fit across the section
- **THEN** the strip scrolls horizontally at a fixed tile size, and can be dragged to scroll, exactly like the other two strips

### Requirement: Poster strips keep a fixed tile size and scroll horizontally only
The "My top anime", "Top series", and "Most rewatched" strips SHALL size their poster tiles as a fixed fraction of the strip's width — ten tiles across the visible width — regardless of how many entries the strip contains. Tiles SHALL NOT shrink to fit additional entries; entries beyond the tenth SHALL be reached by scrolling the strip horizontally.

Each strip SHALL scroll along the horizontal axis only. Vertical scrolling within a strip SHALL be impossible: a trackpad or wheel gesture SHALL NOT be able to displace the strip's contents up or down, including where a hovered tile's scaled-up size exceeds the strip's height.

A strip holding no more entries than fit across its visible width SHALL NOT be scrollable at all: no wheel gesture and no drag SHALL displace its contents by any amount, and the strip SHALL NOT present itself as scrollable — including the grab cursor, which SHALL appear only on a strip that can actually be scrolled. Sub-pixel differences between the tiles' total width and the strip's width SHALL NOT make a strip that fits behave as a scrollable one.

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

## ADDED Requirements

### Requirement: Poster strips show no scrollbar
The "My top anime", "Top series", and "Most rewatched" strips SHALL NOT render a visible scrollbar, in any browser, whether or not they overflow — the posters are the content and a bar under them is noise.

Hiding the scrollbar SHALL NOT cost the strips any scrolling: a strip that holds more entries than fit SHALL still scroll by wheel gesture and by dragging it, exactly as it does today.

This SHALL apply to the poster strips only. The profile page's vertical lists — the "Latest updates" feed and the full edit-history overlay — SHALL keep their scrollbars beside their rows.

#### Scenario: No bar under the posters
- **WHEN** a strip holds more entries than fit across it
- **THEN** no scrollbar is drawn under or over the tiles, at rest or while scrolling

#### Scenario: Scrolling still works
- **WHEN** I drag an overflowing strip, or make a horizontal wheel gesture over it
- **THEN** it scrolls exactly as it did when it had a scrollbar

#### Scenario: Vertical lists keep theirs
- **WHEN** the "Latest updates" feed has more rows than fit
- **THEN** it still shows its scrollbar beside its rows
