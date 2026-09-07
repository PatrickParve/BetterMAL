## MODIFIED Requirements

### Requirement: Chosen titles and pictures survive a rebuild
A series rebuild SHALL preserve the series' chosen title and chosen picture. This SHALL hold whether the rebuild is triggered by a visit, by the rebuild control, by a background build, or by a change in how the main line is classified, and SHALL hold whether or not the rebuild changes the series' identifier.

When a rebuild **absorbs** other stored series into the one it keeps, the kept series' own choices SHALL win. Where the kept series has no chosen title, or no chosen picture, and an absorbed series has one, that value SHALL be adopted before the absorbed series is deleted; where several absorbed series qualify, the one with the largest member overlap SHALL be taken.

When a rebuild **merges** several stored series back into one story component — as it does for a franchise the previous rules had split — the stored series with the largest overlap over story-component members SHALL keep its choices, and the others SHALL surrender theirs under the absorption rule above before being deleted. The surviving series SHALL be identified by the merged component's root, which is not necessarily the identifier it held before.

When a rebuild **re-roots** a series — finding a main-line entry earlier than its current root — the series' identifier SHALL follow the new root, and the chosen title and chosen picture SHALL be carried to it. A re-rooting SHALL NOT be a reason to clear, reset or re-validate either choice.

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

#### Scenario: A re-merge keeps one set of choices
- **WHEN** three stored series that the previous rules split out of one franchise are rebuilt into one story component
- **THEN** the one with the largest overlap keeps its chosen title and chosen picture and is identified by the merged component's root, and the other two surrender any choice the survivor lacks before being deleted

#### Scenario: A re-rooting carries the choices to the new identifier
- **WHEN** a series with a chosen title and a chosen picture is rebuilt and an earlier main-line entry becomes its root
- **THEN** the series is identified by that entry's MAL id and still shows the same chosen title and chosen picture

#### Scenario: A neighbouring series keeps its choices
- **WHEN** a rebuilt series holds another series' root as a version-neighbour extra
- **THEN** that other series is not deleted and its chosen title and picture are untouched

#### Scenario: A choice outlives its source member
- **WHEN** a rebuild removes the member whose title was chosen for the series
- **THEN** the chosen title is kept
