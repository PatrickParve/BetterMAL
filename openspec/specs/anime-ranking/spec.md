# anime-ranking Specification

## Purpose
TBD - created by archiving change add-anime-ranking. Update Purpose after archive.

## Requirements

### Requirement: One ranking over every anime I have scored
The system SHALL maintain a single persisted ranking: one total order over every anime of mine that qualifies for it, from my favourite downward. Every anime in the ranking SHALL carry an overall rank — a 1-based position in that order, with no gaps and no two anime sharing a number — and that rank SHALL be stable across reloads until I change a score, a status, or the order itself.

There SHALL be exactly one ranking. Every view that puts my anime in order by my opinion SHALL read it rather than compute an order of its own, so an anime I place once is placed everywhere.

#### Scenario: Ranking is a total order
- **WHEN** the ranking holds 400 anime
- **THEN** they carry the ranks 1 through 400, one anime per rank

#### Scenario: The ranking survives a reload
- **WHEN** I set an order and reload the app
- **THEN** every anime holds the same rank it held before the reload

### Requirement: What the ranking covers
An anime SHALL be in the ranking when all of the following hold: it carries a score of mine, it has aired at least one episode (resolved by the app's single "has this anime aired" rule), and its status is not Plan to watch. An anime failing any of those SHALL have no rank at all, and SHALL be absent from every ordering the ranking drives rather than sorted to the end of one.

Dropped anime and anime whose media type is Music, CM, or PV SHALL be in the ranking and SHALL carry a rank. They are placed automatically rather than by hand — see "Ranking order is score, then band, then my order".

#### Scenario: Unscored anime are unranked
- **WHEN** an anime in my list carries no score of mine
- **THEN** it has no rank

#### Scenario: Plan to watch is unranked
- **WHEN** an anime I have scored sits at Plan to watch
- **THEN** it has no rank, despite the score

#### Scenario: An unaired anime is unranked
- **WHEN** an anime has aired no episode
- **THEN** it has no rank

#### Scenario: Dropped anime are ranked
- **WHEN** I have dropped an anime and scored it 7
- **THEN** it carries a rank

#### Scenario: A music video is ranked
- **WHEN** I have scored a Music entry 9
- **THEN** it carries a rank

### Requirement: Ranking order is score, then band, then my order
The ranking SHALL order anime first by my score descending: an anime scored 9 SHALL NEVER rank above an anime scored 10, whatever order I set.

Within one score, anime SHALL be ordered by band, in this order:

1. **Hand-ordered** — every anime that is neither dropped nor a Music, CM, or PV entry. These SHALL be ordered by the order I set, with any member I have never placed following every member I have placed, alphabetically by title, case-insensitively.
2. **Short form** — Music, CM, and PV entries I have not dropped, ordered alphabetically by title, case-insensitively.
3. **Dropped** — every anime I have dropped, ordered alphabetically by title, case-insensitively.

An anime that is both dropped and a short-form entry SHALL sit in the dropped band, which is always the bottom of its score. Anime in the short-form and dropped bands SHALL NOT be hand-orderable: their place within their band follows from their title alone, and no ordering I set SHALL move them out of, or within, their band.

#### Scenario: Score dominates my order
- **WHEN** I place an anime scored 9 at the very top of my ranking
- **THEN** it still ranks below every anime scored 10

#### Scenario: Dropped anime fall to the bottom of their score
- **WHEN** a score tier holds four anime I am still watching or have completed and two I dropped
- **THEN** the four rank above the two, whatever their titles

#### Scenario: Dropped anime among themselves
- **WHEN** two dropped anime share a score
- **THEN** they are ordered alphabetically between themselves

#### Scenario: Short form sits above dropped
- **WHEN** a score tier holds a Music entry and a dropped TV entry
- **THEN** the Music entry ranks above the dropped TV entry

#### Scenario: A dropped music video
- **WHEN** an anime is both a Music entry and dropped
- **THEN** it is placed in the dropped band, beneath every short-form entry of the same score

#### Scenario: Anime I have never placed
- **WHEN** a score tier holds anime I have placed by hand and anime I never have
- **THEN** the placed ones come first in the order I set, and the rest follow alphabetically

### Requirement: A newly scored anime takes the last hand-ordered place in its score
The system SHALL place an anime at the end of its score's hand-ordered band whenever a score is set on it — whether that is its first score or a change to a different value. Placing it there SHALL NOT disturb the relative order of any other anime.

Re-saving an anime's existing score unchanged SHALL NOT move it. An anime that leaves the ranking without its score changing — because I dropped it, moved it to Plan to watch, or it left the hand-ordered band for another reason — SHALL keep the place it held, and SHALL return to that place if it becomes hand-orderable again.

#### Scenario: A first score lands last in its tier
- **WHEN** I score an anime 8 for the first time and my 8s already hold 30 anime
- **THEN** it becomes the last hand-ordered 8, and the other 30 keep their order

#### Scenario: Re-scoring moves an anime between tiers
- **WHEN** I change an anime's score from 8 to 9
- **THEN** it leaves the 8s and becomes the last hand-ordered 9

#### Scenario: Saving the same score changes nothing
- **WHEN** I open an anime's editor and save without changing its score
- **THEN** its place in the ranking is unchanged

#### Scenario: Dropping and un-dropping keeps my placement
- **WHEN** I drop an anime I had placed third among my 9s, and later move it back to Completed without changing its score
- **THEN** it is third among my 9s again

### Requirement: Any subset of the ranking is that ranking read in order
The system SHALL treat every narrower ranking as a filter over the one ranking, read from the top down. Narrowing by media type, by air year, by season, by status, or by any other property SHALL preserve the relative order of every anime that survives the filter, and SHALL NOT re-derive an order of its own.

#### Scenario: Ranking my films
- **WHEN** I ask for my films in order
- **THEN** they appear in the same relative order they hold in the whole ranking, with every non-film removed

#### Scenario: Ranking one year
- **WHEN** I ask for my 2025 anime in order
- **THEN** they appear in the same relative order they hold in the whole ranking, with everything that aired in another year removed

#### Scenario: A filter never reorders survivors
- **WHEN** two anime rank 40th and 71st overall and both survive a filter
- **THEN** the 40th still appears above the 71st in the filtered ranking

### Requirement: Every by-my-score ordering reads the ranking
The system SHALL order by rank wherever anime are put in order by my score, so equal scores are separated by my ranking rather than by title, by popularity, or by insertion order. This SHALL apply at least to my top anime on the profile page, my list sorted by my score, a recap's top 10 and its podium, a recap score board's slots, the season and year browsers' my-score sort, and a series page's list of entries tied for my highest score within that series (per the `series-page` capability's "Favourite ordering within a series").

Where such an ordering can include an anime with no rank — an anime I scored but have at Plan to watch, for instance — that anime SHALL be ordered after every ranked anime of the same score, alphabetically by title among other unranked anime of that score.

Unlike the other surfaces this requirement lists, which only ever display the ranking, a series page can also change it: reordering a series' tied favourites repositions the anime within the ranking itself (per the `series-page` capability), not just within that series page's own view. Every ordering this requirement covers SHALL reflect that change exactly as it would a reorder made in the ranking editor.

#### Scenario: Tied scores follow my ranking
- **WHEN** a list ordered by my score holds three anime I scored 8
- **THEN** they appear in the order my ranking gives them, not alphabetically

#### Scenario: One placement shows everywhere
- **WHEN** I move an anime above another of the same score in the ranking editor
- **THEN** it appears above that anime in my list sorted by my score, in the recap top 10, on the score board, in my top anime, and among any series' tied favourites the two belong to

#### Scenario: An unranked anime in a scored ordering
- **WHEN** an ordering by my score includes a scored Plan-to-watch anime alongside ranked anime of the same score
- **THEN** the ranked ones come first and the Plan-to-watch one follows them

#### Scenario: A series-page reorder shows everywhere else too
- **WHEN** I reorder two entries tied for my highest score on a series page
- **THEN** the new order is reflected in the ranking editor and in every other ordering this requirement covers, exactly as if the reorder had been made in the ranking editor

### Requirement: The ranking editor
The system SHALL provide a ranking editor that arranges one score at a time. The editor SHALL offer a score selector listing every score from 10 down to 1 that holds at least one hand-orderable anime, and SHALL show the selected score's hand-orderable anime in full, in ranking order, each row naming the anime and showing its overall rank.

The editor SHALL list only hand-orderable anime. Short-form and dropped anime of the selected score SHALL NOT appear, since their place follows from their band and their title and nothing the editor does could move them.

Reordering SHALL work through the same pointer dragging the profile page's top-anime editor gives — a floating preview, a landing marker, edge auto-scroll, and a cancellable drag that leaves the order untouched. Its per-row controls SHALL differ from that editor's, since arranging a whole score (or the whole library) has no top-ten boundary to promote into: every row SHALL offer a jump-to-top control and a jump-to-bottom control, each moving that row to the very first or very last position of the score in a single action and leaving every other row's relative order intact. The first row SHALL offer only jump-to-bottom and the last only jump-to-top, since the other direction is already where they are.

Reordering SHALL apply only within the selected score. There SHALL be no cut line, because every listed anime is in the ranking already.

#### Scenario: Choosing a score to arrange
- **WHEN** I open the ranking editor and select 8
- **THEN** every hand-orderable anime I scored 8 is listed in ranking order, and nothing of any other score is

#### Scenario: Empty scores are not offered
- **WHEN** I have no hand-orderable anime scored 3
- **THEN** the score selector offers no 3

#### Scenario: Rows carry their overall rank
- **WHEN** I look at a row in the ranking editor
- **THEN** it shows that anime's overall rank alongside its title

#### Scenario: Pinned anime are not listed
- **WHEN** the selected score holds two dropped anime and a Music entry
- **THEN** none of the three is listed in the editor

#### Scenario: Jump to top from deep in a score
- **WHEN** I activate the jump-to-top control on the 60th row of a score
- **THEN** that row becomes the 1st, and the rows that were 1st through 59th each move down one

#### Scenario: Jump to bottom from near the top of a score
- **WHEN** a score holds 200 rows and I activate the jump-to-bottom control on the 3rd
- **THEN** that row becomes the 200th, and the rows that were 4th through 200th each move up one

#### Scenario: The first and last rows offer only one direction
- **WHEN** I look at the first row of a score
- **THEN** it offers jump-to-bottom but not jump-to-top, and the last row offers jump-to-top but not jump-to-bottom

#### Scenario: Dragging inside the selected score
- **WHEN** I drag a row and release it over another row of the same score
- **THEN** the dragged row takes that position, with the same floating preview, landing marker, and edge auto-scroll the top-anime editor gives

#### Scenario: No cut line
- **WHEN** I look at any score in the ranking editor
- **THEN** no cut line is drawn, since every listed anime is ranked

### Requirement: The ranking editor can open on one anime
The system SHALL be able to open the ranking editor already focused on a given anime: the score selector SHALL be on that anime's score, the editor SHALL show only that score, and that anime's row SHALL be scrolled into view and visibly marked so it can be found without hunting.

An anime that is not hand-orderable — unscored, Plan to watch, unaired, dropped, or a short-form entry — SHALL NOT be offered a focused open, since it has no row to focus.

#### Scenario: Opening on a specific anime
- **WHEN** the ranking editor is opened focused on an anime I scored 7
- **THEN** it opens on score 7, with that anime's row in view and marked

#### Scenario: A long score still lands on the anime
- **WHEN** the focused anime is the 300th row of its score
- **THEN** the editor opens scrolled to that row rather than at the top of the score

#### Scenario: No focused open for a pinned anime
- **WHEN** an anime is dropped or is a Music, CM, or PV entry
- **THEN** no action offers to open the ranking editor on it

### Requirement: The ranking editor saves automatically
Every reorder in the ranking editor SHALL persist on its own, with no separate save step: the persisted order SHALL take precedence over the alphabetical default from then on, including for anime that had never been placed before, which the reorder places. There SHALL be nothing left to discard — closing the editor SHALL NOT undo any reorder already made in it.

Closing the editor SHALL still save a reorder that has not yet reached the server: closing SHALL wait for that save (or one already in flight) to finish before the editor actually closes, rather than risking the last reorder to a timer that never got to fire.

A reorder SHALL preserve the relative positions of hand-ordered anime a scoped view did not show, so arranging a score under a media-type filter never disturbs the members that filter hid.

#### Scenario: An arrangement is persisted
- **WHEN** I reorder a score in the ranking editor
- **THEN** the new order holds everywhere the ranking is read, and survives a reload, without a separate save action

#### Scenario: Closing has nothing to discard
- **WHEN** I reorder rows and then close the editor
- **THEN** the reordering is already saved, not discarded

#### Scenario: Closing right after a reorder still saves it
- **WHEN** I reorder a row and close the editor before its save would otherwise have fired
- **THEN** the editor waits for that reorder to save before it closes

#### Scenario: Hidden members keep their positions
- **WHEN** I arrange a score while a media-type filter hides some of that score's hand-ordered members
- **THEN** the hidden members keep their positions relative to the visible members that surrounded them

#### Scenario: A reorder places previously unplaced anime
- **WHEN** I reorder a score whose members were all in the alphabetical default
- **THEN** every one of them is placed from then on, so a newly scored anime lands beneath all of them rather than among them

### Requirement: Ways into the ranking editor
The system SHALL offer the ranking editor from three places:

- the profile page's "My top anime" editor, via an action that leaves the top-anime arrangement and opens the whole-library ranking editor;
- the entry editor, via a Rank action offered whenever that entry is hand-orderable, opening the ranking editor focused on that anime;
- the completion score prompt, via a save-and-rank action that saves the chosen score and then opens the ranking editor focused on the anime just completed.

Each SHALL open the ranking editor over the page it was invoked from, without navigating away.

#### Scenario: From the top-anime editor
- **WHEN** I use the whole-library action in the "My top anime" editor
- **THEN** the ranking editor opens over my whole library rather than only the anime in contention for the top list

#### Scenario: From the entry editor
- **WHEN** I use the Rank action in an entry's editor
- **THEN** the ranking editor opens focused on that anime

#### Scenario: No Rank action for a pinned entry
- **WHEN** I open the editor for an entry that is unscored, Plan to watch, dropped, or a Music, CM, or PV entry
- **THEN** no Rank action is offered

#### Scenario: From the completion prompt
- **WHEN** I finish an anime, pick a score in the completion prompt, and choose save and rank
- **THEN** the score is saved and the ranking editor opens focused on that anime, on the score I just gave
