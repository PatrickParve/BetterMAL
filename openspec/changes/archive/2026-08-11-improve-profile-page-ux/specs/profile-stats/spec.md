## MODIFIED Requirements

### Requirement: Latest updates activity feed
The system SHALL show a "Latest updates" feed built from the ActivityLog, most recent first, scrollable within its box, containing only: anime added to the list (any status), episode-count increases, completions, and score changes. The feed SHALL NOT show episode-count decreases, status changes other than completion, drops, or rewatch-count changes.

Consecutive episode-progress events for the same anime SHALL collapse into a single feed item rather than one item per event. A completion counts as an episode-progress event for this purpose, and SHALL supersede the progress events it collapses with: when watching an episode completed the anime, the feed SHALL report the completion and SHALL NOT also report the episode increase that produced it, nor the run of increases leading up to it.

A completion SHALL read as a completion whether it was reached by watching the last episode or by setting the status to completed directly.

A score change SHALL report the new score, or that the score was cleared when the score was removed.

#### Scenario: Showing recent activity
- **WHEN** the profile page loads
- **THEN** the latest-updates feed lists only additions, episode increases, completions, and score changes in most-recent-first order, scrollable within its box

#### Scenario: Collapsing consecutive increments
- **WHEN** an anime's episode count was increased several times in a row
- **THEN** those increases appear as a single feed item, not one per increment

#### Scenario: Completion supersedes its episode increment
- **WHEN** I watch the final episode of an anime, which marks it completed
- **THEN** the feed shows one item reading as a completion for that anime, and shows no episode-increase item for the same run of episodes

#### Scenario: Completion by status change
- **WHEN** I set an anime's status to completed without incrementing episodes
- **THEN** the feed shows a completion item for that anime

#### Scenario: Score change appears
- **WHEN** I change an anime's score
- **THEN** the feed shows an item for that anime reporting the new score

#### Scenario: Score cleared
- **WHEN** I remove an anime's score
- **THEN** the feed shows an item for that anime reporting that the score was cleared

#### Scenario: Excluding decreases and other status changes
- **WHEN** an anime's episode count decreased, or its status changed to something other than completed, or it was dropped
- **THEN** no such item appears in the latest-updates feed

### Requirement: Full edit-history overlay
The system SHALL provide, in the top-right corner of the "Latest updates" box, a control that opens a full history of every edit as an overlay on top of the page, which closes on Esc or a click outside it.

Each history row SHALL show the anime's poster picture alongside its title, in the same style as the rows of the "Latest updates" box, and SHALL show the anime's English title when one is available, falling back to the default title otherwise. A row's title SHALL be truncated to at most two lines, with the full title available on hover.

Unlike the "Latest updates" feed, the full history SHALL NOT drop any episode-watched entry. A consecutive run of episode-watched entries for the same anime SHALL instead collapse into a single row reporting the range of episodes watched (for example, "Episodes 4-8") rather than one row per episode; a run of a single entry SHALL keep its own "Episode N" wording.

#### Scenario: Opening the full history
- **WHEN** I click the history control in the Latest updates box
- **THEN** an overlay opens listing the full edit history

#### Scenario: Closing the history overlay
- **WHEN** the history overlay is open and I press Esc or click outside it
- **THEN** the overlay closes

#### Scenario: English titles in the history
- **WHEN** a history row's anime has a stored English title
- **THEN** that row shows the English title rather than the default title

#### Scenario: History rows show pictures
- **WHEN** the history overlay lists an anime
- **THEN** that row shows the anime's poster picture next to its title

#### Scenario: Long history title
- **WHEN** a history row's title is longer than two lines
- **THEN** it is cut off at two lines and hovering it reveals the full title

#### Scenario: Consecutive episode-watched entries collapse into a range
- **WHEN** an anime's episode count was increased several times in a row
- **THEN** the full history shows a single row reporting the range of episodes watched, not one row per increase

