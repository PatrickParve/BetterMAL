## MODIFIED Requirements

### Requirement: The Series page lists every series with a member in my list
The system SHALL provide a Series page at `/series` listing every stored series that has at least one member — main line or extra, of any watch status — in my list.

A series is a story component (see the `series-versions` capability), so a franchise whose entries are joined by story relations SHALL be listed **once**, however many of its entries carry version relations. Two tellings joined by nothing but a version relation SHALL be listed as two cards. The page SHALL NOT list a series twice.

A stored series with no member in my list SHALL NOT be listed. Series are derived from the relation graph by exploration as well as from my list (see the `series-page` capability), so a franchise the app built while I browsed a season or opened an unrelated detail page is not part of my library and SHALL NOT appear.

A member held only as a **version neighbour** — an anime outside the series' story component, shown in its More section — SHALL NOT make its host series eligible. Otherwise adding one telling to my list would list every telling adjacent to it.

Membership in my list SHALL be the only eligibility rule. In particular the page SHALL NOT apply the watched-coverage rule the profile's Top series section applies (which requires two of a multi-entry main line to be in my list): that rule exists because Top series ranks franchises by an average, which one entry would misrepresent, whereas this page lists rather than ranks. The two surfaces SHALL therefore be permitted to list different sets of series.

The page SHALL NOT build or rebuild any series, and SHALL make no MyAnimeList request. It renders what is stored.

#### Scenario: A series with one entry in my list is listed
- **WHEN** exactly one member of a five-entry franchise is in my list, whatever its status
- **THEN** that series appears on the Series page

#### Scenario: Two separate tellings are two cards
- **WHEN** I have entries from both Fullmetal Alchemist (2003) and Brotherhood, which share no story relation
- **THEN** the Series page shows a card for each, with its own title, picture, averages and figures

#### Scenario: One franchise is one card
- **WHEN** I have entries from Demon Slayer, whose Mugen Ressha movie and TV arc are alternative versions on the same sequel chain
- **THEN** the Series page shows one card, not two near-identical ones

#### Scenario: Only the telling I follow is listed
- **WHEN** my list holds entries from one telling of a franchise and none from the other
- **THEN** only that telling is listed

#### Scenario: A version-neighbour membership does not list a series
- **WHEN** the only member of a series in my list is a version neighbour it holds as an extra
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

### Requirement: A card's figures cover its own telling
Every figure on a series card — its averages, its status pill, its progress badge, its year span, its main-line episode total and its entry count — SHALL be computed over the members of **that** series alone.

A **version neighbour** that is a member SHALL count toward the figures of the series that holds it, exactly as any other extra does. Where that neighbour also has a series of its own, it counts on both cards: each card is read on its own to describe one series, and each is right for the series it describes. No surface SHALL sum figures across cards.

Where a series' main line holds a version slot, the card's figures SHALL use the **default** combination of alternatives, as the `series-page` capability's picked-route requirement defines. A card carries no picker, and the browser SHALL NOT re-derive figures per alternative.

Related entries — the untraversed relations the series page shows in More — SHALL NOT count toward any card figure, since they are not members.

#### Scenario: A version neighbour counts on the card that holds it
- **WHEN** the Fullmetal Alchemist (2003) series holds Brotherhood as a version-neighbour extra
- **THEN** the 2003 card's entry count and averages include Brotherhood, and Brotherhood's own card is computed over its own members

#### Scenario: A card uses the default route
- **WHEN** a franchise's main line holds four alternative routes
- **THEN** its card shows the episode total and progress of the default route rather than of all four

#### Scenario: Related entries are not counted
- **WHEN** a series' main line carries twelve `character` relations
- **THEN** its card's entry count is unchanged by them
