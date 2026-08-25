## ADDED Requirements

### Requirement: Changes that came from MyAnimeList appear alongside my own

Both surfaces that report what happened to my list — the "Latest updates" feed and the full edit-history overlay — SHALL include the changes applied by the sync paths, in the **same list** as my own edits, positioned by when they were recorded like any other entry. Neither surface SHALL hold them in a separate list, a separate section, or behind a control of their own.

Every rule each surface already applies SHALL apply to them unchanged: which change types the feed shows and which it omits, the collapsing of consecutive episode progress, the merging of a score into the completion it belongs to, the one-row-per-anime-per-field-group rule, the range-collapsing in the history, and the single-phrase wording both surfaces use. A change applied by a sync SHALL be described in the same words as the same change made by me.

Each such row SHALL carry a short marker naming MyAnimeList as the origin, so it is tellable at a glance from a change I made in the app. The marker SHALL sit alongside the row's existing content without changing the row's phrasing, its height, or the layout of any other row. A row for a change I made in the app SHALL carry no marker — the unmarked row is the ordinary one.

The three sync origins SHALL share one marker rather than being distinguished on these surfaces: what a reader needs is whether they made the change here, not which sync path applied it.

The history overlay's title search and date filters SHALL apply to these rows exactly as they apply to my own.

#### Scenario: An anime imported from MyAnimeList appears in the feed

- **WHEN** a background import creates an entry for an anime added on MyAnimeList's own site
- **THEN** the Latest updates feed shows an addition row for it, marked as coming from MyAnimeList

#### Scenario: An accepted diff appears in the feed

- **WHEN** I accept a reconciliation diff that raises an anime's episodes watched
- **THEN** the feed shows a progress row for that anime, marked as coming from MyAnimeList

#### Scenario: A completion applied by a re-sync reads as a completion

- **WHEN** the corrective re-sync completes an anime I had finished on MyAnimeList's own site
- **THEN** the feed shows a completion row for it, marked as coming from MyAnimeList, in the same words a completion of my own uses

#### Scenario: My own edits are unmarked

- **WHEN** I edit an entry in the app and the feed renders its row
- **THEN** that row carries no origin marker

#### Scenario: One list, ordered by time

- **WHEN** my own edits and sync-applied changes fall in the same stretch of history
- **THEN** both surfaces interleave them in one list ordered most-recent-first, with no grouping by origin

#### Scenario: The feed's omissions still hold

- **WHEN** a sync applies a start-date change, a finish-date change, or a status change that is not a completion
- **THEN** no row for it appears in the Latest updates feed, and it appears in the full history like any other change of that kind

#### Scenario: Collapsing applies regardless of origin

- **WHEN** a sync raises an anime's episodes watched several times across separate runs with nothing else recorded for that anime between them
- **THEN** the feed shows a single progress row for that anime reporting its newest count, and the full history collapses the run into one row reporting its range

#### Scenario: History filters reach these rows

- **WHEN** I search the full history by title or narrow it by date
- **THEN** rows marked as coming from MyAnimeList are filtered on the same terms as my own
