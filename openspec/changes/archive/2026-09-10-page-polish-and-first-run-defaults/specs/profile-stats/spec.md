## MODIFIED Requirements

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

### Requirement: All-anime score distribution
The system SHALL show a count of anime per score/rating value, the number of anime I have scored, and the overall mean score, rendered as a bar per score value alongside its count and that score's share of all my rated anime.

The number of anime I have scored SHALL be every entry in my list carrying a score of 1–10, whatever its status — the same population the shares are taken over. It SHALL be shown on its own labelled line (e.g. `Scored: 312`) directly above the mean score line, below the bars.

Each bar's length SHALL be that score's count relative to the largest count across all score values, so the most-common score's bar fills the full width of its track and every other bar is drawn in proportion to it. A score with a count of zero SHALL render an empty track.

Each row SHALL show, after its count, that score's share of all my rated anime as a percentage — the count for that score divided by the total number of anime I have rated. A share that rounds to zero but comes from a non-zero count SHALL be shown as less than one percent rather than as zero.

The count and the share SHALL occupy two separate columns of fixed width, each aligned within itself, rather than being run together into one string: every row's count SHALL line up with every other row's count, and every row's share with every other row's share, whatever their number of digits. Neither column SHALL be sized to its content, since a wider value in one row would then steal width from that row's own bar track and distort which bar reads as longest.

When I have rated no anime at all, every track SHALL render empty, the scored count SHALL read zero, and no share SHALL be shown as a nonsensical value.

#### Scenario: Rendering the distribution
- **WHEN** the profile page loads
- **THEN** it shows how many anime have each score value, that score's share as a percentage, the number of anime I have scored, and the overall mean score

#### Scenario: Scored count above the mean
- **WHEN** I have given a score to 312 anime
- **THEN** a line reading "Scored: 312" is shown directly above the mean score line

#### Scenario: Scored count equals the bars' total
- **WHEN** the distribution's rows hold 40, 90, and 182 anime and every other score holds none
- **THEN** the scored count reads 312

#### Scenario: Unscored entries are not counted
- **WHEN** my list holds entries I have not scored
- **THEN** they are not included in the scored count

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
- **THEN** every bar renders as an empty track, the scored count reads zero, and no share is shown as an invalid number
