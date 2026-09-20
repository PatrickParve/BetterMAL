# profile-stats Specification

## Purpose
The profile-stats capability governs the profile page: stats computed entirely from local data, a filtered activity feed with its full edit-history overlay, My top anime and Most rewatched by series, all-list episode progress, favourite seasons and years, the Top series section and its several ranking bases, the all-anime score distribution and opinion-divergence lists, and the family-banded section titles that tie related sections together visually. My top anime's order reads anime-ranking rather than computing its own.
## Requirements
### Requirement: Anime stats computed from local data
The system SHALL show anime stats computed entirely from the local database: Days, Watching, Completed, On-Hold, Dropped, Plan to Watch, Total Entries, Rewatched, Rewatched episodes, Episodes, and Movies. A Mean Score SHALL NOT be shown among these stats; my mean score is shown under the rating distribution instead (see "All-anime score distribution").

**Episodes** SHALL be the total number of episodes I have watched on a first viewing, NOT counting rewatches. An entry SHALL contribute its episodes watched. An entry currently marked Rewatching SHALL contribute one complete run of the anime — I finished its first viewing before starting the rewatch, and its episodes watched now describe the rewatch in progress — measured by the anime's published total episode count, or by the entry's own episodes watched when no total is published.

**Episodes** SHALL exclude entries whose media type is `movie` or `music`, which are not episodic and would otherwise each add one to a count of episodes. Every other media type — TV, OVA, ONA, special, and unrecognised values — SHALL count. Entries of every status SHALL count, dropped entries included: episodes I watched before abandoning a show are episodes I watched.

**Rewatched episodes** SHALL be the total number of episodes I have rewatched, across **every** media type — TV, OVA, ONA, special, movie, music, and unrecognised values alike, so a film counts as one episode for each time I rewatched it. An entry SHALL contribute one further complete run of the anime for every recorded rewatch, plus, when the entry is currently marked Rewatching, the episodes watched so far in the rewatch in progress. The complete run SHALL be measured by the anime's published total episode count; when no total is published, it SHALL be measured by the entry's own episodes watched, since that is the only length the app knows. Entries of every status SHALL count. Rewatched episodes SHALL be presented directly below Rewatched.

**Movies** SHALL count entries whose media type is `movie` and which have at least one episode watched, whatever their status. It SHALL be presented directly below Episodes. A film rewatched several times SHALL count once — the stat counts films watched, not viewings.

**Days** SHALL be the total runtime of everything I have watched, first viewings and rewatches alike, expressed in days to one decimal. It SHALL be computed per entry as that entry's first-viewing episodes — counted as for Episodes but without the movie/music exclusion — plus its rewatched episodes as counted for Rewatched episodes, multiplied by that anime's runtime per episode, and summed across **every** entry in my list whatever its media type or status: TV, movies, music, dropped, and in-progress alike. Days therefore covers a wider population than Episodes by design, since a film consumes time even though it contributes no episodes.

An anime's runtime per episode SHALL be its cached average episode duration where one is stored, and SHALL otherwise fall back to the same standing per-episode assumption the recap and series pages use, so no two surfaces report different runtimes for the same anime.

#### Scenario: Rendering stats
- **WHEN** the profile page loads
- **THEN** all listed stat values are computed from the local DB without a live API call, and no Mean Score is among them

#### Scenario: A rewatch no longer adds to Episodes
- **WHEN** my list holds a completed twelve-episode series with a rewatch count of one
- **THEN** it contributes twelve to Episodes and twelve to Rewatched episodes

#### Scenario: A rewatch in progress
- **WHEN** my list holds a twelve-episode series marked Rewatching, with a rewatch count of two and episodes watched currently reading one
- **THEN** it contributes twelve to Episodes and twenty-five to Rewatched episodes

#### Scenario: Rewatch of an anime with no published total
- **WHEN** my list holds an entry with eight episodes watched, a rewatch count of one, and no published total episode count
- **THEN** it contributes eight to Episodes and eight to Rewatched episodes

#### Scenario: Never rewatched
- **WHEN** my list holds an entry with a rewatch count of zero that is not marked Rewatching
- **THEN** it contributes nothing to Rewatched episodes

#### Scenario: Movies and music are not episodes
- **WHEN** my list holds a watched film and a watched music video, neither rewatched
- **THEN** neither contributes to Episodes

#### Scenario: A rewatched film counts toward rewatched episodes
- **WHEN** my list holds a film with a rewatch count of two
- **THEN** it contributes two to Rewatched episodes and nothing to Episodes

#### Scenario: Rewatched episodes sits under Rewatched
- **WHEN** the profile page renders its anime stats
- **THEN** Rewatched episodes is shown directly below Rewatched, and Movies is still shown directly below Episodes

#### Scenario: Other media types still count as episodes
- **WHEN** my list holds a watched OVA and a watched special
- **THEN** both contribute their episodes watched to Episodes

#### Scenario: Dropped episodes count
- **WHEN** I dropped a series after watching four of its episodes
- **THEN** those four episodes are counted in Episodes

#### Scenario: Movies stat counts watched films
- **WHEN** my list holds three films with at least one episode watched and two films I have watched none of
- **THEN** Movies reads three

#### Scenario: A rewatched film counts once
- **WHEN** my list holds a film with a rewatch count of two
- **THEN** Movies counts it once

#### Scenario: Days uses each anime's own runtime
- **WHEN** my list holds a twelve-episode series with a cached average episode duration of twenty-three minutes and a film with a cached duration of one hundred and twenty minutes, both fully watched and never rewatched
- **THEN** Days reflects two hundred and seventy-six minutes plus one hundred and twenty minutes, rather than thirteen episodes at the standing assumption

#### Scenario: Days falls back when no duration is cached
- **WHEN** an anime in my list has no cached average episode duration
- **THEN** its contribution to Days uses the app's standing per-episode assumption, matching what the recap and series pages report for the same anime

#### Scenario: Days counts everything watched
- **WHEN** my list holds watched TV series, films, music videos, a dropped show, and a show I am partway through
- **THEN** every one of them contributes its watched runtime to Days

#### Scenario: Rewatches add to time spent
- **WHEN** I rewatch a twelve-episode series once
- **THEN** Days grows by the runtime of twelve further episodes

#### Scenario: A rewatch in progress counts its first viewing in Days
- **WHEN** my list holds a twelve-episode series marked Rewatching, with a rewatch count of two and episodes watched reading one
- **THEN** its contribution to Days is the runtime of thirty-seven episodes — the first viewing, two completed rewatches, and one episode of the current rewatch

### Requirement: Rewatching has its own place in the status breakdown

The profile page's per-status counts SHALL include **Rewatching** as its own figure, rather than folding rewatches into Watching or Completed. The per-status counts SHALL continue to sum to Total Entries.

The episode and time formulas SHALL be unaffected. Episodes is already defined as an entry's episodes watched plus one complete run per recorded rewatch, and Days from that same rewatch-inclusive count. Under this change a rewatch in progress is exactly that — the rewatch count holds the runs already finished, and episodes watched holds the current run's progress, because entering Rewatching resets the count to 0 and the rewatch count is only increased once a run finishes. No formula changes; the arithmetic is identical to the rewatch-in-progress case the capability already describes.

#### Scenario: Rewatches are counted separately

- **WHEN** my list holds entries in every status, including two Rewatching ones
- **THEN** the stats show a Rewatching count of two, and neither the Watching nor the Completed count includes them

#### Scenario: The breakdown still sums

- **WHEN** the per-status counts are added together
- **THEN** they equal Total Entries

#### Scenario: A rewatch in progress counts the same as before

- **WHEN** my list holds a Rewatching twelve-episode series with a rewatch count of two whose episodes watched reads one
- **THEN** it contributes twenty-five episodes, exactly as the same entry did under the previous representation

### Requirement: Profile list-row posters fill the row
The system SHALL render the poster in every profile list row — "Latest updates" rows, full edit-history rows, and both opinion-divergence lists' rows — flush with the row's top, bottom, and leading edges, filling the row's full height with no padding between the poster and those edges. The poster's leading corners SHALL follow the row's own corner radius, and a row SHALL take its height from its list rather than from the poster's natural aspect ratio — the poster SHALL never inflate a row to its own intrinsic size. A row whose anime has no picture SHALL render its placeholder at the same full-height size.

A poster SHALL keep the proportions of the poster art at whatever height its row has: where a list sets its own row height, the poster's width SHALL follow that height rather than staying at a width fixed for some other row height.

Where the anime's displayed picture is **at least as wide as it is tall**, the row SHALL draw it whole at its own proportions on the terms the `artwork-presentation` capability sets out: the row keeps its height, the picture takes the width its proportions give it at that height up to that capability's bound, and the row's title and everything after it begin further along by that extra width. A portrait poster keeps exactly the box described above, and no row's height changes in either case — so a list showing eight whole rows still shows eight whole rows, and a feed sized to five rows still holds five.

This requirement governs the profile's **list rows** only. The poster **strips** in "My top anime", "Most rewatched" and "Top series" are not list rows and SHALL keep the fixed tile size their own requirements set, cropping to it as they do today.

#### Scenario: Poster fills a latest-updates row
- **WHEN** the Latest updates box renders a row
- **THEN** that row's poster touches the row's top, bottom, and leading edges with no gap

#### Scenario: Poster fills a history row
- **WHEN** the full edit-history overlay renders a row
- **THEN** that row's poster touches the row's top, bottom, and leading edges with no gap

#### Scenario: Poster fills a divergence row
- **WHEN** either opinion-divergence list renders a row
- **THEN** that row's poster touches the row's top, bottom, and leading edges with no gap

#### Scenario: Poster follows its row's height
- **WHEN** the Latest updates rows are taller than the divergence and history rows
- **THEN** their posters are correspondingly wider, keeping the poster proportions rather than rendering a narrow crop

#### Scenario: A poster never sets the row height
- **WHEN** any of these rows renders its poster
- **THEN** the row occupies the height its list gives it, and no box grows to accommodate the image's intrinsic size

#### Scenario: A landscape picture is drawn landscape
- **WHEN** a Latest updates, edit-history, or divergence row's anime has a displayed picture wider than it is tall
- **THEN** the whole picture is shown at the row's height and at its own width there, with no part cropped away

#### Scenario: Landscape artwork does not change how many rows fit
- **WHEN** a list holding landscape artwork renders
- **THEN** it shows the same number of whole rows it shows without it, each at the same height

#### Scenario: The poster strips are unaffected
- **WHEN** "My top anime", "Most rewatched" or "Top series" holds an anime with landscape artwork
- **THEN** every tile in that strip is still the same fixed size, cropped to it as before

### Requirement: Latest updates activity feed
The system SHALL show a "Latest updates" feed built from the ActivityLog, most recent first, scrollable within its box, containing only: anime added to the list (any status), episode-count increases, completions, score changes, rewatch-count changes, and anime removed from the list. The feed SHALL NOT show episode-count decreases, status changes other than completion, drops, or start/finish date changes.

Each row SHALL describe its change as a single phrase and SHALL NOT pair a change-type label with a detail that restates it. The phrasing SHALL be:

- an addition: the anime was added to the list, naming the status it was added with
- an episode increase: the episode number reached, or the range when several collapse
- a completion: that the anime was completed, followed by the score when one was set as part of finishing it
- a score change: the new score, or that the score was cleared
- a rewatch-count change: the new rewatch count
- a removal: that the anime was removed from the list

Consecutive episode-progress events for the same anime SHALL collapse into a single feed item rather than one item per event. A completion counts as an episode-progress event for this purpose, and SHALL supersede the progress events it collapses with: when watching an episode completed the anime, the feed SHALL report the completion and SHALL NOT also report the episode increase that produced it, nor the run of increases leading up to it. A removal is not an episode-progress event and SHALL NOT collapse with one.

