## MODIFIED Requirements

### Requirement: The my-list read's database round trips do not grow with my list
Serving my list SHALL issue a number of database round trips, reads and writes both, that does not grow with the number of entries in it. The aired-so-far count each row carries SHALL be resolved for the whole list through the `episode-airing-data` capability's bulk aired-count read, rather than by a read per entry inside a loop, and the resulting figures SHALL be the same ones handed to the airing watch-status settling that runs on this read.

That settling (the `list-editing` capability's "A caught-up entry is completed once its full run is known" and "A completed entry re-opens when a new episode airs") SHALL decide which entries to move from the entries this read has already loaded, with no database round trip of its own. When it moves any, it SHALL re-read all of them in one read and save all of them in one write, however many there are. When it moves none, it SHALL issue no read and no write. The one exception is an entry that another change reaches between that re-read and the save: each such entry SHALL cost at most one further save for the rest (see the `list-editing` capability's "Automatic status settling treats each entry on its own"). That grows with how many entries were changed concurrently, never with the size of my list.

This SHALL change no value the list reports: the bulk read answers exactly what the per-anime read answers for the same anime and instant, an entry whose anime has no stored airing rows stays unknown rather than becoming zero, and the rows, their order and their grouping are unaffected. Every entry settling moves SHALL reach the same status, finish date, activity-log row and sync request it would have reached if it had been the only one moved.

#### Scenario: A large list costs a bounded number of reads
- **WHEN** my list of six hundred entries is served
- **THEN** the aired-so-far counts for every entry are resolved in one read rather than one read per entry

#### Scenario: The rows are unchanged
- **WHEN** my list is served before and after this change for the same stored data at the same instant
- **THEN** both responses carry the same entries in the same order with the same aired counts, and an entry with no stored airing rows reports an unknown count in both

#### Scenario: Many entries settling at once cost one read and one save
- **WHEN** my list is served and forty of its entries qualify to be completed or re-opened on that read
- **THEN** settling re-reads those forty entries in one read and saves all forty in one write before the response is sent, rather than a read and a save per entry

#### Scenario: Nothing to settle costs nothing
- **WHEN** my list is served and none of its entries qualifies to be completed or re-opened
- **THEN** settling issues no database read and no write

#### Scenario: Settled rows are unchanged
- **WHEN** my list is served before and after this change for the same stored data at the same instant, with several entries qualifying to be completed or re-opened
- **THEN** both responses show those entries with the same statuses, and in both the same entries end up with one activity-log row each and are queued for sync once each
