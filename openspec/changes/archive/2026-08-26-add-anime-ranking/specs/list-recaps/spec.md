## MODIFIED Requirements

### Requirement: Top 10 of the period
Every recap SHALL show the ten highest-ranked anime of the included set, ranked by the selected ranking basis. When the basis is **my score**, entries sharing a score SHALL be ordered by my ranking, best-ranked first, per the `anime-ranking` capability — a tied entry with no rank falling after every ranked entry of that score, and remaining ties broken by title case-insensitively. When the basis is **MAL's score**, ties SHALL be broken by title case-insensitively as before. Either way the order SHALL be stable across reloads. Entries with no score on the selected basis SHALL be ranked below every scored entry rather than treated as zero.  When the included set holds fewer than ten entries, the recap SHALL show all of them.

The ten SHALL be presented in two forms: the first five as the podium described in "The top five are presented as a podium", and the remaining ranks as rows below it. The split SHALL be presentational only — the same ten anime in the same order are shown either way — and the rows SHALL continue the podium's numbering rather than restarting, both in the rank each row shows and in the list semantics exposed to assistive technology.

The score a row shows SHALL carry the same score colour role the podium's cards carry: my score in the mine role, MAL's score in the MAL role, per the `score-presentation` capability. Falling below the podium SHALL change a score's size and its surround, never whose opinion it is — a row's score SHALL NOT revert to neutral text.

#### Scenario: Ten of many
- **WHEN** the included set holds 40 anime
- **THEN** the ten highest-ranked are shown in descending order

#### Scenario: Fewer than ten
- **WHEN** the included set holds six anime
- **THEN** all six are shown

#### Scenario: The ten split into a podium and rows
- **WHEN** a recap shows ten anime
- **THEN** the first five are shown as podium cards and ranks six through ten as rows beneath them, numbered six through ten

#### Scenario: Five or fewer entries
- **WHEN** the included set holds five or fewer anime
- **THEN** they are all shown on the podium and no row list is shown

#### Scenario: My ranking decides a tie at the cut line
- **WHEN** the top 10 is ranked by my score and several anime share the score at the cut line
- **THEN** the one I rank highest takes the slot, and the same order is shown on every reload

#### Scenario: MAL ties still break by title
- **WHEN** the top 10 is ranked by MAL's score and several anime share that score
- **THEN** they are ordered by title, as before, since my ranking says nothing about MAL's opinion

#### Scenario: An unranked entry tied on my score
- **WHEN** the top 10 is ranked by my score and a scored Plan-to-watch anime shares a score with ranked anime
- **THEN** the ranked ones come first and the Plan-to-watch one follows them

#### Scenario: Unscored entries rank last
- **WHEN** the included set holds both scored and unscored anime on the selected basis
- **THEN** every scored anime is ranked above every unscored one

#### Scenario: The score colour survives the cut line
- **WHEN** a recap ranked by my score shows rank five on the podium and rank six as a row
- **THEN** both scores carry the mine role's colour, the row's differing only in the size and surround its density calls for

#### Scenario: A row's score follows the basis
- **WHEN** the top 10 is switched to MAL's score
- **THEN** the scores on ranks six through ten carry the MAL role's colour rather than the mine role's

### Requirement: The score board lays a period out by score
The score board SHALL show ten slots, one per score from 10 down to 1 in that order, each holding the poster art of every included anime I gave that score. Each slot SHALL carry its score numeral and how many anime it holds, so the score of a slot is never conveyed by its colour alone.

The board SHALL cover exactly the set the rating distribution covers: every entry the selected period and time filter include that carries one of my scores, unnarrowed by the top 10's media-type control, and recomputed whenever the period or time filter changes. The number of posters in a slot and the count the matching distribution row reports SHALL always agree.

Within a slot, anime SHALL be ordered by my ranking, best-ranked first, per the `anime-ranking` capability, so a slot reads left to right as my order of preference among the anime of that score. An anime with no rank SHALL be placed after every ranked anime of that slot, ordered by title among other unranked anime. The order SHALL be stable across reloads and across a refresh of the same period. Anime with no score of mine SHALL NOT appear in any slot.

All ten slots SHALL be shown even when a slot holds nothing — an empty slot reports the same "nothing scored this" the distribution's empty track reports, and keeping the ten fixed makes two periods comparable. An empty slot SHALL be identifiable as empty rather than appearing to be still loading.

An anime with no poster art SHALL occupy a placeholder of the same size in its slot, so a slot's count and its number of tiles agree whatever art is available.

#### Scenario: Seeing the tens of a period
- **WHEN** I open the score board for a period in which I scored four anime 10
- **THEN** the 10 slot shows those four anime's posters, and reports a count of four

#### Scenario: A slot reads in my order
- **WHEN** I open the score board for a period whose 9 slot holds anime I have ranked
- **THEN** they are laid out best-ranked first rather than alphabetically

#### Scenario: An unranked anime in a slot
- **WHEN** a slot holds both ranked anime and a scored Plan-to-watch anime
- **THEN** the ranked ones come first and the Plan-to-watch one follows them

#### Scenario: The board and the distribution agree
- **WHEN** a distribution row reports 12 anime scored 8
- **THEN** the board's 8 slot holds exactly 12 tiles

#### Scenario: The media-type control does not narrow the board
- **WHEN** I narrow the top 10 to films only and then open the score board
- **THEN** every included anime of the period is still placed in its slot, films and everything else alike

#### Scenario: The board follows the time filter
- **WHEN** I switch a yearly recap's time filter and open the score board
- **THEN** the slots hold the newly included entries, matching the recomputed distribution

#### Scenario: Unscored anime are not on the board
- **WHEN** the period includes anime I have not scored
- **THEN** they appear in no slot, and no slot's count includes them

#### Scenario: A slot nothing was scored
- **WHEN** no included anime carries a score of 3
- **THEN** the 3 slot is still shown, identifiably empty rather than missing or apparently loading

#### Scenario: An anime with no poster
- **WHEN** an included anime has no poster art
- **THEN** its slot holds a placeholder of the same size in its place, and the slot's count still matches the distribution