A completion SHALL read as a completion whether it was reached by watching the last episode or by setting the status to completed directly.

A score set as part of finishing an anime SHALL be reported on the completion's own row rather than as a second row. This SHALL hold both when the score and the completion were saved together and when the score was saved separately moments later, as it is when the completion score prompt is answered.

The feed SHALL show at most one row per anime per field group, reporting that anime's newest value for that group; older rows for the same anime and group SHALL be dropped from the feed. The field groups are: progress (episode increases and completions), score, rewatch count, and list membership (additions and removals). Rows for different anime, or for different field groups of the same anime, SHALL NOT collapse into each other. The full edit history SHALL remain unaffected by this collapsing and SHALL still record every step.

**The feed SHALL cover the last 30 days.** It SHALL carry every row that survives the filtering, merging and collapsing above and whose change falls within the 30 days ending now, with no cap on how many that is — a busy month SHALL be shown in full rather than cut off at a fixed count.

**A quiet month SHALL NOT empty the box.** When the last 30 days yield fewer than 20 rows, the feed SHALL reach further back in time, by the same rules, until it holds 20 rows or the activity log is exhausted. Rows reached this way SHALL be ordered among the rest exactly as they would be otherwise — most recent first, with no marker or break separating the two — so the feed reads as one continuous list rather than a recent section and an older one.

Every rule above SHALL apply identically inside and outside the 30-day window. The window and the 20-row floor SHALL govern only **how far back** the feed reaches, and SHALL change nothing about which change types it carries, how a completion and its score merge, or how rows collapse per anime and field group.

The box SHALL continue to show five whole rows at rest and to scroll within itself, however many rows the feed holds.

A removal SHALL keep reading correctly after the entry it describes is gone — it names the anime from cached metadata, which outlives the entry.

#### Scenario: Showing recent activity
- **WHEN** the profile page loads
- **THEN** the latest-updates feed lists only additions, episode increases, completions, score changes, rewatch-count changes, and removals in most-recent-first order, scrollable within its box

#### Scenario: A row reads as one phrase
- **WHEN** any row of the latest-updates feed renders
- **THEN** its change is described once, with no label-and-detail pair that repeats the same wording

#### Scenario: Addition names the status it was added with
- **WHEN** I add an anime to my list as Watching
- **THEN** the feed shows one row for it reading that it was added to the list as Watching

#### Scenario: Collapsing consecutive increments
- **WHEN** an anime's episode count was increased several times in a row
- **THEN** those increases appear as a single feed item, not one per increment

#### Scenario: Completion supersedes its episode increment
- **WHEN** I watch the final episode of an anime, which marks it completed
- **THEN** the feed shows one item reading as a completion for that anime, and shows no episode-increase item for the same run of episodes

#### Scenario: Completion by status change
- **WHEN** I set an anime's status to completed without incrementing episodes
- **THEN** the feed shows a completion item for that anime

#### Scenario: Completion and score in one row
- **WHEN** I finish an anime and give it a score in the same save
- **THEN** the feed shows a single row for that anime reporting the completion together with the score, and no separate score row for it

#### Scenario: Score from the completion prompt joins the completion row
- **WHEN** I finish an anime by watching its last episode and then save a score in the completion score prompt that follows
- **THEN** the feed still shows a single row reporting the completion together with that score

#### Scenario: Score change appears
- **WHEN** I change an anime's score outside of finishing it
- **THEN** the feed shows an item for that anime reporting the new score

#### Scenario: Score cleared
- **WHEN** I remove an anime's score
- **THEN** the feed shows an item for that anime reporting that the score was cleared

#### Scenario: Rewatch count change appears
- **WHEN** I change an anime's rewatch count
- **THEN** the feed shows an item for that anime reporting the new rewatch count

#### Scenario: Removal appears
- **WHEN** I remove an anime from my list
- **THEN** the feed shows an item for that anime reporting that it was removed from the list, with its title and picture intact

#### Scenario: Re-editing a field right after editing it
- **WHEN** I change an anime's score and then change it again to a different value
- **THEN** the feed shows one row for that anime's score, reporting the value it now has

#### Scenario: Editing a field back to its previous value
- **WHEN** I change an anime's episode count and then change it back
- **THEN** the feed shows one progress row for that anime, reporting the episode count it now has, rather than one row per edit

#### Scenario: Different fields of the same anime stay separate
- **WHEN** I change an anime's score and its rewatch count
- **THEN** the feed shows a row for each of those changes

#### Scenario: Excluding decreases and other status changes
- **WHEN** an anime's episode count decreased, or its status changed to something other than completed, or it was dropped, or its start or finish date changed
- **THEN** no such item appears in the latest-updates feed

#### Scenario: A busy month is shown in full
- **WHEN** my activity over the last 30 days collapses to 60 feed rows
- **THEN** all 60 are in the feed, reachable by scrolling the box, rather than the newest 20 alone

#### Scenario: A quiet month reaches further back
- **WHEN** my activity over the last 30 days collapses to 3 feed rows and older activity exists
- **THEN** the feed holds 20 rows — those 3 followed by the next most recent 17 from before the window — in one unbroken most-recent-first list

#### Scenario: A short history shows what there is
- **WHEN** my entire activity log collapses to 8 feed rows, all of them older than 30 days
- **THEN** the feed holds those 8 rows rather than being empty

#### Scenario: The collapsing rules are unchanged by the wider window
- **WHEN** an anime's score was changed four times within the last 30 days
- **THEN** the feed still shows one score row for that anime, carrying its newest score

#### Scenario: The full history is unaffected
- **WHEN** the feed covers the last 30 days and I open the full edit history
- **THEN** the history still records every edit ever made, without a 30-day bound of its own

### Requirement: Latest-updates titles are truncated to one line
Each row of the "Latest updates" feed SHALL show its anime's title on a single line, cut off with an ellipsis when the title is longer than the row allows, so that a long title never pushes the row's other content out of place. The full title SHALL be available on hover.

#### Scenario: A long title in the feed
- **WHEN** a feed row's title is too long for one line
- **THEN** it is cut off with an ellipsis and the row's change description and timestamp keep their place

#### Scenario: Recovering the full title
- **WHEN** I hover a feed row's truncated title
- **THEN** the full title is shown

### Requirement: Full edit-history overlay
The system SHALL provide, in the top-right corner of the "Latest updates" box, a control that opens a full history of every edit as an overlay on top of the page, which closes on Esc or a click outside it.

The overlay SHALL also be dismissable from a close control in its own top-right corner, aligned with its title: an icon control (a ✕) rather than a labelled button beneath the list. It SHALL carry an accessible label naming what it does, SHALL be reachable and operable by keyboard, and SHALL show a hover state in the same style as the app's other icon controls. It SHALL remain visible and in place regardless of how far the history list is scrolled. No other close control SHALL be offered.

Each history row SHALL show the anime's poster picture alongside its title, in the same style as the rows of the "Latest updates" box, and SHALL show the anime's English title when one is available, falling back to the default title otherwise. A row's title SHALL be truncated to at most two lines, with the full title available on hover.

The history list SHALL show five whole rows when the overlay opens, with no part of a sixth row visible beneath them and no row clipped part-way. Its height SHALL be derived from the height of a row rather than from the viewport, so the same five whole rows are shown at any window height; the rest of the history SHALL be reached by scrolling the list. A history holding fewer than five rows SHALL show what it has without reserving the height of five.

Each history row SHALL describe its change as a single phrase, using the same phrasing as the "Latest updates" feed, and SHALL NOT pair a change-type label with a detail that restates it. Change types the feed omits — status changes, start-date changes, and finish-date changes — SHALL appear in the history, each named once by the same rule: a status change reporting the status it moved from and to, a date change reporting the new date or that the date was cleared.

Unlike the "Latest updates" feed, the full history SHALL NOT drop any episode-watched entry, and SHALL NOT collapse repeated edits of the same field into the newest one. A consecutive run of episode-watched entries for the same anime SHALL instead collapse into a single row reporting the range of episodes watched (for example, "Episodes 4-8") rather than one row per episode; a run of a single entry SHALL keep its own "Episode N" wording.

A score set as part of finishing an anime SHALL be reported on the completion's own row rather than as a second row, on the same terms as in the "Latest updates" feed — whether saved together with the completion or separately moments later. The episode run that led to the completion SHALL keep its own range row rather than being folded into the completion row.

The overlay SHALL let me narrow the history by anime title and by date:

- A search field SHALL filter rows to those whose anime title matches what I typed, matching case-insensitively against both the default and the English title, on any part of the title rather than only its start.
- A start date and an end date SHALL each filter rows to those on or after, and on or before, that date, interpreted as whole days in my local time. Either may be left empty, leaving that end of the range open.
- The search and the date bounds SHALL apply together, and SHALL be clearable back to the unfiltered history without closing the overlay.
- When filters match nothing, the overlay SHALL say so rather than render an empty list.
- Filtering SHALL apply to the collapsed rows the history already shows, and SHALL NOT require a reload of the page or a refetch per keystroke.

#### Scenario: Opening the full history
- **WHEN** I click the history control in the Latest updates box
- **THEN** an overlay opens listing the full edit history

#### Scenario: Closing the history overlay
- **WHEN** the history overlay is open and I press Esc or click outside it
- **THEN** the overlay closes

#### Scenario: Closing from the corner control
- **WHEN** I click the ✕ in the overlay's top-right corner
- **THEN** the overlay closes, and no separate Close button is shown beneath the list

#### Scenario: The close control is keyboard-operable
- **WHEN** I tab to the ✕ and press Enter or Space
- **THEN** the overlay closes, and while focused the control is clearly marked as focused

#### Scenario: Five whole rows on open
- **WHEN** the overlay opens on a history with more than five entries
- **THEN** five rows are visible in full, no part of a sixth is shown, and no row is cut through the middle

#### Scenario: Whole rows at any window height
- **WHEN** the overlay is opened in a taller or shorter window
- **THEN** it still shows five whole rows rather than a fraction of a row

#### Scenario: The rest of the history is still reachable
- **WHEN** the history holds more than five rows
- **THEN** the remainder is reached by scrolling the list, and the ✕ stays in place while it scrolls

#### Scenario: A short history
- **WHEN** the history holds two rows
- **THEN** the overlay shows those two rows without reserving the height of five

#### Scenario: English titles in the history
- **WHEN** a history row's anime has a stored English title
- **THEN** that row shows the English title rather than the default title

#### Scenario: History rows show pictures
- **WHEN** the history overlay lists an anime
- **THEN** that row shows the anime's poster picture next to its title

#### Scenario: Long history title
- **WHEN** a history row's title is longer than two lines
- **THEN** it is cut off at two lines and hovering it reveals the full title

#### Scenario: A history row reads as one phrase
- **WHEN** any row of the full history renders
- **THEN** its change is described once, with no label-and-detail pair that repeats the same wording

#### Scenario: Consecutive episode-watched entries collapse into a range
- **WHEN** an anime's episode count was increased several times in a row
- **THEN** the full history shows a single row reporting the range of episodes watched, not one row per increase

#### Scenario: A finished anime in the history
- **WHEN** I watch an anime's last episodes, which completes it, and score it
- **THEN** the history shows one row reporting the completion together with the score, and above the episode-range row for the run that led to it

#### Scenario: Repeated edits are all kept
- **WHEN** I change an anime's score several times in a row
- **THEN** the full history shows a row for each of those changes

#### Scenario: Searching by title
- **WHEN** I type part of an anime's title into the history's search field
- **THEN** only rows whose anime matches that text, by either its default or English title, remain listed

#### Scenario: Filtering by a date range
- **WHEN** I set a start date and an end date in the history overlay
- **THEN** only rows timestamped within those days, inclusive of both, remain listed

