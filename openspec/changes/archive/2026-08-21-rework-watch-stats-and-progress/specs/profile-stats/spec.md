## MODIFIED Requirements

### Requirement: Anime stats computed from local data
The system SHALL show anime stats computed entirely from the local database: Days, Mean Score, Watching, Completed, On-Hold, Dropped, Plan to Watch, Total Entries, Rewatched, Episodes, and Movies.

**Episodes** SHALL be the total number of episodes I have watched, counting rewatches. An entry SHALL contribute its episodes watched plus one further complete run of the anime for every recorded rewatch, so an entry with a rewatch count of one and a published total of twelve contributes twenty-four. The complete run SHALL be measured by the anime's published total episode count; when no total is published, it SHALL be measured by the entry's own episodes watched, since that is the only length the app knows.

**Episodes** SHALL exclude entries whose media type is `movie` or `music`, which are not episodic and would otherwise each add one to a count of episodes. Every other media type — TV, OVA, ONA, special, and unrecognised values — SHALL count. Entries of every status SHALL count, dropped entries included: episodes I watched before abandoning a show are episodes I watched.

**Movies** SHALL count entries whose media type is `movie` and which have at least one episode watched, whatever their status. It SHALL be presented directly below Episodes, so the count Episodes no longer carries is visible beside it. A film rewatched several times SHALL count once — the stat counts films watched, not viewings.

**Days** SHALL be the total runtime of everything I have watched, expressed in days to one decimal. It SHALL be computed per entry as that entry's rewatch-inclusive episode count — the same count Episodes uses — multiplied by that anime's runtime per episode, and summed across **every** entry in my list whatever its media type or status: TV, movies, music, dropped, and in-progress alike. Days therefore covers a wider population than Episodes by design, since a film consumes time even though it contributes no episodes.

An anime's runtime per episode SHALL be its cached average episode duration where one is stored, and SHALL otherwise fall back to the same standing per-episode assumption the recap and series pages use, so no two surfaces report different runtimes for the same anime.

#### Scenario: Rendering stats
- **WHEN** the profile page loads
- **THEN** all listed stat values are computed from the local DB without a live API call

#### Scenario: A rewatch adds a full run to the episode count
- **WHEN** my list holds a completed twelve-episode series with a rewatch count of one
- **THEN** it contributes twenty-four to Episodes

#### Scenario: A rewatch in progress counts the completed runs plus current progress
- **WHEN** my list holds a twelve-episode series with a rewatch count of two whose episodes watched currently reads one
- **THEN** it contributes twenty-five to Episodes

#### Scenario: Rewatch of an anime with no published total
- **WHEN** my list holds an entry with eight episodes watched, a rewatch count of one, and no published total episode count
- **THEN** it contributes sixteen to Episodes

#### Scenario: Movies and music are not episodes
- **WHEN** my list holds a watched film and a watched music video
- **THEN** neither contributes to Episodes

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
- **WHEN** my list holds a twelve-episode series with a cached average episode duration of twenty-three minutes and a film with a cached duration of one hundred and twenty minutes, both fully watched
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
