## Context

`AnimeMetadataChangeDetector` is the one place a before/after `AnimeMetadata` pair becomes `AnimeUpdate` rows. Callers snapshot, call `ApplyTo`, then hand both to `RecordAsync`; the insert branch skips detection entirely, on the stated grounds that "a first observation is not a reveal and not a move". That contract is sound. What breaks it is that **the caller decides it is an insert by asking whether a row exists**, and the app caches anime rows two very different ways:

- **Full detail** (`ApplyTo`, from `GetAnimeDetailsAsync`) — writes everything and stamps `LastSyncedAt`.
- **Lean listing** (`ApplyLeanTo`) — writes title, picture, score, rank, rating and episode count, and *deliberately* leaves airing status, dates, broadcast, studio, genres, synopsis and relations alone so a browse cannot clobber richer data. It never stamps `LastSyncedAt`.

Three paths create rows leanly: `ReconciliationService` (an entry added on MAL's own site), `TopAnimeService`, and `SeasonBrowseService`. Such a row exists, so the *next* full-detail fetch takes the update branch — and diffs the real premiere date against a null that was never an observation of anything. That is the reported bug: *Clannad: After Story - Another World, Kyou Chapter*, finished long ago, announced as though MAL had just revealed its premiere.

The rest of the app already has the right question and the right answer for it. `metadata-refresh` requires: *"The system SHALL NOT infer detail-completeness from the presence of any particular field"* — and `RefreshTiers`, `AnimeDetailService`, `RelationResolver` and `RelationAdjudicationService` all test `LastSyncedAt == default` to mean "never fully fetched". The detector is the only reader of this data that does not ask.

Two smaller things fall out of looking at the same code:

- The becoming-known kinds are recorded *at most once per anime, ever*, which is what makes them idempotent — but that guard only protects a kind **already recorded**. A reveal that was never recorded can be produced by any future `null → value` flap, and `ApplyTo` writes `AiredFrom = ParseMalDate(node.StartDate)` unconditionally, so one fetch that comes back without a `start_date` re-arms it. There is no airing-status gate to stop it landing on a show that finished in 2008, though the schedule-change kinds have exactly that gate for exactly that reason.
- `SeasonBrowseService` writes `AiredFrom` by hand, outside detection, right after `ApplyLeanTo`. A genuine premiere-date change on any browsed anime is therefore *absorbed*: the value is stored without news, and the next full fetch finds nothing changed. Same family of bug, opposite sign.

## Goals / Non-Goals

**Goals:**
- The feed reports the world changing, never the cache catching up.
- One definition of "first observation", shared with the rest of the app and read from the same field.
- A premiere date is news only where a premiere is still ahead of someone.
- Every path that writes a detected field either detects, or is provably a first observation — no path silently absorbs a change.
- The false updates already on the Home page go away on deploy, without taking legitimate ones with them.

**Non-Goals:**
- No change to what a lean write stores (D6), and no new columns anywhere. The fix reads state the schema already holds.
- No change to relation discovery, announcement resolution, or the series-build trigger. A first full fetch keeps discovering its edges (D3).
- No change to the AniList airing refresh. Its reveal is guarded by the effective total, which a lean write populates whenever MAL publishes one.
- Not a retroactive audit of episode-count reveals. Which of those came from a lean row is not recoverable from the data (D5).

## Decisions

### D1. "First observation" means the first full-detail fetch, carried on the snapshot

`AnimeMetadataSnapshot` gains `HadFullDetail`, captured in `Snapshot()` as `anime.LastSyncedAt != default`. `RecordFieldUpdates` returns immediately when it is false.

It has to ride the snapshot rather than be read at record time, because `ApplyTo` sets `LastSyncedAt = now` — by the time `RecordAsync` runs, every row looks fully fetched. This is the same reason the snapshot exists at all.

*Why the row-level timestamp and not the field:* a per-field test ("was `AiredFrom` ever populated?") cannot distinguish an anime whose premiere date we never fetched from one MAL genuinely has no date for, which is precisely the inference `metadata-refresh` forbids. `LastSyncedAt` answers the question the detector actually needs — *did anyone ever look?* — and answers it identically for every field a lean write skips.

*Alternative rejected:* have the lean paths mark their rows as partial with a new column. It duplicates a fact `LastSyncedAt` already carries, and would need backfilling for the rows already in this state — which is the entire population the bug affects.

*Alternative rejected:* have callers pass `isFirstFullFetch` at each call site. Three call sites today, and the next one added is the one that forgets. The snapshot is already the mechanism that makes this impossible to skip.

### D2. Premiere-date-released comes under the finished-airing gate; episode-count-released does not

The mask that today clears `StartDateChanged | BroadcastSlotChanged` for an anime whose freshly-written status is not `not_yet_aired` or `currently_airing` widens to include `StartDateReleased`.

*Why gate the reveal at all when D1 removes its cause:* D1 removes the cause that exists **today**. The gate removes the whole class — the flap described in Context, and any future write path that leaves `AiredFrom` momentarily null. Reveals are once-per-anime-forever only after they have been recorded once; before that they are unprotected, so the cheap structural guard is worth having.

*Why the gate is evaluated at write time and never re-evaluated:* the same treatment `Announced` already gets (add-anime-updates-feed D6). A reveal recorded while a show was still unaired stays on the feed after it starts and finishes airing; re-evaluating at read time would make cards vanish as shows ended, which is not what the eligibility rules are for.

*Why `EpisodeCountReleased` stays out of the mask:* for a finished show, a premiere date describes an event already in the past that the anime's own record already displays — there is nothing to act on. An episode count is a fact about what there is to watch, and `anime-updates` explicitly specifies the AniList-supplied total for the case where "MyAnimeList publishes none" — largely finished OVA/ONA entries. Gating it would delete a specified behaviour to fix a bug it is not causing.

### D3. Only the field diff falls silent; relation discovery is untouched

`RecordAsync` keeps calling `RecordDiscoveries` unconditionally. A lean row holds no relations, so its first full fetch discovers all of them, resolves each through `AnnouncementResolutionService`, and enqueues the series build — exactly as today.

*Why not skip everything, matching the insert branch:* an announcement is already gated on the newly-related anime's airing status, so a stub's relation burst announces only genuinely unaired anime — which is news, and correct. Suppressing it would also drop the `ISeriesBuildTrigger.Enqueue` that keeps a newly-cached anime's series page from waiting on a visit or the 30-day staleness window. Two separate concerns share one method; only one of them is wrong.

### D4. A lean listing write detects, when the row it writes to was already fully fetched

The three lean call sites snapshot before `ApplyLeanTo` and record after. Detection over a lean write must not diff relations — a lean write does not touch them, and the call sites do not `Include` them, so diffing would read an unloaded collection as an empty set and manufacture removals.

That is expressed by making `AnimeMetadataSnapshot.Relations` **nullable**, with `null` meaning *relations were not observed*: `Snapshot()` populates it, a new `SnapshotListing()` leaves it null, and `RecordDiscoveries` returns early on null. One snapshot type, one record method, and the "a lean write is not a relation observation" rule becomes structural rather than a comment.

Combined with D1, a lean write to a never-fully-fetched row records nothing, and a lean write to a fully-fetched row detects the two kinds its fields can produce: an episode-count reveal (every lean write carries `num_episodes`) and, for season browsing, a premiere-date reveal or change.

*Why this is in scope for a bug fix:* `SeasonBrowseService`'s hand-written `AiredFrom` is a live instance of the same defect class — a detected field written by a non-detecting path — and it costs the user real news rather than manufacturing fake news. Fixing one direction and leaving the other would leave the requirement "every path that writes anime data detects the updates it can" false in the same file the change is editing.

*Alternative rejected:* stop `SeasonBrowseService` writing `AiredFrom`. The dashboard derives season membership from it (`MainDashboardService`), and a lean-cached row would lose its premiere date entirely.

### D5. The false reveals already recorded are retired by a bounded data migration

A data-only migration (no schema change) clears the `StartDateReleased` bit where the anime had already finished airing at the moment the update was detected, then deletes any row left with `Kinds = 0`:

```sql
UPDATE "AnimeUpdates" u SET "Kinds" = u."Kinds" & ~4
FROM "AnimeMetadata" a
WHERE a."Id" = u."AnimeId" AND (u."Kinds" & 4) <> 0
  AND a."AiringStatus" = 'finished_airing'
  AND COALESCE(a."AiredTo", a."AiredFrom") < (u."DetectedAt" AT TIME ZONE 'UTC')::date;
DELETE FROM "AnimeUpdates" WHERE "Kinds" = 0;
```

*Why the `DetectedAt` comparison rather than just today's status:* a show that was airing when its premiere date was revealed and has since finished would otherwise be swept up, and under D2's write-time gate that update is legitimate. Comparing against the detection moment retires exactly the rows the new rules would never have written. `AiredTo` falls back to `AiredFrom` for the finished anime MAL publishes no end date for; both cannot be null on a row that just revealed a premiere date.

*Why clearing a bit rather than deleting rows outright:* `Kinds` is a flags column and an update can cover a legitimate episode-count release in the same row (add-anime-updates-feed D1). Deleting would take that with it.

*Why this does not re-record:* the retired rows hold a real `AiredFrom` by now, so the next diff sees known → known. The once-per-anime guard is not what is holding them back, which is what makes the retirement safe to run.

*Precedent:* the same migration that created `AnimeUpdates` baselined every pre-existing `RelationDiscovery` as processed, for the same reason — not opening a feed with a wall of backdated news.

### D6. `ApplyLeanTo` is not widened to write airing status and dates

MAL's listing payload uses the same field selection as the detail one, so a lean node genuinely carries `status`, `start_date`, `end_date` and `broadcast`. Writing them would make the stub rows hold a premiere date and close this particular hole by other means.

*Rejected because it spreads the defect D4 fixes.* Every field a non-detecting path writes is a field whose genuine change that path absorbs. Teaching `ApplyLeanTo` to write `BroadcastDayOfWeek` would mean a browse of the current season silently swallowing slot changes for every show in it — the exact bug `SeasonBrowseService`'s `AiredFrom` line already causes. The lean/full split is doing real work and stays.

## Risks / Trade-offs

- **A genuine reveal that happens between a lean write and the first full fetch is lost.** → Accepted, and unavoidable: the two are indistinguishable from the stored state. `metadata-refresh` treats a never-fully-fetched anime as maximally stale and sorts it ahead of every tiered candidate, so the window is short.

- **Detection on lean writes adds work to browse and reconciliation passes.** → A season fetch touches a few hundred rows; each adds one in-memory field comparison, and only a row that actually diffs reaches the recorder's single dedupe query. Reconciliation already writes per-entry. No new queries on the common path.

- **Lean-write detection could surface updates for anime nobody follows.** → It cannot reach the UI: eligibility is evaluated at read time and admits only non-dropped entries of mine and anime related to one. A browsed season's rows are recorded and simply never shown, which is already true of every other recorded update.

- **The migration is irreversible.** → `Down` cannot restore deleted rows and will be a no-op. The data destroyed is exactly the data this change declares was never news, and the feed is one week old; the same precedent (baselining discoveries as processed) was accepted on the same grounds.

- **`AiringStatus` on a lean-only row is null, so D2's gate cannot classify it.** → Immaterial: D1 stops detection on those rows before the gate is reached, and a fully-fetched row always has a status.

## Migration Plan

1. Ship the detector changes and the three lean call sites together — the gate (D2) must be in place before the migration runs, or the scheduled refresh can re-record what the migration clears.
2. The data migration (D5) runs on startup with the rest of the EF migrations. No downtime, no schema lock beyond the `AnimeUpdates` row updates.
3. Verify on the Home page: the retired cards are gone, and the history's older, legitimate entries are intact.

Rollback is a code revert. The retired rows do not come back, and would not be re-recorded by the reverted code either, since the anime now hold the dates whose absence produced them.

## Open Questions

None. The one judgement call — episode-count reveals staying ungated for finished anime (D2) — is settled in favour of the behaviour `anime-updates` already specifies for AniList-supplied totals.
