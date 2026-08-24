## ADDED Requirements

### Requirement: Most rewatched by series
The "Most rewatched" section SHALL offer a **Series** scope that re-reads the section by franchise instead of by single anime, ranking my series by the total time I have spent rewatching them.

A series' total SHALL be the sum, over **every** member of that series — main line and extras alike — of that member's rewatch time, where a member's rewatch time is its recorded number of rewatches multiplied by its episode count multiplied by its episode duration. A member not in my list, or in my list with no rewatches, SHALL contribute nothing.

A member's episode count for this purpose SHALL be its published total episode count, so a completed rewatch counts as a full run through that entry regardless of where its current progress sits. When no total is published, the entry's own episodes-watched figure SHALL be used, since that is the only length the app knows for it. A member's episode duration SHALL be its published average episode duration, falling back to the same assumed duration the profile's other time figures use when none is published. These are the same rules the profile's existing rewatch-inclusive episode and time figures use, so a franchise total and the profile's own stats can never disagree about what a rewatch is worth.

First viewings SHALL NOT count. A member watched once and never rewatched SHALL contribute nothing to its series' total, however long it is.

A series SHALL be listed when its total is above zero, and SHALL be omitted otherwise. No further eligibility rule SHALL apply — in particular the coverage rule that governs **Top series** (requiring two aired main-line entries in my list) SHALL NOT apply here, because a total is a sum rather than an average and a franchise of which I have rewatched only one entry has a total that is exactly right.

The strip SHALL be ordered by total rewatch time descending, ties broken alphabetically by title, case-insensitively. It SHALL NOT be capped: every listed series SHALL be reachable by scrolling the strip to its end.

The scope SHALL present the same strip form as the section's other scopes — the same tile size, the same badge position and form, the same hover treatment, and the same horizontal drag-scroll. Two things SHALL differ: each tile's badge SHALL show that series' total rewatch time rather than an integer count, and opening a tile SHALL navigate to that series' page rather than to an anime's detail page.

A rewatched anime that belongs to no stored series SHALL simply be absent from the Series scope; it SHALL continue to appear in every media-type scope.

#### Scenario: Ranking franchises by rewatch time
- **WHEN** I select the Series scope on "Most rewatched"
- **THEN** the strip lists my series ordered by the total time I have spent rewatching each of them, longest first

#### Scenario: Summing across a franchise's seasons
- **WHEN** a series' first season of 12 episodes has been rewatched twice and its second season of 13 episodes once, each with 24-minute episodes
- **THEN** the series' total is the time for 37 episodes — 24 from the first season's two rewatches and 13 from the second season's one — and not the time for its first viewings

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

### Requirement: Rewatch time is stated in days and decimal hours
A series' total rewatch time SHALL be shown as whole days and decimal hours. The days part SHALL be omitted entirely when the total is under a day, so a total of a few hours reads as hours alone.

Hours SHALL carry one decimal place. A total under one hour SHALL instead carry two decimal places, so a short rewatch is stated rather than rounded away. Trailing zeros SHALL be trimmed, so a value of exactly two hours reads `2h` and a value of exactly four tenths of an hour reads `0.4h`.

The total SHALL be rounded to a tenth of an hour before days are split off it, so a value just short of a whole number of days can never render an hours part of 24.

A total above zero that would otherwise render as `0h` SHALL render as `<0.01h`, so a series listed for time spent never reports no time spent.

#### Scenario: A total spanning days
- **WHEN** a series' total rewatch time is three days, seven hours, and forty minutes
- **THEN** it reads "3d 7.7h"

#### Scenario: A total under a day
- **WHEN** a series' total rewatch time is seven hours and forty minutes
- **THEN** it reads "7.7h", with no days part

#### Scenario: A half hour is not rounded away
- **WHEN** a series' total rewatch time is one hour and thirty minutes
- **THEN** it reads "1.5h"

#### Scenario: A whole number of hours
- **WHEN** a series' total rewatch time is exactly two hours
- **THEN** it reads "2h", not "2.0h"

#### Scenario: A total under an hour
- **WHEN** a series' total rewatch time is twenty-three minutes
- **THEN** it reads "0.38h"

#### Scenario: A round fraction of an hour
- **WHEN** a series' total rewatch time is twenty-four minutes
- **THEN** it reads "0.4h", not "0.40h"

