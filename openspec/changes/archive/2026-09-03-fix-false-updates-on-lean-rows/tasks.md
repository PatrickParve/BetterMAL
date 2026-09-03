## 1. The detector learns what a first observation is (design D1, D3, D4)

- [x] 1.1 Add `bool HadFullDetail` to `AnimeMetadataSnapshot` in `Services/Updates/AnimeMetadataChangeDetector.cs`, and make `Relations` nullable (`HashSet<(int, string)>?`), documenting on the record that `null` means *relations were not observed by this write* — the lean-listing case — rather than *this anime has no relations*.
- [x] 1.2 Set `HadFullDetail = anime.LastSyncedAt != default` in `Snapshot()`. Comment why it must be captured here rather than read in `RecordAsync`: `ApplyTo` stamps `LastSyncedAt = now`, so by record time every row looks fully fetched. Name `RefreshTiers`/`AnimeDetailService`/`RelationResolver` as the other readers of the same test, and `metadata-refresh`'s "SHALL NOT infer detail-completeness from the presence of any particular field" as why it is not a per-field check.
- [x] 1.3 Add `SnapshotListing(AnimeMetadata anime)` to `IAnimeMetadataChangeDetector` and its implementation: same episode-count/premiere-date/broadcast capture and same `HadFullDetail`, with `Relations = null`. Document that it exists for `ApplyLeanTo` callers, which neither touch relations nor `Include` them — so diffing would read an unloaded collection as an empty set and manufacture removals.
- [x] 1.4 Make `RecordDiscoveries` return `false` immediately when `before.Relations is null`, so a listing write can never enqueue a series build or write a `RelationDiscovery`.
- [x] 1.5 Make `RecordFieldUpdates` return immediately when `!before.HadFullDetail`, with a comment stating the rule from `anime-updates`: the first *full-detail* fetch is the first observation, and a lean row's null premiere date was never an observation of anything.
- [x] 1.6 Update the `RecordAsync` doc comment: it currently says callers must skip it on the insert branch. Keep that, and add that a row cached only by a lean listing write is likewise not a prior observation of the detail fields — which the detector now enforces itself rather than trusting the caller.

## 2. A premiere date is news only while a show has not finished airing (design D2)

- [x] 2.1 In `RecordFieldUpdates`, widen the finished-airing mask from `StartDateChanged | BroadcastSlotChanged` to also include `StartDateReleased`, renaming the local constant to something that no longer claims to be schedule-only (e.g. `airingGatedKinds`).
- [x] 2.2 Extend that block's comment with the reasoning for the reveal: a premiere date arriving for a show that finished years ago is MAL's records reaching us, not a date anyone is waiting for; and state explicitly that `EpisodeCountReleased` is deliberately **not** in the mask, because `anime-updates` specifies the AniList-supplied total precisely for finished anime MAL publishes no count for.
- [x] 2.3 Confirm the gate reads the freshly-written `anime.AiringStatus` (post-`ApplyTo`) and is therefore evaluated once at write time, matching `AnnouncementResolutionService`'s announcement gate — not re-evaluated on read.

## 3. Lean listing writes stop being a blind spot (design D4)

- [x] 3.1 Inject `IAnimeMetadataChangeDetector` into `Services/Season/SeasonBrowseService.cs`. In `FetchAndCacheAsync`'s existing-row branch, take `SnapshotListing(existing)` before `ApplyLeanTo`, and call `RecordAsync` after the hand-written `tracked.AiredFrom = ...` line so the premiere date is inside the diff. Leave the created-row branch undetected, and extend the existing `AiredFrom` comment to say the write is now detected rather than absorbed.
- [x] 3.2 Same treatment in `Services/Library/TopAnimeService.cs`'s existing-row branch (episode count only — it writes no premiere date).
- [x] 3.3 Same treatment in `Services/Sync/ReconciliationService.cs`, which only ever *creates* lean rows today (`if (existingAnimeIds.Add(animeId))`); confirm whether the else-path needs a lean upsert at all, and if it stays create-only, record nothing and note why in the comment rather than adding a detector call that can never fire.
- [x] 3.4 Check the batching: all three call sites already own their `SaveChangesAsync`, and `RecordAsync` only adds to the tracked context — confirm no call site saves between snapshot and record.

