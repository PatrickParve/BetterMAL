## RENAMED Requirements

- FROM: `### Requirement: Rewatch time is stated in days and decimal hours`
- TO: `### Requirement: Series watch totals are stated in days and decimal hours`

## ADDED Requirements

### Requirement: Profile scope controls offer only scopes with entries

The scope controls on the profile page's "My top anime" and "Most rewatched" boxes SHALL offer a media type only when that media type holds at least one entry the box would list. A media type with nothing behind it SHALL NOT be drawn as an option at all.

What counts as "holds at least one entry" SHALL be that box's own membership rule, so a scope is never offered as empty and never withheld as full:

- for **My top anime**, at least one entry of that media type that the shared ranking covers — the same population the box's list is drawn from, which excludes unscored, plan-to-watch and unaired entries;
- for **Most rewatched**, at least one entry of that media type with a rewatch count above zero.

**All** SHALL always be offered on both boxes. On "Most rewatched", **Series** SHALL always be offered too: it is a franchise scope rather than a media type, and its emptiness carries information the media types' does not — it can be empty while media types hold entries, when the franchises behind them have not been built from my list yet, and it can hold entries while every media type is empty, since a first rewatch in progress has a rewatch count of zero yet has taken time.

A box whose control is left with no media-type option — **All** alone, which is the case only when the box has nothing to list at all — SHALL NOT draw the control, rather than drawing a row of one button with nothing to choose between. "Most rewatched" always offers **All** and **Series** and so always draws its control.

The offered options SHALL be decided from my whole list rather than from the scope currently selected, so switching scope SHALL NOT change which options are offered.

A selection restored by back/forward navigation that names a media type no longer offered SHALL be read as **All**, and the box SHALL show the All scope's contents. The stored selection itself SHALL NOT be rewritten as a side effect: nothing on the page SHALL write a scope selection except my selecting one.

Because a media type is offered only when its scope holds entries, no per-media-type empty message SHALL be shown by either box. Only the **All** scope's message, and on "Most rewatched" the **Series** scope's message, SHALL remain.

#### Scenario: A media type with nothing rewatched is not offered
- **WHEN** nothing on my list with a rewatch count above zero is an ONA or a special
- **THEN** the "Most rewatched" control offers All, Series, and only those of TV, Movie and OVA that do hold rewatched entries, with no ONA or Specials button drawn

#### Scenario: A media type with nothing scored is not offered
- **WHEN** none of the entries my ranking covers is a movie
- **THEN** the "My top anime" control draws no Movie button

#### Scenario: All is always offered
- **WHEN** either box's control is drawn
- **THEN** All is among its options

#### Scenario: Series is offered even when it is empty
- **WHEN** I have rewatched TV shows but no series have been built from my list yet
- **THEN** the "Most rewatched" control still offers Series, and selecting it shows the Series scope's empty message

#### Scenario: Series is offered when only an in-progress rewatch has time
- **WHEN** my only rewatching is a first rewatch in progress, whose rewatch count is still zero
- **THEN** no media type is offered on "Most rewatched", the control still offers All and Series, and Series lists that anime's franchise

#### Scenario: A control with nothing to choose between is not drawn
- **WHEN** my ranking covers no anime at all
- **THEN** the "My top anime" box draws no media-type control, and shows its empty message alone

#### Scenario: Switching scope does not change the options
- **WHEN** I select a media type on either box
- **THEN** the same set of options stays on the row, so I can switch straight to any other offered scope

#### Scenario: A restored selection that is no longer offered falls back to All
- **WHEN** I return by back-navigation to a profile page whose stored selection names a media type that no longer holds entries
- **THEN** the box shows the All scope, with All marked as selected, rather than an empty strip

#### Scenario: A restored selection is not rewritten
- **WHEN** a restored selection falls back to All because its media type is no longer offered
- **THEN** nothing is written to my stored selection as a result — only my choosing a scope writes one

#### Scenario: Both boxes decide independently
- **WHEN** I have rewatched only TV shows but have scored anime of several media types
- **THEN** "Most rewatched" offers All, Series and TV while "My top anime" offers All and every media type it has scored entries for

### Requirement: Most time spent by series

The system SHALL show a "Most time spent" section on the profile page, directly below "Most rewatched", ranking my series by the total time I have spent watching them.

A series' total SHALL be the sum, over **every** member of that series — main line and extras alike — of that member's watch time, where a member's watch time is its first viewing plus every rewatch, valued at that member's episode duration. A member not in my list SHALL contribute nothing.

A member's first viewing SHALL be its episodes watched, so episodes of a currently-airing anime count as they are watched rather than only once it finishes; for a member marked Rewatching — whose episodes watched describe the run in progress — the first viewing SHALL be one complete run, measured by the published total episode count or, when none is published, by its own episodes watched. A member's rewatches SHALL be counted exactly as "Most rewatched by series" counts them: its recorded number of rewatches times its episode count, plus, when it is marked Rewatching, the episodes watched so far in that in-progress run. A member's episode duration SHALL be its published average episode duration, falling back to the same assumed duration the profile's other time figures use when none is published.

