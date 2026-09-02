## MODIFIED Requirements

### Requirement: Reconciliation does not demote a rewatch

Because a Rewatching entry is pushed as `watching`, MyAnimeList reports it back as `watching`. Reconciliation SHALL treat a local **Rewatching** entry as **matching** a remote `watching` status, and SHALL NOT record a difference or rewrite the entry to Watching on that basis.

The rule SHALL hold across the whole reconciliation path, not the comparison alone:

- **Comparing.** A local Rewatching entry against a remote `watching` status SHALL NOT itself constitute a difference.
- **Recording.** Where a difference is raised for that entry by some *other* field — its episode count, score, dates, or rewatch count edited on MyAnimeList's own site — the difference held for review SHALL record the entry's status as **Rewatching**, not as the `watching` MyAnimeList reported. The review screen SHALL therefore show the status the entry will actually hold once accepted.
- **Applying.** Accepting a held difference SHALL re-check the entry as it stands at that moment: where the entry is Rewatching and the difference carries Watching, the entry SHALL stay Rewatching. A difference computed before an entry became a rewatch SHALL NOT demote it when accepted afterwards.

This SHALL constrain the status only. A remote status that is anything other than `watching` SHALL be diffed, recorded, and applied normally, so a change genuinely made on MyAnimeList still reaches the local entry. Every other field — episodes watched, score, dates, rewatch count — SHALL be compared and applied exactly as it is today.

Without this rule the first reconciliation after a rewatch is pushed would silently replace the user's Rewatching entry with Watching, destroying the distinction the status exists to hold.

#### Scenario: A rewatch survives reconciliation

- **WHEN** reconciliation compares a local Rewatching entry against a MyAnimeList entry reporting `watching`
- **THEN** no status difference is recorded and the entry stays Rewatching

#### Scenario: A difference raised by another field records Rewatching

- **WHEN** a local Rewatching entry's episode count was changed on MyAnimeList's own site, so reconciliation raises a difference for it while MyAnimeList reports its status as `watching`
- **THEN** the difference held for review records the status as Rewatching, and the review screen shows Rewatching

#### Scenario: Accepting that difference keeps the rewatch

- **WHEN** I accept a difference that carries the new episode count for that Rewatching entry
- **THEN** the episode count is applied and the entry's status is still Rewatching

#### Scenario: A stale difference does not demote a newer rewatch

- **WHEN** a difference was computed while an entry was Watching, the entry has since become Rewatching, and I then accept the difference
- **THEN** the entry stays Rewatching, and the difference's other fields are applied as usual

#### Scenario: A real remote change still applies

- **WHEN** reconciliation compares a local Rewatching entry against a MyAnimeList entry reporting `dropped`
- **THEN** the difference is recorded and applied like any other remote status change

#### Scenario: Other fields still diff

- **WHEN** a local Rewatching entry and its MyAnimeList copy differ in episodes watched or score
- **THEN** those differences are recorded exactly as they would be for any other status

## ADDED Requirements

### Requirement: A corrective re-sync does not demote a rewatch

The corrective re-sync re-reads every MyAnimeList list entry and applies it to local data immediately, without review. It SHALL apply the same rewatch rule reconciliation applies: where the local entry is **Rewatching** and MyAnimeList reports `watching`, the entry SHALL stay **Rewatching**.

The rule SHALL be the one rule, shared, rather than restated per path — every path that reads a MyAnimeList list status onto an entry that already exists locally SHALL resolve the incoming status through it, so no future read-back path can be added that demotes a rewatch by omission.

An entry being created for the first time from MyAnimeList data has no local status to preserve, so the rule SHALL have no effect there: the incoming status is used as reported.

Every other field the re-sync writes — episodes watched, score, dates, rewatch count — SHALL be applied exactly as it is today, and every other remote status SHALL be applied as reported.

#### Scenario: A rewatch survives a corrective re-sync

- **WHEN** a corrective re-sync reads a local Rewatching entry's MyAnimeList copy, which reports `watching`
- **THEN** the entry's status is still Rewatching after the run, and no status change is written to the activity log

#### Scenario: The re-sync still applies other fields to a rewatch

- **WHEN** that same entry's episode count differs between MyAnimeList and local data
- **THEN** the MyAnimeList episode count is applied and the status stays Rewatching

#### Scenario: A real remote change still applies during a re-sync

- **WHEN** a corrective re-sync reads a local Rewatching entry whose MyAnimeList copy reports `completed`
- **THEN** the entry becomes Completed, exactly as any other remote status change is applied

#### Scenario: A newly imported entry is unaffected

- **WHEN** a corrective re-sync encounters a MyAnimeList entry with no local counterpart and MyAnimeList reports `watching`
- **THEN** the entry is created as Watching
