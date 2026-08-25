## ADDED Requirements

### Requirement: A recap counts only progress recorded in the app

Wherever a recap reads the activity log — to decide whether an entry belongs to a **What I watched** period, to judge whether that option is available for a period, and to compute a period's episode count — it SHALL read only progress recorded from **my own use of the app**, and SHALL ignore progress recorded because a sync observed it on MyAnimeList.

A synced record's moment is the moment the sync ran, not the moment I watched: a weekly reconciliation, or a corrective re-sync after a long gap, would otherwise drop a stretch of viewing into whichever period the sync happened to fall in, and a single catch-up run could move months of episodes into one day.

The consequence SHALL be stated plainly rather than worked around: episodes I watched and recorded only on MyAnimeList's own site SHALL NOT contribute to a recap, exactly as they do not today. An entry that qualifies for a period on its **completion date** SHALL still qualify however that completion date arrived.

This SHALL constrain only what the recap reads from the activity log. Every other recap rule — the two-armed selection, the once-per-period counting, the per-period episode sum, the pre-tracking fallback to stored episodes and rewatch count — SHALL apply exactly as it does today to the progress that does count.

#### Scenario: Synced progress does not enter a recap

- **WHEN** an accepted reconciliation diff or a corrective re-sync raises an anime's episodes watched inside a period
- **THEN** that increase contributes nothing to the period's episode count and does not by itself place the anime in the period's **What I watched** selection

#### Scenario: Availability is judged on the same terms

- **WHEN** a period's only recorded episode progress came from a sync
- **THEN** the period is judged as holding no logged progress, exactly as if that progress had not been recorded

#### Scenario: My own progress counts as it does today

- **WHEN** I watch and record episodes in the app inside a period
- **THEN** they place the anime in that period and contribute to its episode count exactly as they do today

#### Scenario: A completion date still places an anime

- **WHEN** an anime's completion date falls inside a period and the only record of its progress came from a sync
- **THEN** the anime is still included on its completion date, and its episodes are counted by the rule that applies where the log holds no progress for the period
