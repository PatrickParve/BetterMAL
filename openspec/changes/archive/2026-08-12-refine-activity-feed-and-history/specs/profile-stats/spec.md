## MODIFIED Requirements

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

### Requirement: Full edit-history overlay
The system SHALL provide, in the top-right corner of the "Latest updates" box, a control that opens a full history of every edit as an overlay on top of the page, which closes on Esc or a click outside it.

Each history row SHALL show the anime's poster picture alongside its title, in the same style as the rows of the "Latest updates" box, and SHALL show the anime's English title when one is available, falling back to the default title otherwise. A row's title SHALL be truncated to at most two lines, with the full title available on hover.

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