These are the same rules the profile's **Days** stat applies per entry, so a franchise total and the profile's own time figures can never disagree about what a viewing is worth.

A series SHALL be listed when its total is above zero — which is exactly "at least one member in my list with at least one episode watched" — and SHALL be omitted otherwise. A franchise whose only listed members are plan-to-watch, or have no episodes watched, SHALL NOT be listed rather than listed at zero. No further eligibility rule SHALL apply: neither the coverage rule that governs **Top series** nor any minimum member count, because a total is a sum and a franchise of which I have watched one entry has a total that is exactly right.

The strip SHALL be ordered by total watch time descending, ties broken alphabetically by title, case-insensitively. It SHALL NOT be capped: every listed series SHALL be reachable by scrolling the strip to its end.

The strip SHALL present the same form as "Most rewatched"'s Series scope — the same tile size, the same badge position and form, the same white/silver badge colour, the same hover treatment, and the same horizontal drag-scroll — with each tile showing that series' picture, carrying its total watch time as its badge, and opening that series' page. Its total SHALL be stated per "Series watch totals are stated in days and decimal hours".

The section SHALL carry no scope or filter control: it answers one question, at franchise level, and its picture, badge and link are all series-level. Its title SHALL be plain and unbanded, per "Profile section titles are banded by family".

When no series has any watch time, the section SHALL show a message in place of the strip rather than an empty strip.

#### Scenario: Ordering by time spent
- **WHEN** the profile page loads and I have watched several franchises for different lengths of time
- **THEN** the "Most time spent" strip lists them from most time spent to least

#### Scenario: Extras and rewatches count
- **WHEN** a franchise's total is computed
- **THEN** it includes its extras as well as its main line, and every recorded rewatch of every member as well as each member's first viewing

#### Scenario: A currently-airing entry counts what I have watched
- **WHEN** I have watched five episodes of a still-airing member of a franchise
- **THEN** those five episodes count toward that franchise's total, rather than nothing until it finishes

#### Scenario: A member not in my list contributes nothing
- **WHEN** a franchise has a member I have never added to my list
- **THEN** that member adds nothing to the franchise's total

#### Scenario: A franchise with nothing watched is omitted
- **WHEN** every member of a franchise in my list is plan-to-watch, with no episodes watched
- **THEN** that franchise is not listed at all, rather than listed with a total of zero

#### Scenario: One watched entry is enough
- **WHEN** I have watched one entry of a many-entry franchise and none of the others
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

#### Scenario: Nothing watched at all
- **WHEN** no series has any watch time
- **THEN** the box shows a message in place of the strip

#### Scenario: The section agrees with the Days stat
- **WHEN** I compare one franchise's total against what its members contribute to the profile's Days figure
- **THEN** the two are computed from the same per-entry episodes and the same per-entry durations

## MODIFIED Requirements

### Requirement: My top anime media-type filter
The system SHALL provide a media-type filter on the "My top anime" box offering All (default) plus those of TV, Movie, OVA, ONA and Specials that hold at least one entry, per "Profile scope controls offer only scopes with entries". Selecting a type SHALL recompute the top list from only my scored entries of that media type, applying exactly the same rules as the unfiltered list (score tiers descending, all 10s uncapped, fill to a minimum of 10, my persisted tier order, truncated-tier membership by tier order). When a filtered subset has fewer than 10 scored entries the list SHALL show all of them without padding from other media types.

The selected filter SHALL be a view control only and SHALL NOT be persisted. On a fresh visit to the profile page — a navbar link, a typed URL, or a reload — the box SHALL open on All. When the profile page is restored by back/forward navigation, the box SHALL show the filter that was selected when the page was left, unless that filter names a media type no longer offered, in which case it SHALL show All.

Switching the filter SHALL NOT visibly clear the box's contents while the new selection loads: the previously shown list, and any control whose visibility depends on it, SHALL remain in place until the new selection's list is ready.

#### Scenario: Filtering to a single media type
- **WHEN** I select Movie in the "My top anime" filter
- **THEN** the list shows only my scored movies, ranked by the same score-tier and ordering rules as the unfiltered list

#### Scenario: Filtered subset smaller than ten
- **WHEN** I select a media type for which I have scored fewer than 10 anime
- **THEN** the list shows only those anime and does not pad with anime of other media types

#### Scenario: Only media types with scored entries are offered
- **WHEN** I have scored no OVAs
- **THEN** the filter offers no OVA option, so the list can never be filtered to an empty scope

#### Scenario: Filter resets on a fresh visit
- **WHEN** I select a media type, navigate away, and reach the profile page again by clicking its navbar link
- **THEN** the filter is back on All

#### Scenario: Filter is restored on a back navigation
- **WHEN** I select a media type, open an anime from the strip, and then navigate back
- **THEN** the "My top anime" box still shows that media type and its filtered contents

