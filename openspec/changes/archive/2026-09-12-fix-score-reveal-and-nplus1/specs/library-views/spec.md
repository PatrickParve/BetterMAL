## ADDED Requirements

### Requirement: The my-list read's database round trips do not grow with my list
Serving my list SHALL issue a number of database reads that does not grow with the number of entries in it. The aired-so-far count each row carries SHALL be resolved for the whole list through the `episode-airing-data` capability's bulk aired-count read, rather than by a read per entry inside a loop, and the resulting figures SHALL be the same ones handed to the airing watch-status settling that runs on this read.

This SHALL change no value the list reports: the bulk read answers exactly what the per-anime read answers for the same anime and instant, an entry whose anime has no stored airing rows stays unknown rather than becoming zero, and the rows, their order and their grouping are unaffected.

#### Scenario: A large list costs a bounded number of reads
- **WHEN** my list of six hundred entries is served
- **THEN** the aired-so-far counts for every entry are resolved in one read rather than one read per entry

#### Scenario: The rows are unchanged
- **WHEN** my list is served before and after this change for the same stored data at the same instant
- **THEN** both responses carry the same entries in the same order with the same aired counts, and an entry with no stored airing rows reports an unknown count in both
