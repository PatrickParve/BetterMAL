# Fetch anime detail from MAL on demand instead of only from cache

Commit `ba79acb` (2026-07-14)

## Problem

1. The detail endpoint served anime solely from the local `AnimeMetadata` cache.
   Any anime without a row 404'd — surfaced as "Couldn't load this anime." when
   navigating to an uncached sequel/prequel or clicking a search result the user
   had never interacted with.
2. Reconciliation cached rows from MAL's fields-limited my-list payload while
   stamping `LastSyncedAt`, leaving the row missing genres/synopsis/background/
   source yet looking fully synced — so the detail page never backfilled it
   (required a manual "Refresh data").

## Changes

### `AnimeDetailService.cs`
- Live-fetches and caches whenever the row is missing or not detail-complete,
  and only 404s when the anime is genuinely unknown to MAL.
- Completeness is gated on `Genres` — the marker every full fetch sets and
  every lean/reconciliation row lacks — so pre-existing mis-stamped rows
  self-heal on first visit without a data migration, and cache hits don't
  re-fetch.

### `MetadataRefreshService.cs`
- `RefreshOneAsync` now upserts: fetches full detail from MAL first (an
  invalid id throws before any DB write), then insert-if-missing /
  update-if-present.

### `ReconciliationService.cs`
- Caches newly-discovered anime lean (`ToLeanAnimeMetadata`) instead of
  falsely marking them fully synced (`ToAnimeMetadata`), so the detail page's
  completeness check still triggers a full fetch on first visit.

## Files touched

- `backend/AnimeTracker.Api/Services/Detail/AnimeDetailService.cs` (+17/-10)
- `backend/AnimeTracker.Api/Services/Metadata/MetadataRefreshService.cs` (+12/-4)
- `backend/AnimeTracker.Api/Services/Sync/ReconciliationService.cs` (+8/-2)
