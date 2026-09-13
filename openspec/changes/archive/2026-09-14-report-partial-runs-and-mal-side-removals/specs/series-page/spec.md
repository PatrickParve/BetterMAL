## MODIFIED Requirements

### Requirement: Build all series from my list
The system SHALL provide an action on the Settings page that builds a series for every anime in my list that belongs to no stored series, and rebuilds every series that was built before the current main-line classification rules took effect, so the profile page's Top series ranking can be completed and corrected on demand rather than only filling in over time.

A target SHALL be considered covered only when it holds a **primary** membership in an up-to-date stored series, so an anime stored only as another series' version neighbour is still built into its own.

The action SHALL run in the background and SHALL NOT block the request that starts it. The request that starts it SHALL report the run as in flight, without waiting for the background run to begin, so that a single press is enough for the Settings page to show progress and disable the control. While it runs, the system SHALL report progress as the number of targets processed out of the total, and the Settings page SHALL show that progress and refresh it while the run is in flight. After a run finishes, its final counts SHALL remain visible until another run starts.

The action's control SHALL be disabled while a run is in flight, so one run cannot be started on top of another.

A target already covered by an earlier build in the same run — because building one anime's franchise also stores its other members — SHALL be counted as processed without being built again, so progress reflects real remaining work and no franchise is built once per member.

A failure on one target SHALL be logged and SHALL NOT abort the run; remaining targets SHALL still be processed. A run in which any target failed SHALL end as **failed** rather than complete, saying how many targets could not be built out of the total (see `background-jobs`, "A run always ends as complete or failed"). A target whose story relations lead to no other anime has nothing to store and SHALL NOT count as failed. A failure that ends the whole run SHALL be reported as a failed run with the counts it reached, and SHALL leave the control usable again, so a run that dies before or during target resolution never leaves the page reporting a build that is not happening.

Individual builds SHALL use the same traversal rules, fetch budget, partial/truncated marking, and single-flight collapsing as every other build, so a bulk run racing a user opening a series page results in one build, not two.

#### Scenario: Building every missing series
- **WHEN** I use the "Build all series from my list" action
- **THEN** the request returns immediately and a background run builds a series for each anime in my list that has none

#### Scenario: One press shows progress
- **WHEN** I press the action once
- **THEN** the page reports the run as in flight and begins showing progress without a second press

#### Scenario: Out-of-date series are rebuilt too
- **WHEN** a run starts and some of my list's anime belong to series built before the current classification rules
- **THEN** those series are rebuilt by the run, not skipped as already covered

#### Scenario: One build covers a whole franchise
- **WHEN** a run builds a franchise and later reaches another anime of the same story component
- **THEN** that target is counted as processed without being built again

#### Scenario: A version-neighbour membership does not count as covered
- **WHEN** an anime in my list is stored only as another series' version neighbour and has story relations of its own
- **THEN** the run builds its own series rather than skipping it

#### Scenario: A folded-in neighbour is covered by its host
- **WHEN** an anime in my list has no story relations of its own and is already a version-neighbour extra of a stored series
- **THEN** it is counted as covered, since no series of its own can exist

#### Scenario: Progress is visible while it runs
- **WHEN** a run is in flight
- **THEN** the Settings page shows how many targets have been processed out of the total, updating as the run proceeds

#### Scenario: Final counts stay after it finishes
- **WHEN** a run finishes with no target failing
- **THEN** the Settings page reports the run as complete with its final counts

#### Scenario: The action cannot be double-started
- **WHEN** a run is in flight
- **THEN** the action's control is disabled

#### Scenario: One failing target does not stop the run
- **WHEN** building one target of 40 fails
- **THEN** the failure is logged, the run continues with the remaining targets, and it ends as failed, saying that 1 of 40 could not be built

#### Scenario: A lone anime is not a failure
- **WHEN** a target's story relations lead to no other anime, and no other target fails
- **THEN** the target is counted as processed and the run ends as complete

#### Scenario: A run that fails outright is reported as failed
- **WHEN** a run fails before or during target resolution
- **THEN** the page reports a failed run rather than a run still in progress, and the control becomes usable again

#### Scenario: A bulk build and a page visit collapse into one
- **WHEN** a bulk run is building a series and I open that series' page at the same moment
- **THEN** one build runs and the page is served from it
