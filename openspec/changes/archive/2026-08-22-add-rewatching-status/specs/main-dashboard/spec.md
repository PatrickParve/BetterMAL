## ADDED Requirements

### Requirement: Rewatches appear in the currently-watching carousel

The main dashboard's "Currently watching" section SHALL include entries whose status is **Rewatching** alongside those whose status is Watching, since both are runs in progress and both are incremented from the same control.

A Rewatching card SHALL be rendered identically to a Watching one — same picture, title, progress row, and "+" control — and SHALL take its place in the section's existing ordering (episodes watched descending, then title) rather than being grouped separately or pinned. A Rewatching entry's anime has always finished airing, so no next-episode countdown applies to its card; its absence SHALL be the ordinary "no countdown known" case rather than a special one.

Incrementing a Rewatching card SHALL behave exactly as incrementing a Watching one, including the rule that finishing the run returns the entry to Completed and increases its rewatch count, per the `list-editing` capability. Once that happens the entry is no longer in progress, so it SHALL leave this section on the next read, the same way a completed first viewing does.

#### Scenario: A rewatch is on the dashboard

- **WHEN** an entry's status is Rewatching
- **THEN** it appears in the Currently watching section, rendered like any other card there

#### Scenario: Ordered together, not grouped apart

- **WHEN** the section holds both Watching and Rewatching entries
- **THEN** they are ordered together by episodes watched descending and then title, with no separation between the two statuses

#### Scenario: Incrementing a rewatch from the dashboard

- **WHEN** I press "+" on a Rewatching card below the last available episode
- **THEN** its episodes watched increases by one and its bar and count update in place, exactly as for a Watching card

#### Scenario: A finished rewatch leaves the section

- **WHEN** I press "+" on a Rewatching card and thereby reach everything available
- **THEN** the entry returns to Completed with its rewatch count increased, and is no longer in Currently watching on the next read
