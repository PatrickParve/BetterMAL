## Context

This section records what was read in the code at `a946ee9` and queried against the running Docker stack's Postgres on 2026-09-26, confirming the user's suspicion about MAL 65024.

**The path a discovery takes today.** `AnimeMetadataChangeDetector.RecordAsync` (`AnimeMetadataChangeDetector.cs:120`) runs after every write of an anime's relation set. It diffs the edges, and for each newly-appeared one it does two things, in this order:

1. `RecordDiscoveries` writes a `RelationDiscovery` row per new edge — gated on the owning anime having had full detail before this write and being a non-Dropped list entry of the user's (line 135).
2. `seriesBuildTrigger.Enqueue(anime.Id)` queues a background series rebuild of the owning anime, unconditionally (line 151).

`SeriesBuildTrigger.Enqueue` releases a semaphore that `SeriesBuildTriggerBackgroundService` is already waiting on, so the rebuild starts as soon as the thread pool gets to it. `SeriesGraphBuilder.TraverseStoryComponentAsync` walks the owner's edges breadth-first; for a far end with no cached row at all it spends one of its eight visit-budget fetches on `refreshService.RefreshOneAsync(animeId)` (`SeriesGraphBuilder.cs:324`). That call's insert branch (`MetadataRefreshService.cs:160`) stores `details.ToAnimeMetadata(now)`, and `ToAnimeMetadata` sets `LastSyncedAt = now`.

`AnnouncementResolutionService.ResolveAsync` runs on `MetadataRefreshBackgroundService`'s ten-minute tick, ahead of the staleness batch. It reads the far end's cached row and computes `hadFullDetail = anime is not null && anime.LastSyncedAt != default` (`AnnouncementResolutionService.cs:55`). By then the rebuild's row is committed, so the answer is yes, the discovery is marked processed, and no announcement is recorded.

**The evidence.** From the live database:

```
RelationDiscoveries #476: 62001 → 65024 sequel
  DiscoveredAt 2026-09-19 22:25:29.606   ProcessedAt 2026-09-19 22:32:07.070
Series 62001: BuiltAt 2026-09-19 22:25:31.917
  SeriesMembers: 62001 (main line, order 0), 65024 (main line, order 1)
AnimeUpdates where AnimeId = 65024: none
```

The rebuild committed 2.3 seconds after the discovery, with 65024 stored as a member — which it could only do by fetching it, 65024 having had no row before. The resolver ran 6.5 minutes later and found the evidence overwritten. `LastSyncedAt` for 65024 now reads 2026-09-22 23:14:56, a later refresh, so the original stamp is no longer visible; the series build time is what dates it.

**Why this is not a race.** The enqueue is synchronous, on the same code path, immediately after the discovery is written, and the resolver waits for a ten-minute tick. The rebuild wins whenever it has budget to reach the far end. The far ends that do get missed are the ones the rebuild cannot reach: beyond the eight-fetch visit budget, or on an edge `SeriesRelations.IsTraversable` excludes. So today's behaviour is closer to "announcements survive only by accident" than to "announcements are usually fine".

**Why 64905 and 64847 are not counter-examples.** Their announcements were recorded on 2026-09-03 07:55 and 2026-08-26 16:28. The never-fully-fetched gate entered `AnnouncementResolutionService` in commit `82b9cb9` on 2026-09-06; before it, the resolver only checked airing status and did not care that a rebuild had fetched the anime first. Since `82b9cb9`, exactly two discoveries have named an unaired or airing far end: #474 (64847, which already held an announcement from August, so the recorder's once-per-anime dedupe would have suppressed a second one anyway) and #476, which is the bug. Blast radius to date: one card.

**The error has a name in this codebase.** `AnimeMetadataSnapshot.HadFullDetail` exists because `ApplyTo` stamps `LastSyncedAt` before the diff can read it, and `AnnouncementResolutionService.cs:48-54` repeats the reasoning for its own fetch, calling it "the same trap". Both capture the fact before their *own* fetch. The fetch that destroys it here belongs to a different subsystem, on a different thread, minutes earlier. The fix is the same move made one level higher: capture it when the edge appears, which is the earliest moment the question can be asked honestly, and store it.

**Constraint on repair.** `anime-updates` already rules (spec.md:313) that an announcement recorded for an anime holding a list entry at record time is a false announcement and is deleted. 65024 has held one since 2026-09-22, so a backdated card for it would be false by the feature's own definition. The user chose forward-only.

## Goals / Non-Goals

**Goals:**
- A newly discovered not-yet-aired sequel of a list entry gets its Announced card, whatever else fetches the anime between the edge appearing and the resolver running.
- The announcement decision stops depending on mutable global state that other subsystems are free to write.
- The series rebuild keeps fetching new members exactly as it does — the updates feed adapts to it, not the other way round.
- The resolver stops spending a MAL call on an anime whose full detail is already cached.

