## ADDED Requirements

### Requirement: Aired-episode counts are readable in bulk
The system SHALL expose an aired-so-far episode count for many anime at once, resolved from stored airing rows in a single database read rather than one read per anime, so a whole-list surface can resolve every count it needs without issuing a query per entry.

The bulk read SHALL return the same value the single-anime read returns for each anime — the highest episode number whose stored air instant is at or before the given instant — and SHALL apply no clamping, no projection, and no estimation, exactly as the single-anime read does. An anime with no aired row SHALL be reported as unknown rather than as zero, keeping "nothing stored" distinguishable from "nothing aired yet".

Requesting counts for no anime SHALL be a valid call returning no counts, without touching the database.

#### Scenario: Many counts, one read
- **WHEN** aired counts are requested for two hundred anime at once
- **THEN** they are resolved in a single database read

#### Scenario: Bulk agrees with single
- **WHEN** an anime's aired count is read on its own and again as part of a bulk request at the same instant
- **THEN** both reads report the same count

#### Scenario: An anime with no stored rows is unknown
- **WHEN** a bulk request includes an anime that has no stored airing rows
- **THEN** that anime is reported as unknown rather than as zero

#### Scenario: An empty request is valid
- **WHEN** aired counts are requested for an empty set of anime
- **THEN** no counts are returned and no query is issued
