## MODIFIED Requirements

### Requirement: The dashboard read's database round trips do not grow with my list
Serving the dashboard SHALL issue a number of database round trips, reads and writes both, that does not grow with the number of entries in my list. Every per-entry airing fact the payload carries — the aired-so-far count behind the broadcast progress bars and the caught-up rule, the "airing today" slot and episode number, and the next-episode countdown — SHALL be resolved through the `episode-airing-data` capability's bulk reads, each taking the whole set of anime it needs answers for, rather than by a read per entry inside a loop.

A fact already resolved for the whole list within one dashboard read SHALL be reused from that result rather than read again for an individual anime: the same aired-so-far figure backs the completion settling, the caught-up filter, the currently-watching cards and the current-season section, and SHALL be read once for all of them.

The completion settling (the `list-editing` capability's "A caught-up entry is completed once its full run is known" and "A completed entry re-opens when a new episode airs") SHALL decide which entries to move from the entries this read has already loaded, with no database round trip of its own. When it moves any, it SHALL re-read all of them in one read and save all of them in one write, however many there are. When it moves none, it SHALL issue no read and no write. The one exception is an entry that another change reaches between that re-read and the save: each such entry SHALL cost at most one further save for the rest (see the `list-editing` capability's "Automatic status settling treats each entry on its own"). That grows with how many entries were changed concurrently, never with the size of my list.

This SHALL change no value the dashboard reports. Each bulk read answers exactly what the per-anime read answers for the same anime and instant, an anime with nothing stored stays unknown rather than becoming zero, and the sections' contents and ordering are unaffected. Every entry settling moves SHALL reach the same status, finish date, activity-log row and sync request it would have reached if it had been the only one moved, and SHALL still be settled before the dashboard selects which entries are Watching.

#### Scenario: A large list costs a bounded number of reads
- **WHEN** the dashboard is served for a list of six hundred entries
- **THEN** the number of database reads it issues is the same as for a list of six, rather than growing with the entry count

#### Scenario: An already-resolved count is not re-read
- **WHEN** the dashboard has resolved aired-so-far counts for the whole list and then builds the current-season section, whose cards each show an aired count
- **THEN** it reads those counts from the result it already has, issuing no further read per card

#### Scenario: The payload is unchanged
- **WHEN** the dashboard is served before and after this change for the same stored data at the same instant
- **THEN** both responses carry the same sections, the same cards in the same order, and the same aired counts, airing-today slots and countdowns

#### Scenario: Many entries settling at once cost one read and one save
- **WHEN** the dashboard is served and forty entries in my list qualify to be completed or re-opened on that read
- **THEN** settling re-reads those forty entries in one read and saves all forty in one write before the sections are built, rather than a read and a save per entry

#### Scenario: Nothing to settle costs nothing
- **WHEN** the dashboard is served and no entry in my list qualifies to be completed or re-opened
- **THEN** settling issues no database read and no write

#### Scenario: Several re-opened shows reach Currently watching on the same visit
- **WHEN** three Completed entries on currently-airing anime each qualify to be re-opened, and the home page is the first place I open
- **THEN** all three are re-opened by that one save and all three appear in Currently watching on that same visit
