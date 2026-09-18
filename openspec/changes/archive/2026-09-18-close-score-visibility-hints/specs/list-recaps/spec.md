## MODIFIED Requirements

### Requirement: Hot takes
Every recap SHALL report up to five **hot takes** — the included entries where my score and MAL's community score disagree sharply enough, and in a definite enough direction, to count as a disagreement rather than as ordinary variation. An included entry SHALL be eligible only when it has both my score and a MAL score and it satisfies one of the two rules the profile page's opinion-divergence lists apply:

- **They liked it, I didn't** — the divergence runs at least one standard deviation in MAL's direction, my score is at or below the "average" boundary of the personal scale, and MAL's community score is at or above the community-like boundary.
- **I liked it, they didn't** — the divergence runs at least one standard deviation in my direction, my score is at or above the "very good" boundary of the personal scale, and MAL's community score is at or below that same community boundary.

The thresholds and boundaries SHALL be the ones the profile page already uses, drawn from a single shared definition rather than restated, so an anime that is a hot take in a recap is one the profile page also names, and one the profile page does not name never appears as a hot take.

Qualifying hot takes SHALL be ordered by the size of the divergence, largest first, with ties broken by title case-insensitively so the order is stable across reloads. Each hot take SHALL show the anime, both scores, and which direction the divergence runs, so a pick I rated far above the community reads differently from one I rated far below it.

Divergence SHALL be measured on a common scale rather than by subtracting a 1–10 personal score from a MAL community average directly, using the same normalisation the profile page's opinion-divergence lists already apply.

When fewer than five included entries qualify, the recap SHALL show those that do; when none qualify — including when my list holds too few scored pairs for the normalisation to be computed at all — it SHALL say so rather than present near-agreements as hot takes.

While the global hide-scores toggle is on, the recap SHALL render only those of its hot takes whose entry is **Completed, Dropped, or Rewatching**, and SHALL omit the rest **entirely** rather than rendering them with a placeholder or a reveal control. A hot take states which way the disagreement runs as row text, and qualifying at all bounds the MAL score on one side, so the row leaks a bound on the score it is hiding whatever its own score cell renders. This is the rule the profile page's opinion-divergence lists apply to the same anime for the same reason, and the two SHALL agree.

Because the five are selected by divergence before this rule is applied, omission SHALL leave **fewer than five hot takes, possibly none**, rather than promoting the next-most-divergent settled entry in a dropped one's place — selection is decided by divergence alone and SHALL NOT depend on the viewer's hide state. When it leaves none, the recap SHALL show the same "no hot takes" message it shows when nothing qualified. While the hide toggle is off, every selected hot take SHALL render, whatever its status.

#### Scenario: Five hot takes
- **WHEN** a recap's included set holds more than five entries that satisfy either rule
- **THEN** the five with the largest divergence are shown, largest first, each naming both scores

#### Scenario: Ordered by the size of the disagreement
- **WHEN** several entries qualify by different margins
- **THEN** they are ordered by how far apart the two scores are, largest first

#### Scenario: A near-agreement is not a hot take
- **WHEN** an included entry has both scores but its divergence falls short of one standard deviation
- **THEN** it is not shown as a hot take, however few other entries qualify

#### Scenario: A high score MAL also liked is not a hot take
- **WHEN** I scored an anime 9 and MAL's community score for it is 8.4
- **THEN** it is not shown as a hot take, since neither rule's like/dislike boundaries are crossed

#### Scenario: Agreement with the profile page
- **WHEN** an anime appears in one of the profile page's opinion-divergence lists and falls inside a recap's included set
- **THEN** that anime is eligible to appear as a hot take in that recap

#### Scenario: Divergence direction is visible
- **WHEN** a hot take is one I scored far above MAL's average, and another is one I scored far below it
- **THEN** the two are distinguishable on screen rather than presented identically

#### Scenario: Fewer than five qualify
- **WHEN** only two included entries satisfy either rule
- **THEN** two hot takes are shown

#### Scenario: An unsettled hot take is omitted while scores are hidden
- **WHEN** the hide toggle is on and one of a recap's hot takes is an entry I am Watching, have On-hold, or Plan to watch
- **THEN** it is not rendered at all — no row, no direction label, no reveal control — while the settled hot takes beside it are rendered as before

#### Scenario: Omission does not promote a replacement
- **WHEN** the hide toggle is on and two of a recap's five hot takes are unsettled
- **THEN** three hot takes are shown, and the sixth-most-divergent entry is not brought in to replace either omitted one

#### Scenario: Every hot take omitted
- **WHEN** the hide toggle is on and every hot take the recap selected is an entry I have not settled
- **THEN** the recap shows the same "no hot takes for this period" message it shows when nothing qualified

#### Scenario: Hiding omits nothing once scores are shown
- **WHEN** the hide toggle is off
- **THEN** every hot take the recap selected is rendered, whatever its status

#### Scenario: Nothing qualifies
- **WHEN** no included entry satisfies either rule
- **THEN** the recap states that there are no hot takes for this period

#### Scenario: Too few scored pairs to normalise
- **WHEN** my whole list holds too few entries scored by both me and MAL for the shared normalisation to be computed
- **THEN** the recap states that there are no hot takes for this period rather than ranking on an uncomputable divergence
