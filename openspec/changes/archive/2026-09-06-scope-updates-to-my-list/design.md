## Context

`anime-updates` splits its work across a write side and a read side. The write side hangs detection off every function that writes cached anime metadata (`IAnimeMetadataChangeDetector`, `AnnouncementResolutionService`, `EpisodeScheduleRefreshService`), deliberately, so no refresh path can silently skip it. The read side (`AnimeUpdateService`) decides *whose* news each row is, at read time, from the user's list and the anime's relation edges.

The original design put every relevance question on the read side on purpose — archived `2026-08-26-add-anime-updates-feed` design decision **D4**, "eligibility is derived at read time, not stored", justified by mutability: dropping a show should retire its news with no cleanup, and adding an anime should surface news already recorded for it. The first half of that is still wanted. The second half is exactly the behaviour this change removes.

Two costs have shown up in the live database:

- **Volume.** 13,473 of 14,088 cached anime are not list entries. Season browsing, Top-Anime and any detail page the user opens all cache anime and all run detection, so 456 of 470 relation-discovery rows and 7 of 14 update rows describe anime the read side then discards.
- **A concrete bug.** Archived decision **D3** deliberately kept relation discovery *outside* the first-full-fetch gate ("a lean row's first full fetch still discovers all its edges"). *Yani Neko Mini Anime*, cached leanly by a season page, got its first full fetch when its page was opened; its whole relation set was recorded as newly discovered; the edge to *Yani Neko* resolved into an announcement for an anime the user had been Watching since 16 Aug.

Constraints the design has to respect:

- The relation comparison has a **second consumer**: `ISeriesBuildTrigger`, which rebuilds a series when a new edge appears so a new entry reaches the series page without a visit. Narrowing what gets *recorded* must not narrow what gets *compared*.
- The read-side eligibility test is non-trivial — both edge directions, every MAL relation type, minus edges AniList has contradicted — and the write side must apply *the same* test, not a re-derived approximation, or the two will disagree about what news exists.
- Backend is .NET 10 on PostgreSQL; the local SDK is 9.0, so builds go through the `sdk:10.0` Docker image (and the `~/Documents` bind-mount caveat applies). Data-only EF migrations with an empty `Down` are the established pattern for retiring rows (`RetireFalseStartDateReleasedUpdates`).

## Goals / Non-Goals

**Goals:**

- Nothing is written to the updates log for an anime that is neither the user's non-Dropped entry nor one relation step from one.
- Adding an anime to the list never surfaces news predating the moment it became the user's.
- A first full-detail fetch is a baseline, not a discovery — closing the *Yani Neko* class of bug at its source.
- The write-side gate and the read-side filter are literally the same code.
- Series rebuilds keep firing for every anime whose relations change.
- The rows already recorded under the old rules are removed.

**Non-Goals:**