#### Scenario: An open-ended date bound
- **WHEN** I set only a start date
- **THEN** every row from that day onwards remains listed and earlier rows are hidden

#### Scenario: Search and dates combined
- **WHEN** both a title search and a date bound are set
- **THEN** only rows matching the title and falling inside the date range remain listed

#### Scenario: Clearing the filters
- **WHEN** I clear the search field and the date inputs
- **THEN** the full unfiltered history is listed again without closing the overlay

#### Scenario: Filters match nothing
- **WHEN** the active filters exclude every row
- **THEN** the overlay states that no history matches rather than showing an empty list

### Requirement: Profile top-row box proportions
The "Anime stats" box SHALL be laid out narrow enough that each stat's value reads as belonging to its label without a connecting rule, and SHALL stay within that narrow width at any window width or display resolution rather than growing with the available space. The remaining width of the profile page's top row SHALL be taken by the other boxes in that row.

The "Latest updates" box's list SHALL fill the box down to its bottom edge rather than stopping at a fixed height, so the box has no dead space below its last visible row when the row of boxes is taller than the list's fixed height would be.

#### Scenario: Stats box on a wide display
- **WHEN** the profile page is viewed on a very wide display
- **THEN** the "Anime stats" box stays narrow and its labels and values stay close together

#### Scenario: Latest updates fills its box
- **WHEN** the profile page's top row is taller than the latest-updates list's contents would otherwise occupy
- **THEN** the list's scroll area extends to the bottom of its box, leaving no empty gap beneath it

### Requirement: Profile lists and the rankings overlay show no scrollbar
The scrollable vertical lists on the profile page and in its overlays SHALL NOT render a visible scrollbar, in any browser, whether or not they overflow — the rows are the content, and a bar drawn beside them inside an already-small box reads as chrome.

This SHALL apply to the "Latest updates" feed, both opinion-divergence lists ("They liked it, I didn't" and "I liked it, they didn't"), the full edit-history overlay's list, and the list in the rankings "See all" overlay — which, being the same overlay the recap page's rankings open, SHALL therefore show no scrollbar there either, for Season ranking, Year ranking, Seasons by time watched, and Years by time watched alike.

Hiding the scrollbar SHALL cost none of these lists any scrolling: a list holding more rows than fit SHALL still scroll by wheel, trackpad, keyboard, and drag exactly as it does today, and every row it holds SHALL remain reachable.

No gutter SHALL be reserved where the scrollbar was: the width it occupied SHALL be given back to the rows.

#### Scenario: No bar beside the feed
- **WHEN** the "Latest updates" feed holds more rows than fit
- **THEN** no scrollbar is drawn beside or over its rows, at rest or while scrolling

#### Scenario: No bar in the divergence lists
- **WHEN** an opinion-divergence list holds more than ten anime
- **THEN** no scrollbar is drawn beside its rows

#### Scenario: No bar in the edit-history overlay
- **WHEN** the full edit-history overlay holds more rows than fit
- **THEN** no scrollbar is drawn beside its rows

#### Scenario: No bar in a "See all" overlay
- **WHEN** I open the "See all" overlay on a ranking holding more than eight rows, from the profile page or from the recap page
- **THEN** no scrollbar is drawn beside its rows

#### Scenario: Scrolling still works
- **WHEN** I make a wheel gesture over any of these lists, or drag inside it
- **THEN** it scrolls exactly as it did when it had a scrollbar, and its last row is reachable

#### Scenario: The rows take the gutter back
- **WHEN** one of these lists is drawn without its scrollbar
- **THEN** its rows extend to the edge the scrollbar's gutter used to hold, with no empty strip beside them

### Requirement: Profile lists show whole rows only
The "Latest updates" feed SHALL show exactly five rows at rest, with no part of a sixth row visible beneath them. Each opinion-divergence list SHALL show at most ten rows at rest, with no part of an eleventh visible beneath them; a list with fewer than ten rows SHALL show what it has without reserving space for the rest.

The feed SHALL reconcile this with filling its box: rather than stopping at a fixed height and leaving dead space, its rows SHALL take their height from the height the box gives the feed, so that five of them fill it exactly. Row height SHALL therefore follow the box — a taller box makes taller rows, not four and a fraction — and the feed SHALL keep a floor below which it does not shrink, so it cannot collapse when nothing else in the row of boxes is tall.

Rows partly scrolled out of view while scrolling are not covered by this: the requirement is about what the lists show before and after a scroll gesture, not during one.

#### Scenario: No sliver of a sixth update
- **WHEN** the profile page loads with more than five entries in the Latest updates feed
- **THEN** five rows are visible in full and no part of the sixth is shown

#### Scenario: Whole rows at any window width
- **WHEN** the window width changes enough to change the height of the profile page's top row of boxes
- **THEN** the feed still shows exactly five whole rows, resized to fill the new height

#### Scenario: Feed still fills its box
- **WHEN** the top row of boxes is taller than the feed's floor height
- **THEN** the feed's five rows extend to the bottom of the box, leaving no empty gap beneath them

#### Scenario: The rest is still reachable
- **WHEN** the feed holds more than five rows
- **THEN** the remainder is reached by scrolling the feed

#### Scenario: No sliver of an eleventh divergence row
- **WHEN** an opinion-divergence list holds more than ten anime
- **THEN** ten rows are visible in full, no part of the eleventh is shown, and the rest is reached by scrolling the list

#### Scenario: A short divergence list
- **WHEN** an opinion-divergence list holds three anime
- **THEN** its box shows those three rows and does not reserve the height of ten

### Requirement: My top anime with minimum-of-ten fill and manual selection
The system SHALL show my top anime, always showing at least 10 when I have scored at least 10 anime. The list SHALL be ordered by score descending and grouped into score tiers: a lower-scored anime SHALL NEVER appear above a higher-scored one. It SHALL include all anime I scored 10; if there are 10 or more such anime it shows all of them with no cap. If there are fewer than 10, it SHALL fill the remainder up to 10 from the next-highest score tiers in descending score order; the tier that does not fully fit SHALL be truncated so the list stops at 10.

The order within a single score tier SHALL be the order the `anime-ranking` capability gives that score — my hand-ordered anime first in the order I set (never-placed members following them alphabetically), then short-form entries, then dropped anime. The section SHALL NOT compute an order of its own, so an anime I place here is placed everywhere the ranking is read, and an anime I drop falls to the bottom of its tier here as it does everywhere else. Membership of the truncated tier SHALL be expressed by that same tier order: the tier members that fit occupy the remaining slots, in tier order.

The system SHALL provide a **Rank** control in the box's top-right corner whenever any tier has more than one member. The control SHALL open an editor listing the hand-orderable members of every tier of the current list in full — including members of the truncated tier that do not fit — with a cut line marking how many of that tier's listed members are included. Short-form and dropped members SHALL NOT be listed, since the ranking places them by band and title and no editing here could move them; when a tier's cut falls among those unlisted members, every listed member of that tier is included and no cut line SHALL be drawn for it. Reordering members within a tier SHALL change their order in the list; moving a member across the cut line SHALL change which members of that tier are included. Every reorder SHALL persist into the one ranking on its own, with no separate save step, and the persisted order SHALL take precedence over the alphabetical default.

The editor SHALL additionally offer an action that leaves this top-list arrangement and opens the whole-library ranking editor described by the `anime-ranking` capability, so every scored anime can be arranged and not only those in contention for the top list.

Each tier in the editor SHALL have a promote boundary equal to the smaller of that tier's included-member count and 10. A row positioned before that boundary SHALL offer single-step move-up and move-down controls, disabled at the ends of its tier. A row positioned at or after that boundary SHALL offer a single promote control instead of the two single-step controls; activating it SHALL move that row to the last position before the boundary and push every row from that position onward down by one, leaving the rest of the tier order intact. The promote boundary SHALL NOT move when a row is promoted, so in a truncated tier promoting an excluded member takes the last included slot and drops the displaced member below the cut line. Each of these controls SHALL render its glyph centered within the control.

Reordering by dragging a row SHALL work across the whole tier in one drag: while a row is being dragged and held within an edge zone at the top or bottom of the editor's scrollable area, that area SHALL scroll in that direction continuously until the pointer leaves the edge zone or the drag ends, and the reorder SHALL apply to the row under the pointer when the drag is released. Dragging SHALL apply only within one tier; a row released over a different tier SHALL leave every tier's order unchanged.

While a drag is in progress the editor SHALL show a floating copy of the dragged row that follows the pointer, SHALL dim that row in its original place, and SHALL mark where the row would land. The editor SHALL NOT reorder the tier until the drag is released, so the rows under the pointer do not move mid-drag. A drag SHALL be cancellable, leaving every tier's order unchanged; while a drag is in progress the cancel key SHALL cancel the drag and SHALL NOT close the editor or discard any pending edits. Dragging SHALL NOT select the text of any row the pointer passes over.

#### Scenario: Ten or more perfect scores
- **WHEN** I have 10 or more anime scored 10
- **THEN** all of them are shown with no cap at 10

#### Scenario: Fewer than ten perfect scores (default fill)
- **WHEN** I have fewer than 10 anime scored 10 and have never ordered the lower tiers
- **THEN** the list is filled up to 10 from the next-highest score tiers, each tier in alphabetical order

#### Scenario: The edit control is named Rank
- **WHEN** I look at the "My top anime" box with more than one member in some tier
- **THEN** its top-right control reads **Rank**

#### Scenario: Reordering a tier that exactly fills the list
- **WHEN** I have exactly 10 anime scored 10 and I reorder them in the editor
- **THEN** the top list shows those same 10 anime in my chosen order, and the order is still there after a reload

#### Scenario: Score still dominates order
- **WHEN** I reorder anime across a list containing both 10s and 9s
- **THEN** every anime scored 10 still appears above every anime scored 9, and my ordering only applies within each of those tiers

#### Scenario: Dropped members sink within their tier
- **WHEN** a tier of the top list holds anime I have completed and one I dropped
- **THEN** the dropped one appears beneath every completed member of that tier, whatever its title

#### Scenario: Pinned members are not in the editor
- **WHEN** a tier shown in the editor holds a dropped anime and a Music entry
- **THEN** neither is listed among that tier's rows, while both still occupy their places in the top list itself

#### Scenario: The cut falls among unlisted members
- **WHEN** a truncated tier's included members cover every one of its hand-orderable members and stop part-way through its dropped ones
- **THEN** that tier's rows are all shown as included and no cut line is drawn for it

#### Scenario: Swapping a member of a truncated tier
- **WHEN** a tier has more members than the remaining slots and I move one of its excluded members above the cut line
- **THEN** that anime takes a slot in the top list, the member it displaced drops below the cut line and out of the list, and the change is persisted

#### Scenario: Newly scored anime joins an ordered tier
- **WHEN** I score a new anime into a tier whose order I have already set
- **THEN** it appears last among that tier's hand-ordered members

#### Scenario: Opening the whole-library ranking editor
- **WHEN** I use the whole-library action in the top-anime editor
- **THEN** the ranking editor opens over every anime I have scored, not only those in contention for the top list

#### Scenario: Promoting a row from deep in a long tier
- **WHEN** a tier has 40 members all included in the list and I activate the promote control on its 37th row
- **THEN** that row becomes the 10th row of the tier, the rows that were 10th through 36th each move down one place, and the rows after the 37th keep their positions

#### Scenario: Promote replaces the single-step arrows outside the top ten
- **WHEN** I open the editor on a tier whose promote boundary is 10
- **THEN** rows 1 through 10 show move-up and move-down controls and rows 11 onward show only the promote control

#### Scenario: Promoting an excluded member of a truncated tier
- **WHEN** a truncated tier includes 4 of its members and I activate the promote control on its 9th row
- **THEN** that row becomes the 4th row of the tier and is now above the cut line, the member that was 4th becomes 5th and falls below the cut line, and saving persists both

