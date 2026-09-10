## REMOVED Requirements

### Requirement: Changes that came from MyAnimeList appear alongside my own

**Reason**: The sync paths no longer record anything (`activity-recording`, "The sync paths record nothing"). Neither the Latest updates feed nor the full edit history has sync-applied rows to show, so there is nothing to interleave and nothing to mark.

**Migration**: The "via MAL" marker is removed from both surfaces, and every row renders as the ordinary unmarked row. The rules this requirement restated for sync-applied rows — which change types the feed shows, the collapsing of consecutive episode progress, the merging of a score into its completion, the one-row-per-anime-per-field-group rule, the range-collapsing in the history, the single-phrase wording, and the history's title search and date filters — are requirements of their own and keep applying, unchanged, to every row.
