## MODIFIED Requirements

### Requirement: Event-driven refresh triggers
The system SHALL additionally refresh airing data on each of the following events, independent of the elapsed-time schedule:

- An anime is added to my list — that one anime is refreshed immediately, whatever its airing status. A finished anime is included, so its full episode history is fetched on add rather than waiting for the one-time backfill or a manual full refresh.
- The on-demand refresh action is triggered on an anime's detail page — that one anime is refreshed immediately.
- The current viewing season changes at a Winter/Spring/Summer/Fall boundary — all currently-airing and not-yet-aired my-list anime are refreshed.

Refreshing on add SHALL NOT delay the response to the add request.

The add trigger SHALL fire only when the add creates a new list entry; editing an existing entry (status, episodes watched, score, rewatch count) SHALL NOT trigger an airing refresh.

#### Scenario: Adding an airing anime
- **WHEN** an anime whose airing status is currently airing is added to my list
- **THEN** its airing data is fetched immediately, and the add request completes without waiting for that fetch

#### Scenario: Adding an upcoming anime
- **WHEN** an anime whose airing status is not yet aired is added to my list
- **THEN** its airing data is fetched immediately

#### Scenario: Adding a finished anime
- **WHEN** an anime that has finished airing is added to my list
- **THEN** its airing data is fetched immediately, giving it its full stored episode history without waiting for a backfill pass

#### Scenario: Adding an anime with no known airing status
- **WHEN** an anime whose airing status is not recorded is added to my list
- **THEN** its airing data is fetched immediately, rather than the add being skipped for lack of a status

#### Scenario: Editing an existing entry
- **WHEN** the status, episodes watched, score, or rewatch count of an anime already in my list is changed
- **THEN** no airing refresh is triggered by that edit

#### Scenario: Season boundary
- **WHEN** the current viewing season changes to a new Winter, Spring, Summer, or Fall quarter
- **THEN** all currently-airing and not-yet-aired my-list anime have their airing data refreshed
