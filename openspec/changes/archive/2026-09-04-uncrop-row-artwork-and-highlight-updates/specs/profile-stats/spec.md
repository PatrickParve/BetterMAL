## MODIFIED Requirements

### Requirement: Profile list-row posters fill the row
The system SHALL render the poster in every profile list row — "Latest updates" rows, full edit-history rows, and both opinion-divergence lists' rows — flush with the row's top, bottom, and leading edges, filling the row's full height with no padding between the poster and those edges. The poster's leading corners SHALL follow the row's own corner radius, and a row SHALL take its height from its list rather than from the poster's natural aspect ratio — the poster SHALL never inflate a row to its own intrinsic size. A row whose anime has no picture SHALL render its placeholder at the same full-height size.

A poster SHALL keep the proportions of the poster art at whatever height its row has: where a list sets its own row height, the poster's width SHALL follow that height rather than staying at a width fixed for some other row height.

Where the anime's displayed picture is **at least as wide as it is tall**, the row SHALL draw it whole at its own proportions on the terms the `artwork-presentation` capability sets out: the row keeps its height, the picture takes the width its proportions give it at that height up to that capability's bound, and the row's title and everything after it begin further along by that extra width. A portrait poster keeps exactly the box described above, and no row's height changes in either case — so a list showing eight whole rows still shows eight whole rows, and a feed sized to five rows still holds five.

This requirement governs the profile's **list rows** only. The poster **strips** in "My top anime", "Most rewatched" and "Top series" are not list rows and SHALL keep the fixed tile size their own requirements set, cropping to it as they do today.

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

#### Scenario: Landscape artwork does not change how many rows fit
- **WHEN** a list holding landscape artwork renders
- **THEN** it shows the same number of whole rows it shows without it, each at the same height

#### Scenario: The poster strips are unaffected
- **WHEN** "My top anime", "Most rewatched" or "Top series" holds an anime with landscape artwork
- **THEN** every tile in that strip is still the same fixed size, cropped to it as before
