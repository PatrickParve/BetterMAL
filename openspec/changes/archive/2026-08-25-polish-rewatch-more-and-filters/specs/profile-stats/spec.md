## MODIFIED Requirements

### Requirement: Most rewatched by series
The "Most rewatched" section SHALL offer a **Series** scope that re-reads the section by franchise instead of by single anime, ranking my series by the total time I have spent rewatching them.

A series' total SHALL be the sum, over **every** member of that series — main line and extras alike — of that member's rewatch time, where a member's rewatch time is its recorded number of rewatches multiplied by its episode count multiplied by its episode duration, **plus, when that member is marked Rewatching, the episodes it has watched so far in that in-progress run, valued at the same episode duration**. A member not in my list SHALL contribute nothing.

An in-progress rewatch SHALL therefore contribute the time already spent on it, rather than nothing until the run finishes. Because entering Rewatching resets episodes-watched to zero and the rewatch count only increases once a run completes, the two figures never overlap: the rewatch count accounts for the runs already finished and episodes-watched accounts for the current one. A member marked Rewatching whose rewatch count is still zero — a first rewatch in progress — SHALL contribute the episodes it has watched so far and SHALL therefore make its series eligible for this scope, where previously it contributed nothing.

A member in my list with no recorded rewatches and not marked Rewatching SHALL contribute nothing.

A member's episode count for this purpose SHALL be its published total episode count, so a completed rewatch counts as a full run through that entry regardless of where its current progress sits. When no total is published, the entry's own episodes-watched figure SHALL be used, since that is the only length the app knows for it. A member's episode duration SHALL be its published average episode duration, falling back to the same assumed duration the profile's other time figures use when none is published. These are the same rules the profile's existing rewatch-inclusive episode and time figures use, so a franchise total and the profile's own stats can never disagree about what a rewatch is worth.

First viewings SHALL NOT count. A member watched once and never rewatched SHALL contribute nothing to its series' total, however long it is. The episodes of an in-progress rewatch are not a first viewing and SHALL count.

A series SHALL be listed when its total is above zero, and SHALL be omitted otherwise. No further eligibility rule SHALL apply — in particular the coverage rule that governs **Top series** (requiring two aired main-line entries in my list) SHALL NOT apply here, because a total is a sum rather than an average and a franchise of which I have rewatched only one entry has a total that is exactly right.

The strip SHALL be ordered by total rewatch time descending, ties broken alphabetically by title, case-insensitively. It SHALL NOT be capped: every listed series SHALL be reachable by scrolling the strip to its end.

The scope SHALL present the same strip form as the section's other scopes — the same tile size, the same badge position and form, the same hover treatment, and the same horizontal drag-scroll. Two things SHALL differ: each tile's badge SHALL show that series' total rewatch time rather than an integer count, and opening a tile SHALL navigate to that series' page rather than to an anime's detail page.

A rewatched anime that belongs to no stored series SHALL simply be absent from the Series scope; it SHALL continue to appear in every media-type scope.

The section's per-media-type scopes SHALL be unaffected by this rule: they rank by the recorded rewatch count, which is an integer count of completed runs, and an in-progress run SHALL NOT change it.

#### Scenario: Ranking franchises by rewatch time
- **WHEN** I select the Series scope on "Most rewatched"
- **THEN** the strip lists my series ordered by the total time I have spent rewatching each of them, longest first

#### Scenario: Summing across a franchise's seasons
- **WHEN** a series' first season of 12 episodes has been rewatched twice and its second season of 13 episodes once, each with 24-minute episodes
- **THEN** the series' total is the time for 37 episodes — 24 from the first season's two rewatches and 13 from the second season's one — and not the time for its first viewings

#### Scenario: An in-progress rewatch counts the episodes already rewatched
- **WHEN** a series' 12-episode season has a rewatch count of two and is marked Rewatching with three episodes watched
- **THEN** it contributes the time for 27 episodes — 24 from its two completed rewatches and 3 from the run in progress — rather than 24

#### Scenario: A first rewatch in progress makes a series eligible
- **WHEN** the only rewatched member of a series is marked Rewatching with a rewatch count of zero and five episodes watched
- **THEN** the series appears in the Series scope with the time for those five episodes, rather than being omitted

#### Scenario: A completed entry that is not being rewatched still counts nothing
- **WHEN** a member is marked Completed with a rewatch count of zero
- **THEN** it contributes nothing to its series' total

#### Scenario: The media-type scopes ignore an in-progress rewatch
- **WHEN** an anime is marked Rewatching with a rewatch count of one and four episodes watched, and I look at the All scope
- **THEN** its badge still reads 1, because that scope counts completed rewatches

#### Scenario: Extras count toward a franchise's total
- **WHEN** a series' OVA, which is not on its main line, has been rewatched once
- **THEN** that OVA's rewatch time is included in the series' total

#### Scenario: A rewatched film
- **WHEN** a one-episode film of 120 minutes has been rewatched three times
- **THEN** it contributes six hours to its series' total

#### Scenario: First watches do not count
- **WHEN** I have completed every entry of a long series exactly once and rewatched none of them
- **THEN** that series does not appear in the Series scope

#### Scenario: A rewatch of an entry with no published total
- **WHEN** an entry with no published episode count has 40 episodes watched and one recorded rewatch
- **THEN** its rewatch time is counted as 40 episodes' worth, matching how the profile's own rewatch-inclusive figures count it

#### Scenario: A rewatch of an entry with no published duration
- **WHEN** a rewatched entry has no published average episode duration
- **THEN** its episodes are valued at the same assumed duration the profile's other time figures use

#### Scenario: A barely-covered franchise is still ranked
- **WHEN** a series' main line has three aired seasons, only one of which is in my list, and I have rewatched that one
- **THEN** the series is listed in the Series scope, unlike in Top series, because its total is a sum rather than an average

#### Scenario: Breaking a tie
- **WHEN** two series have the same total rewatch time
- **THEN** they appear in alphabetical order by title

#### Scenario: Opening a series from the strip
- **WHEN** I click a tile in the Series scope
- **THEN** that series' page opens, not an anime's detail page

#### Scenario: The strip is uncapped
- **WHEN** I have more rewatched series than fit across the width of the box
- **THEN** the strip scrolls horizontally and reaches the last of them, with none dropped

#### Scenario: An anime whose franchise has not been built
- **WHEN** an anime I have rewatched belongs to no stored series
- **THEN** it does not appear in the Series scope, and it still appears in the All scope