**Non-Goals:**
- Any retroactive card, reopened discovery or data-repair migration. Chosen by the user; reasoned above.
- Changing when the series rebuild is enqueued, what it fetches, or its budgets. The enqueue's placement after `RecordDiscoveries` becomes load-bearing and gets a comment, but no behaviour there changes.
- Changing the ten-minute tick, the batch sizes or the nightly call cap.
- Widening what counts as a discovery, or the one-relation-step-from-my-list rule.
- Anything in the frontend. The Announced card already renders correctly; it was never written.

## Decisions

### D1. The verdict is recorded on the discovery row, not re-derived later

`RelationDiscovery` gains `RelatedAnimeHadFullDetail`, written when the row is written: whether the newly-related anime had ever had a full-detail fetch at the moment its edge first appeared. The resolver reads it instead of the anime's current `LastSyncedAt`.

This is the only option that makes the answer stable. The question "had we ever fully fetched this anime before this edge appeared?" has exactly one moment at which it can be answered honestly, and that moment is when the edge appears. Every later reading is a reading of state that any number of subsystems may have written in between — the series rebuild today, and a detail-page visit (`AnimeDetailService.cs:55`), a device-transfer import (`TransferImportRunner.cs:108`) or the airing refresh trigger on any other day. Recording the verdict closes all of them at once, including ones added later, which re-ordering any single caller would not.

Alternatives considered:

- **Defer the series rebuild until the discovery is resolved.** Inverts the dependency: the updates feed would gate a series-page feature that has nothing to do with it, and `anime-updates` explicitly requires the opposite (spec.md:388, "its other consumer — the background series rebuild … SHALL be unaffected by these rules"). It also delays a new season appearing on the series page by up to ten minutes for no user-visible gain.
- **Have the resolver compare `LastSyncedAt` against `DiscoveredAt`** and treat "fetched after the discovery" as still-announceable. Fails on the ordinary case: an anime legitimately fetched long ago is refreshed on its tier the day after an edge to it appears, and would then look announceable. `LastSyncedAt` records the *latest* fetch, not the first, so it cannot answer a question about the past.
- **Resolve the announcement synchronously at discovery time.** Needs a MAL call for the airing status from inside a write path, on a code path that runs inside `RefreshStaleBatchAsync`'s loop and inside request handling. That is what the batched, budgeted, once-per-tick resolver exists to avoid.
- **Have the series rebuild fetch without stamping `LastSyncedAt`.** Would break `RefreshTiers`, `AnimeDetailService`'s TTL and `RelationResolver`'s far-end-fetched test, all of which read that field for the same meaning. Fixing the updates feed by corrupting the field four other features depend on is the worse trade in every direction.

### D2. The column is nullable, and null means "fall back to today's check"

`bool?`, with no backfill. A null verdict means the row was written before this change, and the resolver evaluates such a row exactly as it does today, from the anime's current `LastSyncedAt`.

The live database has 21 discoveries, all processed, and processed discoveries are never reconsidered, so the fallback covers nothing that exists — it exists for rows written between now and the deployment. Backfilling those from current state would reproduce the bug for them (the rebuild has already run), and backfilling them as announceable could announce an anime that was genuinely known. Preserving today's behaviour for rows that predate the fix is the honest option and needs no judgement call about data we cannot reconstruct. It matches the precedent in `RelationDiscovery`'s own documentation, where pre-existing rows were backfilled as processed rather than guessed at.

The column can be tightened to non-nullable later, once no null rows remain, as a separate cleanup. Not worth doing now.

### D3. The capture is a batched, projected read, and it is committed state that is read

`RecordDiscoveries` becomes `async` and, before writing any row, runs one query over the distinct new far-end ids:

```csharp
db.AnimeMetadata.AsNoTracking()
  .Where(a => farEndIds.Contains(a.Id))
  .Select(a => new { a.Id, a.LastSyncedAt })
```

An id absent from the result had no row at all, and so had no full detail. One query per detection that produces discoveries at all — which needs a new edge *and* a non-Dropped list entry of the user's, so a handful of times a week, not per refresh.

The projection is deliberate, not incidental. `RefreshStaleBatchAsync` refreshes up to twenty anime against one `DbContext` and saves once at the end (`MetadataRefreshService.cs:63-118`), so a far end may be another anime in the same batch whose `ApplyTo` has already stamped `LastSyncedAt` in memory. A tracked entity query would return that modified instance through identity resolution and report full detail for an anime that has never been fetched as far as the database is concerned. A projection reads the committed row, which is the same "before this unit of work" basis `AnimeMetadataSnapshot` already uses for the owner.

### D4. The capture happens before the enqueue, and that ordering is documented

Within `RecordAsync`, the far-end lookup and the discovery writes stay ahead of `seriesBuildTrigger.Enqueue`, as they already are. The ordering is now load-bearing rather than arbitrary, and gets a comment saying so: the enqueue is what triggers the fetch that used to erase the evidence, and it must not run before the evidence is read.

In principle the enqueue could be moved past `SaveChangesAsync` in each caller to make the ordering structural rather than conventional, but that means touching every writer of anime metadata to fix a problem a comment and a test already close.