#### Scenario: Promote is repeatable up the tier
- **WHEN** I promote a row into the last top-ten slot and then use the move-up control on it
- **THEN** it moves one place at a time from there, so the promote control and the arrows compose without any intermediate save

#### Scenario: Dragging to the edge scrolls the editor
- **WHEN** I drag a row to the bottom edge of the editor's scroll area and hold it there
- **THEN** the list keeps scrolling down while I hold, and releasing over a row drops the dragged row at that row's position without any grab-and-drop cycling

#### Scenario: Auto-scroll stops with the drag
- **WHEN** I move the dragged row out of the edge zone, or release it, or cancel the drag
- **THEN** the automatic scrolling stops immediately

#### Scenario: Floating preview follows the pointer
- **WHEN** I press on a row and move the pointer
- **THEN** a floating copy of that row follows the pointer, the row stays dimmed in its original place, and the tier's rows do not move until I release

#### Scenario: Landing position is shown before release
- **WHEN** I hold a dragged row over another row of the same tier
- **THEN** the editor marks the position the dragged row would take, on the far side of the hovered row relative to where the drag started

#### Scenario: Cancelling a drag keeps the editor open
- **WHEN** I press the cancel key while dragging a row
- **THEN** the drag ends with no reorder, the editor stays open, and the reordering I did before the drag is still pending

#### Scenario: Drag across tiers is rejected
- **WHEN** I drag a row from one score tier and release it over a row in a different tier
- **THEN** no tier's order changes

#### Scenario: Pressing a row control does not start a drag
- **WHEN** I press the move-up, move-down, or promote control on a row
- **THEN** that control acts on click and no drag begins

#### Scenario: Dragging does not select text
- **WHEN** I press on a row and drag the pointer down across several other rows before releasing
- **THEN** none of the rows the pointer passed over end up with their title text or controls selected

#### Scenario: Row controls are centered
- **WHEN** I look at a move-up, move-down, or promote control on any row
- **THEN** the arrow or promote glyph sits centered within that control, not offset to one side

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

### Requirement: Top-anime ordering is shared across filters
The system SHALL draw every media-type filter view from the one persisted ranking, so ordering an anime once affects the unfiltered list, every filtered list containing it, and every other place in the app that orders by my score. Reordering within a filtered view SHALL preserve the relative positions of the same tier's hand-ordered members that the filter hides.

#### Scenario: Order set under a filter shows in the unfiltered list
- **WHEN** I reorder two anime of the same score while the Movie filter is active
- **THEN** switching back to All shows those two anime in that same relative order

#### Scenario: Hidden members keep their positions
- **WHEN** I reorder a tier while a media-type filter hides some of that tier's members
- **THEN** the hidden members keep their positions relative to the visible members that surrounded them

#### Scenario: Order set here shows outside the profile
- **WHEN** I reorder two anime of the same score in the "My top anime" editor
- **THEN** my list sorted by my score, the recap top 10, and the score board all show them in that same relative order

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

### Requirement: Poster-tile hover in My top anime and Most rewatched
The system SHALL give each poster tile in the "My top anime" and "Most rewatched" strips a hover/focus state distinct from the border-and-background language used elsewhere in the app: the tile SHALL scale up slightly in place, with no border, ring, or background change drawn over or around the poster. The strip SHALL reserve enough space that a scaled-up edge tile is not clipped by the strip's scroll boundary, and the hovered tile SHALL render above its neighbours rather than being overlapped as it grows into the gap beside them.

#### Scenario: Hovering a poster tile
- **WHEN** I move the pointer over a tile in the "My top anime" or "Most rewatched" strip
- **THEN** that tile scales up slightly in place and no border, ring, or background appears

#### Scenario: Scaled edge tile is not clipped
- **WHEN** the strip is scrolled fully to one end and I hover the tile at that end
- **THEN** the scaled-up tile is fully visible, with no part of it cut off by the strip's edge

#### Scenario: Hovered tile is not overlapped by its neighbours
- **WHEN** a tile scales up and grows into the gap beside an adjacent tile
- **THEN** the hovered tile renders above its neighbours rather than being partly covered by them

### Requirement: Poster strips keep a fixed tile size and scroll horizontally only
The "My top anime", "Top series", and "Most rewatched" strips SHALL size their poster tiles as a fixed fraction of the strip's width — ten tiles across the visible width — regardless of how many entries the strip contains. Tiles SHALL NOT shrink to fit additional entries; entries beyond the tenth SHALL be reached by scrolling the strip horizontally.

Ten tiles SHALL actually fit: the tile's full rendered width, including any border it carries, SHALL be what the ten-across computation divides the strip's visible width into, so that a strip holding exactly ten entries shows all ten whole — the tenth SHALL NOT be clipped at the strip's trailing edge, and this SHALL hold for a strip that cannot be scrolled as much as for one that can.

Each strip SHALL scroll along the horizontal axis only. Vertical scrolling within a strip SHALL be impossible: a trackpad or wheel gesture SHALL NOT be able to displace the strip's contents up or down, including where a hovered tile's scaled-up size exceeds the strip's height.

A strip holding no more entries than fit across its visible width SHALL NOT be scrollable at all: no wheel gesture and no drag SHALL displace its contents by any amount, and the strip SHALL NOT present itself as scrollable — including the grab cursor, which SHALL appear only on a strip that can actually be scrolled. Sub-pixel differences between the tiles' total width and the strip's width SHALL NOT make a strip that fits behave as a scrollable one.

#### Scenario: Exactly ten entries are all whole
- **WHEN** a strip holds exactly ten entries
- **THEN** all ten tiles are fully visible, with the tenth's trailing edge inside the strip rather than cut off by it

#### Scenario: The tenth tile of a longer strip
- **WHEN** a strip holds more than ten entries and sits at its starting scroll position
- **THEN** the tenth tile is whole and the eleventh is the first one reached by scrolling

#### Scenario: More than ten top anime
- **WHEN** I have more than ten anime scored 10
- **THEN** the tiles stay the same size as when there were exactly ten, and the rest are reached by scrolling the strip

#### Scenario: Exactly ten entries do not scroll
- **WHEN** a media-type filter leaves a strip with exactly ten entries
- **THEN** the strip cannot be scrolled or dragged in either direction, and its cursor does not offer to drag it

#### Scenario: Switching to a filter that fits stops the scrolling
- **WHEN** I switch from a filter whose strip scrolls to one whose entries all fit
- **THEN** the strip stops being scrollable, rather than keeping a few pixels of travel from the wider list

#### Scenario: Tile size matches between the strips
- **WHEN** two of the strips are shown at the same window width
- **THEN** their tiles are the same size

#### Scenario: No vertical drift
- **WHEN** I make a vertical scroll gesture with the pointer over any strip
- **THEN** the strip's contents do not move up or down

### Requirement: Poster strips show no scrollbar
The "My top anime", "Top series", and "Most rewatched" strips SHALL NOT render a visible scrollbar, in any browser, whether or not they overflow — the posters are the content and a bar under them is noise.

Hiding the scrollbar SHALL NOT cost the strips any scrolling: a strip that holds more entries than fit SHALL still scroll by wheel gesture and by dragging it, exactly as it does today.

The profile page's vertical lists SHALL be drawn without a scrollbar for the same reason, under "Profile lists and the rankings overlay show no scrollbar".

#### Scenario: No bar under the posters
- **WHEN** a strip holds more entries than fit across it
- **THEN** no scrollbar is drawn under or over the tiles, at rest or while scrolling

#### Scenario: Scrolling still works
- **WHEN** I drag an overflowing strip, or make a horizontal wheel gesture over it
- **THEN** it scrolls exactly as it did when it had a scrollbar

#### Scenario: Vertical lists are drawn the same way
- **WHEN** the "Latest updates" feed has more rows than fit
- **THEN** it too is drawn without a scrollbar

### Requirement: All-anime score distribution
The system SHALL show a count of anime per score/rating value plus the overall mean score, rendered as a bar per score value alongside its count and that score's share of all my rated anime.

Each bar's length SHALL be that score's count relative to the largest count across all score values, so the most-common score's bar fills the full width of its track and every other bar is drawn in proportion to it. A score with a count of zero SHALL render an empty track.

Each row SHALL show, after its count, that score's share of all my rated anime as a percentage — the count for that score divided by the total number of anime I have rated. A share that rounds to zero but comes from a non-zero count SHALL be shown as less than one percent rather than as zero.

The count and the share SHALL occupy two separate columns of fixed width, each aligned within itself, rather than being run together into one string: every row's count SHALL line up with every other row's count, and every row's share with every other row's share, whatever their number of digits. Neither column SHALL be sized to its content, since a wider value in one row would then steal width from that row's own bar track and distort which bar reads as longest.

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

#### Scenario: Counts line up down the block
- **WHEN** one score's count is 7 and another's is 143
- **THEN** the two counts are aligned with each other in a single column rather than sitting at different horizontal positions

#### Scenario: Percentages line up down the block
- **WHEN** one score's share is 4% and another's is 27%
- **THEN** the two percentages are aligned with each other in a single column, independently of the counts beside them

#### Scenario: Bar tracks are unaffected by a wide value
- **WHEN** one row's count and share are far wider than another's
- **THEN** both rows' bar tracks are the same width, so their bars remain directly comparable

#### Scenario: A very small share
- **WHEN** a score has at least one anime but its share rounds down to zero percent
- **THEN** its row shows less than one percent rather than zero percent

#### Scenario: Nothing rated yet
- **WHEN** I have rated no anime
- **THEN** every bar renders as an empty track and no share is shown as an invalid number

### Requirement: Opinion divergence lists
The system SHALL show two opinion-divergence lists comparing my score against MAL's score for anime I have rated: "They liked it, I didn't" and "I liked it, they didn't".

Membership SHALL be decided on *standardized* scores rather than the raw difference between them. MAL's community averages occupy a far narrower band than personal 1–10 scores, so an identical raw gap on both sides can never populate both lists; each score SHALL therefore be measured in standard deviations from the mean of its own scale. The population for both scales SHALL be the anime I have rated that also carry a MAL average — the same population both lists draw from — and the mean and standard deviation SHALL be recomputed from that population whenever the profile is built, so the rules track my list as it grows rather than sitting on fixed constants.

For each anime in that population, its divergence SHALL be how many standard deviations MAL's score sits above its own mean minus how many my score sits above mine. An anime SHALL qualify for a list when its divergence reaches 1.0 in that list's direction **and** it passes that list's label gate, which keeps the heading's claim honest about both parties:

- "They liked it, I didn't" — MAL's score is at least 7.5 and my score is at most 5.
- "I liked it, they didn't" — my score is at least 8 and MAL's score is at most 7.5.

The 5 and 8 boundaries are MAL's own score labels: 5 is "Average" and below it lies everything worse, 8 is "Very Good" and above it everything better. The scores between them — 6 ("Fine") and 7 ("Good") — are a neutral band, and an anime I scored in that band SHALL fall in neither list however far MAL's average sits from it. The 7.5 boundary splits MAL's community scale between its "Good" and "Very Good" labels.

Each list SHALL be ordered by divergence, strongest first, with ties broken by title case-insensitively. Neither list SHALL be capped by rank or count: subject only to the hidden-state rule below, every anime that qualifies SHALL be present, reachable by scrolling its box.

While the global hide-scores toggle is on, each list SHALL render only those of its qualifying anime whose entry is **Completed, Dropped, or Rewatching** — the statuses the "Always show MAL scores for completed and dropped shows" setting covers — and SHALL omit the rest of its rows **entirely**, rather than rendering them with a placeholder or a reveal control. Membership of either list is itself a claim about the anime's MAL score: appearing in "They liked it, I didn't" says the score is at least 7.5, and appearing in "I liked it, they didn't" says it is at most 7.5. A row therefore leaks a bound on the score it is hiding whatever its own score cell renders, and for an entry the user has not settled — Watching, On-hold, Plan to watch, or not in the list at all — that is a viewing still ahead of them, which is exactly what the hide toggle exists to protect. A placeholder row would not do: the leak is the row's presence, not its score cell.