### Requirement: All-anime score distribution
The system SHALL show a count of anime per score/rating value plus the overall mean score, rendered as a bar per score value alongside its count and that score's share of all my rated anime.

Each bar's length SHALL be that score's count relative to the largest count across all score values, so the most-common score's bar fills the full width of its track and every other bar is drawn in proportion to it. A score with a count of zero SHALL render an empty track.

Each row SHALL show, after its count, that score's share of all my rated anime as a percentage — the count for that score divided by the total number of anime I have rated. A share that rounds to zero but comes from a non-zero count SHALL be shown as less than one percent rather than as zero.

When I have rated no anime at all, every track SHALL render empty and no share SHALL be shown as a nonsensical value.

#### Scenario: Rendering the distribution
- **WHEN** the profile page loads
- **THEN** it shows how many anime have each score value, that score's share as a percentage, and the overall mean score

#### Scenario: The most-common score fills its track
- **WHEN** one score has more anime than any other score
- **THEN** that score's bar fills the full width of its track

#### Scenario: Bars are proportional to the largest
- **WHEN** one score has half as many anime as the most-common score
- **THEN** its bar fills half the track width

#### Scenario: Share shown alongside the count
- **WHEN** a score accounts for 30% of all the anime I've rated
- **THEN** its row shows that score's count followed by 30%

#### Scenario: A very small share
- **WHEN** a score has at least one anime but its share rounds down to zero percent
- **THEN** its row shows less than one percent rather than zero percent

#### Scenario: Nothing rated yet
- **WHEN** I have rated no anime
- **THEN** every bar renders as an empty track and no share is shown as an invalid number

### Requirement: My top anime media-type filter
The system SHALL provide a media-type filter on the "My top anime" box with the options All (default), TV, Movie, OVA, ONA, and Specials. Selecting a type SHALL recompute the top list from only my scored entries of that media type, applying exactly the same rules as the unfiltered list (score tiers descending, all 10s uncapped, fill to a minimum of 10, my persisted tier order, truncated-tier membership by tier order). When a filtered subset has fewer than 10 scored entries the list SHALL show all of them without padding from other media types.

The selected filter SHALL be a view control only and SHALL NOT be persisted. On a fresh visit to the profile page — a navbar link, a typed URL, or a reload — the box SHALL open on All. When the profile page is restored by back/forward navigation, the box SHALL show the filter that was selected when the page was left.

Switching the filter SHALL NOT visibly clear the box's contents while the new selection loads: the previously shown list, and any control whose visibility depends on it, SHALL remain in place until the new selection's list is ready.

#### Scenario: Filtering to a single media type
- **WHEN** I select Movie in the "My top anime" filter
- **THEN** the list shows only my scored movies, ranked by the same score-tier and ordering rules as the unfiltered list

#### Scenario: Filtered subset smaller than ten
- **WHEN** I select a media type for which I have scored fewer than 10 anime
- **THEN** the list shows only those anime and does not pad with anime of other media types

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
The system SHALL provide a media-type filter on the "Most rewatched" box with the same options as the "My top anime" filter: All (default), TV, Movie, OVA, ONA, and Specials. Selecting a type SHALL rebuild the strip from only my rewatched entries of that media type, applying the same membership and ordering rules. The Specials option SHALL match both MAL media types the UI treats as specials.

The filter SHALL be a view control only and SHALL NOT be persisted: on a fresh visit the section SHALL open on All, and when the profile page is restored by back/forward navigation the section SHALL show the filter that was selected when the page was left. The two boxes' filters SHALL be independent, so changing the media type on one box SHALL NOT change the other box's selection or contents.

Switching the filter SHALL NOT visibly clear the strip's contents while the new selection loads: the previously shown strip SHALL remain in place until the new selection's list is ready.

#### Scenario: Filtering to a single media type
- **WHEN** I select Movie in the "Most rewatched" filter
- **THEN** the strip shows only my rewatched movies, still ordered most rewatched first

#### Scenario: Filters are independent
- **WHEN** I select Movie in the "Most rewatched" filter
- **THEN** the "My top anime" box keeps its own selected filter and contents

