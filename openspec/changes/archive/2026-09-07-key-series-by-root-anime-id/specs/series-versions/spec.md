## MODIFIED Requirements

### Requirement: A build derives and stores only the seed's series
A build SHALL traverse and persist the story component of the anime it was seeded from, and SHALL NOT persist any other series.

Traversal SHALL run in two phases. The first SHALL exhaust the seed's story component over story relations, spending the build's fetch and probe budgets there. The second SHALL follow each member's version relations one hop to find that component's version neighbours, classifying them from cached rows without spending any budget, and SHALL NOT expand beyond them.

A build whose first phase yields a single anime that has version neighbours SHALL instead build the story component of the version neighbour with the largest story component, folding the seeded anime into it as a version neighbour, so opening a lone alternative version reaches the series it belongs to rather than reporting no series. Where such a seed has no version neighbour either, the build SHALL report that the anime belongs to no series, as it does today.

A neighbouring series SHALL be stored when it is itself visited, bulk-built, or enqueued by a relation discovery. A tile that opens a neighbouring series SHALL therefore always resolve: the series read endpoint builds a series for an anime that has none.

Series identity across a rebuild SHALL be resolved by matching the derived component to the stored series it overlaps most; that series SHALL keep its members, its chosen title and its chosen picture, and SHALL be stored under the identifier the derived root gives it — unchanged where the root did not move. Every other stored series overlapping the component SHALL be deleted, and SHALL surrender its chosen title and picture to the survivor where the survivor has none, exactly as an absorbed series does today.

Where the surviving series' identifier changes, the change SHALL take effect together with the deletion of the stored series holding that identifier, so no build leaves a franchise unstored or two series claiming one identifier.

Overlap and deletion SHALL be computed over **story-component members only**, never over version neighbours, so a series that holds a neighbouring series' root as an extra can never delete that neighbour's stored series.

#### Scenario: A build stores one series
- **WHEN** a build is seeded from an entry of Fate/stay night
- **THEN** that franchise's series is stored, and Prisma☆Illya's, Fate/Grand Order's and Fate/Apocrypha's are not

#### Scenario: A neighbouring series is built on demand
- **WHEN** I activate a tile that opens a version neighbour's series and no series is stored for it
- **THEN** its series is built by that visit and its page renders

#### Scenario: A lone alternative version resolves to its franchise
- **WHEN** I open the series page for Clannad Movie, whose only relations are version relations
- **THEN** the Clannad series is built and shown, with the Movie among its extras

#### Scenario: Fragments left by the previous rules are absorbed
- **WHEN** Clannad, Clannad: After Story and Clannad Movie are stored as three separate series and any of them is next read
- **THEN** one series remains, keeping the members and choices of whichever of the three overlapped the component most, stored under the merged component's root, and the other two are deleted

#### Scenario: A neighbour's series is not deleted
- **WHEN** the Fullmetal Alchemist (2003) series is rebuilt while holding Brotherhood as a version-neighbour extra
- **THEN** Brotherhood's own stored series is left intact

#### Scenario: A rebuild does not shuffle identity
- **WHEN** a series is rebuilt with no membership change
- **THEN** it keeps the identifier it had, since its root is unchanged

#### Scenario: A survivor takes an absorbed series' identifier
- **WHEN** a rebuild's component is rooted at an entry belonging to a series it absorbs rather than to the survivor
- **THEN** the survivor is stored under that root's identifier, keeping its own chosen title and picture, and the absorbed series is gone

#### Scenario: Favourite ranks survive a re-derivation
- **WHEN** a series holding ordered favourites is re-derived under these rules
- **THEN** each member keeps its rank in the series it remains a member of
