## MODIFIED Requirements

### Requirement: Most time spent by series

The system SHALL show a "Most time spent" section on the profile page, directly below "Most rewatched", ranking my series by the total time I have spent watching them.

A series' total SHALL be the sum, over **every** member of that series — main line and extras alike — of that member's watch time, where a member's watch time is its first viewing plus every rewatch, valued at that member's episode duration. A member not in my list SHALL contribute nothing.

A member's first viewing SHALL be its episodes watched, so episodes of a currently-airing anime count as they are watched rather than only once it finishes; for a member marked Rewatching — whose episodes watched describe the run in progress — the first viewing SHALL be one complete run, measured by the published total episode count or, when none is published, by its own episodes watched. A member's rewatches SHALL be counted exactly as "Most rewatched by series" counts them: its recorded number of rewatches times its episode count, plus, when it is marked Rewatching, the episodes watched so far in that in-progress run. A member's episode duration SHALL be its published average episode duration, falling back to the same assumed duration the profile's other time figures use when none is published.

These are the same rules the profile's **Days** stat applies per entry, so a franchise total and the profile's own time figures can never disagree about what a viewing is worth.

A series SHALL be listed only when **its main series has been watched**: at least one **main-line** member is in my list with a watch time above zero by the rules above, which is to say at least one episode of it watched. Any watch status SHALL qualify: Dropped, On-hold, Watching, Completed and Rewatching alike. A member marked Rewatching SHALL qualify through its first viewing, which counts as one complete run. Every main-line member SHALL count toward this rule, including an alternative in a version slot that is not the default one, since watching any telling of the main story is watching the main series. A series that fails this rule SHALL be omitted rather than listed at zero or listed with a partial total.

Extras SHALL NOT satisfy this rule, however much of them I have watched. A franchise of which I have watched only a movie, an OVA, a spin-off or any other extra SHALL NOT be listed. Neither SHALL a series whose only watched member is a version neighbour, since a version neighbour is never main line. Once a series qualifies, its extras still count toward its total in full. The rule decides whether a series is listed, never how much its total is.

No further eligibility rule SHALL apply: neither the coverage rule that governs **Top series** nor any minimum member count, because a total is a sum and a franchise of which I have watched one main-line entry has a total that is exactly right.

The strip SHALL be ordered by total watch time descending, ties broken alphabetically by title, case-insensitively. It SHALL NOT be capped: every listed series SHALL be reachable by scrolling the strip to its end.

The strip SHALL present the same form as "Most rewatched"'s Series scope — the same tile size, the same badge position and form, the same white/silver badge colour, the same hover treatment, and the same horizontal drag-scroll — with each tile showing that series' picture, carrying its total watch time as its badge, and opening that series' page. Its total SHALL be stated per "Series watch totals are stated in days and decimal hours".

The section SHALL carry no scope or filter control: it answers one question, at franchise level, and its picture, badge and link are all series-level. Its title SHALL be plain and unbanded, per "Profile section titles are banded by family".

When no series qualifies, the section SHALL show a message in place of the strip rather than an empty strip. The message SHALL say that no series has any of its main series watched yet. It SHALL keep pointing to the Settings page's bulk series build, since a series that has not been built cannot be listed either.

#### Scenario: Ordering by time spent
- **WHEN** the profile page loads and I have watched several franchises for different lengths of time
- **THEN** the "Most time spent" strip lists them from most time spent to least

#### Scenario: Extras and rewatches count
- **WHEN** a listed franchise's total is computed
- **THEN** it includes its extras as well as its main line, and every recorded rewatch of every member as well as each member's first viewing

#### Scenario: A currently-airing entry counts what I have watched
- **WHEN** I have watched five episodes of a still-airing main-line member of a franchise
- **THEN** that franchise is listed, and those five episodes count toward its total rather than nothing until it finishes

#### Scenario: A member not in my list contributes nothing
- **WHEN** a listed franchise has a member I have never added to my list
- **THEN** that member adds nothing to the franchise's total

#### Scenario: A franchise with nothing watched is omitted
- **WHEN** every member of a franchise in my list is plan-to-watch, with no episodes watched
- **THEN** that franchise is not listed at all, rather than listed with a total of zero

#### Scenario: A franchise with only extras watched is omitted
- **WHEN** I have completed a franchise's movie, which is an extra, and have no main-line member of it in my list
- **THEN** that franchise is not listed, even though its movie has watch time

#### Scenario: A plan-to-watch main line does not qualify
- **WHEN** a franchise's main-line entries are all in my list as plan-to-watch with no episodes watched, and I have completed one of its OVAs
- **THEN** that franchise is not listed

#### Scenario: One episode of a dropped main-line entry qualifies
- **WHEN** I dropped a franchise's first season after one episode and have watched nothing else of it
- **THEN** that franchise is listed, with a total of that one episode

#### Scenario: A rewatching main-line entry qualifies
- **WHEN** a franchise's only main-line entry in my list is marked Rewatching with no episodes of the new run watched yet
- **THEN** that franchise is listed, with its first viewing counted as one complete run

#### Scenario: A non-default alternative qualifies
- **WHEN** a franchise's main line holds a version slot, and the only main-line member I have watched is an alternative marked Rewatching with no episodes of the new run watched, which is therefore not the slot's default
- **THEN** that franchise is listed

#### Scenario: A version neighbour does not qualify
- **WHEN** the only member of a series I have watched is a version neighbour that series holds
- **THEN** that series is not listed

#### Scenario: A qualifying franchise still counts its extras
- **WHEN** I have watched one episode of a franchise's main line and all of its three-hour movie
- **THEN** that franchise is listed, and its total covers both the episode and the movie

#### Scenario: One watched entry is enough
- **WHEN** I have watched one main-line entry of a many-entry franchise and none of the others
- **THEN** that franchise is listed, with a total covering only what I have watched

#### Scenario: Breaking a tie
- **WHEN** two series have the same total watch time
- **THEN** they appear in alphabetical order by title

#### Scenario: Uncapped list scrolls to the end
- **WHEN** I have more listed series than fit across the width of the box
- **THEN** the strip scrolls horizontally and reaches the last of them, with none dropped

#### Scenario: Opening a series from the strip
- **WHEN** I click a tile in the "Most time spent" strip
- **THEN** that series' page opens

#### Scenario: The badge matches the rewatch-time badge
- **WHEN** I look at a tile in "Most time spent" and one in "Most rewatched"'s Series scope
- **THEN** the two badges share the same shape, size, position and white/silver colour, differing only in the figure they state

#### Scenario: The section carries no control
- **WHEN** I look at the "Most time spent" box
- **THEN** it shows a plain title and a strip, with no media-type row, no Series option and no reordering control

#### Scenario: Nothing qualifies
- **WHEN** no series has any main-line member watched
- **THEN** the box shows a message in place of the strip saying no series has its main series watched yet, with the link to the Settings page's bulk series build

#### Scenario: The section agrees with the Days stat
- **WHEN** I compare one listed franchise's total against what its members contribute to the profile's Days figure
- **THEN** the two are computed from the same per-entry episodes and the same per-entry durations
