## ADDED Requirements

### Requirement: Automatic status settling treats each entry on its own
When one evaluation of "A caught-up entry is completed once its full run is known" and "A completed entry re-opens when a new episode airs" moves more than one entry, the system SHALL treat each moved entry exactly as if it had been the only one. The evaluation can happen on any read path or on the recurring airing schedule. Each moved entry SHALL get its own activity-log row and its own MyAnimeList sync request, and re-openings and completions SHALL be able to happen in the same evaluation. How many entries are moved together SHALL NOT change what happens to any one of them.

Each entry SHALL be checked against its stored status again immediately before it is moved. Some other change may already have moved it off the status the rule starts from: another read settling it first, an edit from the progress row or the entry editor, or an import or sync. Such an entry SHALL be left as that change left it, with no status change, activity-log row or sync request from this evaluation.

The same SHALL hold for a change that reaches an entry after that check but before the move is saved. That one entry SHALL NOT be moved by this evaluation, and SHALL NOT receive a log row or a sync request from it. The other entries in the same evaluation SHALL still be moved, logged and queued for sync as normal, and SHALL NOT be discarded along with it.

Either way, a page whose read ran the evaluation SHALL show each such entry with the status the database holds for it. An entry left alone this way is not skipped for good: the rules are evaluated again on the next read and on the recurring airing schedule, as they always are.

#### Scenario: Several entries complete on one read
- **WHEN** I open my list and three Watching entries have each watched their anime's known total, and each anime has aired in full
- **THEN** all three become Completed with a finish date, each with its own activity-log row, and each is queued for sync once

#### Scenario: Several entries re-open on one read
- **WHEN** I open my list and three Completed entries are each on a currently-airing anime whose known total they haven't watched
- **THEN** all three become Watching, each with its own activity-log row, and each is queued for sync once

#### Scenario: A re-opening and a completion on the same read
- **WHEN** I open my list and one entry qualifies to be re-opened while another qualifies to be completed
- **THEN** the first becomes Watching and the second becomes Completed, each with its own activity-log row and its own sync request

#### Scenario: An entry another change already moved is left alone
- **WHEN** two Watching entries qualify to be completed when my list's read picks them, and before settling checks them again one of them is marked Dropped from the entry editor
- **THEN** that entry stays Dropped, no completion is logged or queued for it, and my list shows it as Dropped, while the other entry is still completed, logged and queued for sync

#### Scenario: A change landing just before the save doesn't hold up the rest
- **WHEN** three entries qualify to be moved on a read, and a sync push saves one of them after settling has checked it but before settling saves
- **THEN** that one entry is not moved by this read and gets no log row or sync request from it, the other two are moved, logged and queued for sync, and the next read evaluates the first entry again

#### Scenario: Nothing is queued for an entry that wasn't moved
- **WHEN** an evaluation picks four entries and one of them turns out to have been moved by another change already
- **THEN** exactly three sync requests are queued, one for each entry this evaluation actually moved