## 4. Retire the reveals already recorded against finished anime (design D5)

- [x] 4.1 Add a data-only EF migration (no schema change; the model snapshot must come back unchanged). Building needs the `sdk:10.0` Docker image — the local SDK is 9.0 — and the bind-mount caveat under `~/Documents` applies.
- [x] 4.2 In `Up`, clear the `StartDateReleased` bit where the anime had already finished airing when the update was detected:
  `UPDATE "AnimeUpdates" u SET "Kinds" = u."Kinds" & ~4 FROM "AnimeMetadata" a WHERE a."Id" = u."AnimeId" AND (u."Kinds" & 4) <> 0 AND a."AiringStatus" = 'finished_airing' AND COALESCE(a."AiredTo", a."AiredFrom") < (u."DetectedAt" AT TIME ZONE 'UTC')::date;`
  then `DELETE FROM "AnimeUpdates" WHERE "Kinds" = 0;`
- [x] 4.3 Comment the migration with why it is bounded by `DetectedAt` rather than today's status (a reveal recorded while a show was still airing is legitimate and must survive the show finishing), and why it clears a bit rather than deleting rows (`Kinds` is a flags column and the row may also carry a legitimate episode-count release).
- [x] 4.4 Leave `Down` empty with a comment that the retirement is irreversible, following the `AddAnimeUpdatesAndRelationDiscoveryProcessedAt` precedent that baselined pre-existing discoveries as processed.

## 5. Tests

- [x] 5.1 **Fix the existing fixtures first.** Every detection test in `Services/Metadata/MetadataRefreshServiceTests.cs` seeds its row with `LastSyncedAt` at default, so under 1.5 they would all fall silent. Give the rows that stand for an already-fully-fetched anime an old-but-non-default `LastSyncedAt` (old enough to stay stale for `RefreshStaleBatchAsync`'s tier ladder). `RefreshStaleBatchAsync_UnknownToKnownStartDateRecordsRelease` sets `LastSyncedAt = default` deliberately, to be selected as maximally stale — it needs the timestamp that keeps it selected *and* fully-fetched.
- [x] 5.2 New test: an anime cached leanly and never fully fetched, whose first full-detail fetch supplies both an episode count and a premiere date, records **no** update (the reported bug).
- [x] 5.3 New test: that same first full-detail fetch still writes a `RelationDiscovery` row per edge and still enqueues the series build (design D3), so 1.5 is proved not to have silenced discovery.
- [x] 5.4 New test: a fully-fetched, `finished_airing` anime whose premiere date moves unknown → known records nothing; the same transition on `not_yet_aired` and `currently_airing` still records `StartDateReleased`.
- [x] 5.5 New test: a fully-fetched, `finished_airing` anime whose episode count moves unknown → known **does** record `EpisodeCountReleased` (D2's deliberate asymmetry — this is the test that stops a future reader "tidying" it into the mask).
- [x] 5.6 New tests in `Services/Season/SeasonBrowseServiceTests.cs`: a season browse over a fully-fetched anime records the premiere-date change it writes; over a never-fully-fetched row it records nothing; and in neither case does it write a `RelationDiscovery`.
- [x] 5.7 Extend `Services/Updates/AnimeMetadataChangeDetectorSeriesBuildTests.cs` to cover that a listing snapshot (`Relations = null`) never enqueues a series build.
- [x] 5.8 Run the backend test suite and confirm no other fixture depended on a default `LastSyncedAt` meaning "diff me".

## 6. Verify against the real database

- [x] 6.1 Before deploying, list the rows the migration will retire — `AnimeUpdates` carrying `StartDateReleased` joined to `AnimeMetadata` on the D5 predicate — and confirm the reported anime (*Clannad: After Story - Another World, Kyou Chapter*) is among them and that nothing recorded for an unaired or airing show is.
- [x] 6.2 After deploying, confirm the Home page's Updates row no longer shows those cards, that the history still holds the legitimate older entries, and that a subsequent scheduled refresh pass does not re-record any of them.
- [x] 6.3 Update `openspec/specs/anime-updates/spec.md` via the archive step (`/opsx:archive`), not by hand.
