## MODIFIED Requirements

### Requirement: A series' displayed title and picture are resolved in one place
The system SHALL resolve a series' displayed title, English title, and picture through a single shared resolution, used by every surface that names or pictures a series: the series page, the series browser's cards, series search results, and the profile's Top series.

The resolution SHALL be: the series' chosen title when it has one, else its root member's title; the series' chosen picture when it has one, else its root member's displayed picture.

Resolution SHALL be **per series**, and each series is one story component with one root. Two tellings joined only by a version relation are two series, so each resolves its own title and picture, and choosing one's SHALL NOT affect the other's.

The picked alternative of a version slot SHALL NOT affect the resolution: a series' root is the first entry of its watch order and does not move with the picker, so the header a reader sees is stable across a switch.

No surface SHALL project a series' title or picture from its root member directly.

#### Scenario: One resolution, every surface
- **WHEN** a series has a chosen title and a chosen picture
- **THEN** the series page, the browser card, the search result, and the profile's Top series all show that title and that picture

#### Scenario: An unchosen series falls back to its root
- **WHEN** a series has neither a chosen title nor a chosen picture
- **THEN** every surface shows its root member's title and displayed picture, exactly as before this capability existed

#### Scenario: Separate tellings resolve independently
- **WHEN** I give one of two version-linked series a chosen title
- **THEN** the other's title is unchanged, on every surface

#### Scenario: Switching routes does not rename the series
- **WHEN** I pick a different alternative of a version slot
- **THEN** the series' displayed title and picture are unchanged

### Requirement: The titles offered for a series
The system SHALL offer, as the titles a series may take, every main-line member's MAL title and every main-line member's English title where MAL provides one, deduplicated, in main-line watch order.

Every main-line entry SHALL be offered, including every alternative of a version slot and every branch entry, whichever alternative is picked — the offered set describes the series, not the current view.

Extras' titles SHALL NOT be offered. A series' title describes the franchise's main line, and extras carry names — commercials, promos, recap specials — that are not candidates for it. A **version neighbour** is an extra here as everywhere, so the title of a separate telling this series merely links to SHALL NOT be offered.

#### Scenario: Offered titles span the main line
- **WHEN** a series has three main-line members, two of which have English titles
- **THEN** the picker offers five titles, in main-line order, with duplicates removed

#### Scenario: Every route's titles are offered
- **WHEN** a series' main line holds two alternative routes and one is picked
- **THEN** the picker offers the titles of both routes' entries

#### Scenario: Extras are not offered
- **WHEN** a series has extras with titles of their own
- **THEN** none of them is offered

#### Scenario: A separate telling's titles are not offered
- **WHEN** a series holds another telling's root as a version-neighbour extra
- **THEN** that title is not offered

### Requirement: Chosen titles and pictures survive a rebuild
A series rebuild SHALL preserve the series' chosen title and chosen picture. This SHALL hold whether the rebuild is triggered by a visit, by the rebuild control, by a background build, or by a change in how the main line is classified.

When a rebuild **absorbs** other stored series into the one it keeps, the kept series' own choices SHALL win. Where the kept series has no chosen title, or no chosen picture, and an absorbed series has one, that value SHALL be adopted before the absorbed series is deleted; where several absorbed series qualify, the one with the largest member overlap SHALL be taken.

When a rebuild **merges** several stored series back into one story component — as it does for a franchise the previous rules had split — the stored series with the largest overlap over story-component members SHALL keep its identifier and its choices, and the others SHALL surrender theirs under the absorption rule above before being deleted.

A rebuild SHALL NOT delete, or take the choices of, a stored series that overlaps this one only through a **version neighbour**, since that series describes a different story component.

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

#### Scenario: A re-merge keeps one identity and its choices
- **WHEN** three stored series that the previous rules split out of one franchise are rebuilt into one story component
- **THEN** the one with the largest overlap keeps its identifier, its chosen title and its chosen picture, and the other two surrender any choice the survivor lacks before being deleted

#### Scenario: A neighbouring series keeps its choices
- **WHEN** a rebuilt series holds another series' root as a version-neighbour extra
- **THEN** that other series is not deleted and its chosen title and picture are untouched

#### Scenario: A choice outlives its source member
- **WHEN** a rebuild removes the member whose title was chosen for the series
- **THEN** the chosen title is kept
