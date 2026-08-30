## MODIFIED Requirements

### Requirement: A series' displayed title and picture are resolved in one place
The system SHALL resolve a series' displayed title, English title, and picture through a single shared resolution, used by every surface that names or pictures a series: the series page, the series browser's cards, series search results, and the profile's Top series.

The resolution SHALL be: the series' chosen title when it has one, else its root member's title; the series' chosen picture when it has one, else its root member's displayed picture.

Resolution SHALL be **per telling**. Each telling of a franchise is its own stored series with its own root, so each resolves its own title and picture, and choosing one telling's title or picture SHALL NOT affect another's.

No surface SHALL project a series' title or picture from its root member directly.

#### Scenario: One resolution, every surface
- **WHEN** a series has a chosen title and a chosen picture
- **THEN** the series page, the browser card, the search result, and the profile's Top series all show that title and that picture

#### Scenario: An unchosen series falls back to its root
- **WHEN** a series has neither a chosen title nor a chosen picture
- **THEN** every surface shows its root member's title and displayed picture, exactly as before this capability existed

#### Scenario: Tellings resolve independently
- **WHEN** I give one telling of a franchise a chosen title
- **THEN** the other telling's title is unchanged, on every surface

### Requirement: The titles offered for a series
The system SHALL offer, as the titles a series may take, every main-line member's MAL title and every main-line member's English title where MAL provides one, deduplicated, in main-line watch order.

The main line SHALL be that of the telling whose title is being chosen, so another telling's titles are never offered for this one.

Extras' titles SHALL NOT be offered. A series' title describes the franchise's main line, and extras carry names — commercials, promos, recap specials — that are not candidates for it. A **boundary member** is an extra here as everywhere, so the title of the telling this one is an alternative of SHALL NOT be offered.

#### Scenario: Offered titles span the main line
- **WHEN** a series has three main-line members, two of which have English titles
- **THEN** the picker offers five titles, in main-line order, with duplicates removed

#### Scenario: Extras are not offered
- **WHEN** a series has extras with titles of their own
- **THEN** none of them is offered

#### Scenario: The other telling's titles are not offered
- **WHEN** I choose a title for one telling of a franchise
- **THEN** the picker offers only that telling's main-line titles

### Requirement: Chosen titles and pictures survive a rebuild
A series rebuild SHALL preserve the series' chosen title and chosen picture. This SHALL hold whether the rebuild is triggered by a visit, by the rebuild control, by a background build, or by a change in how the main line is classified.

When a rebuild **absorbs** other stored series into the one it keeps, the kept series' own choices SHALL win. Where the kept series has no chosen title, or no chosen picture, and an absorbed series has one, that value SHALL be adopted before the absorbed series is deleted; where several absorbed series qualify, the one with the largest member overlap SHALL be taken.

When a rebuild **splits** a stored series into several tellings, the telling that claims the stored series — the one with the largest member overlap, per the `series-versions` capability — SHALL keep its chosen title and chosen picture. The other tellings SHALL start with neither, since the stored choice described a series that no longer exists and no rule can say which telling it meant.

A stored choice SHALL NOT be re-validated against the rebuilt membership. A chosen title whose source member has left the series, or a chosen picture belonging to a member that is no longer part of it, SHALL be kept.

#### Scenario: A rebuild keeps the choices
- **WHEN** a series with a chosen title and picture is rebuilt
- **THEN** both are unchanged

#### Scenario: An absorption fills in a missing choice
- **WHEN** a rebuild merges two series, the survivor has no chosen title, and the absorbed one does
- **THEN** the survivor takes the absorbed series' title

#### Scenario: An absorption does not overwrite a choice
- **WHEN** a rebuild merges two series and both have chosen titles
- **THEN** the survivor keeps its own

#### Scenario: A split keeps the choice on one telling
- **WHEN** a stored series with a chosen title and picture splits into two tellings
- **THEN** the telling with the larger overlap keeps both, and the other starts with neither

#### Scenario: A choice outlives its source member
- **WHEN** a rebuild removes the member whose title was chosen for the series
- **THEN** the chosen title is kept