#### Scenario: Switching filters does not clear the box
- **WHEN** I select a different media type in the "My top anime" filter
- **THEN** the box keeps showing its previous list, without an empty flash, until the new type's list is ready

### Requirement: Most rewatched media-type filter
The system SHALL provide a media-type filter on the "Most rewatched" box offering All (default) plus those of TV, Movie, OVA, ONA and Specials that hold at least one rewatched entry, per "Profile scope controls offer only scopes with entries". Selecting a type SHALL rebuild the strip from only my rewatched entries of that media type, applying the same membership and ordering rules. The Specials option SHALL match both MAL media types the UI treats as specials.

The same control SHALL carry one further option, **Series**, alongside the media types. Selecting it SHALL replace the strip's contents with the franchise ranking defined by "Most rewatched by series". It SHALL be an option of this one control rather than a second control beside it, since it answers the same question the media types answer — which slice of my rewatching to show. It SHALL be drawn in the page's accent exactly as the media-type options are, carrying no score-role colour. **Series** SHALL be offered at all times, whether or not its scope holds anything, per "Profile scope controls offer only scopes with entries".

The filter SHALL be a view control only and SHALL NOT be persisted: on a fresh visit the section SHALL open on All, and when the profile page is restored by back/forward navigation the section SHALL show the option that was selected when the page was left, Series included — unless that option names a media type no longer offered, in which case it SHALL show All. The two boxes' filters SHALL be independent, so changing the selection on one box SHALL NOT change the other box's selection or contents.

Switching the selection SHALL NOT visibly clear the strip's contents while the new one loads: the previously shown strip SHALL remain in place until the new selection's list is ready. This SHALL hold when switching into and out of Series as it does between media types.

#### Scenario: Filtering to a single media type
- **WHEN** I select Movie in the "Most rewatched" filter
- **THEN** the strip shows only my rewatched movies, still ordered most rewatched first

#### Scenario: Selecting the Series option
- **WHEN** I select Series in the "Most rewatched" filter
- **THEN** the strip shows my franchises ranked by total rewatch time in place of the anime tiles

#### Scenario: The Series option sits on the same control
- **WHEN** I look at the "Most rewatched" box's controls
- **THEN** Series is one of the options on the same row as All and the media types that hold rewatched entries, not a separate control beside them

#### Scenario: The Series option carries no score colour
- **WHEN** Series is selected
- **THEN** it is drawn in the page's accent, as the media-type options are, and not in either score role's colour

#### Scenario: Only media types with rewatched entries are offered
- **WHEN** none of my rewatched entries is an ONA or a special
- **THEN** the control offers neither ONA nor Specials

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
The system SHALL show a message in place of the strip whenever the current scope has nothing rewatched, worded for that scope: "No shows have been rewatched" under All, and "No series have been rewatched" under Series.

No per-media-type message SHALL exist, since a media type is offered only when it holds rewatched entries and a restored selection naming one that no longer does falls back to All — so a media-type scope can never be shown empty. See "Profile scope controls offer only scopes with entries".

#### Scenario: Nothing rewatched at all
- **WHEN** no anime on my list has a rewatch count above zero and the filter is on All
- **THEN** the box shows "No shows have been rewatched"

#### Scenario: No series with rewatch time
- **WHEN** I select Series and no stored series has any rewatch time
- **THEN** the box shows "No series have been rewatched"

#### Scenario: An empty media type is not reachable
- **WHEN** I have rewatched TV shows but no OVAs
- **THEN** no OVA option is offered, so no "No OVAs have been rewatched" message is ever shown

### Requirement: Series watch totals are stated in days and decimal hours
A series-level watch total on the profile page — the total rewatch time shown by "Most rewatched"'s Series scope, and the total watch time shown by "Most time spent" — SHALL be shown as whole days and decimal hours. The days part SHALL be omitted entirely when the total is under a day, so a total of a few hours reads as hours alone. Both sections SHALL state their totals by this one rule, so two totals of the same length read identically wherever they appear.

Hours SHALL carry one decimal place. A total under one hour SHALL instead carry two decimal places, so a short total is stated rather than rounded away. Trailing zeros SHALL be trimmed, so a value of exactly two hours reads `2h` and a value of exactly four tenths of an hour reads `0.4h`.

The total SHALL be rounded to a tenth of an hour before days are split off it, so a value just short of a whole number of days can never render an hours part of 24.

The days part SHALL NOT be capped: a total of hundreds of days SHALL state them all, since a whole franchise's watch time is expected to be far larger than any one rewatch total.

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

#### Scenario: A time-spent total reads the same way
- **WHEN** a series' total watch time in "Most time spent" is three days, seven hours, and forty minutes
- **THEN** it reads "3d 7.7h", exactly as the same total does in "Most rewatched"'s Series scope

#### Scenario: A very large total states every day
- **WHEN** a series' total watch time is one hundred and forty-two days and six and a half hours
- **THEN** it reads "142d 6.5h", with the days part uncapped