Omission SHALL be confined to that state. While the hide toggle is off, both lists SHALL render every qualifying anime regardless of its status, since nothing needs withholding once scores are shown. Omission SHALL NOT change which anime qualify, the order they are ranked in, or the population the standardization is computed over — all three are decided before it and are unaffected by it. A list left with no rows to render SHALL show the same empty state it shows when nothing qualified.

Divergence needs a population to normalize against. When fewer than 10 anime carry both my score and a MAL average, or when either scale's standard deviation across that population is zero, both lists SHALL be empty rather than ranking on a spread that does not exist.

#### Scenario: They liked it, I didn't
- **WHEN** I rated an anime 3 and its MAL average is 8.70, more than 1.0 standard deviation apart once each score is standardized against its own scale
- **THEN** it appears in the "They liked it, I didn't" list

#### Scenario: I liked it, they didn't
- **WHEN** I rated an anime 9 and its MAL average is 6.31, more than 1.0 standard deviation apart once each score is standardized against its own scale
- **THEN** it appears in the "I liked it, they didn't" list

#### Scenario: Both lists populate
- **WHEN** my scores run lower on average and spread wider than the MAL averages of the same anime
- **THEN** neither list is starved by that difference in scale — each holds every anime that diverges by the same standardized amount in its own direction

#### Scenario: A score I did not dislike stays out
- **WHEN** an anime's MAL average diverges upward from my score by more than 1.0 standard deviation but I scored it 6
- **THEN** it does not appear in "They liked it, I didn't", because the list claims I didn't like it and a 6 does not say that

#### Scenario: A community score that is not a dislike stays out
- **WHEN** I scored an anime 10 and it diverges downward by more than 1.0 standard deviation, but its MAL average is 7.8
- **THEN** it does not appear in "I liked it, they didn't", because the list claims they didn't like it and a 7.8 average does not say that

#### Scenario: Strongest disagreement first
- **WHEN** either list renders
- **THEN** its rows run from the largest standardized divergence to the smallest

#### Scenario: Too little to normalize against
- **WHEN** fewer than 10 of my rated anime carry a MAL average
- **THEN** both lists are empty and each box shows its empty state

#### Scenario: An unsettled qualifying anime is omitted while scores are hidden
- **WHEN** the hide toggle is on and an anime that qualifies for a divergence list is one I am Watching, have On-hold, Plan to watch, or do not have in my list at all
- **THEN** no row for it appears in that list at all — not a placeholder row, not a row carrying a reveal control

#### Scenario: A settled qualifying anime still appears while scores are hidden
- **WHEN** the hide toggle is on and an anime that qualifies for a divergence list is one I have Completed, Dropped, or am Rewatching
- **THEN** its row appears as it does today, its MAL score following the "always show completed" setting exactly as before

#### Scenario: Hiding omits nothing once scores are shown
- **WHEN** the hide toggle is off
- **THEN** both lists show every qualifying anime, whatever its status, in the same order as before

#### Scenario: A list emptied by hiding shows its ordinary empty state
- **WHEN** the hide toggle is on and every anime qualifying for one of the lists is one I have not settled
- **THEN** that list shows the same empty state it shows when nothing qualified, rather than a message about withheld rows

### Requirement: Top series section
The profile page SHALL show a **Top series** section that ranks my franchises, presented like My top anime and Most rewatched: a horizontally-scrolling, drag-scrollable strip of fixed-size poster tiles, uncapped, using the same tile size and hover behaviour as the other two strips.

A series SHALL be eligible for the section when at least one of its members — main line or extra — is in my list. A series with no member in my list SHALL NOT appear.

A series whose main line holds two or more entries that have started airing SHALL additionally require that **at least two of those aired main-line entries are in my list**. A series that clears the first condition but not this one SHALL NOT be listed, because its averages would rank a whole franchise on a single entry's worth of my viewing. An entry counts as in my list at **any** status — plan-to-watch included — and a main-line entry that has been announced but has not aired a single episode SHALL NOT count toward either total, so a series whose main line holds one aired entry is unaffected by this rule however many sequels are confirmed.

That exclusion SHALL be unconditional: no control SHALL switch it off, and it SHALL apply under either ranking basis. It SHALL be independent of the single-entry filter, which narrows the strip further when the user switches it on.

Each tile SHALL show the series' poster and **both** of its main-series averages: MAL's average across the main line, and my average across the main line. Those figures SHALL be the same figures that series' own page shows for `MAL · main series` and `Mine · main series`, computed the same way, so the two surfaces can never disagree. The two figures SHALL be distinguishable by the score colour roles (see the `score-presentation` capability).

Opening a tile SHALL navigate to that series' page, not to an anime's detail page.

Each tile SHALL make available, without requiring navigation, how many main-line entries each average was computed over — the same "N of M scored" figure the series page shows beside its chips.

#### Scenario: Ranking my franchises
- **WHEN** I open the profile page
- **THEN** a Top series section lists my series as poster tiles, each showing both its MAL main-series average and my main-series average

#### Scenario: Only series I have watched something of
- **WHEN** a stored series has no member in my list
- **THEN** it does not appear in Top series

#### Scenario: A franchise I have barely started
- **WHEN** a series' main line has three seasons that have aired and only one of them is in my list
- **THEN** the series is not listed, under either ranking basis

#### Scenario: Two of a franchise's seasons is enough
- **WHEN** a series' main line has three seasons that have aired and two of them are in my list
- **THEN** the series is listed and ranked normally

#### Scenario: An unaired sequel does not make a franchise barely-watched
- **WHEN** a series' main line has one aired season, which is in my list, plus a second season that is announced but has not aired an episode
- **THEN** the series is listed, because only one main-line entry has actually aired

#### Scenario: Plan-to-watch counts as coverage
- **WHEN** a series' main line has two aired seasons, one completed and one sitting in my list as plan-to-watch
- **THEN** the series is listed, because both aired main-line entries are in my list

#### Scenario: A series I only know through an extra
- **WHEN** the only member of a series in my list is one of its extras, and the series' main line has a single aired entry
- **THEN** the series is eligible, and its two averages are still computed over its main line only

#### Scenario: An extra is not coverage of a multi-season franchise
- **WHEN** the only member of a series in my list is one of its extras, and the series' main line has three aired entries
- **THEN** the series is not listed, because at most one of its aired main-line entries is in my list

#### Scenario: The exclusion cannot be switched off
- **WHEN** I look for a way to see the franchises this rule hides
- **THEN** the section offers none, and switching the single-entry filter or the ranking basis does not bring them back

#### Scenario: Figures match the series page
- **WHEN** I compare a tile's two averages with that series' page
- **THEN** they equal the page's `MAL · main series` and `Mine · main series` averages

#### Scenario: Opening a series from the strip
- **WHEN** I click a tile
- **THEN** I am taken to that series' page

#### Scenario: The strip scrolls rather than wrapping
- **WHEN** I have more series than fit across the section
- **THEN** the strip scrolls horizontally at a fixed tile size, and can be dragged to scroll, exactly like the other two strips

### Requirement: Top series ranking basis
Top series SHALL be ranked by a **main-series average**, and SHALL offer a control that switches which average ranks it: **my score** or **MAL's score**. The control SHALL default to my score.

The control's two options SHALL carry the colour of the score role each selects: **My score** the mine role, **MAL score** the MAL role, per the `score-presentation` capability's requirement that a score-role control carries its role's colour. The option carrying the MAL role SHALL NOT be drawn in the purple this app reserves for my own score, and the colour SHALL apply to the option's selected and hovered states alike rather than only one of them.

Carrying those colours SHALL be scoped to this control. The media-type tabs on **My top anime** and **Most rewatched**, which share the same control form, SHALL keep the page's accent — they select a media type, not a score role.

Ranking SHALL be by the selected average in descending order. **The two bases break their ties differently**, because only one of them has a second opinion of mine to consult.

**Under my score**, ties SHALL be broken by the identical chain the `series-browser` capability's **My average** sort uses, so the two surfaces can never order the same two franchises differently:

1. **the average position the series' main-line entries hold in my rankings**, ascending — the series whose entries sit nearer the top of my rankings first. The figure SHALL be the mean rank over the series' main-line entries **that hold a rank**, taken over the whole main line. An entry that holds no rank SHALL be left out of the mean entirely — not counted as a worst rank, not as a zero, and not as a member the mean is divided by — so an entry I have not scored can neither raise nor lower its series' average position. A series **none** of whose main-line entries holds a rank SHALL be placed after every tied series that has such a figure.
2. **the count of main-line episodes aired so far**, descending — the larger franchise first;
3. **display title**, ascending.

Both figures SHALL be the same figures the Series page's cards carry, computed the same way over the same member sets: the average position over the whole main line, matching the average it breaks a tie in; the aired-episode count over the main line's default combination of version alternatives, as every episode figure on a card without a version picker is.

**Under MAL's score**, ties SHALL be broken by the number of scored main-line entries the average was computed over, descending — so an average earned across more entries places higher — and then by title, case-insensitively. My rankings SHALL NOT enter this ordering: they are an opinion of mine, and the MAL basis is deliberately an ordering that says nothing about what I think.

Switching the basis SHALL reorder the strip without reloading the page's data and without the strip collapsing or changing height.

Both averages SHALL remain visible on every tile regardless of which basis is selected; the basis chooses the ordering, not what is shown.

#### Scenario: Default ranking
- **WHEN** I open the profile page without having changed the control
- **THEN** Top series is ranked by my main-series average, highest first

#### Scenario: Ranking by MAL's score
- **WHEN** I switch the control to MAL's score
- **THEN** the strip reorders by MAL's main-series average, highest first, and both averages are still shown on every tile

#### Scenario: Tied my-averages fall to my rankings
- **WHEN** the basis is my score and two series both average 8.00, the main-line entries of one sitting at ranks 3 and 7 in my rankings while the other's sit at ranks 40 and 60
- **THEN** the first is placed above the second

#### Scenario: Only ranked entries count toward the average position
- **WHEN** a tied series has three main-line entries of which only one is ranked
- **THEN** its average position is that one entry's rank, not a figure penalised for the two without one

#### Scenario: An unscored entry does not drag its series down
- **WHEN** two series are tied on my average, one having four main-line entries of which I have scored the two sitting at ranks 4 and 6, and the other having exactly those two entries and nothing else
- **THEN** both have the same average position, because the two unscored entries hold no rank and are left out of the mean rather than counted against it

#### Scenario: A tied series with nothing ranked sorts after those with ranks
- **WHEN** the basis is my score, two series are tied on my average, and none of one series' main-line entries appears in my rankings
- **THEN** that series is placed after the tied series that does have an average position

#### Scenario: Still tied falls to episodes aired
- **WHEN** two series are tied on my average and on their average position in my rankings, and one has 62 main-line episodes aired against the other's 24
- **THEN** the 62-episode series is placed first

#### Scenario: Still tied falls to the title
- **WHEN** two series are tied on my average, on average position, and on episodes aired
- **THEN** they are ordered by display title ascending

#### Scenario: The strip and the Series page agree
- **WHEN** two series appear tied on my average on both the profile's Top series strip and the Series page sorted by My average
- **THEN** the two surfaces place them in the same order, because both read the same average position and the same aired-episode count

#### Scenario: An average earned over more entries wins a tie under MAL's score
- **WHEN** the basis is MAL's score and two series have the same MAL average, one computed over five scored entries and one over two
- **THEN** the one computed over five places first

