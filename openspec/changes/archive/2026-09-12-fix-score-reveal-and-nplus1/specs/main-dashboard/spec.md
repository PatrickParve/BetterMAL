## ADDED Requirements

### Requirement: The dashboard read's database round trips do not grow with my list
Serving the dashboard SHALL issue a number of database reads that does not grow with the number of entries in my list. Every per-entry airing fact the payload carries — the aired-so-far count behind the broadcast progress bars and the caught-up rule, the "airing today" slot and episode number, and the next-episode countdown — SHALL be resolved through the `episode-airing-data` capability's bulk reads, each taking the whole set of anime it needs answers for, rather than by a read per entry inside a loop.

A fact already resolved for the whole list within one dashboard read SHALL be reused from that result rather than read again for an individual anime: the same aired-so-far figure backs the completion settling, the caught-up filter, the currently-watching cards and the current-season section, and SHALL be read once for all of them.

This SHALL change no value the dashboard reports. Each bulk read answers exactly what the per-anime read answers for the same anime and instant, an anime with nothing stored stays unknown rather than becoming zero, and the sections' contents and ordering are unaffected.

#### Scenario: A large list costs a bounded number of reads
- **WHEN** the dashboard is served for a list of six hundred entries
- **THEN** the number of database reads it issues is the same as for a list of six, rather than growing with the entry count

#### Scenario: An already-resolved count is not re-read
- **WHEN** the dashboard has resolved aired-so-far counts for the whole list and then builds the current-season section, whose cards each show an aired count
- **THEN** it reads those counts from the result it already has, issuing no further read per card

#### Scenario: The payload is unchanged
- **WHEN** the dashboard is served before and after this change for the same stored data at the same instant
- **THEN** both responses carry the same sections, the same cards in the same order, and the same aired counts, airing-today slots and countdowns
