## MODIFIED Requirements

### Requirement: The Series page lists every series with a member in my list
The system SHALL provide a Series page at `/series` listing every stored series that has at least one member — main line or extra, of any watch status — in my list.

A franchise told more than once SHALL therefore be listed once **per telling**, since each telling is its own stored series (see the `series-versions` capability). The page SHALL NOT collapse a franchise's tellings into one card, and SHALL NOT list a telling twice.

A stored series with no member in my list SHALL NOT be listed. Series are derived from the relation graph by exploration as well as from my list (see the `series-page` capability), so a franchise the app built while I browsed a season or opened an unrelated detail page is not part of my library and SHALL NOT appear. A telling qualifies on its **own** members, so one telling of a franchise can be listed while another is not.

A member held only as a **boundary member** — another telling's anchor, shown so that it can be linked to — SHALL NOT make its host series eligible. Otherwise adding one telling to my list would list every telling adjacent to it.

Membership in my list SHALL be the only eligibility rule. In particular the page SHALL NOT apply the watched-coverage rule the profile's Top series section applies (which requires two of a multi-entry main line to be in my list): that rule exists because Top series ranks franchises by an average, which one entry would misrepresent, whereas this page lists rather than ranks. The two surfaces SHALL therefore be permitted to list different sets of series.

The page SHALL NOT build or rebuild any series, and SHALL make no MyAnimeList request. It renders what is stored.

#### Scenario: A series with one entry in my list is listed
- **WHEN** exactly one member of a five-entry franchise is in my list, whatever its status
- **THEN** that series appears on the Series page

#### Scenario: Two tellings are two cards
- **WHEN** I have entries from both Fullmetal Alchemist (2003) and Brotherhood
- **THEN** the Series page shows a card for each, with its own title, picture, averages and figures

#### Scenario: Only the telling I follow is listed
- **WHEN** my list holds entries from one telling of a franchise and none from the other
- **THEN** only that telling is listed

#### Scenario: A boundary membership does not list a telling
- **WHEN** the only member of a telling's series in my list is another telling's anchor, held as its boundary member
- **THEN** that series is not listed

#### Scenario: An explored-only series is not listed
- **WHEN** a series was built while I browsed a season and none of its members is in my list
- **THEN** that series does not appear on the Series page

#### Scenario: An extra-only membership still lists the series
- **WHEN** the only member of a series in my list is one of its extras, not a main-line entry
- **THEN** the series appears on the Series page

#### Scenario: Opening the page costs no fetches
- **WHEN** I open the Series page
- **THEN** no series is built or rebuilt and no MyAnimeList request is made

#### Scenario: The page lists series Top series omits
- **WHEN** a franchise has three aired main-line entries of which only one is in my list
- **THEN** it appears on the Series page even though the profile's Top series section excludes it

## ADDED Requirements

### Requirement: A card's figures cover its own telling
Every figure on a series card — its averages, its status pill, its progress badge, its year span, its main-line episode total and its entry count — SHALL be computed over the members of **that** series alone.

A member shared between two tellings SHALL count toward each of their cards. Each card is read on its own to describe one telling, and omitting a shared entry from one of them would understate that telling.

A **boundary member** SHALL NOT count toward any figure of the series that holds it as a boundary, and SHALL NOT be included in its entry count. It belongs to the telling it anchors, and counting it twice would inflate both.

Related entries — the untraversed relations the series page shows in More — SHALL NOT count toward any card figure, since they are not members.

#### Scenario: A shared entry counts on both cards
- **WHEN** an OVA is a member of two tellings
- **THEN** both cards include it in their entry counts and their averages

#### Scenario: A boundary member is not counted
- **WHEN** a telling holds another telling's anchor as its boundary member
- **THEN** that anchor is absent from its entry count, its averages and its year span

#### Scenario: Related entries are not counted
- **WHEN** a series' main line carries twelve `character` relations
- **THEN** its card's entry count is unchanged by them