#### Scenario: My rankings do not order the MAL basis
- **WHEN** the basis is MAL's score and two series are tied on MAL average and on scored main-line count, with very different average positions in my rankings
- **THEN** they are ordered by title, not by my rankings

#### Scenario: Switching the basis is immediate
- **WHEN** I switch the ranking basis
- **THEN** the strip reorders immediately without a visible reload and without collapsing

#### Scenario: The basis control wears its role's colour
- **WHEN** I select **MAL score** and then **My score**
- **THEN** the selected option is blue in the first case and purple in the second, matching the colour the tiles' own MAL and my-score figures carry

#### Scenario: The media-type tabs are unaffected
- **WHEN** I select a media type on **My top anime** or **Most rewatched**
- **THEN** the selected tab is drawn in the page's accent exactly as it is today

### Requirement: The Top series read carries its tie-break figures
The Top series read endpoint SHALL carry, for every series it returns, the two figures the my-score ordering breaks its ties on: the average position the series' main-line entries hold in my rankings (absent when no main-line entry holds a rank), and the count of main-line episodes aired so far.

Both SHALL be derived at read time from the entries and members already stored — neither SHALL be persisted, so a score edit that moves an entry in my rankings changes the ordering on the next read with no rebuild.

The response SHALL already be ordered by the full my-score chain, so a client that never touches the ranking-basis control shows the same order as one that does. Adding these figures SHALL NOT change which series the endpoint returns: eligibility — membership in my list, and the two-aired-main-line-entries coverage rule — is unchanged, and no control switches either off.

#### Scenario: The figures arrive with the section
- **WHEN** the profile page reads its Top series section
- **THEN** each series carries its average ranking position and its main-line aired-episode count alongside its two averages

#### Scenario: A series with nothing ranked carries no position
- **WHEN** none of a series' main-line entries appears in my rankings
- **THEN** that series carries no average ranking position at all, rather than a zero or a worst-case value

#### Scenario: A score edit moves a series without a rebuild
- **WHEN** I change my score on an entry so it moves in my rankings, and then reload the profile page
- **THEN** its series' average position reflects the new rank, with no series rebuild

#### Scenario: The wire order matches the page's default order
- **WHEN** the Top series read is returned
- **THEN** its series are already in my-score order with the full tie-break chain applied

#### Scenario: Eligibility is unchanged
- **WHEN** the section is read
- **THEN** exactly the series that were listed before carry the new figures, and none is added or dropped

### Requirement: Series unrankable under the selected basis are omitted
A series that has no value under the **selected** basis SHALL be omitted from the strip while that basis is selected: under my score, a series with no scored main-line entry; under MAL's score, a series whose main line carries no MAL score at all.

Switching the basis MAY therefore change which series are listed, not only their order.

#### Scenario: A series I have not scored
- **WHEN** the basis is my score and a series has no scored main-line entry
- **THEN** it is omitted from the strip

#### Scenario: The same series under MAL's score
- **WHEN** I then switch the basis to MAL's score and that series' main line does carry MAL scores
- **THEN** it appears in the strip

#### Scenario: A main line with no MAL score at all
- **WHEN** the basis is MAL's score and a series' main line is entirely unscored by MAL
- **THEN** it is omitted from the strip

### Requirement: Top series ranking basis is restored on back-navigation
The selected ranking basis SHALL behave like the existing profile filters: it SHALL reset to its default on a fresh visit to the profile page, and SHALL be restored when returning to the page by back-navigation. It SHALL be independent of the My top anime and Most rewatched media-type filters.

#### Scenario: Basis resets on a fresh visit
- **WHEN** I set the basis to MAL's score, navigate away, and later open the profile page fresh
- **THEN** the basis is back to my score

#### Scenario: Basis is restored going back
- **WHEN** I set the basis to MAL's score, open a series from the strip, and press back
- **THEN** the strip is still ranked by MAL's score

#### Scenario: Independent of the other filters
- **WHEN** I change the Top series basis
- **THEN** the My top anime and Most rewatched media-type filters are unchanged

### Requirement: Top series single-entry filter
Top series SHALL offer a control that hides every series whose **main line** holds a single entry, and shows them again when it is switched off. The control SHALL default to showing them, so the section lists the same series it lists today until the control is used.

What counts SHALL be the number of main-line entries, not the series' total member count: a series with one main-line entry plus any number of extras — OVAs, specials, side stories — SHALL count as single-entry and SHALL be hidden while the filter is on. A series with two or more main-line entries SHALL remain listed regardless of how many extras it has.

A main-line entry that has been announced but has not yet aired a single episode SHALL NOT count toward that total: a series is multi-entry only once a second main-line entry has actually started airing, not merely once it has been confirmed.

The control SHALL narrow which series are listed and SHALL NOT change the order of those that remain, nor what any tile shows.

The control SHALL be independent of the ranking basis: switching one SHALL NOT reset the other, and a series SHALL be listed only when it survives both — it has a value under the selected basis and, while the filter is on, more than one main-line entry.

The control SHALL make its current state apparent without requiring the user to compare the strip before and after pressing it.

#### Scenario: Default lists single-entry series
- **WHEN** I open the profile page without having used the control
- **THEN** Top series lists every eligible series, single-entry ones included, exactly as it does today

#### Scenario: Hiding single-entry series
- **WHEN** I switch the filter on
- **THEN** every series whose main line holds a single entry disappears from the strip, and the multi-entry series remain in the same relative order

#### Scenario: Extras do not make a series multi-entry
- **WHEN** the filter is on and a series has one main-line entry plus several extras in my list
- **THEN** that series is hidden, because only its main-line count is considered

#### Scenario: A franchise with extras stays listed
- **WHEN** the filter is on and a series has three main-line seasons plus extras
- **THEN** that series remains in the strip

#### Scenario: An announced-but-unaired sequel doesn't make a series multi-entry
- **WHEN** the filter is on and a series has one main-line entry that has aired plus a second main-line entry that's announced but hasn't aired a single episode
- **THEN** that series is hidden, because the second entry doesn't count until it starts airing

#### Scenario: Showing them again
- **WHEN** I switch the filter back off
- **THEN** the single-entry series reappear in the strip in their ranked positions

#### Scenario: Filtering is immediate
- **WHEN** I switch the filter
- **THEN** the strip updates immediately without reloading the page's data and without collapsing

#### Scenario: Composing with the ranking basis
- **WHEN** the filter is on and I switch the ranking basis to MAL's score
- **THEN** the strip reorders by MAL's main-series average and single-entry series stay hidden

#### Scenario: The control shows its state
- **WHEN** the filter is on
- **THEN** the control reads as active rather than looking identical to its off state

### Requirement: Top series single-entry filter is restored on back-navigation
The single-entry filter SHALL behave like the Top series ranking basis: it SHALL reset to its default — single-entry series shown — on a fresh visit to the profile page, and SHALL be restored when returning to the page by back-navigation.

#### Scenario: Filter resets on a fresh visit
- **WHEN** I switch the filter on, navigate away, and later open the profile page fresh
- **THEN** single-entry series are listed again

#### Scenario: Filter is restored going back
- **WHEN** I switch the filter on, open a series from the strip, and press back
- **THEN** single-entry series are still hidden

#### Scenario: Independent of the ranking basis
- **WHEN** I switch the filter on and then change the ranking basis
- **THEN** the filter is still on, and switching the filter afterwards leaves the basis where I set it

### Requirement: Top series empty and incomplete states
When no series is eligible, the section SHALL say so rather than rendering an empty strip.

Because a series is only known once it has been built, the section SHALL disclose that its coverage grows over time, and its empty state SHALL point at the Settings action that builds every series from my list (see the `series-page` capability) rather than leaving the absence unexplained.

Reading the section SHALL never block on building a series and SHALL never fail because a series is missing — a series not yet built is simply not listed yet.

When series are eligible but the single-entry filter leaves none to show, the section SHALL say that the filter is what emptied it, so the state reads as a filter effect rather than as missing data, and SHALL NOT point at the Settings build action.

#### Scenario: Nothing to rank yet
- **WHEN** no series with a member in my list has been built
- **THEN** the section explains that series are still being discovered and names the Settings action that builds them all, instead of showing an empty strip

#### Scenario: Nothing rankable under the selected basis
- **WHEN** series are eligible but none has a value under the selected basis
- **THEN** the section says so for that basis rather than showing an empty strip

#### Scenario: Reading never waits on a build
- **WHEN** I open the profile page while series are still being built in the background
- **THEN** the section renders immediately with whatever series are already known

#### Scenario: The filter hid everything
- **WHEN** the single-entry filter is on and every series rankable under the selected basis has a single main-line entry
- **THEN** the section says the filter left nothing to show, rather than showing an empty strip or blaming missing series data

### Requirement: All-list episode progress
The profile page SHALL show a progress bar reporting how many episodes I have watched against how many episodes the anime in my list hold in total, so the `Episodes` stat has something to be measured against. It SHALL be rendered with the app's existing episode progress-bar treatment — a filled track with a `watched / total` label — and SHALL be read-only: it SHALL offer no increment control and no editable watched field, since it summarises the list rather than any one entry.

The figure SHALL be computed from local data with no live API call, over every list entry **except dropped entries and not-yet-aired entries with nothing else to resolve a total from** (see below), with these rules:

- A **dropped** entry SHALL be excluded from both sides of the figure. A show I abandoned is not outstanding work and does not belong in a measure of how much of my list is left to watch. Every other status SHALL count, Plan to watch included.
- Every media type SHALL count, `movie` and `music` included — a film with one episode contributes one episode to the total, and one to the watched side once I have watched it.
- An entry's **total** SHALL be its anime's published total episode count where one exists. Where none exists, it SHALL be the anime's aired-so-far episode count as derived from stored airing data, when that count is known and greater than zero — a still-airing show has a knowable number of episodes released to watch, even without a published run length.
- An entry with **neither** a published total nor a known non-zero aired count, whose anime has **not started airing**, has nothing to progress against yet: it SHALL be excluded from both sides of the figure and SHALL NOT be counted toward or listed among the unresolved entries. This is a normal state, not a data gap, and gets the same treatment as a dropped entry.
- An entry with **neither** a published total nor a known non-zero aired count, whose anime **has** started airing (or has finished airing), SHALL be **unresolved**: it SHALL be excluded from both sides of the figure but SHALL be counted and individually listed (see below), since this reflects a genuine gap in the app's data rather than a show that simply hasn't aired anything yet. The system SHALL NOT estimate a total for either case.
- An entry's contribution to the watched side SHALL be its episodes watched clamped to its own total as resolved above, so a stored over-count can never push the bar past 100%.
- Rewatches SHALL NOT multiply an entry's contribution: an entry contributes at most its resolved total however many times I have rewatched it. Unlike the `Episodes` stat, this bar measures coverage of my list, which a repeat viewing does not extend.

The section SHALL state how many entries are unresolved, so entries the bar cannot account for are visible rather than silently dropped. When no entry is unresolved, the section SHALL NOT show that note. The section SHALL NOT report how many entries the figure covers out of my total entry count.

When at least one entry is unresolved, the section SHALL offer a control that opens a list naming every unresolved entry individually — each identified well enough (at minimum, its title and a link to its own detail page) that I can see exactly which anime the count covers, not just how many. This control SHALL NOT be shown when no entry is unresolved.

The rendered total SHALL be the resolved episode total as a plain number. It SHALL NOT be suffixed with `+` or any other marker suggesting the total is a lower bound, since every counted entry has a resolved total and the unresolved ones are reported separately.

When no entry in my list resolves to a total episode count, the section SHALL say so plainly rather than render a bar against a zero total or a percentage that is not a number.

#### Scenario: Progress across the whole list
- **WHEN** the profile page loads
- **THEN** it shows a progress bar whose label reads my total episodes watched against the total episodes of the anime in my list, computed from local data

