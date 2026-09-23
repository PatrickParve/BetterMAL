## RENAMED Requirements

- FROM: `### Requirement: Poster strips keep a fixed tile size and scroll horizontally only`
- TO: `### Requirement: Poster strips keep a fixed tile height and scroll horizontally only`

## MODIFIED Requirements

### Requirement: Profile list-row posters fill the row
The system SHALL render the poster in every profile list row — "Latest updates" rows, full edit-history rows, and both opinion-divergence lists' rows — flush with the row's top, bottom, and leading edges, filling the row's full height with no padding between the poster and those edges. The poster's leading corners SHALL follow the row's own corner radius, and a row SHALL take its height from its list rather than from the poster's natural aspect ratio — the poster SHALL never inflate a row to its own intrinsic size. A row whose anime has no picture SHALL render its placeholder at the same full-height size.

A poster SHALL keep the proportions of the poster art at whatever height its row has: where a list sets its own row height, the poster's width SHALL follow that height rather than staying at a width fixed for some other row height.

Where the anime's displayed picture is **not a poster**, meaning an upright or a wide picture in the sense the `artwork-presentation` capability defines, the row SHALL draw it whole at its own proportions on the terms the `artwork-presentation` capability sets out: the row keeps its height, the picture takes the width its proportions give it at that height up to that capability's bound, and the row's title and everything after it begin further along by that extra width. A poster keeps exactly the box described above, and no row's height changes in either case — so a list showing eight whole rows still shows eight whole rows, and a feed sized to five rows still holds five.

This requirement governs the profile's **list rows** only. The poster **strips** ("My top anime", "Top series", "Most rewatched" and "Most time spent") are not list rows. They draw their pictures whole as height-bound tiles under "Poster strips keep a fixed tile height and scroll horizontally only".

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

#### Scenario: A landscape picture is drawn landscape
- **WHEN** a Latest updates, edit-history, or divergence row's anime has a displayed picture wider than it is tall
- **THEN** the whole picture is shown at the row's height and at its own width there, with no part cropped away

#### Scenario: An upright picture is not cropped
- **WHEN** a Latest updates, edit-history, or divergence row's anime has an upright displayed picture, such as a 4:5 picture
- **THEN** the whole picture is shown at the row's height and at its own width there, a little wider than a poster

#### Scenario: Landscape artwork does not change how many rows fit
- **WHEN** a list holding landscape artwork renders
- **THEN** it shows the same number of whole rows it shows without it, each at the same height

#### Scenario: The poster strips follow their own rule
- **WHEN** "My top anime", "Top series", "Most rewatched" or "Most time spent" holds an anime with landscape artwork
- **THEN** that tile keeps the strip's tile height rather than a list row's, and its whole picture is drawn under the strips' own rule

### Requirement: Poster strips keep a fixed tile height and scroll horizontally only
The "My top anime", "Top series", "Most rewatched", and "Most time spent" strips SHALL size their poster tiles as a fixed fraction of the strip's width (ten poster tiles across the visible width), regardless of how many entries the strip contains. Tiles SHALL NOT shrink to fit additional entries. Entries beyond those that fit SHALL be reached by scrolling the strip horizontally.

Every tile in a strip SHALL be the height of a poster tile. A tile whose picture is a poster SHALL be exactly the size a poster tile is today. A tile whose picture is upright or wide SHALL keep that height and take the width its picture's proportions give it there. That width SHALL be bounded as the `artwork-presentation` capability bounds a height-bound tile, so the whole picture is shown rather than cropped to the poster tile. The tile's badge, and in "Top series" its score chips beneath the picture, keep their positions relative to the tile.

Ten poster tiles SHALL actually fit: the tile's full rendered width, including any border it carries, SHALL be what the ten-across computation divides the strip's visible width into, so that a strip holding exactly ten entries pictured as posters shows all ten whole — the tenth SHALL NOT be clipped at the strip's trailing edge, and this SHALL hold for a strip that cannot be scrolled as much as for one that can.

Each strip SHALL scroll along the horizontal axis only. Vertical scrolling within a strip SHALL be impossible: a trackpad or wheel gesture SHALL NOT be able to displace the strip's contents up or down, including where a hovered tile's scaled-up size exceeds the strip's height.

A strip whose tiles all fit across its visible width SHALL NOT be scrollable at all: no wheel gesture and no drag SHALL displace its contents by any amount, and the strip SHALL NOT present itself as scrollable — including the grab cursor, which SHALL appear only on a strip that can actually be scrolled. Whether the tiles fit SHALL be decided by their actual rendered widths, not by how many entries there are. So a strip of ten entries that holds a wider tile scrolls, and a strip becomes scrollable, or stops being scrollable, as soon as a tile's picture loads and changes that tile's width. A hovered tile's scaled-up size SHALL NOT count toward whether the tiles fit. Sub-pixel differences between the tiles' total width and the strip's width SHALL NOT make a strip that fits behave as a scrollable one.

#### Scenario: Exactly ten entries are all whole
- **WHEN** a strip holds exactly ten entries, all pictured as posters
- **THEN** all ten tiles are fully visible, with the tenth's trailing edge inside the strip rather than cut off by it

#### Scenario: The tenth tile of a longer strip
- **WHEN** a strip holds more than ten entries, all pictured as posters, and sits at its starting scroll position
- **THEN** the tenth tile is whole and the eleventh is the first one reached by scrolling

#### Scenario: More than ten top anime
- **WHEN** I have more than ten anime scored 10
- **THEN** the tiles stay the same size as when there were exactly ten, and the rest are reached by scrolling the strip

#### Scenario: Exactly ten entries do not scroll
- **WHEN** a media-type filter leaves a strip with exactly ten entries, all pictured as posters
- **THEN** the strip cannot be scrolled or dragged in either direction, and its cursor does not offer to drag it

#### Scenario: Switching to a filter that fits stops the scrolling
- **WHEN** I switch from a filter whose strip scrolls to one whose entries all fit
- **THEN** the strip stops being scrollable, rather than keeping a few pixels of travel from the wider list

#### Scenario: Tile size matches between the strips
- **WHEN** two of the strips are shown at the same window width
- **THEN** their poster tiles are the same size, and every tile in both is the same height

#### Scenario: No vertical drift
- **WHEN** I make a vertical scroll gesture with the pointer over any strip
- **THEN** the strip's contents do not move up or down

#### Scenario: A landscape tile is wider but no taller
- **WHEN** "My top anime" holds an anime whose displayed picture is landscape
- **THEN** its tile is the same height as the poster tiles beside it and wider than them, and the whole picture is shown

#### Scenario: A wider tile makes a full strip scroll
- **WHEN** a strip holds exactly ten entries, one of them pictured as a landscape picture
- **THEN** the strip can be scrolled, offers the grab cursor, and the entries past its visible width are reached by scrolling it

#### Scenario: A strip that fits stays still while a tile is hovered
- **WHEN** a strip whose tiles all fit has one of them hovered and scaled up
- **THEN** the strip still cannot be scrolled or dragged
