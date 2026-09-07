## ADDED Requirements

### Requirement: A series is identified by its root entry's MAL id
A series' identifier SHALL be the MyAnimeList id of its root entry — the earliest main-line member, the same entry whose title and picture the series falls back to. The identifier SHALL be derived from the stored series rather than assigned by the store, so that two installations that build the same franchise from the same relation data hold the same identifier for it without exchanging anything, and so that rebuilding a store from empty reproduces the identifiers it had.

The system SHALL hold exactly one identifier for a series. It SHALL NOT store a second identity alongside it, and the identifier a series is stored under, the identifier its membership records refer to, and the identifier its API responses carry SHALL be the same value.

Where the root entry changes — because a rebuild reveals an earlier main-line entry, or because a merge's surviving series takes a component rooted elsewhere — the identifier SHALL change with it. The series' chosen title, chosen picture and membership SHALL survive that change: what moves is the number, not the series.

A series' identifier and its root entry's anime identifier SHALL therefore be the same number, and a surface needing either SHALL read the one value.

#### Scenario: The identifier is the root's MAL id
- **WHEN** a series is built whose earliest main-line entry is the anime with MAL id 1735
- **THEN** the series is stored under identifier 1735, and its membership records refer to it by that identifier

#### Scenario: The same franchise gets the same identifier on another machine
- **WHEN** the same franchise is built from an empty store on a second installation
- **THEN** it is identified by the same number, with nothing exchanged between the two

#### Scenario: A rebuild from empty reproduces the identifiers
- **WHEN** every stored series is deleted and rebuilt from the same relation data
- **THEN** each series is identified by the number it had before

#### Scenario: An older entry moves the identifier
- **WHEN** a rebuild finds a main-line entry earlier than the current root
- **THEN** the series is identified by that entry's MAL id from then on, and keeps its members, its chosen title and its chosen picture

#### Scenario: One identifier, not two
- **WHEN** a series is read through any surface
- **THEN** it reports a single identifier, which is its root entry's MAL id

## MODIFIED Requirements

### Requirement: Series persistence and identity
The system SHALL persist each derived series under the identifier its root entry's MAL id gives it, together with its member set, recording for each member whether it is main line, its position within its list, its relation group, whether it is a story-component member or a version neighbour, whether that series is the member's primary one, and — for a main-line entry — its version slot and its branch, plus the time the series was built.

A member SHALL be recorded per series rather than per anime, so an anime that is a version neighbour of two series holds one record in each. Exactly one of an anime's records SHALL be marked primary, as the `series-versions` capability defines.

A build SHALL persist the seed's series alone. Its component SHALL be matched to the stored series it overlaps most, computing overlap over **story-component members only**; that series keeps its members, its chosen title and its chosen picture, and takes the identifier its root implies — which is the identifier it already had unless the rebuild moved its root. Every other stored series overlapping the component is deleted after surrendering any chosen title or picture the survivor lacks. A newly announced entry therefore folds into the existing series rather than creating a competing one, and the fragments left by an earlier rules change collapse back into one series rather than persisting.

Where a build's surviving series must take an identifier a stored series still holds, that stored series SHALL be one this same build deletes, and the deletion and the re-identification SHALL take effect together: a build SHALL NOT leave a franchise with no stored series, and SHALL NOT leave two series claiming one identifier.

The system SHALL NOT store the series' score averages, computing them at read time instead, so editing a score never leaves a stale average behind.

#### Scenario: Identity survives a rebuild
- **WHEN** a series is rebuilt after a new sequel is announced
- **THEN** the series keeps its members, its choices and the identifier its unchanged root gives it, now with the new entry as a member

#### Scenario: A rebuild that re-roots keeps everything but the number
- **WHEN** a rebuild adds a main-line entry earlier than the series' current root
- **THEN** the series is stored under the new root's MAL id, still holding every member and its chosen title and picture, and no series remains under the old identifier

#### Scenario: Two stored series absorbed into one
- **WHEN** a build's component covers the members of two separately stored series
- **THEN** one series remains, holding every member, and the other stored series is deleted

#### Scenario: A merge takes the identifier of the component's root
- **WHEN** a build's component covers two stored series and its root belongs to the one with the smaller overlap
- **THEN** the larger-overlap series survives with its choices, is stored under that root's MAL id, and the other is deleted — with no moment in which both hold that identifier

#### Scenario: A version neighbour holds its own record
- **WHEN** an anime is a version neighbour of two series
- **THEN** two membership records exist for it, each carrying its own relation group and favourite rank, and neither is primary where it holds a story-component membership elsewhere

#### Scenario: A neighbour's stored series is not absorbed
- **WHEN** a rebuilt series holds another series' root as a version-neighbour extra
- **THEN** that other series is not deleted, since overlap is computed over story-component members only

#### Scenario: Editing a score changes the average immediately
- **WHEN** I change my score on one entry and reopen the series page
- **THEN** my series averages reflect the new score with no rebuild
