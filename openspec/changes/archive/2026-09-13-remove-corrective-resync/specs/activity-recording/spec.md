## MODIFIED Requirements

### Requirement: The sync paths record nothing

The two sync paths — the background import of anime found on MyAnimeList but missing locally, and the acceptance of a reconciliation diff — SHALL record nothing in the activity log, on any run, whatever they apply.

A change these paths apply was made somewhere else: in the app on another device, which records it in its own log, or on MyAnimeList's own site. Recording it here as well would put the same change in the log twice once two devices' logs are combined — at a later moment than it was made, and coarser, since a sync sees only the net change and not the steps that led to it. Such records SHALL NOT be written, rather than written and removed later.

The consequence SHALL be stated plainly rather than worked around: a change made on MyAnimeList's own site SHALL NOT appear in the activity log on any device.

These paths SHALL go on applying exactly what they apply today, in the same order and under the same conditions; only the recording stops. Whether an import records anything SHALL NOT depend on whether it is the first run, whether it was interrupted and resumed, or whether any activity has been recorded before it.

Declining a change held for review is not a sync path in this sense. It is a change made in the app and is recorded per "Every change made in the app records what it changed".

#### Scenario: A first import writes no history

- **WHEN** the list is imported for the first time
- **THEN** the activity log holds no records for the anime it imported

#### Scenario: A later import writes no history either

- **WHEN** an anime is added on MyAnimeList's own site after my history has begun, and a later import creates its entry
- **THEN** the entry is created exactly as before, and no activity is recorded for that anime

#### Scenario: An accepted diff records nothing

- **WHEN** I accept a reconciliation diff that raises one anime's episodes watched, changes its score, and creates an entry for another anime not tracked locally
- **THEN** both entries hold the diff's values, and no activity is recorded for either anime

#### Scenario: A completion made on MyAnimeList's site stays out of my history

- **WHEN** I complete an anime on MyAnimeList's own site and a sync path applies that completion here
- **THEN** the entry reads as Completed, and no record of the completion appears in the activity log, the Latest updates feed, or the full edit history