- Changing what gets **cached**. Season browsing, Top-Anime and search cache exactly as much as they do today; only the news checker falls silent for them.
- Changing display eligibility, the reason text, the card layout, the 30-day window, or the history overlay.
- Changing the nightly refresh scope (the user's list plus unaired anime linked to it) or `AdjacentAnimeSet`.
- Retiring news retroactively when an entry is dropped. Read-time evaluation still handles that, with no cleanup job — decision D4's surviving half.
- Announcing anime two relation steps away, or re-announcing an anime after a genuine retraction.

## Decisions

### D1. The relevance gate goes in `AnimeUpdateRecorder`, not at each call site

`AnimeUpdateRecorder.RecordAsync` is the only place an `AnimeUpdate` row is created. Putting the gate there covers all four callers at once — `AnimeMetadataChangeDetector.RecordFieldUpdates`, `AnnouncementResolutionService`, and `EpisodeScheduleRefreshService`'s episode-count and episodes-moved call sites — and covers any writer added later, which is the same argument that put detection on the write functions in the first place (archived D8).

*Alternative rejected:* gating in `AnimeMetadataChangeDetector` alone. It would miss both `EpisodeScheduleRefreshService` call sites and the announcement path, and would leave the recorder — the thing whose entire job is deciding whether a row is written — with only half the rules.

*Consequence:* the gate runs after the caller has computed its kinds, so a little detection work is done for anime that record nothing. That is the right order anyway: it keeps the gate one query behind a cheap `kinds == 0` early-out, and it keeps each caller's diff logic ignorant of list membership.

### D2. The eligibility test is extracted into one service both sides call

New `Services/Updates/AnimeUpdateRelevance.cs`:

```csharp
public interface IAnimeUpdateRelevance
{
    // Write side. Loads what it needs from the anime id alone.
    Task<bool> IsRelevantAsync(int animeId, CancellationToken ct = default);

    // Read side. The qualifying affiliation edge, or null — the reason text
    // AnimeUpdateService renders is built on top of this.
    Task<ResolvedRelationEdge?> FindAffiliateAsync(AnimeMetadata anime, CancellationToken ct = default);
}
```

`AnimeUpdateService.TryDeriveAffiliateReasonAsync` becomes a thin wrapper: call `FindAffiliateAsync`, humanize the winner. Its relation-precedence ordering and `RelationInverse` humanizing stay where they are — those are display concerns, not eligibility.

*Why one service rather than two independent implementations:* the spec's claim is that the write gate **is** the read filter. Two copies of "both directions, any relation type, minus Contradicted" would drift, and the failure mode is silent — news recorded that can never be shown, or worse, news suppressed that would have been.

*Alternative rejected:* storing eligibility on the `AnimeUpdate` row at write time and reading it back. That would break the surviving half of D4 — dropping an entry would need a cleanup job to retire its news.

### D3. The write-side test works from an anime id and queries the database, not from the caller's entity

`IRelationResolver.GetEdgesAsync(anime)` requires `anime.RelatedAnime` to be loaded. At recorder time that holds for the full-detail paths, but **not** for the lean listing paths (`SeasonBrowseService`, `TopAnimeService` neither `Include` relations nor touch them) nor for `EpisodeScheduleRefreshService`. An unloaded collection reads as empty, which would hide every outgoing edge and judge a genuinely linked anime irrelevant.

So `IsRelevantAsync(animeId)` loads its own state, in three steps, cheapest first:

1. `UserAnimeEntries.Any(e => e.AnimeId == animeId && e.Status != Dropped)` — one indexed lookup, and the common relevant case.
2. A single existence query over `AnimeRelatedAnime` in **both** directions joined to non-Dropped `UserAnimeEntries`. No qualifying pair at all ⇒ irrelevant, return without touching the resolver. This is the common **irrelevant** case — every stranger a season browse writes — and it costs one query.
3. Only when step 2 finds candidates: load the anime `AsNoTracking().Include(a => a.RelatedAnime)` and run `IRelationResolver.GetEdgesAsync` to apply the Contradicted rule, then re-check the far ends.

`AsNoTracking` in step 3 is deliberate: it reads committed state, ignoring the caller's in-flight unsaved changes, and matches the read side exactly. The staleness this introduces is bounded and harmless — the only edges invisible to it are ones written in the same not-yet-saved unit of work, which belong to an anime's own fetch, and an anime whose relations were just replaced by its *first* full fetch records no field updates anyway (D5 below). The announcement path runs in a separate pass over already-committed discoveries.

### D4. Discovery recording is gated; discovery *comparison* is not

`AnimeMetadataChangeDetector.RecordAsync` currently computes new edges and writes a `RelationDiscovery` per edge in the same loop. Split those:

```
newEdges = diff(anime.RelatedAnime, before.Relations)   // pure; skipped when before.Relations is null
if (newEdges.Any())            seriesBuildTrigger.Enqueue(anime.Id)     // unchanged, every anime
if (before.HadFullDetail && await IsOwnNonDroppedEntryAsync(anime.Id))  // the two new gates
    foreach (e in newEdges)    db.RelationDiscoveries.Add(...)
```

- `before.HadFullDetail` is **R4**: a first full fetch is a baseline. This is a direct reversal of archived decision **D3**, whose scenario "A lean row's first full fetch still discovers its relations" is the bug.
- The own-entry check is **R2**: discoveries are the raw material for announcements, and an announcement is only ever wanted for something appearing on the user's own show. It is a plain entry lookup, not `IAnimeUpdateRelevance` — R2 is deliberately narrower than R1. One step from the user's list is where news comes from; one step from *that* is not.
- The enqueue stays outside both gates, which is **R5**. A first full fetch is exactly when a series most needs rebuilding, and a stranger's relations still shape series the user can reach.

*Alternative rejected:* keeping discovery writes unconditional and filtering in `AnnouncementResolutionService`. It leaves the 456-row pile-up in place and spends the resolver's MAL calls on anime that can never announce.

### D5. The announcement gate is evaluated before the resolver's own fetch

`AnnouncementResolutionService` currently fetches the newly-related anime when it has no cached row, then gates on airing status. That fetch stamps `LastSyncedAt`, so "had we ever fully fetched this?" is unanswerable afterwards — the same trap `AnimeMetadataSnapshot.HadFullDetail` already exists to avoid on the metadata path.

New order per discovery group:

1. Read the cached row (if any) and capture `hadFullDetail = row is { LastSyncedAt: not default }`.
2. Capture `hasEntry = UserAnimeEntries.Any(e => e.AnimeId == animeId)` — *any* entry, not just non-Dropped: an anime the user has ever tracked is not a new show.
3. `announceable = !hadFullDetail && !hasEntry`. When false: mark the discoveries processed, spend **no** MAL call, record nothing.
4. When true and the row is absent or lean, fetch it (needed for airing status), then apply the existing not-finished-airing gate and record.

Step 4 also closes a latent hole: today the fetch happens only when the row is entirely absent, so a lean row's null `AiringStatus` fails the airing gate and the anime is silently never announced. Under `announceable`, "never fully fetched" is precisely the set that needs the fetch.

*Why `hasEntry` ignores Dropped:* R3 asks whether the system had ever seen this anime, not whether the user currently cares. A dropped entry is still proof it is not a new discovery.

### D6. The retirement migration is data-only SQL, with the Contradicted rule written out

Following `RetireFalseStartDateReleasedUpdates`: one migration, no schema change, model snapshot untouched, empty `Down` with a comment that the retirement is irreversible.

Three statements:

1. **Announcements about anime already tracked when they were announced** (R3's second clause) — `DELETE FROM "AnimeUpdates" u WHERE (u."Kinds" & 1) <> 0 AND EXISTS (SELECT 1 FROM "ActivityLogs" a WHERE a."AnimeId" = u."AnimeId" AND a."Timestamp" < u."DetectedAt")`. This is what removes the *Yani Neko* row (70) by rule rather than by id. An announcement is never merged with another kind (the resolver records `Announced` alone), so deleting the row is safe and no bit-clearing dance is needed.

   The rule is deliberately about state *at the moment the announcement fired*, not state *now*: an anime the system already knew about before announcing it was never a new show, but an anime that was genuinely unknown when announced, and added to the list only afterward — plausibly because of the announcement — was real news at the time and stays recorded regardless of when it was added. `UserAnimeEntries` carries no added-at timestamp, so `ActivityLogs` (Added, StatusChanged, episode and score events — any of them proves the anime was already a tracked entry) is the source of truth for ordering. A first draft of this statement joined `UserAnimeEntries` directly with no timing check at all — "is this anime tracked" rather than "was this anime tracked when announced" — and over-deleted two rows (announcements for anime added to the list shortly after they were announced) before task 7.1's live-database verification caught it.
2. **Rows failing R1** — delete every `AnimeUpdates` row whose anime is neither a non-Dropped entry nor connected to one by a non-Contradicted edge.
3. **Discoveries failing R2** — `DELETE FROM "RelationDiscoveries" d WHERE NOT EXISTS (SELECT 1 FROM "UserAnimeEntries" e WHERE e."AnimeId" = d."AnimeId" AND e."Status" <> 'Dropped')`. `Status` is stored as text (`HasConversion<string>`).

Statement 2 needs the Contradicted rule in SQL. It is exactly expressible, because **`Contradicted` never depends on AniList's relation *type*** — `RelationResolver.Adjudicate` reaches `Contradicted` only when `matchingEdges` is empty, and `AniListRelationTypeMapper.Matches` is consulted only to choose between `Confirmed` and `Corroborated`. So for a pair (V, F):

```
contradicted(edge) = bothKnownToAniList(V,F)                       -- AnimeAiringSyncs rows for both,
                                                                   -- AniListId NOT NULL and RelationsFetchedAt NOT NULL
                  AND NOT EXISTS(AniListRelations between V and F, either direction)
                  AND eligible(edge)                               -- invert(type) IS NULL OR the other side fully fetched
                  AND NOT malConfirmed(edge)                       -- the far end stores the inverse type back
```

`invert(type) IS NULL` is a fixed ten-value `NOT IN` list, from `RelationInverse.Map`: `sequel, prequel, parent_story, side_story, summary, full_story, alternative_version, alternative_setting, character, other`. "Fully fetched" is `"LastSyncedAt" <> '0001-01-01T00:00:00Z'`. Reverse-derived edges are only considered where V stores no outgoing edge to F, mirroring the resolver's dedup, and `malConfirmed` is impossible for them by construction.

*Why duplicating confidence logic in SQL is acceptable here, when D2 argues against duplication:* a migration runs once against a known database and is then frozen. It cannot drift out of step with the resolver, because it never runs again. The live-code duplication D2 forbids is a different thing entirely.

*Alternative rejected:* a run-once startup pass calling `IAnimeUpdateRelevance` directly. It would need its own completion marker, and running it unguarded on every boot would turn a one-off cleanup into a permanent retirement rule that deletes news whenever an entry is dropped — the exact behaviour R6 says must stay reversible.

*Alternative rejected:* dropping the Contradicted clause and deleting on the looser "any edge at all" test. That is conservative (it can only under-delete), but the brief asks for the real test in the migration rather than a hand count, and the residue would be permanently invisible rows nobody can account for later.

Before the migration ships it is run as a `SELECT` against the live database and checked against the expected outcome — 8 rows deleted, 6 kept (69, 68, 67, 42, 41, 9) — the same pre-deploy verification the previous retirement migration used.

### D7. The read-side scenario "Adding the anime surfaces its news" is deleted, not reworded

Under R1 there is no row to surface, so the scenario would describe behaviour that cannot occur. Its removal is the deliberate behaviour drop, not an oversight: it is precisely the mechanism that put two-week-old news on newly added anime. The other four read-side scenarios (own entry qualifies, sequel shown, dropping retires, contradicted edge does not qualify) are unchanged, and read-time evaluation itself is unchanged.

## Risks / Trade-offs

- **A genuinely relevant update is dropped because the gate ran a moment too early.** An anime that becomes list-adjacent *after* an update was detected records nothing, and there is no backfill. → This is the intended trade (worked examples 1–3 in the brief). The window is small in practice: the nightly refresh covers the list plus unaired linked anime, so anything actually adjacent is re-examined within a day, and a genuine later change is recorded then.
- **Relation-set staleness in the write gate** (D3's `AsNoTracking` read). An edge written in the same unsaved unit of work is invisible to the gate. → Bounded to an anime's own fetch, and that fetch's field updates are already silenced by `HadFullDetail` when it is the first one. Later passes see the committed edges.
- **Extra queries on the write path.** Every candidate update costs one entry lookup, most cost a second existence query, and the few with candidate edges cost a resolver call. → Step 2 of D3 makes the mass-irrelevant case (a season browse over ~300 strangers) one cheap query per *changed* anime, and only anime whose fields actually moved reach the gate at all.
- **The SQL Contradicted rule is subtly wrong and deletes a row it should keep.** → Deletion is irreversible, so the migration is run as a `SELECT` first and reconciled against the expected 8-deleted / 6-kept outcome before it ships. The kept set is small enough to eyeball.
- **Series rebuilds silently stop.** The refactor in D4 moves the enqueue away from the code that writes discovery rows, and a test suite that only asserts on discovery rows would not notice. → An explicit test that a first full fetch of a stranger writes zero discoveries **and** still enqueues the build.
- **Existing tests seed anime with no list entry.** Every detection test that asserts an update is recorded will start failing once the gate lands, and the failure looks like a regression rather than a fixture problem. → Fixture updates are the first task of the test section, ahead of the new cases, as in the previous change.

## Migration Plan

1. Ship the code gates and the extracted relevance service together; they are inert with respect to existing rows.
2. Run the migration's statements as `SELECT`s against the live database; confirm 8 rows deleted, 6 kept, and 456 of 470 discoveries removed.
3. Deploy. `db.Database.Migrate()` at startup applies it.
4. Confirm afterwards: the menu shows the six surviving rows, browsing a season page records nothing new, and the nightly pass still announces genuinely new sequels.

Rollback: reverting the code restores the old write behaviour, but the deleted rows do not come back — `Down` is deliberately empty, matching `AddAnimeUpdatesAndRelationDiscoveryProcessedAt` and `RetireFalseStartDateReleasedUpdates`. The data destroyed is exactly the data this change declares was never news.

## Open Questions

None. The three questions the brief raised — whether linked anime should keep being refreshed after they start airing, whether dropped entries record anything, and whether the stale discoveries are archived rather than deleted — are settled in the brief as: no change to refresh scope, dropped entries record nothing, and delete rather than archive.