#### Scenario: Rounding never produces a 24-hour part
- **WHEN** a series' total rewatch time is just under two days
- **THEN** it reads "2d 0h" rather than "1d 24h"

#### Scenario: A very short total
- **WHEN** a series' total rewatch time is under half a minute
- **THEN** it reads "<0.01h" rather than "0h"

## MODIFIED Requirements

### Requirement: Most rewatched section
The system SHALL show a "Most rewatched" section on the profile page, positioned directly below the "My top anime" box and rendered in the same style as it: a horizontal strip of poster tiles, each tile linking to that anime's detail page and carrying a badge in the same position as the top-anime strip's score badge.

Under its media-type scopes, the section SHALL contain every list entry whose rewatch count is greater than zero, regardless of watch status, and SHALL exclude every entry with a rewatch count of zero. The badge on each tile SHALL show that entry's rewatch count. The section additionally offers a Series scope, whose membership, ordering, badge, and link target are defined by "Most rewatched by series"; everything else in this requirement describes the media-type scopes.

The rewatch-count badge SHALL carry the same badge form as the top-anime strip's score badge — the same position, size, pill shape, and dark tint that keeps it legible over any poster, plus a matching border — but SHALL be rendered in white/silver rather than in either score colour role. It SHALL NOT use the "mine" purple or the "MAL" blue, so a rewatch count is never read as a score, while still reading as a deliberate badge rather than as plain text laid over the poster. The Series scope's time badge SHALL carry that same form and colour.

The strip SHALL be ordered by rewatch count descending. Ties SHALL be broken alphabetically by title, case-insensitively, and by nothing else — my score SHALL NOT participate in the ordering, so equally-rewatched entries read in one predictable sequence rather than one that shifts whenever a score changes. The strip SHALL NOT be capped at any size — every qualifying entry SHALL be reachable by scrolling the strip to its end.

The section SHALL be read-only: it SHALL NOT offer any reordering control, and its order SHALL be derived entirely from rewatch counts rather than from the persisted top-anime ordering.

#### Scenario: Ordering by rewatch count
- **WHEN** the profile page loads and I have rewatched several anime different numbers of times
- **THEN** the "Most rewatched" strip lists them from most rewatched to least rewatched

#### Scenario: The count badge carries its own colour
- **WHEN** I look at a tile in the "Most rewatched" strip
- **THEN** its rewatch count sits in a white/silver bordered badge of the same shape, size, and position as the top-anime strip's score badge, distinct from both score colours

#### Scenario: A rewatch count is not mistaken for a score
- **WHEN** I look at the "My top anime" and "Most rewatched" strips together
- **THEN** the top-anime badges read as scores in the purple "mine" role and the rewatch badges read as counts in white/silver, so the two are never confused

#### Scenario: The badge stays legible over any poster
- **WHEN** a rewatched tile's poster is bright, pale, or busy behind the badge
- **THEN** the badge's count remains legible against it

#### Scenario: Entries never rewatched are excluded
- **WHEN** an anime on my list has a rewatch count of zero
- **THEN** it does not appear in the "Most rewatched" strip

#### Scenario: Rewatched but not completed
- **WHEN** an anime has a rewatch count above zero and a status other than completed
- **THEN** it still appears in the "Most rewatched" strip

#### Scenario: Breaking a tie
- **WHEN** two anime have the same rewatch count
- **THEN** they appear in alphabetical order by title, regardless of how I have scored them

#### Scenario: A score change does not reorder the strip
- **WHEN** I change my score on an anime in the "Most rewatched" strip without changing its rewatch count
- **THEN** its position in the strip is unchanged

#### Scenario: Uncapped list scrolls to the end
- **WHEN** I have more rewatched anime than fit across the width of the box
- **THEN** the strip scrolls horizontally and reaches the last of them, with none dropped from the list

#### Scenario: No reorder control
- **WHEN** I look at the "Most rewatched" box
- **THEN** it offers no control for editing the order

#### Scenario: Opening an anime from the strip
- **WHEN** I click a tile in the "Most rewatched" strip under a media-type scope
- **THEN** that anime's detail page opens

#### Scenario: The Series scope's badge matches the others
- **WHEN** I look at a tile in the Series scope
- **THEN** its rewatch time sits in a badge of the same shape, size, position, and white/silver colour as the count badges in the media-type scopes

