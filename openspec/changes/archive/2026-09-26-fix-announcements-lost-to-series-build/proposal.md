## Why

Season 2 of *Daemons of the Shadow Realm* (MAL 65024, "Yomi no Tsugai 2nd Season") never produced an **Announced** card, although it is exactly what the feature exists for: a not-yet-aired sequel appearing in the relations of a show on the user's list (MAL 62001). The user's suspicion was right, and the live database confirms the mechanism exactly.

`AnimeMetadataChangeDetector.RecordAsync` does two things for a newly-appeared relation edge, in this order: it writes the `RelationDiscovery` row, and then it enqueues a background series rebuild of the anime whose relations changed. The rebuild runs within seconds. `SeriesGraphBuilder` traverses the new edge, finds the far end has no cached row, and spends a fetch on it (`SeriesGraphBuilder.cs:324`) — a full-detail fetch that stamps `LastSyncedAt`. Ten minutes later `AnnouncementResolutionService` asks "had we ever fully fetched this anime?", reads the row the rebuild just created, and answers yes. The discovery is marked processed and nothing is announced.

The timings in the database are conclusive:

| Fact | Value |
|---|---|
| `RelationDiscoveries` #476 (62001 → 65024, sequel) discovered | 2026-09-19 22:25:29.606 |
| `Series` 62001 built, with 65024 stored as main-line member order 1 | 2026-09-19 22:25:31.917 |
| Discovery #476 marked processed, no announcement | 2026-09-19 22:32:07.070 |
| `AnimeUpdates` rows for 65024 | none |

The series build landed 2.3 seconds after the discovery and admitted 65024 as a member, which it can only do by fetching it. The resolver ran 6.5 minutes later, on the next `MetadataRefreshBackgroundService` tick, and read the evidence the rebuild had already overwritten.

Two corrections to the framing in the report. First, this is not a race with an uncertain winner: the rebuild is enqueued synchronously by the same call that writes the discovery, and the resolver waits for a ten-minute tick, so the rebuild wins essentially always, for any newly discovered far end it has budget to reach. Second, 64905 and 64847 are not evidence that it sometimes works. Their announcements were recorded on 2026-09-03 and 2026-08-26, and the never-fully-fetched gate did not exist until commit `82b9cb9` on 2026-09-06 — before that the resolver announced on airing status alone and was indifferent to a rebuild having fetched the anime first. Every discovery of a genuinely new far end since that commit has been at risk, and only one qualified: #476, the one the user noticed.

The underlying error is one the codebase has already identified and solved twice. `AnimeMetadataSnapshot.HadFullDetail` exists because `ApplyTo` stamps `LastSyncedAt`, so "had we ever fetched this?" must be captured before the write that answers it. `AnnouncementResolutionService.cs:49` repeats the reasoning for its own fetch. Both stop one step short: the fact is captured before *that code's own* fetch, while the fetch that actually destroys it belongs to a different subsystem and happens in between.

## What Changes

- **A discovery records what was true about the far-end anime when the edge appeared.** `RelationDiscovery` gains a column holding whether the newly-related anime had ever been fully fetched at the moment the discovery was written — read before the same method enqueues the series rebuild, so no fetch of any kind can run first.
- **The resolver trusts that recorded verdict instead of re-deriving it.** The never-fully-fetched half of the announcement gate is read from the discovery row, not from the anime's current `LastSyncedAt`. Any later fetch — the series rebuild, a visit to the anime's detail page, a device-transfer import — becomes irrelevant to whether the edge was news.
- **The list-entry half of the gate stays a read-time check, deliberately.** It is not corrupted by the system's own background work, only by the user adding the anime themselves, and `anime-updates` already rules that an announcement for an anime holding a list entry at record time is a false announcement to be deleted.
- **The resolver stops spending a MAL call it no longer needs.** Today an announceable anime always implied a missing or lean row, so the fetch was unconditional. With the verdict recorded, an announceable anime may already carry full detail — usually because the series rebuild fetched it. The resolver now fetches only when the row is missing or lean, and reads the airing status it already has otherwise.
- Discoveries written before this change carry no verdict. They keep today's read-time behaviour exactly. There are none pending in the live database, so this path is for safety, not for data that exists.
- **No retroactive repair.** Discovery #476 stays processed and 65024 gets no backdated card. It has held a list entry since 2026-09-22, and `anime-updates` treats an announcement recorded for an anime that already holds one as false and deletes it. Writing that card now would contradict the rule the feature is built on.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `anime-updates`: "An announcement is recorded only for an anime that has not finished airing" currently requires the never-fully-fetched condition to be established "before the resolver fetches the anime". That is too narrow — it is silent about fetches by other subsystems between the discovery and the resolution — and this change tightens it to the moment the discovery is recorded, with the verdict stored on the discovery. "Newly-discovered relations are resolved before they become news" changes correspondingly: processing reads the recorded verdict rather than establishing it, and fetches only where the anime's record is missing or lean. "Relation discoveries are recorded only on my own entries, and never from a first full fetch" gains the requirement that recording a discovery captures that verdict, before the series rebuild it enqueues can run.

## Impact

**Backend only.** No API, DTO or frontend change — the Announced card, its wording and the updates view are untouched.

- `backend/AnimeTracker.Api/Models/RelationDiscovery.cs`: new nullable `RelatedAnimeHadFullDetail` column and the documentation of what it means.
- `backend/AnimeTracker.Api/Data/AnimeTrackerDbContext.cs`: no configuration needed beyond the property; the existing `DiscoveredAt`/`ProcessedAt` indexes are unaffected.
- New EF migration adding the column, nullable, with no backfill.
- `backend/AnimeTracker.Api/Services/Updates/AnimeMetadataChangeDetector.cs`: `RecordDiscoveries` becomes asynchronous and batches one lookup of the far ends' `LastSyncedAt` before writing the rows; the `seriesBuildTrigger.Enqueue` call stays exactly where it is, after them.
- `backend/AnimeTracker.Api/Services/Updates/AnnouncementResolutionService.cs`: the gate reads the discovery group's recorded verdict, the fetch becomes conditional, and the D5 comment is rewritten to describe why the verdict is no longer derivable at resolve time.
- Tests: `AnnouncementResolutionServiceTests`, `AnimeMetadataChangeDetectorTests`, `AnimeMetadataChangeDetectorSeriesBuildTests`, `MetadataRefreshServiceTests` and `MetadataRefreshBackgroundServiceTests` construct `RelationDiscovery` rows or assert on call tallies.
- `openspec/specs/anime-updates/spec.md`: three requirements reworded.
