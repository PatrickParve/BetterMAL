## ADDED Requirements

### Requirement: A local date's episode is readable in bulk
The system SHALL expose, for many anime at once, the episode airing on a given local date — the same answer the single-anime read gives, resolved from stored airing rows in a single database read rather than one read per anime — so a whole-list surface can decide what airs today without issuing a query per entry.

The bulk read SHALL report, per anime, the local time of day and episode number of that anime's earliest stored row falling within the given local date, using the same local-day boundaries the single-anime read uses, so that a day shifted by daylight saving is covered identically by both. An anime with no stored row on that date SHALL be absent from the result rather than present with an empty value, keeping "nothing airs" distinguishable from "an episode airs at midnight". No episode number SHALL be projected from a broadcast cadence, exactly as for the single-anime read.

Requesting the date's episodes for no anime SHALL be a valid call returning nothing, without touching the database.

#### Scenario: Many dates, one read
- **WHEN** the episode airing today is requested for six hundred anime at once
- **THEN** they are resolved in a single database read

#### Scenario: Bulk agrees with single
- **WHEN** an anime's episode for a local date is read on its own and again as part of a bulk request for the same date
- **THEN** both reads report the same local time and episode number

#### Scenario: An anime with nothing on that date is absent
- **WHEN** a bulk request includes an anime with no stored row falling on the requested local date
- **THEN** that anime is absent from the result

#### Scenario: Two rows on one date
- **WHEN** an anime has two stored rows falling within the same local date
- **THEN** the bulk read reports the earlier of the two, as the single-anime read does

#### Scenario: An empty request is valid
- **WHEN** the date's episodes are requested for an empty set of anime
- **THEN** the call succeeds, returns nothing, and issues no database read

### Requirement: Next airing instants are readable in bulk
The system SHALL expose the next stored air instant for many anime at once, resolved in a single database read rather than one read per anime, so a surface showing a countdown per card can resolve every one of them without a query apiece.

The bulk read SHALL return, per anime, the earliest stored air instant strictly after the given instant — the same value the single-anime read returns. An anime with no future stored row SHALL be absent from the result rather than present with a placeholder instant, keeping "nothing scheduled" distinguishable from any real instant.

Requesting next instants for no anime SHALL be a valid call returning nothing, without touching the database.

#### Scenario: Many instants, one read
- **WHEN** the next airing instant is requested for forty anime at once
- **THEN** they are resolved in a single database read

#### Scenario: Bulk agrees with single
- **WHEN** an anime's next airing instant is read on its own and again as part of a bulk request at the same instant
- **THEN** both reads report the same instant

#### Scenario: An anime with no future row is absent
- **WHEN** a bulk request includes an anime whose stored rows all lie in the past
- **THEN** that anime is absent from the result

#### Scenario: An empty request is valid
- **WHEN** next instants are requested for an empty set of anime
- **THEN** the call succeeds, returns nothing, and issues no database read