### D5. Only the never-fully-fetched half of the gate moves; the list-entry half stays a read-time check

The gate has two halves (`AnnouncementResolutionService.cs:55-56`). Only `hadFullDetail` is corrupted by the system's own background work. `hasEntry` changes only when the user adds the anime to their list themselves, and `anime-updates` already rules that an announcement recorded for an anime that holds a list entry at record time is false and is deleted (spec.md:313). Snapshotting `hasEntry` at discovery time would manufacture exactly the announcements that rule exists to remove — an anime the user added in the window between discovery and resolution would get a card that the same specification calls false.

Read at resolve time, it keeps saying the useful thing: if the user has since added the anime, they already know about it, and there is no news to deliver.

For 65024 this makes no difference to the replayed outcome. The user added it on 2026-09-22, three days after the discovery was resolved on 2026-09-19; under the fix it would have been announced at 22:32 on the 19th, while it still held no entry.

### D6. An anime is announceable if any of its pending discoveries says it was new

The resolver groups pending discoveries by `RelatedAnimeId`, and a group can mix verdicts — an edge from one list entry appearing while the anime was unknown, and another, later edge from a second list entry appearing after something fetched it. The group is announceable when **any** of its rows recorded the anime as never-fully-fetched; a null row contributes today's read-time verdict rather than nothing.

Any, not all: a discovery that was genuinely news does not stop being news because a second, later edge to the same anime was not. The once-per-anime dedupe in `AnimeUpdateRecorder` (`AnimeUpdateRecorder.cs:36`) already guarantees at most one Announced card per anime for all time, so "any" cannot produce duplicates.

### D7. The resolver fetches only when it still needs to

Today's fetch is unconditional for an announceable anime, and its comment explains why: an announceable anime was by definition one with a missing or lean row, so a fetch was always needed for the airing gate to have a status to read. With D1 that implication is gone — an announceable anime commonly *does* have full detail by the time the resolver sees it, because the series rebuild fetched it two seconds after the discovery. The resolver now fetches only when the cached row is missing, or exists with `LastSyncedAt == default` (lean, hence no airing status), and otherwise reads the airing status it already has.

This is what `anime-updates` already asks for (spec.md:455, "fetching it from MyAnimeList where it has none, or where its only row is a lean listing one"); the unconditional fetch was an implementation shortcut justified by an implication that this change removes. It also means the common announcement now costs zero MAL calls, so a tick's ten-anime allowance goes further and the nightly cap is spent on anime that actually need fetching.

Consequence for the failure paths: the not-found, outage and generic-failure branches only run when a fetch actually happens. An anime whose cached row already answers the question cannot fail, cannot end the pass, and records no attempt on the tally.

The airing status read this way may be minutes or, in the rare case, days old rather than freshly fetched. That is consistent with how the gate is already specified — evaluated once at record time, never re-evaluated, and explicitly allowed to be outrun by the show starting to air (spec.md:421).

## Risks / Trade-offs

- **A schema change for a one-card bug.** → The column is one nullable boolean with no backfill, no index and no data migration, and the alternative designs all trade it for a worse coupling (D1). The bug is systematic rather than rare: every genuinely new far end the series rebuild can reach is affected, and the low count to date only reflects how recently the gate landed.
- **`RecordDiscoveries` becomes async and adds a query.** → It runs only when a detection both finds a new edge and the owning anime is a non-Dropped list entry of the user's, and it is one batched projection over a primary key. `RecordAsync` is already async and already awaits `IsOwnNonDroppedEntryAsync` on the same path.
- **The conditional fetch (D7) could announce from a stale cached airing status.** → In the common case the row was fetched seconds after the discovery by the rebuild. In the uncommon case the row is full detail from some earlier fetch, which means the anime was *not* new and the verdict makes it unannounceable anyway. The remaining sliver is an anime fetched between the discovery and the tick by something other than the rebuild, minutes old at worst.
- **Existing resolver tests build discoveries with no verdict.** → They land on the null fallback and keep asserting today's behaviour, which is what D2 intends; new tests cover the recorded-verdict paths explicitly. Worth checking case by case during implementation rather than mass-updating the fixtures, since each one documents a rule.
- **Deploying while a discovery is pending.** → It resolves under the fallback, i.e. exactly as it would have without the change. No loss relative to today; no gain either. There are none pending as of 2026-09-26.

## Migration Plan

1. Add the column and the EF migration. Nullable, no default, no backfill.
2. Deploy the backend. Existing rows keep null and the fallback; new discoveries carry a verdict from the first detection after startup.
3. Verify against the live database once a genuinely new far end next appears: the discovery row should carry `RelatedAnimeHadFullDetail = false` for an anime with no prior row, and an `AnimeUpdates` row with the Announced kind should follow within one tick.

Rollback is a straight revert plus the down migration; the column is read by nothing else, and discoveries written while it was live resolve under the fallback afterwards.

## Open Questions

None.
