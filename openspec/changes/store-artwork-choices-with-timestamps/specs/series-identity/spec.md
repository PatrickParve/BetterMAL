## MODIFIED Requirements

### Requirement: Chosen titles and pictures survive a rebuild
A series rebuild SHALL preserve the series' chosen title and chosen picture, each with the time it was made. This SHALL hold whether the rebuild is triggered by a visit, by the rebuild control, by a background build, or by a change in how the main line is classified.

A choice and its recorded time SHALL move together and only together. A rebuild SHALL NOT carry a choice without its time, SHALL NOT carry a time without its choice, and SHALL NOT record a new time for a choice it merely carries or adopts: rebuilding is not choosing, and a choice made three weeks ago SHALL still report a three-week-old time after any number of rebuilds.

When a rebuild **absorbs** other stored series into the one it keeps, the kept series' own choices SHALL win. Where the kept series has no chosen title, or no chosen picture, and an absorbed series has one, that value SHALL be adopted — together with the time that series recorded for it — before the absorbed series is deleted; where several absorbed series qualify, the one with the largest member overlap SHALL be taken.

When a rebuild **merges** several stored series back into one story component — as it does for a franchise the previous rules had split — the stored series with the largest overlap over story-component members SHALL keep its identifier and its choices, and the others SHALL surrender theirs under the absorption rule above before being deleted.

A rebuild SHALL NOT delete, or take the choices of, a stored series that overlaps this one only through a **version neighbour**, since that series describes a different story component.

A stored choice SHALL NOT be re-validated against the rebuilt membership. A chosen title whose source member has left the series, or a chosen picture belonging to a member that is no longer part of it, SHALL be kept.

#### Scenario: A rebuild keeps the choices
- **WHEN** a series with a chosen title and picture is rebuilt
- **THEN** both are unchanged, and so are the times they were chosen

#### Scenario: An absorption fills in a missing choice
- **WHEN** a rebuild merges two series, the survivor has no chosen title, and the absorbed one does
- **THEN** the survivor takes the absorbed series' title along with the time that series recorded for it

#### Scenario: An absorption does not overwrite a choice
- **WHEN** a rebuild merges two series and both have chosen titles
- **THEN** the survivor keeps its own, with its own time

#### Scenario: A re-merge keeps one identity and its choices
- **WHEN** three stored series that the previous rules split out of one franchise are rebuilt into one story component
- **THEN** the one with the largest overlap keeps its identifier, its chosen title and its chosen picture, and the other two surrender any choice the survivor lacks — each with its recorded time — before being deleted

#### Scenario: A re-root does not restamp a choice
- **WHEN** a rebuild moves a series to a new root, deleting and re-inserting the row, and that series has a chosen picture from three weeks ago
- **THEN** the re-inserted series holds the same chosen picture and the same three-week-old time

#### Scenario: A neighbouring series keeps its choices
- **WHEN** a rebuilt series holds another series' root as a version-neighbour extra
- **THEN** that other series is not deleted and its chosen title and picture are untouched

#### Scenario: A choice outlives its source member
- **WHEN** a rebuild removes the member whose title was chosen for the series
- **THEN** the chosen title is kept