### Requirement: Most rewatched media-type filter
The system SHALL provide a media-type filter on the "Most rewatched" box with the same options as the "My top anime" filter: All (default), TV, Movie, OVA, ONA, and Specials. Selecting a type SHALL rebuild the strip from only my rewatched entries of that media type, applying the same membership and ordering rules. The Specials option SHALL match both MAL media types the UI treats as specials.

The same control SHALL carry one further option, **Series**, alongside the media types. Selecting it SHALL replace the strip's contents with the franchise ranking defined by "Most rewatched by series". It SHALL be an option of this one control rather than a second control beside it, since it answers the same question the media types answer — which slice of my rewatching to show. It SHALL be drawn in the page's accent exactly as the media-type options are, carrying no score-role colour.

The filter SHALL be a view control only and SHALL NOT be persisted: on a fresh visit the section SHALL open on All, and when the profile page is restored by back/forward navigation the section SHALL show the option that was selected when the page was left, Series included. The two boxes' filters SHALL be independent, so changing the selection on one box SHALL NOT change the other box's selection or contents.

Switching the selection SHALL NOT visibly clear the strip's contents while the new one loads: the previously shown strip SHALL remain in place until the new selection's list is ready. This SHALL hold when switching into and out of Series as it does between media types.

#### Scenario: Filtering to a single media type
- **WHEN** I select Movie in the "Most rewatched" filter
- **THEN** the strip shows only my rewatched movies, still ordered most rewatched first

#### Scenario: Selecting the Series option
- **WHEN** I select Series in the "Most rewatched" filter
- **THEN** the strip shows my franchises ranked by total rewatch time in place of the anime tiles

#### Scenario: The Series option sits on the same control
- **WHEN** I look at the "Most rewatched" box's controls
- **THEN** Series is one of the options on the same row as All, TV, Movie, OVA, ONA, and Specials, not a separate control beside them

#### Scenario: The Series option carries no score colour
- **WHEN** Series is selected
- **THEN** it is drawn in the page's accent, as the media-type options are, and not in either score role's colour

#### Scenario: Returning from Series to a media type
- **WHEN** I select Series and then select TV again
- **THEN** the strip shows my rewatched TV anime, ordered by rewatch count

#### Scenario: Filters are independent
- **WHEN** I select Movie in the "Most rewatched" filter
- **THEN** the "My top anime" box keeps its own selected filter and contents

#### Scenario: Filter resets on a fresh visit
- **WHEN** I select an option, navigate away, and reach the profile page again by clicking its navbar link
- **THEN** the "Most rewatched" filter is back on All

#### Scenario: Both filters are restored on a back navigation
- **WHEN** I select different options in the two boxes, open an anime, and then navigate back
- **THEN** each box still shows the option it had

#### Scenario: The Series selection is restored on a back navigation
- **WHEN** I select Series, open a series from the strip, and then navigate back
- **THEN** the section is still on Series, showing the franchise ranking

#### Scenario: Switching filters does not clear the strip
- **WHEN** I select a different option in the "Most rewatched" filter
- **THEN** the strip keeps showing its previous contents, without an empty flash, until the new selection's list is ready

### Requirement: Most rewatched empty states
The system SHALL show a message in place of the strip whenever the current scope has nothing rewatched, worded for that scope: "No shows have been rewatched" under All, "No TV shows have been rewatched" under TV, "No movies have been rewatched" under Movie, "No OVAs have been rewatched" under OVA, "No ONAs have been rewatched" under ONA, "No specials have been rewatched" under Specials, and "No series have been rewatched" under Series.

#### Scenario: Nothing rewatched at all
- **WHEN** no anime on my list has a rewatch count above zero and the filter is on All
- **THEN** the box shows "No shows have been rewatched"

#### Scenario: Nothing rewatched in one media type
- **WHEN** I select Movie and none of my rewatched anime are movies
- **THEN** the box shows "No movies have been rewatched"

#### Scenario: No series with rewatch time
- **WHEN** I select Series and no stored series has any rewatch time
- **THEN** the box shows "No series have been rewatched"

#### Scenario: Empty scope while other scopes have entries
- **WHEN** I have rewatched TV shows but no OVAs and I select OVA
- **THEN** the box shows "No OVAs have been rewatched" rather than falling back to the unfiltered list
