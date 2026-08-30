## ADDED Requirements

### Requirement: An alternative version or setting is its own series
A franchise told more than once — a remake, a second adaptation of the same source, a retelling in a different setting — SHALL be stored and shown as one series per telling, not as one series holding them all.

The system SHALL identify the anime that anchor those tellings, and SHALL derive one series per anchor. `alternative_version` and `alternative_setting` SHALL both produce anchors, and SHALL be governed by the same rules throughout this capability; wherever this capability says *version relation*, it means either of them.

An anchor SHALL be a member of the traversed component that carries a version relation, in either direction, to another member of that component, **and** that is main-line-eligible under the `series-page` capability's eligibility rules. A member that is ineligible — a `special`, `music` or `pv` entry, a recap of another member, or another member's side content — SHALL NOT anchor a series, however MyAnimeList relates it, because an uncensored cut or an alternate edit of a special is routinely tagged `alternative_version` and would otherwise split a franchise into a series per special.

Eligibility SHALL be decided over the whole undivided component, before it is partitioned, so that the answer does not depend on which telling a build was seeded from.

A component containing no anchor SHALL produce exactly one series, exactly as it does today.

#### Scenario: Two tellings become two series
- **WHEN** a component holds Fullmetal Alchemist (2003) and Fullmetal Alchemist: Brotherhood, related to each other by `alternative_version`, each with its own seasons, films and specials
- **THEN** two series are stored, one rooted on each, rather than one series holding both

#### Scenario: An alternative setting is its own series too
- **WHEN** a member carries an `alternative_setting` relation to another member and both are main-line-eligible
- **THEN** each anchors its own series, under the same rules an `alternative_version` pair gets

#### Scenario: An alternate cut of a special does not split a franchise
- **WHEN** a franchise's OVA carries an `alternative_version` relation to an uncensored edit of itself, and both are media type `special`
- **THEN** neither anchors a series, and the franchise remains one series

#### Scenario: An ordinary franchise is unchanged
- **WHEN** a component carries no version relation between two eligible members
- **THEN** one series is stored, with the root, main line and watch order it has today

### Requirement: Members are assigned to the version they are nearest to, and shared where they tie
The system SHALL assign every member of a partitioned component to one or more of its anchors by shortest path over **story relations alone** — the traversal set excluding the two version relations.

A member SHALL belong to the series of every anchor at that minimum distance. A member nearer to one anchor than to any other SHALL therefore belong to that anchor's series alone, and a member equidistant from two or more anchors SHALL belong to each of their series.

A member that reaches no anchor at all over story relations SHALL be assigned by shortest path over every traversed relation instead, so no member of a component is left out of every series.

Each series SHALL additionally hold, as members, every anchor directly linked to one of its own members by a version relation. Such a **boundary member** SHALL never be main line in that series, and SHALL never be traversed through when the series is derived — which is what keeps the entries exclusive to another telling off this one's page.

#### Scenario: A film exclusive to one telling stays there
- **WHEN** a film is the sequel of the 2003 series and has no story relation to Brotherhood
- **THEN** it is a member of the 2003 series only, and does not appear on Brotherhood's page

#### Scenario: A shared entry belongs to both
- **WHEN** an OVA is a side story of both tellings at equal distance
- **THEN** it is a member of both series and appears once on each page

#### Scenario: The other telling itself is shown
- **WHEN** I open Brotherhood's series page
- **THEN** the 2003 series' root is a member of it, shown as an alternative version, while that series' own films and specials are not

#### Scenario: A boundary member is never main line
- **WHEN** a series holds another telling's anchor as a boundary member
- **THEN** that anchor is an extra there, and the series' own main line and watch order are computed without it

#### Scenario: Nothing exclusive leaks through a shared entry
- **WHEN** a shared OVA carries a story relation to an entry exclusive to the other telling
- **THEN** that entry is assigned to the telling it is nearer to, and does not appear on the other's page

### Requirement: An anime may belong to more than one series, with one of them primary
The system SHALL permit an anime to be a member of more than one series, recording its membership per series — whether it is main line there, its position there, and its favourite rank there — so that the same anime can be an extra of one telling and a main-line entry of another.

An anime SHALL appear at most once on any single series page.

Exactly one of an anime's memberships SHALL be its **primary** series. The primary SHALL be the series whose anchor it is nearest to, ties broken by the earlier aired-from date of that anchor and then by the lower MAL id. A boundary member's primary SHALL be its own series, never a series it is only a boundary of.

Every surface that asks for *the* series an anime belongs to — the detail page's series link, search's series lookup, the artwork and picture services, and the relation-confidence fallback — SHALL resolve to the primary series. A question about whether two anime belong to the same series SHALL be answered by whether they share **any** series.

#### Scenario: A shared entry has one primary
- **WHEN** an OVA is a member of two tellings' series
- **THEN** exactly one of the two is recorded as its primary

#### Scenario: The detail page links to the primary
- **WHEN** I open the detail page of an anime that is a member of two series
- **THEN** its series link opens its primary series

#### Scenario: An anchor's primary is its own series
- **WHEN** an anchor is a boundary member of a neighbouring telling's series and the root of its own
- **THEN** its primary is its own series

#### Scenario: Same-series questions accept any shared series
- **WHEN** two anime are both members of one series and one of them is also a member of another
- **THEN** they are treated as being in the same series

#### Scenario: No duplicate tiles
- **WHEN** a series page renders an anime that is a member of it and also related to its main line by a further relation
- **THEN** that anime is rendered exactly once

### Requirement: A telling with no relations of its own is still a series
A version that is a single entry — an alternative version or setting with no sequels, side stories or specials of its own — SHALL still be stored as a series and SHALL still have its own page.

This follows from boundary members counting as members: such a version holds itself and the telling it is an alternative of, so the existing rule that a one-member component is not a series is unchanged and does not exclude it.

#### Scenario: A lone alternative version gets a page
- **WHEN** an anime's only relation is an `alternative_version` to another anime
- **THEN** a series is stored for it, holding itself and that other anime, and it has its own page

#### Scenario: A truly unrelated anime is still not a series
- **WHEN** an anime has no relations at all
- **THEN** no series is stored for it and the endpoint reports that it belongs to none

### Requirement: A build derives and stores every telling in the component at once
A build SHALL persist every series its partition produces, not only the one containing the anime it was seeded from, so that a telling shown as an alternative version always has a page to link to and the series browser lists every telling as soon as one of them is built.

Series identity across a rebuild SHALL be resolved by matching each newly derived series to the stored series it overlaps most, taking the pairs in descending order of overlap and claiming each stored series at most once. A claimed series SHALL keep its identifier, its chosen title and its chosen picture. Unclaimed stored series that overlap the component SHALL be deleted, and SHALL surrender their chosen title and picture to the claiming series that has no such value of its own, exactly as an absorbed series does today.

A telling that no stored series matches SHALL be created as a new series.

#### Scenario: One build stores both tellings
- **WHEN** a build is seeded from Brotherhood's second special
- **THEN** both Brotherhood's series and the 2003 series are stored by that build

#### Scenario: A split keeps one identity and mints the other
- **WHEN** a series stored before this capability held both tellings and is rebuilt
- **THEN** the telling with the larger overlap keeps the stored identifier, its chosen title and its chosen picture, and the other telling is stored as a new series

#### Scenario: A rebuild does not shuffle identities
- **WHEN** a component with three tellings is rebuilt with no membership change
- **THEN** each telling keeps the identifier it had

#### Scenario: Favourite ranks survive the split
- **WHEN** a series holding ordered favourites splits into two tellings
- **THEN** each member keeps its rank in the series it remains a member of