#### Scenario: An unknown total falls back to the aired count
- **WHEN** my list holds a currently-airing anime with no published total episode count whose stored airing data shows eight episodes have aired, and I have watched five
- **THEN** the bar counts eight toward the total and five toward the watched side

#### Scenario: Neither total nor aired count is known, but the anime is airing
- **WHEN** my list holds a currently-airing anime with no published total episode count and no stored airing data
- **THEN** it contributes to neither side of the figure and is counted and listed as unresolved

#### Scenario: A not-yet-aired anime with no published length is excluded, not unresolved
- **WHEN** my list holds an announced anime with no published total episode count and no episodes aired yet
- **THEN** it contributes to neither side of the figure, and is neither counted nor listed among the unresolved entries

#### Scenario: A not-yet-aired anime with a published length still counts
- **WHEN** my list holds an announced anime with a published total episode count that has not started airing yet
- **THEN** its published total is counted normally, exactly as an airing or finished anime's would be

#### Scenario: Unresolved entries are reported
- **WHEN** some of my entries are unresolved
- **THEN** the section states how many, rather than how many entries the figure covers out of my total

#### Scenario: Nothing is unresolved
- **WHEN** every entry in my list resolves to a total episode count, or is excluded as not-yet-aired
- **THEN** the section shows no unresolved note

#### Scenario: Unresolved entries can be opened as a list
- **WHEN** some of my entries are unresolved
- **THEN** the section offers a control that, when used, shows every one of those entries by name, each linking to its own detail page

#### Scenario: No list control when nothing is unresolved
- **WHEN** no entry is unresolved
- **THEN** the section offers no control to open an unresolved-entries list

#### Scenario: The total is not marked as a lower bound
- **WHEN** the bar renders with some totals supplied by aired counts
- **THEN** its total reads as a plain number with no `+` appended

#### Scenario: Dropped entries are excluded
- **WHEN** my list holds a dropped anime with a published episode count
- **THEN** neither its watched episodes nor its total are counted in the bar

#### Scenario: Plan-to-watch counts toward the total
- **WHEN** my list holds a plan-to-watch anime with a published episode count
- **THEN** its episodes are counted in the total and contribute nothing to the watched side, so the bar reflects it as unwatched

#### Scenario: Films count
- **WHEN** my list holds a film I have watched
- **THEN** it contributes one episode to both the watched side and the total

#### Scenario: An over-count cannot exceed the total
- **WHEN** an entry's stored episodes watched exceeds its resolved total episode count
- **THEN** it contributes only that total to the watched side and the bar does not exceed 100%

#### Scenario: Rewatches do not inflate progress
- **WHEN** I have rewatched an anime several times
- **THEN** it contributes at most its resolved total to the watched side, exactly as a single completed watch does

#### Scenario: The bar is read-only
- **WHEN** I click or focus the progress bar's label
- **THEN** nothing becomes editable and no increment control is offered

#### Scenario: Nothing to measure
- **WHEN** no anime in my list resolves to a total episode count
- **THEN** the section says so rather than rendering a bar against a zero total

### Requirement: The episode-progress figure is set as a headline figure
The `watched / total` figure on the profile page's episode-progress bar SHALL be set at a larger type size than the same figure carries on cards and list rows, so the summary of a whole list reads as the headline of its section rather than as row furniture. It SHALL remain the shared episode progress-bar treatment in every other respect — the same filled track, the same label content, the same read-only behaviour — and no other episode progress bar in the app SHALL change size.

The larger size SHALL NOT change the section's layout beyond the label's own height: the bar SHALL still span the section's width and the unresolved-entries note beneath it SHALL be unmoved.

#### Scenario: The profile's figure is larger
- **WHEN** I look at the profile page's Episode progress section
- **THEN** its watched-against-total figure is set noticeably larger than the figure on a currently-watching card

#### Scenario: Other progress bars are unchanged
- **WHEN** I look at a currently-watching card, a my-list row, or an anime detail page after this change
- **THEN** its episode progress figure is the size it has always been

#### Scenario: The section does not otherwise change
- **WHEN** the profile page's episode-progress section is shown with unresolved entries
- **THEN** the bar spans the section as before and the unresolved note sits where it did

### Requirement: Favourite seasons and years
The profile page SHALL show a **Favourite seasons** ranking and a **Favourite years** ranking, covering my whole list rather than any selected period, so my best-liked seasons and years are reachable without opening a recap.

Each anime SHALL be attributed to the season and the year its air date falls in — the same air-date attribution the recap page's rankings use. An entry whose anime has no air date SHALL be attributed to neither and SHALL be excluded from both rankings. Only seasons and years holding at least one anime I scored SHALL be ranked; a season or year I scored nothing from SHALL be omitted rather than shown as empty or zero.

Both rankings SHALL rank on the same Bayesian weighted average the recap page's season and year rankings use, with the same trust thresholds — 5 for a season, 20 for a year — and the same global mean `C`, my mean score across every scored entry in my list. A season's or year's weighted score on the profile page SHALL therefore equal the score a recap covering that same season or year reports for it, so the two surfaces can never disagree.

Ties SHALL be resolved by the identical sequence the recap page's rankings apply, as set out in the `list-recaps` capability: the weighted score at full precision rather than at the two decimals displayed, so a higher score is never overruled; then, only for scores that are exactly equal, the number of scored anime; then a score-by-score comparison from 10 down to 1 where the group holding more anime at the highest differing score ranks first; and finally recency — the newer season or year ahead of the older. The order SHALL be stable across reloads, and a season's or year's rank relative to another SHALL be the same here as on a recap covering both.

Each ranked row SHALL show its rank, its name (season and year, or the year), how many of my anime it was computed over, and the weighted score it was ranked on. **Every** ranked row SHALL additionally show the posters of three of my anime from that season or year — fewer when fewer qualify — rather than only the top-ranked row, so each row is illustrated, both inline and in the overlay. Which three, and in what order, SHALL follow the rule the `list-recaps` capability's "Top three posters for every ranked season and year" sets out: my ranking's order, best-ranked leftmost, so two anime I scored the same are separated by where I placed them rather than by their titles. Following a row SHALL open the recap for that season or year. Rows SHALL follow the shared ranking-row height the `list-recaps` capability defines, so a row with fewer posters than another still stands the same height.

At most five rows SHALL be shown in each ranking. When more than five qualify, that ranking SHALL offer a control that opens an overlay listing every qualifying season or year in rank order — the same overlay treatment the recap page's rankings use.

The two rankings SHALL be presented alongside one another rather than stacked, so my best season and best year are comparable at a glance. On a display too narrow for two columns, they SHALL stack with Favourite seasons first.

When neither ranking has a single qualifying group — nothing I scored carries an air date — the section SHALL say so plainly rather than render two empty rankings.

#### Scenario: Ranking my seasons and years
- **WHEN** the profile page loads and I have scored anime across several seasons and years
- **THEN** it shows a Favourite seasons ranking and a Favourite years ranking, each best first

#### Scenario: Whole list, not a period
- **WHEN** I have scored anime that aired in 2009 and in 2023
- **THEN** both years are candidates for the Favourite years ranking, with no period selection needed

#### Scenario: Agreement with the recap page
- **WHEN** I compare fall 2019's weighted score in the profile's Favourite seasons ranking against the score a recap covering fall 2019 reports
- **THEN** the two are equal

#### Scenario: A thin season is pulled toward the global mean
- **WHEN** one season holds a single anime I scored 10 and another holds eight anime averaging 8.5, and my global mean is 7.4
- **THEN** the eight-anime season ranks above the single-anime one

#### Scenario: Equal scores resolved by coverage then by score
- **WHEN** two years' weighted scores are exactly equal, they were computed over the same number of scored anime, and one holds two 10s where the other holds one
- **THEN** the year with two 10s ranks first, exactly as it would on a recap covering both years

#### Scenario: A higher score is never overruled
- **WHEN** two years both display a weighted score of 7.80 but one's underlying score is higher past the second decimal
- **THEN** the higher-scoring year ranks first, here and on a recap covering both

#### Scenario: Wholly identical years fall back to the newer
- **WHEN** two years' weighted scores are exactly equal, they hold the same number of scored anime, and they hold the same number of my anime at every score from 10 down to 1
- **THEN** the newer year is ranked ahead of the older one

#### Scenario: Groups I scored nothing from are omitted
- **WHEN** I have watched anime that aired in 2013 but scored none of them
- **THEN** 2013 appears in neither the Favourite years ranking nor its overlay

#### Scenario: Anime with no air date are excluded
- **WHEN** my list holds a scored anime with no air date
- **THEN** it contributes to no season and no year in either ranking

#### Scenario: Five at a time with an overlay for the rest
- **WHEN** more than five years qualify
- **THEN** the five best-ranked are shown and a control opens an overlay listing every qualifying year in rank order

#### Scenario: Five or fewer
- **WHEN** only four seasons qualify
- **THEN** all four are shown and no overlay control is offered

#### Scenario: Every row is illustrated
- **WHEN** a ranking shows five rows
- **THEN** each of the five shows the posters of three of my anime from that season or year

#### Scenario: My ranking picks and orders the three
- **WHEN** a ranked year holds five anime I scored 9 and none higher
- **THEN** the three I rank highest are shown, best-ranked leftmost, rather than the three whose titles come first alphabetically

#### Scenario: The overlay is illustrated too
- **WHEN** I open the overlay listing every qualifying season or year
- **THEN** each of its rows shows posters under the same rule as the inline rows

#### Scenario: Fewer than three posters available
- **WHEN** a ranked season holds only two scored anime
- **THEN** that row shows two posters, and the row stands at the same height as the rows showing three

#### Scenario: Opening a ranked row
- **WHEN** I follow a row of either ranking
- **THEN** the recap page opens on that season or that year

#### Scenario: The two rankings sit side by side
- **WHEN** the profile page is shown on a wide display
- **THEN** Favourite seasons and Favourite years are presented next to each other rather than one above the other

#### Scenario: Narrow display stacks them
- **WHEN** the profile page is shown on a display too narrow for two columns
- **THEN** Favourite seasons is shown first and Favourite years below it

#### Scenario: Nothing to rank
- **WHEN** nothing I have scored carries an air date
- **THEN** the section says so rather than rendering two empty rankings

### Requirement: Favourites rankings can be ranked by how many of a score they hold
Each of the profile page's two favourites rankings — **Favourite years** and **Favourite seasons** — SHALL offer a control that changes what the ranking ranks on, so a year or season can be found by how many anime of a given score it holds rather than only by its weighted average.

The control SHALL sit directly beneath its ranking's title and above its rows, and SHALL belong to that ranking alone: the two rankings SHALL hold independent selections, and changing one SHALL NOT change the other. It SHALL read as **All**, followed by the words **With most:** and one button per score, ordered **10 down to 1**.

A score SHALL be offered only when at least one group in that ranking holds at least one anime I scored it, so no offered button can produce an empty ranking. **All** SHALL always be offered and SHALL be the selection on a fresh visit; under it the ranking is exactly what it is today — the Bayesian weighted average, its tie-break sequence, its five-row cap, its "See all" overlay, and its posters, all unchanged.

Selecting a score N SHALL re-rank that ranking by **how many anime I scored N** each group holds, most first, and SHALL omit every group holding none. Groups tied on that count SHALL be ordered by the identical sequence the unfiltered ranking applies — the weighted score at full precision, then the number of scored anime, then a score-by-score comparison from 10 down to 1, then the newer group ahead of the older — so the order is stable across reloads and never arbitrary. Ranks SHALL be numbered from one down the re-ranked order rather than carrying over the positions the groups held under **All**.

