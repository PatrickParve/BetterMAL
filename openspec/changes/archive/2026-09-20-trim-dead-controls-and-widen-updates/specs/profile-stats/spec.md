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