#### Scenario: Filter resets on a fresh visit
- **WHEN** I select a media type, navigate away, and reach the profile page again by clicking its navbar link
- **THEN** the "Most rewatched" filter is back on All

#### Scenario: Both filters are restored on a back navigation
- **WHEN** I select different media types in the two boxes, open an anime, and then navigate back
- **THEN** each box still shows the media type it had

#### Scenario: Switching filters does not clear the strip
- **WHEN** I select a different media type in the "Most rewatched" filter
- **THEN** the strip keeps showing its previous contents, without an empty flash, until the new type's list is ready

## ADDED Requirements

### Requirement: Poster strips keep a fixed tile size and scroll horizontally only
The "My top anime" and "Most rewatched" strips SHALL size their poster tiles as a fixed fraction of the strip's width — ten tiles across the visible width — regardless of how many entries the strip contains. Tiles SHALL NOT shrink to fit additional entries; entries beyond the tenth SHALL be reached by scrolling the strip horizontally.

Each strip SHALL scroll along the horizontal axis only. Vertical scrolling within a strip SHALL be impossible: a trackpad or wheel gesture SHALL NOT be able to displace the strip's contents up or down, including where a hovered tile's scaled-up size exceeds the strip's height.

#### Scenario: More than ten top anime
- **WHEN** I have more than ten anime scored 10
- **THEN** the tiles stay the same size as when there were exactly ten, and the rest are reached by scrolling the strip

#### Scenario: Tile size matches between the two strips
- **WHEN** the "My top anime" and "Most rewatched" strips are shown at the same window width
- **THEN** their tiles are the same size

#### Scenario: No vertical drift
- **WHEN** I make a vertical scroll gesture with the pointer over either strip
- **THEN** the strip's contents do not move up or down

### Requirement: Profile top-row box proportions
The "Anime stats" box SHALL be laid out narrow enough that each stat's value reads as belonging to its label without a connecting rule, and SHALL stay within that narrow width at any window width or display resolution rather than growing with the available space. The remaining width of the profile page's top row SHALL be taken by the other boxes in that row.

The "Latest updates" box's list SHALL fill the box down to its bottom edge rather than stopping at a fixed height, so the box has no dead space below its last visible row when the row of boxes is taller than the list's fixed height would be.

#### Scenario: Stats box on a wide display
- **WHEN** the profile page is viewed on a very wide display
- **THEN** the "Anime stats" box stays narrow and its labels and values stay close together

#### Scenario: Latest updates fills its box
- **WHEN** the profile page's top row is taller than the latest-updates list's contents would otherwise occupy
- **THEN** the list's scroll area extends to the bottom of its box, leaving no empty gap beneath it

### Requirement: Scrollbars sit beside scrollable content
Every scrollable list on the profile page and in its overlays — the "Latest updates" feed and the full edit-history list — SHALL lay out its scrollbar beside the rows rather than over them, so no row's right-hand edge, border, or content is covered by the scrollbar.

#### Scenario: Feed scrollbar does not cover rows
- **WHEN** the "Latest updates" feed has more rows than fit and shows a scrollbar
- **THEN** the scrollbar sits beside the rows and no row is drawn underneath it

#### Scenario: History scrollbar does not cover rows
- **WHEN** the full edit-history overlay shows a scrollbar
- **THEN** the scrollbar sits beside the rows and no row is drawn underneath it

### Requirement: Latest-updates titles are truncated to one line
Each row of the "Latest updates" feed SHALL show its anime's title on a single line, cut off with an ellipsis when the title is longer than the row allows, so that a long title never pushes the row's other content out of place. The full title SHALL be available on hover.

#### Scenario: A long title in the feed
- **WHEN** a feed row's title is too long for one line
- **THEN** it is cut off with an ellipsis and the row's change description and timestamp keep their place

#### Scenario: Recovering the full title
- **WHEN** I hover a feed row's truncated title
- **THEN** the full title is shown