A row's name, its height, and where following it leads SHALL be unchanged under a score selection. Its supporting figures SHALL name the count it was ranked on alongside how many of my anime the group was computed over, so the row states why it placed where it did. Its **posters** SHALL be drawn from the selected score alone — the three anime of that score the group holds that I rank highest, best-ranked leftmost, per the `list-recaps` capability's "Top three posters for every ranked season and year" — so a row's illustration agrees with the figure it was ranked on rather than showing the group's highest-scored anime regardless of the selection. Returning to **All** SHALL return every row's posters to the group's three best overall.

The five-row cap and the "See all" overlay SHALL apply to the re-ranked ranking: at most five rows are shown, the overlay lists every group the selection keeps, in the selected order, and the overlay SHALL name the selection it is showing. A selection that leaves five or fewer groups SHALL offer no overlay control, exactly as an unfiltered ranking of that size does not.

Each score button SHALL wear the colour its score carries in the recap page's rating distribution — the same score-tier colours, so a score is the same colour wherever it is shown — on pointer hover, on keyboard focus, and while it is the selection. At rest and unselected it SHALL be neutral. The **All** button SHALL wear its section's own colour family — the year family in Favourite years, the season family in Favourite seasons — rather than a score tier, since it names no score. No state SHALL change a button's size, so the control row SHALL NOT reflow as the pointer moves along it.

Every button in the control SHALL occupy **the same width**, whatever its label: a one-digit score, a two-digit score, and **All** SHALL be equally wide, with each label centred in its button, so the strip reads as an even row rather than stepping in and out with the digit count. The shared width SHALL be a floor rather than a fixed size, so a label that would not fit is never clipped.

A selection SHALL be part of the profile page's restorable state per the `page-state-restoration` capability: it SHALL be restored on a back/forward navigation and SHALL open on **All** on a fresh visit. A restored selection naming a score the reloaded ranking no longer offers SHALL fall back to **All** rather than showing an empty ranking.

The same control SHALL be offered by the recap page's own season and year rankings, per the `list-recaps` capability, with the presentation rules above applying identically there. Only the mechanism that carries the selection differs: the recap page carries its selections in the page URL alongside its period, where this page carries its own in restorable state.

#### Scenario: The control is offered under each title
- **WHEN** the profile page shows Favourite years and Favourite seasons
- **THEN** each ranking shows an All button and a "With most:" row of score buttons between its title and its rows

#### Scenario: All is the default and changes nothing
- **WHEN** I open the profile page without touching the control
- **THEN** All is selected and both rankings are ranked exactly as they are by weighted average

#### Scenario: Ranking by how many 10s
- **WHEN** I select 10 in Favourite years
- **THEN** the years are re-ranked with the year holding the most anime I scored 10 first, numbered from one

#### Scenario: Groups holding none of the score are dropped
- **WHEN** I select 9 and one of the years shown under All holds no anime I scored 9
- **THEN** that year is not shown in the ranking or in its "See all" overlay

#### Scenario: A tie on the count falls back to the ranking's own order
- **WHEN** two seasons each hold three anime I scored 8 and 8 is selected
- **THEN** they are ordered by weighted score, then scored count, then score-by-score, then the newer first — the same sequence All uses

#### Scenario: Only scores that exist are offered
- **WHEN** nothing in my list is scored 1
- **THEN** no 1 button is offered in either ranking

#### Scenario: The two rankings are independent
- **WHEN** I select 10 in Favourite years
- **THEN** Favourite seasons is still on All

#### Scenario: A row states what it was ranked on
- **WHEN** a year is shown under a selection of 10
- **THEN** its row states how many anime I scored 10 it holds, alongside how many of my anime it was computed over

#### Scenario: A row is illustrated by the selected score
- **WHEN** I select 8 in Favourite years and a shown year holds anime I scored 10, 9, and 8
- **THEN** that row's posters are the 8s I rank highest, not its 10s and 9s

#### Scenario: All restores the group's own best
- **WHEN** I return Favourite years to All
- **THEN** every row shows the three anime I rank highest from that year again, whatever their scores

#### Scenario: The overlay follows the selection
- **WHEN** I select 8 in Favourite seasons and open its "See all" overlay
- **THEN** the overlay lists every season holding an 8, in the selected order, names the selection it is showing, and illustrates each row with that season's 8s

#### Scenario: No overlay when the selection leaves few groups
- **WHEN** a selected score is held by only three years
- **THEN** all three are shown and no "See all" control is offered

#### Scenario: Buttons wear their score's colour
- **WHEN** I hover the 10 button, then the 9 button, then the 8 button
- **THEN** each highlights in the colour its score carries in the recap page's rating distribution

#### Scenario: The selected button keeps its colour
- **WHEN** 9 is selected and the pointer is elsewhere
- **THEN** the 9 button still reads as the selection, drawn in its score's colour

#### Scenario: All wears its section's family
- **WHEN** I hover the All button in Favourite seasons
- **THEN** it highlights in the season family's colour rather than in a score tier's

#### Scenario: One-digit and two-digit buttons match
- **WHEN** a ranking offers buttons for 10 down to 1
- **THEN** the 1 button is exactly as wide as the 10 button, and both are as wide as All

#### Scenario: Labels are centred
- **WHEN** I look along the row of score buttons
- **THEN** each numeral sits centred in its button rather than against one edge

#### Scenario: The control row does not reflow
- **WHEN** I move the pointer along a ranking's score buttons
- **THEN** none of them changes size and nothing beside them moves

#### Scenario: A selection survives back-navigation
- **WHEN** I select 10 in Favourite years, follow a row into a recap, and go back
- **THEN** 10 is still selected and the ranking is still ranked by it

#### Scenario: A fresh visit opens on All
- **WHEN** I select 10 and then reach the profile page again from the navigation bar
- **THEN** both rankings are back on All

### Requirement: The rankings "See all" overlay is sized by its rows and carries no close control
The overlay a favourites ranking's "See all" control opens SHALL show **eight whole rows** when it opens, with no part of a ninth row visible beneath them and no row clipped part-way, so the list ends where a row ends rather than mid-poster. Its height SHALL be derived from the height of a row rather than from the viewport, so the same eight whole rows are shown at any window height; the rest of the ranking SHALL be reached by scrolling the list, which SHALL keep every row it holds. A ranking of fewer than eight rows SHALL show what it has without reserving the height of eight.

The overlay SHALL offer **no close control of its own**: neither a labelled Close button beneath the list nor an icon control in its corner. Its dismissals SHALL be the ones every overlay in the app already provides — Esc and a click outside it — and those SHALL be unchanged. The list SHALL therefore be the last thing in the overlay, with nothing below it, so the whole box is the ranking.

Nothing else about the overlay SHALL change: its rows, their order, their posters, their family colour and banding, and where following a row leads SHALL be exactly as they are.

#### Scenario: Eight whole rows on open
- **WHEN** I open the "See all" overlay on a ranking with more than eight qualifying rows
- **THEN** eight rows are visible in full, no part of a ninth is shown, and no row is cut through the middle

#### Scenario: Whole rows at any window height
- **WHEN** I open the same overlay in a taller or a shorter window
- **THEN** it still shows eight whole rows rather than a fraction of a row

#### Scenario: The rest is still reachable
- **WHEN** the ranking holds more rows than the eight shown
- **THEN** the list scrolls to the rest of them

#### Scenario: Fewer than eight rows
- **WHEN** the ranking holds six qualifying rows
- **THEN** the overlay shows those six without reserving the height of eight

#### Scenario: No close control is offered
- **WHEN** the overlay is open
- **THEN** no Close button is shown beneath the list and no ✕ is shown in its corner, and the list is the last thing in the overlay

#### Scenario: Esc and click-outside close it
- **WHEN** the overlay is open and I press Esc or click outside it
- **THEN** the overlay closes

#### Scenario: The rows are otherwise unchanged
- **WHEN** I open the "See all" overlay from **Favourite years**
- **THEN** its rows, their order, their posters, and its banded title in the year family are exactly as they were

### Requirement: Profile section titles are banded by family
The profile page SHALL present the titles of the sections whose subject a colour family names as bands, using the banded-title treatment the `section-colour-language` capability defines:

- **They liked it, I didn't** SHALL carry the MAL family and **I liked it, they didn't** the mine family — the same two colours the recap's "MAL liked it more" and "I liked it more" labels carry, so the pair of lists reads as one claim stated in two directions, and which list is about whose opinion is readable before either title is;
- **Favourite years** SHALL carry the year family and **Favourite seasons** the season family, matching the families the recap page's year-level and season-level rankings carry, so the same ranking of the same groups is the same colour on both pages.

Each band SHALL span the full width of the list it heads, from that list's left edge to its right edge, with its title centred on it.

The page's remaining section titles SHALL stay plain and unbanded, since none of them is about a season, a year, or a disagreement.

Banding SHALL change nothing else about these four sections: the divergence lists' membership, ordering, scrolling, and empty states, and the favourites rankings' rows, posters, five-row cap, "See all" overlay, and side-by-side layout, SHALL be exactly as they are unbanded.

#### Scenario: The divergence pair is colour-coded by whose opinion it is
- **WHEN** the profile page shows its two opinion-divergence lists
- **THEN** "They liked it, I didn't" is headed by a band filled in the MAL family's blue and "I liked it, they didn't" by one filled in the mine family's purple

#### Scenario: A band spans its list
- **WHEN** I look at the **Favourite seasons** section
- **THEN** its band runs the full width of the ranking beneath it, with the title centred on the band

#### Scenario: The divergence bands match the recap's labels
- **WHEN** I compare the profile's "They liked it, I didn't" band against a recap hot take labelled "MAL liked it more"
- **THEN** the two carry the same colour

#### Scenario: Favourites match the recap's rankings
- **WHEN** I compare the profile's **Favourite years** and **Favourite seasons** bands against a recap's **Year ranking** and **Season ranking** bands
- **THEN** the year-level titles carry one family on both pages and the season-level titles the other

#### Scenario: Plain titles stay plain
- **WHEN** the profile renders its stats, rating-distribution, latest-updates, and top-anime headings
- **THEN** each is drawn as a plain heading with no band behind it

#### Scenario: A banded section behaves identically
- **WHEN** more than five years qualify for **Favourite years**
- **THEN** its "See all" control opens the same overlay with the same rows in the same order as it does unbanded

### Requirement: Profile rows highlight in their section's family
Every row of a profile section that carries a colour family SHALL take that family's colour in place of the app's accent in the standard hover treatment, on pointer hover and keyboard focus alike, per the `section-colour-language` capability:

- a row of **They liked it, I didn't** SHALL highlight in the MAL family's blue and a row of **I liked it, they didn't** in the mine family's purple, so a row's highlight says whose opinion the list it sits in is about;
- a row of **Favourite years** SHALL highlight in the year family and a row of **Favourite seasons** in the season family;
- the "See all" overlay a favourites ranking opens SHALL carry the same family as the ranking that opened it, banding its own title and highlighting its rows in that family.

Rows of the page's other lists — latest updates, top anime, and the rating distribution — SHALL keep the accent highlight they have today.

Only the highlight's colour SHALL change: a row's size, position, resting appearance, and behaviour on click SHALL be exactly what they are today.

#### Scenario: A divergence row highlights by whose opinion it is
- **WHEN** I hover a row of "They liked it, I didn't" and then a row of "I liked it, they didn't"
- **THEN** the first highlights blue and the second purple

#### Scenario: A favourites row highlights in its family
- **WHEN** I hover a row of **Favourite seasons**
- **THEN** it highlights in the season family's colour rather than the app's accent

#### Scenario: The overlay keeps the ranking's family
- **WHEN** I open the "See all" overlay from **Favourite years**
- **THEN** its title is banded in the year family and its rows highlight in that same family

#### Scenario: Unfamilied lists are unchanged
- **WHEN** I hover a row of the latest-updates feed
- **THEN** it takes the app's accent highlight exactly as it does today

