## MODIFIED Requirements

### Requirement: Series score averages
The series page SHALL show, for each of MAL's score and mine, an average across main-line entries and an average across all entries, rendered to two decimals (e.g. `8.42`):

- the MAL average, computed as the unweighted mean of the entries that have a MAL score;
- my average, computed as the unweighted mean of my scores on entries I have scored, where a score of 0 means unscored and is excluded.

Averages SHALL NOT be weighted by episode count, so a movie counts the same as a season. When no entry in a group has a score, the page SHALL show "No score" rather than a zero.

A score chip SHALL show its average and its label and nothing else. It SHALL NOT append the count of entries the average was computed over — the `N of M scored` suffix — to either the MAL chips or my chips, since the per-entry scores it summarises are already listed in full in the watch order below it and the suffix crowds the figure the chip exists to show. The count SHALL NOT reappear as a tooltip, a title attribute, or any other rendered form of the same figure.

When a series has no extras, the two across-all-entries averages SHALL NOT be rendered at all, since they are computed over exactly the same member set as the main-line averages and would duplicate them.

Every MAL average SHALL honour the global hide-scores toggle exactly as MAL scores do elsewhere. An average SHALL be shown in full rather than blurred only when, in this order of precedence:

1. every main-line entry that has finished airing is marked **Completed or Dropped** in my list, **and no main-line entry is currently airing** — in which case both MAL averages SHALL be shown, unconditionally; or
2. every entry in that average's group that has finished airing is marked **Completed or Dropped** in my list, **and** no member of the series is currently airing.

Completed and Dropped SHALL count identically in both rules, on the same grounds as the `score-visibility` capability's always-show setting: a finished-airing entry I have dropped is one I have settled, so it can no longer be spoiled by the group's average. An entry that has finished airing and is **not in my list at all** SHALL NOT satisfy either rule — the rules ask what I decided about an entry, and an absent entry carries no decision.

A member of the series that is currently airing SHALL therefore suppress the reveal of both MAL averages, whether or not I have scored it — unless it's a spin-off/extra airing after the main line has otherwise completely finished, in which case rule 1 still applies. A main-line entry that is itself currently airing always suppresses rule 1, since the main line has not actually finished in that case — only rule 2 can apply, and it will not, since a currently-airing member fails its own "no member of the series is currently airing" condition too. Otherwise the average SHALL remain blurred behind its reveal control.

#### Scenario: Both MAL averages shown
- **WHEN** I open a series with four main-line entries and three extras
- **THEN** the page shows one MAL average over the four main-line entries and one over all seven

#### Scenario: My averages exclude unscored entries
- **WHEN** I have scored three of a series' five main-line entries
- **THEN** my main-series average is the mean of those three scores, with the two unscored entries excluded from it

#### Scenario: No scored-count suffix on any chip
- **WHEN** I open a series page and look at the MAL and Mine score chips
- **THEN** each shows only its label and its average, with no "N of M scored" count appended

#### Scenario: Unscored series
- **WHEN** I have scored none of a series' entries
- **THEN** my averages read "No score" rather than 0.00

#### Scenario: Hidden MAL averages
- **WHEN** the hide-scores toggle is on and I have neither completed nor dropped every finished-airing entry of the series
- **THEN** the MAL averages are blurred like every other MAL score, with the value absent from the rendered output

#### Scenario: A dropped main-line entry does not suppress the reveal
- **WHEN** the hide-scores toggle is on, the always-show setting is on, and a series' main line holds three finished-airing entries of which I completed two and dropped the third, with nothing currently airing
- **THEN** both MAL averages are shown in full, because a dropped entry counts as settled exactly like a completed one

#### Scenario: A finished-airing entry missing from my list still suppresses the reveal
- **WHEN** the hide-scores toggle is on and a series' main line holds a finished-airing entry that is not in my list at all
- **THEN** the MAL averages stay blurred, since that entry is neither completed nor dropped

#### Scenario: A series with no extras shows only the main-series averages
- **WHEN** I open a series where every member is main line
- **THEN** the across-all-entries averages are not rendered, and only the main-series MAL and my averages are shown

#### Scenario: An airing main-line member suppresses the reveal
- **WHEN** the hide-scores toggle is on, I have completed or dropped every main-line entry that has finished airing, and the newest main-line season is currently airing
- **THEN** both MAL averages stay blurred, even though I'm caught up on everything that's aired so far

#### Scenario: Settling the main series always reveals both MAL averages
- **WHEN** the hide-scores toggle is on, I have completed or dropped every main-line entry that has finished airing, and a spin-off special is currently airing
- **THEN** both the main-series and the across-all-entries MAL averages are shown in full
