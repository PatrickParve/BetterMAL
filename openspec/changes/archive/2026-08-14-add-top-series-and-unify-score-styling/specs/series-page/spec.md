## ADDED Requirements

### Requirement: Series builds can be triggered by the profile's Top series read
The system SHALL schedule background series builds for anime in my list that belong to no stored series, triggered by reading the profile page's Top series section — in addition to the existing page-visit and search triggers.

The number of builds scheduled by one such read SHALL be bounded, so that opening the profile page on a store with thousands of unbuilt anime schedules a small batch rather than the whole catalogue. Repeat reads SHALL continue where earlier ones left off rather than re-scheduling the same anime, so the section's coverage improves visit by visit.

Scheduling SHALL never delay, alter, or fail the response: the section SHALL be served from whatever is already stored, and a scheduling or build failure SHALL be invisible to it.

A background build triggered this way SHALL be identical in every other respect to a visit-triggered one — the same traversal rules, fetch budget, partial/truncated marking, and single-flight collapsing.

#### Scenario: Reading Top series schedules missing builds
- **WHEN** I open the profile page and some anime in my list belong to no stored series
- **THEN** background builds are scheduled for a bounded batch of them, and the section renders immediately from what is already stored

#### Scenario: Coverage improves across visits
- **WHEN** I open the profile page again after an earlier read scheduled its batch
- **THEN** the newly built series are ranked, and the next batch of still-unbuilt anime is scheduled

#### Scenario: A scheduling failure is invisible
- **WHEN** scheduling or a scheduled build fails
- **THEN** the profile page is unaffected and the failure is logged rather than surfaced

### Requirement: Build all series from my list
The system SHALL provide an action on the Settings page that builds a series for every anime in my list that belongs to no stored series, so the profile page's Top series ranking can be completed on demand rather than only filling in over time.

The action SHALL run in the background and SHALL NOT block the request that starts it. While it runs, the system SHALL report progress as the number of targets processed out of the total, and the Settings page SHALL show that progress and refresh it while the run is in flight. After a run finishes, its final counts SHALL remain visible until another run starts.

The action's control SHALL be disabled while a run is in flight, so one run cannot be started on top of another.

A target already covered by an earlier build in the same run — because building one anime's franchise also stores its other members — SHALL be counted as processed without being built again, so progress reflects real remaining work and no franchise is built once per member.

A failure on one target SHALL be logged and SHALL NOT abort the run; remaining targets SHALL still be processed.

Individual builds SHALL use the same traversal rules, fetch budget, partial/truncated marking, and single-flight collapsing as every other build, so a bulk run racing a user opening a series page results in one build, not two.

#### Scenario: Building every missing series
- **WHEN** I use the "Build all series from my list" action
- **THEN** the request returns immediately and a background run builds a series for each anime in my list that has none

#### Scenario: Progress is visible while it runs
- **WHEN** a run is in flight
- **THEN** the Settings page shows how many targets have been processed out of the total, updating as the run proceeds

#### Scenario: Final counts stay after it finishes
- **WHEN** a run completes
- **THEN** the Settings page reports the run as complete with its final counts

#### Scenario: The action cannot be double-started
- **WHEN** a run is in flight
- **THEN** the action's control is disabled

#### Scenario: One build covers a whole franchise
- **WHEN** a run builds a series and later reaches another anime that build already stored as a member
- **THEN** that target is counted as processed without being built again

#### Scenario: One failing target does not stop the run
- **WHEN** building one target fails
- **THEN** the failure is logged and the run continues with the remaining targets

#### Scenario: A bulk build and a page visit collapse into one
- **WHEN** a bulk run is building a series and I open that series' page at the same moment
- **THEN** one build runs and the page is served from it

## MODIFIED Requirements

### Requirement: Score and progress colour language
The series page SHALL use one colour convention throughout: MAL's figures and broadcast progress SHALL use the app's blue — the colour the airing-progress bar already fills with for episodes aired — and my own figures and my watched progress SHALL use the app's purple accent. This SHALL apply to the score averages, the per-entry score bars on the timeline, and the progress fills alike, so which side of a figure is "the world" and which is "me" is readable without labels.

The two score colours SHALL be the app-wide score colour roles defined by the `score-presentation` capability, not a page-local convention: the same blue and purple SHALL carry the same meaning on every other view that renders a MAL score or my score. Broadcast and watched progress fills SHALL follow those same two roles here.

The page SHALL additionally use green to mark that something is on air right now. Green SHALL mark that state only — it SHALL NOT be used for any figure, score, or progress fill, so it never competes with blue-for-MAL/broadcast or purple-for-mine. Broadcast progress on a currently-airing card SHALL therefore stay blue while the card's airing indicator is green: blue measures how much has broadcast, green says it is still broadcasting. Green SHALL remain specific to this page's airing cues and SHALL NOT become part of the app-wide score language.

The score averages SHALL be rendered as compact chips within the header rather than as full-width panels, each naming what it averages, its value, and the count it was computed over, and each honouring its existing reveal rules under the hide-scores toggle.

#### Scenario: MAL and my averages are colour-keyed
- **WHEN** I open a series
- **THEN** the MAL average chips carry the same colour as the aired fill of the progress bar, and my average chips carry the same colour as my watched fill

#### Scenario: Green marks airing and nothing else
- **WHEN** I open a series whose latest season is currently airing
- **THEN** that card's airing indicator is green, while its broadcast fill stays the page's blue and its score chips stay blue and purple

#### Scenario: Averages sit in the header
- **WHEN** I open a series
- **THEN** the score averages appear as compact chips inside the header block rather than as a row of full-width panels below it

#### Scenario: Chips still honour hidden scores
- **WHEN** the hide-scores toggle is on and the series' reveal rules do not apply
- **THEN** the MAL average chips are blurred exactly as the panels were, with the value absent from the rendered output

#### Scenario: The same colours carry the same meaning elsewhere
- **WHEN** I leave the series page for the anime detail page, My List, or the profile page
- **THEN** MAL scores there carry the same blue and my scores the same purple as the series page's chips
