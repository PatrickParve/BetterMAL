## ADDED Requirements

### Requirement: Series builds can be triggered in the background by search
The system SHALL support building a series in the background, triggered by a search that found no stored series for its top-ranked anime match (see the `navigation-and-search` capability), in addition to the existing trigger of opening a series page.

A background-triggered build SHALL be identical in every other respect to a visit-triggered one: the same traversal rules, the same visit fetch budget, the same partial/truncated marking, and the same single-flight collapsing — so a background build and a user opening that series page at the same moment SHALL result in one build, not two.

A background build SHALL be best-effort: a failure SHALL be logged and dropped, leaving no stored series and affecting nothing the user is doing.

An anime whose story relations resolve to nothing else SHALL still store no series, exactly as on a visit-triggered build.

#### Scenario: A search-triggered build stores the series
- **WHEN** a search schedules a build for an anime belonging to an unbuilt franchise
- **THEN** the series is built and stored just as it would be by opening its series page

#### Scenario: Background build and page visit collapse into one
- **WHEN** a background build for a series is in progress and I open that series' page
- **THEN** one build runs and the page is served from it

#### Scenario: A background build failure is contained
- **WHEN** a background build fails part-way through
- **THEN** the failure is logged, no partial result is presented to the user, and nothing the user is doing is interrupted

#### Scenario: A lone anime still stores no series
- **WHEN** a background build runs for an anime whose story relations resolve to nothing else
- **THEN** no series is stored, matching the visit-triggered behaviour
