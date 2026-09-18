## ADDED Requirements

### Requirement: Hiding a score also withholds what would give it away

Hiding a MAL score SHALL withhold not only its digits but anything the app renders in the same view that would let the hidden value be inferred, in whole or in part. Replacing the digits while leaving a figure beside them that ranks, bounds, or compares the same score would satisfy the letter of the hide toggle and none of its purpose.

Three kinds of rendering fall under this rule, each with its own remedy:

- **A value derived from the community score by a reversible transform**, such as MyAnimeList's rank — the score sorted descending. Such a value SHALL follow the same hide/reveal rules as the score itself: the global toggle, the "Always show MAL scores for completed and dropped shows" setting with its Completed/Dropped/Rewatching statuses, and the "no stand-in characters" placeholder rule. Each such value SHALL have its own reveal, independent of the score's — revealing one SHALL NOT reveal the other. A figure that is **not** derived from the community score SHALL NOT be gated by this rule; popularity, which ranks by member count, is displayed in full.

- **Membership of a list whose heading or row text bounds the score**, such as the profile page's opinion-divergence lists and a recap's hot takes. Where the fact of appearing at all asserts something about the hidden score, the app SHALL omit the row **entirely** while the toggle is on and the entry is not Completed, Dropped, or Rewatching — not render it with a placeholder or a reveal control, since the leak is the row's presence rather than its score cell. Such omission SHALL NOT change which items qualify for the list, how they are ranked, or any population a statistic is computed over; it applies to rendering alone, and SHALL NOT apply while the toggle is off. A list left with nothing to render SHALL fall back to the empty state it already shows when nothing qualified, rather than reporting that rows were withheld — which would itself be a signal about the scores being hidden.

- **A comparative claim about entries the user has not reached**, such as which entry of an unfinished series currently holds its highest MAL score. Where a stat's answer can bias anticipation for something still ahead of the user, or can change on its own as more of the work airs and is scored, the app SHALL withhold the whole stat behind a reveal control while the global hide toggle is on and the work it compares is not yet settled — the whole stat, rather than its parts one by one. While the toggle is off, such a stat SHALL render regardless of settledness: the toggle governs this rule as it governs the two above it, so a user asks for the protection by hiding scores rather than by any separate control.

A whole-stat reveal of this kind SHALL follow the same non-persistence rule as a per-score reveal, per "Reveal is not persisted": it SHALL last for the page view it was granted on, SHALL be dropped when the page being viewed changes — including a move between two pages of the same kind, where the client keeps one component mounted and only its parameter changes — and SHALL NOT survive a reload or be restored by a back or forward navigation.

For the page view it was granted on, that reveal SHALL hold. Controls that change what the page shows without leaving it, and data arriving beneath the page, SHALL NOT withdraw it — including data that would have withheld the stat had the user not revealed it. Non-persistence is a rule about leaving and returning, not a licence to close content under a reader who asked for it.

Where such a stat's gate is a condition on current data, that condition SHALL be evaluated afresh on each render and SHALL NOT be recorded when the stat is first shown or when its reveal control is used, so a stat that stops qualifying is withheld again on the next render without a navigation and without a separate step to invalidate it.

This requirement states the rule the app's score-hiding already implies; it does not relax any existing one. Where it and another requirement both bear on a rendering, the stricter SHALL govern.

#### Scenario: A derived figure follows the score's own rule
- **WHEN** the hide toggle is on and a view shows a figure that is the community score under a reversible transform, such as an anime's MyAnimeList rank
- **THEN** that figure is replaced by its own reveal control, with no stand-in characters, and is shown in full only where the score itself would be

#### Scenario: Each derived figure reveals on its own
- **WHEN** the hide toggle is on and I reveal either a score or a figure derived from it
- **THEN** only the one I used the control on is revealed, and the other still shows its own control

#### Scenario: A figure not derived from the score is untouched
- **WHEN** the hide toggle is on and a view shows a figure that ranks by something other than the community score, such as popularity by member count
- **THEN** it is shown in full, with no placeholder and no reveal control

#### Scenario: A row whose presence bounds the score is omitted outright
- **WHEN** the hide toggle is on and an anime I have not Completed, Dropped, or am Rewatching qualifies for a list whose heading or row text bounds its MAL score
- **THEN** no row for it is rendered at all — not a placeholder, not a reveal control

#### Scenario: Omission does not reach the underlying statistic
- **WHEN** rows are omitted from such a list because scores are hidden
- **THEN** which anime qualify, the order they are ranked in, and any population a statistic is computed over are all unchanged

#### Scenario: Omission stops when scores are shown
- **WHEN** the hide toggle is off
- **THEN** every such list renders every item that qualifies, whatever the entry's status

#### Scenario: A list emptied by omission says only that it is empty
- **WHEN** the hide toggle is on and every item qualifying for such a list is one I have not settled
- **THEN** the list shows its ordinary empty state, rather than reporting that rows were withheld

#### Scenario: A comparative stat is withheld whole until its subject is settled
- **WHEN** the hide toggle is on, a stat names which entry of a series currently holds the series' highest MAL score, and the series is not yet settled
- **THEN** the whole stat is replaced by a reveal control naming no entry, including entries I have already completed

#### Scenario: A comparative stat is not withheld while scores are shown
- **WHEN** the hide toggle is off and such a stat's subject is not yet settled
- **THEN** the stat renders in full, with no reveal control

#### Scenario: A whole-stat reveal does not persist
- **WHEN** I reveal such a stat and then navigate to another page of the same kind, or away and back, or reload
- **THEN** it shows its reveal control again rather than the content I had revealed

#### Scenario: A whole-stat reveal holds for its page view
- **WHEN** I reveal such a stat and then use the page's own controls, or the page's data is refreshed beneath me, without leaving the page
- **THEN** it stays revealed

#### Scenario: A stat that stops qualifying is withheld again
- **WHEN** such a stat is showing because its gate was satisfied, and the underlying data then changes so that it no longer is
- **THEN** the stat is withheld again on the next render, without a navigation and without a reload
