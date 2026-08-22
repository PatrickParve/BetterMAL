## ADDED Requirements

### Requirement: Rewatching has its own place in the status breakdown

The profile page's per-status counts SHALL include **Rewatching** as its own figure, rather than folding rewatches into Watching or Completed. The per-status counts SHALL continue to sum to Total Entries.

The episode and time formulas SHALL be unaffected. Episodes is already defined as an entry's episodes watched plus one complete run per recorded rewatch, and Days from that same rewatch-inclusive count. Under this change a rewatch in progress is exactly that — the rewatch count holds the runs already finished, and episodes watched holds the current run's progress, because entering Rewatching resets the count to 0 and the rewatch count is only increased once a run finishes. No formula changes; the arithmetic is identical to the rewatch-in-progress case the capability already describes.

#### Scenario: Rewatches are counted separately

- **WHEN** my list holds entries in every status, including two Rewatching ones
- **THEN** the stats show a Rewatching count of two, and neither the Watching nor the Completed count includes them

#### Scenario: The breakdown still sums

- **WHEN** the per-status counts are added together
- **THEN** they equal Total Entries

#### Scenario: A rewatch in progress counts the same as before

- **WHEN** my list holds a Rewatching twelve-episode series with a rewatch count of two whose episodes watched reads one
- **THEN** it contributes twenty-five episodes, exactly as the same entry did under the previous representation
