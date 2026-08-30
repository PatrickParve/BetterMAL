## ADDED Requirements

### Requirement: A refresh that discovers a relation enqueues a series build
Wherever a fetch writes an anime's relations and a relation edge is newly discovered for it, the system SHALL enqueue that anime for a background series build, so a newly announced entry, a newly reported side story, or a newly reported alternative version reaches the series page without waiting for a visit or for the 30-day staleness window.

This SHALL apply to every path that writes relations — the scheduled tiered refresh, the visit-triggered refresh, the on-demand single-anime refresh, and a probe or member fetch made during a series build alike — so no refresh path can silently leave a series behind.

The enqueue SHALL NOT block the refresh, and the build it triggers SHALL use the same traversal rules, partitioning, fetch budget and single-flight collapsing every other build uses. Enqueueing an anime already queued SHALL be a no-op, so one refresh pass discovering many edges causes one build per franchise rather than one per edge.

Nothing else on the series page needs this treatment: scores, episode counts, airing status and my own progress are recomputed on every read of the page and are never stored on the series.

#### Scenario: A newly announced sequel reaches the series
- **WHEN** a scheduled refresh discovers a `sequel` relation from an anime in my list to an anime it did not previously relate to
- **THEN** that anime's series is built in the background and the new entry is on its page the next time I open it

#### Scenario: A manual refresh does the same
- **WHEN** I refresh a single anime by hand and that fetch discovers a new relation
- **THEN** its series is enqueued exactly as the scheduled refresh would enqueue it

#### Scenario: A newly discovered alternative version becomes its own series
- **WHEN** a refresh discovers an `alternative_version` relation on a member of a stored series
- **THEN** the background build partitions the franchise and both tellings are stored

#### Scenario: Many edges cause one build
- **WHEN** one refresh pass discovers six new relation edges across four members of the same franchise
- **THEN** the franchise is built once, not once per edge

#### Scenario: A refresh that discovers nothing enqueues nothing
- **WHEN** a refresh rewrites an anime's relations with no edge it did not already have
- **THEN** no series build is enqueued

#### Scenario: The refresh is not slowed by the enqueue
- **WHEN** a refresh discovers a relation
- **THEN** the refresh completes without waiting for the series build to run
