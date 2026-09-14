## Context

This change fixes triage items N4 and R5. The proposal has the evidence with line references. This section covers only what shapes the design. Paths without a prefix are under `backend/AnimeTracker.Api/`, and test paths are under `backend/AnimeTracker.Api.Tests/`.

**How the four pages read entries today.** Each calls `IUserAnimeEntryRepository.GetAllAsync()` and gets fully loaded, untracked `UserAnimeEntry` objects with `Anime` set. They hand those objects, or their `Anime`, to shared helpers that take the entity types:
- `AnimeRankingSnapshot.Build(IReadOnlyList<UserAnimeEntry>, …)`, whose `RankedEntries` is a `List<UserAnimeEntry>`
- `RankBandResolver.Resolve(UserAnimeEntry)` and `AnimeRankingKey.Compare`
- `IAiringWatchStatusService.SettleAsync(IReadOnlyCollection<UserAnimeEntry>, …)`
- `AiredEpisodeGate.HasAired`/`EverythingHasAired(AnimeMetadata, …)`
- `WatchMath.EpisodeSeconds`/`IsMovie`/`IsMusic(AnimeMetadata)` and `WatchMath`'s entry overloads
- `RecapEntrySelector`, `RecapStatsBuilder`, `RecapRankingBuilder` and `ScoreDivergence`, over `List<UserAnimeEntry>`
- `IEpisodeScheduleService`'s `IReadOnlyCollection<AnimeMetadata>` bulk overloads

Every one of these is also called with full entities from somewhere this change doesn't touch: the detail page, the schedule refresh job, `AnimeRankingService`, `UserAnimeEntryEditService`, `RecapAvailabilityService` and the series services.

**Re-deriving the field set.** Grepping every `.Anime.<Field>` and `anime.<Field>` read in the four services and each helper above, and following every call that takes an `AnimeMetadata`, gives exactly the 11 fields in the proposal. `EpisodeScheduleService` reads only `Id`. `ProfileService.ScheduleMissingSeriesBuildsAsync` reads only `AnimeId`. No path reads `Anime.UserEntry` or `HeldForReviewAt`.

**Test infrastructure.**
- The tests use the EF InMemory provider. There's no Postgres-backed test.
- About 20 test files each declare a private `FakeUserAnimeEntryRepository`, and each fake implements the whole interface.
- `MyListServiceRankTests` and `MyListServiceReopenTests` already use the real `UserAnimeEntryRepository` over InMemory.
- `IEpisodeScheduleService` (`Services/Airing/IEpisodeScheduleService.cs:17-51`) already uses default interface methods: "a test double need not restate it — but any implementation backed by a database MUST override this".

**How settling works today** (`Services/Entries/AiringWatchStatusService.cs`):
- One `foreach` over the caller's entries, in the caller's order. Re-open is checked first, then complete. Each qualifying entry waits on `ReopenOneAsync` or `CompleteOneAsync`, and its result is written to the caller's object as `entry.Status = …`.
- Each helper does a tracked `FirstOrDefaultAsync(e => e.AnimeId == animeId)`. EF resolves identity, so if the request's `DbContext` already tracks that entry, the tracked instance comes back with its tracked values.
- It re-checks the status. A missing row leaves the caller's status as it was (`?? WatchStatus.Completed` / `?? WatchStatus.Watching`). A moved row returns the fresh status.
- Otherwise it mutates the entry, adds an `ActivityLog`, and calls `SaveChangesAsync`, which saves everything pending in the scoped context.
- On `DbUpdateConcurrencyException` it removes the log row (which detaches the `Added` entity), reloads the entry, logs at Information, and returns the reloaded status. It never tries again.
- After a successful save it calls `syncScheduler.ScheduleSync(animeId)`.
- `IAiringWatchStatusService.cs:23-27` says settling writes back `Status` "and, for a completion, CompletedAt". The code writes back only `Status`. See Open Questions.

**What EF Core and Npgsql do on a conflicting save.**
- `UserAnimeEntry` maps `xmin` as a `uint` shadow property, `RowVersion`: `ValueGeneratedOnAddOrUpdate`, `IsConcurrencyToken` (`Data/AnimeTrackerDbContext.cs:48-58`).
- A `SaveChangesAsync` with more than one command runs them in one transaction, and rolls the whole transaction back if any command fails.
- A `WHERE xmin = @original` update that matches no row raises `DbUpdateConcurrencyException`.
- None of the settle callers has an ambient transaction. The codebase's four explicit transactions are in `EpisodeAiringRepository`, `SeriesGraphBuilder`, `TransferImportRunner` and `ExportService`.
- **InMemory does neither of these.** It never bumps `RowVersion`, and it applies a save's changes one at a time with no rollback.

**Constraints.**
- The backend builds and tests only in `mcr.microsoft.com/dotnet/sdk:10.0`, from a copy under `/private/tmp`. The local SDK is 9.0, and `~/Documents` can't be bind-mounted.
- Two commits, N4 and R5, back to back. No Claude attribution lines.

## Goals / Non-Goals

**Goals:**
- My List, Home, Profile and Recap read the 11 `AnimeMetadata` columns they use and no others.
- Their responses are byte-for-byte what they are today, and a test proves it for all four.
- A later read of a column the lean read leaves out, on any of these pages, fails a test.
- `GetAllAsync()` and its 10 other calls don't change.
- Settling moves any number of entries in one read and one save, and costs nothing when nothing qualifies.
- A conflicting concurrent change to one entry drops only that entry. The rest still save.
- The eligibility rules, what each caller sees, each moved entry's log row and sync request, and the conflict logging all stay as they are.

**Non-Goals:**
- Moving any other `GetAllAsync()` caller onto the lean read (proposal Follow-ups).
- A narrower read for `AiringScheduleService`.
- Changing `AnimeRankingSnapshot`, `RankBandResolver`, `AnimeRankingKey`, `WatchMath`, the recap builders or `IEpisodeScheduleService`.
- Changing the eligibility conditions at `AiringWatchStatusService.cs:30-32,44-47` or `AiredEpisodeGate`.
- Retrying a settle after a conflict within the same read. Today's code doesn't, and the next read does it anyway.
- Writing `CompletedAt` back onto the caller's objects (Open Questions).
- Any frontend change.

## Decisions

### D1. The lean read returns partly filled `UserAnimeEntry` objects, not a new type

```csharp
public Task<List<UserAnimeEntry>> GetAllForListViewAsync(CancellationToken ct = default) =>
    db.UserAnimeEntries.AsNoTracking()
        .Select(e => new UserAnimeEntry
        {
            AnimeId = e.AnimeId,
            Status = e.Status,
            EpisodesWatched = e.EpisodesWatched,
            MyScore = e.MyScore,
            StartedAt = e.StartedAt,
            CompletedAt = e.CompletedAt,
            RewatchCount = e.RewatchCount,
            PendingSync = e.PendingSync,
            LastSyncedAt = e.LastSyncedAt,
            HeldForReviewAt = e.HeldForReviewAt,
            Anime = new AnimeMetadata
            {
                Id = e.Anime.Id,
                Title = e.Anime.Title,
                EnglishTitle = e.Anime.EnglishTitle,
                PictureUrl = e.Anime.PictureUrl,
                MediaType = e.Anime.MediaType,
                TotalEpisodes = e.Anime.TotalEpisodes,
                AiringStatus = e.Anime.AiringStatus,
                MalScore = e.Anime.MalScore,
                PopularityRank = e.Anime.PopularityRank,
                AiredFrom = e.Anime.AiredFrom,
                AverageEpisodeDurationSeconds = e.Anime.AverageEpisodeDurationSeconds,
            },
        })
        .ToListAsync(ct);
```

- **Every entry column is copied, and only `Anime` is trimmed.** The entry row is narrow. A complete entry keeps `UserAnimeEntryDto.FromEntity`, the recap and profile selectors, and `SettleAsync`'s eligibility checks working with no field-by-field reasoning. `RowVersion` is left out. These objects are never tracked or saved.
- **EF translation.** A member-init projection gives one `SELECT` of 21 columns with an `INNER JOIN`, because `AnimeId` is a required foreign key. The same row set as the `Include`. Objects EF builds from a projection are never tracked, just like `AsNoTracking` today. `Status`'s string conversion applies inside projections. InMemory evaluates the same projection.
- **The four services change only the method name they call:** `MyListService.cs:16`, `MainDashboardService.cs:18`, `ProfileService.cs:46,93,101,168` and `RecapService.cs:16`. Every helper keeps its signature.

**Alternatives considered:**
- **A projection record, such as `UserAnimeEntryListRow(...)`.** This codebase uses records where the consumer is written for the projection (`SeriesRankingMemberProjection`, `AnimeTitleProjection`). Here the consumers are shared helpers that other callers use with full entities. Every helper in Context would need a second overload, a generic form, or an interface. `AnimeRankingSnapshot.RankedEntries` would change type, and changing the ranking snapshot is out of scope. It would cost several times the fix for compile-time safety D3 gets another way. Rejected.
- **An interface, such as `IListViewAnime`, implemented by `AnimeMetadata` and a record.** Every helper signature would still change. Rejected.
- **Table splitting**, moving `Synopsis`/`Background`/`Genres`/`PictureUrls` into an owned dependent that's loaded only for the detail page. That's a model change and a migration, and it touches every `AnimeMetadata` reader. Rejected.

**Trade-off:** a lean `AnimeMetadata` compiles anywhere a full one does. D3 and D4 replace the safety that gives up, and the interface doc comment states the contract (D2).

### D2. A default interface method that the repository overrides

On `IUserAnimeEntryRepository`:

```csharp
/// <summary>The same entries as <see cref="GetAllAsync"/>, every entry column
/// filled, but <c>Anime</c> carries only the 11 fields the list and aggregate
/// pages read (My List, Home, Profile, Recap): Id, Title, EnglishTitle,
/// PictureUrl, MediaType, TotalEpisodes, AiringStatus, MalScore,
/// PopularityRank, AiredFrom, AverageEpisodeDurationSeconds. Every other
/// AnimeMetadata property is left at its default. Never attach these objects
/// to a DbContext. ListViewReadParityTests fails if one of those pages starts
/// reading a column this leaves out. Extend the projection and that test's
/// fixture together. The default implementation returns the full read, so a
/// test double need not restate it; any implementation backed by a database
/// MUST override it.</summary>
Task<List<UserAnimeEntry>> GetAllForListViewAsync(CancellationToken ct = default) => GetAllAsync(ct);
```

`UserAnimeEntryRepository` implements it as D1 shows. A public method with the matching signature implements the interface member.

- **Why a default method:**
  - The ~20 fakes keep compiling.
  - The existing service tests keep feeding full entities, and pass unchanged.
  - The lean shape gets real coverage from D3, D4, and the two test files that already use the real repository.
  - It's the pattern `IEpisodeScheduleService` already uses, down to the "MUST override" wording.
- **Risk:** the repository forgets to override, so the pages quietly keep the full read. D4 asserts that the columns left out come back as defaults, which the default method would fail.
- **Alternative:** an abstract member, with every fake updated. That's churn in about 20 files, and it adds no coverage, since each fake would return its full entities anyway. Rejected.

### D3. A parity test guards the field list

This is new: `Services/Library/ListViewReadParityTests.cs`.

- **Two stores per case.** A `SeedAsync(AnimeTrackerDbContext)` fixture is loaded into two fresh InMemory databases, because settling writes and each path needs its own store:
  - **Lean:** the real `UserAnimeEntryRepository`.
  - **Full:** a `FullReadRepository : IUserAnimeEntryRepository` that wraps the real repository and sends `GetAllForListViewAsync` to `GetAllAsync`.
- **Collaborators.** Real ones where the codebase's class works over InMemory: `TopAnimeSelectionRepository`, `ActivityLogRepository`, `AiringWatchStatusService` with a recording `IEntrySyncScheduler`, `SeriesRankingLookup`, and `AnimeRankingService`. Fakes for `IEpisodeScheduleService` (a fixed aired dictionary, next-airing instants and an airing-today episode), `IBroadcastLocalTimeConverter` and `ISeriesBuildTrigger` (records what's enqueued).
- **Calls, each made on both stores:**
  - `GetMyListAsync`
  - `GetDashboardAsync`
  - `GetProfileAsync`, `GetTopAnimeSectionAsync("all")` and `("movie")`, `GetRewatchedSectionAsync("all")`, and `GetTopSeriesSectionAsync` (for its backfill read, comparing the enqueued ids)
  - `GetRecapAsync` for a multi-year "aired" period, a yearly "watched" period and a season
- **Compared between the two runs:**
  - `JsonSerializer.Serialize` of each result
  - each store's entries afterwards (`AnimeId`, `Status`, `CompletedAt`, `PendingSync`)
  - the activity-log rows (`AnimeId`, `ChangeType`, `ChangeDetail`)
  - the scheduled sync ids
- **The fixture covers every branch the 11 fields feed:**
  - settling's re-open branch and complete branch
  - ranked entries in all three ranked bands (hand-ordered, a `music` short-form, dropped), and unranked ones (plan-to-watch, `not_yet_aired`)
  - a `movie` with a stored duration, a `tv` entry with a null duration (the 24-minute fallback) and a rewatched entry
  - an `AiredFrom` in the current season, for the dashboard, and air dates across several years, for Recap and Profile favourites
  - `MalScore` on scored entries, for opinion divergence
  - a `CompletedAt` and an episode-progress `ActivityLog` row inside the recap periods
  - a currently-airing entry with an unknown total, for Profile's unresolved-episode list
- **A reflection guard.** For every public writable instance property of `AnimeMetadata` except the navigations `RelatedAnime` and `UserEntry`, at least one fixture anime must hold a non-default value: not null, not zero, not `default(DateTimeOffset)`, not an empty list. If someone adds a column, the test fails until the fixture sets it, and parity then checks it. Values are distinctive (for example `Rank = 4242`, `Synopsis = "SYNOPSIS-1"`), so a read of a column the lean read leaves out changes the output.
- **Timing.** Both runs of a case use the same fixture instants, generated relative to `DateTimeOffset.UtcNow` once per test. The services' own `UtcNow` reads land milliseconds apart. A run that crosses UTC midnight between the two paths could differ on `CompletedAt`, and that's accepted as negligible.

**Alternatives considered:**
- **Model proxies that throw on columns the lean read leaves out.** That needs virtual properties or a changed model. Rejected.
- **A source scan for `.Anime.X` reads.** Brittle, and it misses reads through helpers that take `AnimeMetadata`. Rejected.
- **A comment only.** Weaker than the test. It's kept alongside it (D2's doc comment).

### D4. A repository test pins the lean shape

In `Data/Repositories/UserAnimeEntryRepositoryTests.cs`, seed three entries, one of whose anime sets every property (the D3 guard's helper can build it). Then assert:
- `GetAllForListViewAsync` and `GetAllAsync` return the same `AnimeId` set.
- For each entry, the 10 entry fields are equal and the 11 anime fields are equal.
- Every other `AnimeMetadata` property on the lean result is at its default, `RelatedAnime` is empty, and `UserEntry` is null. This is the assertion the default interface method would fail.
- The returned objects aren't tracked (`db.ChangeTracker.Entries()` doesn't grow).

### D5. Settling picks with no database, then makes one read, applies, and saves once

```csharp
public async Task SettleAsync(IReadOnlyCollection<UserAnimeEntry> entries, IReadOnlyDictionary<int, int> airedSoFarByAnimeId, CancellationToken ct = default)
{
    var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

    // Pass 1, no database: today's two conditions and their comments, moved verbatim.
    var picked = new List<(UserAnimeEntry CallerEntry, Direction Direction)>();
    foreach (var entry in entries)
    {
        if (/* :30-32 */) { picked.Add((entry, Direction.Reopen)); continue; }
        if (/* :44-47 */) picked.Add((entry, Direction.Complete));
    }
    if (picked.Count == 0)
        return;

    // One tracked read for every picked entry.
    var ids = picked.Select(p => p.CallerEntry.AnimeId).Distinct().ToList();
    var trackedById = await db.UserAnimeEntries.Where(e => ids.Contains(e.AnimeId)).ToDictionaryAsync(e => e.AnimeId, ct);

    // Pass 2, in caller order: re-check under the fresh read, then queue the change and its log row.
    var applied = new Dictionary<int, (UserAnimeEntry Entry, ActivityLog Log, Direction Direction)>();
    foreach (var (callerEntry, direction) in picked)
    {
        if (!trackedById.TryGetValue(callerEntry.AnimeId, out var tracked)) continue;
        // ReopenOne/CompleteOne's re-check and mutation, unchanged: Completed→Watching or
        // Watching→Completed (CompletedAt ??= today), PendingSync = true, db.ActivityLogs.Add(log).
        ...
    }

    await SaveDroppingConflictsAsync(applied, ct); // D6

    foreach (var animeId in applied.Keys)
        syncScheduler.ScheduleSync(animeId); // D7

    foreach (var (callerEntry, _) in picked)
        if (trackedById.TryGetValue(callerEntry.AnimeId, out var tracked))
            callerEntry.Status = tracked.Status;
}
```

`Direction` is a private two-value enum. `ReopenOneAsync` and `CompleteOneAsync` become two small private non-async methods, `TryReopen(UserAnimeEntry tracked)` and `TryComplete(UserAnimeEntry tracked, DateOnly today)`. Each returns the queued `ActivityLog`, or null when the re-check fails. They keep their re-check comments.

- **Same outcome as today, entry by entry.**
  - The status written back is the tracked entity's status after the save: the new status when applied, the reloaded one after a conflict, or the other change's status when the re-check fails.
  - A row deleted since the caller's read leaves the caller's status as it was, which matches today's `??` fallback.
  - Log rows are created in caller order, each stamped with `DateTimeOffset.UtcNow` when it's queued, as today.
  - The tracked read resolves identity exactly as today's `FirstOrDefaultAsync`, and `SaveChangesAsync` still saves whatever else is pending in the scoped context. On these paths that's nothing.
- **Nothing picked means no database touch.** That's today's behaviour too, since the per-entry helpers only ran on a hit. It's pinned by D8.
- **Round trips:** at most one read and one save, plus one more save per conflict (D6). The detail page (one entry) makes the same two round trips as today.

**Alternatives considered:**
- **`ExecuteUpdateAsync(... WHERE Status = …)`.** It can't say which rows it changed, so it can't write matching log rows or schedule syncs. It skips the `xmin` check, and InMemory doesn't support it. Rejected.
- **The per-entry helpers run concurrently (`Task.WhenAll`).** `DbContext` isn't thread-safe. Rejected.

### D6. A conflict drops only the entries it names, and the rest are saved again, in a bounded loop

**This needs a loop. EF's exception doesn't give enough to finish in one pass**, because of two behaviours:
1. **The failed save rolls back everything in it.** Every batch here has more than one command (an update and a log insert per entry), so EF wraps them in a transaction. When a command conflicts, the rollback undoes the entries that didn't conflict as well. Their changes are still pending in the change tracker, since EF doesn't accept changes after a failed save and discards the store-generated values it had started to propagate (the new `xmin`, the log ids). So saving again sends exactly those entries.
2. **`ex.Entries` isn't guaranteed to name every conflict.** EF raises the exception at the first command whose affected-row count is wrong, and stops reading results there. `ex.Entries` names that command's entry. A second conflicting entry in the same batch only shows up on the next attempt.

```csharp
private async Task SaveDroppingConflictsAsync(Dictionary<int, (UserAnimeEntry Entry, ActivityLog Log, Direction Direction)> applied, CancellationToken ct)
{
    while (applied.Count > 0)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Comment covers: rollback means the non-conflicting entries weren't saved
            // either; EF names only the first conflicting command; no re-apply after
            // reload (today's behaviour — the next read settles it).
            var conflicted = ex.Entries.Select(e => e.Entity).OfType<UserAnimeEntry>().ToList();
            if (conflicted.Count == 0 || conflicted.Any(c => !applied.ContainsKey(c.AnimeId)))
                throw;

            foreach (var entry in conflicted)
            {
                var (_, log, direction) = applied[entry.AnimeId];
                applied.Remove(entry.AnimeId);
                db.ActivityLogs.Remove(log);
                await db.Entry(entry).ReloadAsync(ct);
                logger.LogInformation(/* today's per-direction message */, entry.AnimeId);
            }
        }
    }
}
```

- **It always ends.** Each attempt succeeds, removes at least one entry from `applied`, or rethrows. So there are at most (entries moved + 1) saves, and only a conflict on every entry reaches that bound.
- **It rethrows when the exception names something outside this batch.** A pending change this service didn't make isn't its to swallow, and looping on it wouldn't end.
  - Today's catch would have swallowed it. On the four callers nothing else is pending when settling saves, so this can't change behaviour there.
  - It's recorded as a risk.
- **No re-apply after a reload,** the same as today. The reloaded entry may still qualify, for example when a push only cleared `PendingSync`, and the next read or the schedule job settles it. That keeps the loop bounded.
- **Logging:** the same two Information messages as today, one per conflicting entry.

**Alternatives considered:**
- **One pass over `ex.Entries`, then one more save with no loop.** Wrong when two entries conflict (behaviour 2). Rejected.
- **After any conflict, fall back to today's per-entry saves.** Correct, but it brings back one round trip per entry exactly when there's contention. Rejected.
- **Drop the whole batch on any conflict and let the next read retry.** Simpler, but one push in flight would hold up every other entry's settle for that read, and the new `list-editing` requirement says the others still save. Rejected.

### D7. `ScheduleSync` runs after the loop, for each entry still in `applied`

- Today it runs after each entry's own save. Now it runs once per entry that was actually saved, after the batch's final successful save.
- An entry whose re-check failed, or that conflicted, is never scheduled.
- `DebouncedEntrySyncScheduler` debounces per anime, so scheduling a few milliseconds later changes nothing anyone can observe.

### D8. Settling tests

**`Services/Entries/AiringWatchStatusServiceTests.cs`.** The existing 11 tests pass unchanged. New cases:
1. **Several completions:** three Watching entries at their totals on `finished_airing` anime. All three end Completed, with a `CompletedAt`, `PendingSync` set, one `Completed` log row each, and each id scheduled once. The caller's objects read Completed. Exactly one save.
2. **Several re-opens:** the same, from Completed on `currently_airing` anime with aired > watched. All three end Watching.
3. **Mixed:** two re-opens and two completions, interleaved in caller order, in one save.
4. **Nothing qualifies:** the service is built over a disposed `AnimeTrackerDbContext`, so any database touch throws `ObjectDisposedException`. `SettleAsync`, over entries none of which qualify (including a Rewatching entry at its total and a Completed entry on a finished show), completes without throwing and schedules nothing.
5. **Moved before the batched read:** three entries qualify to complete. After the caller's untracked read, one is changed to Dropped through the same context (or the tracker is cleared and it's changed through a second context). That entry stays Dropped, gets no log row and isn't scheduled, and its caller object reads Dropped. The other two complete.
6. **Conflict at the save:** see "Simulating a conflict" below. Three entries qualify. The first save names entry B and throws. B ends with the competing change's values, no log row and no sync, and its caller object reads its reloaded status. A and C are saved with their log rows and syncs. There are two saves in total.
7. **Two conflicts across attempts:** the first save names A and throws, the second names B and throws, and the third succeeds. Only C is moved. There are three saves, and the loop ends.
8. **Sync only after the save:** the recording scheduler records `db`'s completed-save count when each `ScheduleSync` is called. Every call sees the final save already done, and the ids are exactly the entries that moved.

**Counting on InMemory:**
- **Saves:** subscribe to `db.SavingChanges` and `db.SavedChanges`.
- **Reads:** InMemory has no per-query hook, so "one read" is asserted indirectly. With the tracker cleared after seeding, `db.ChangeTracker.Tracked` (`FromQuery`) must fire for every picked entry before the first `SavingChanges`, with no `Tracked` event after it. That rules out a read, save, read, save pattern. The single `Where(Contains)` is otherwise held by the code's shape and by review.

**Simulating a conflict.** InMemory neither bumps `RowVersion` nor rolls back, so a real conflict there would partly apply the batch. The retry would then insert already-written log rows again, and InMemory throws on duplicate keys. So cases 6 and 7 use a test `SaveChangesInterceptor`, added to the options with `AddInterceptors`, which works like this:
- On a scripted attempt, `SavingChangesAsync` writes the competing change through a second `AnimeTrackerDbContext` on the same InMemory store, so the reload sees it.
- It then throws `new DbUpdateConcurrencyException("simulated", [entry.GetInfrastructure()])` for that entry before anything is written. That matches Postgres, where the whole transaction rolls back.
- `EntityEntry.GetInfrastructure()` returns EF's internal entry type, so the helper carries `#pragma warning disable EF1001` and a comment saying why.
- Task 5.1 checks that this compiles against EF Core 10 before the cases are written. If it doesn't, 5.1 finds another way to get an `IUpdateEntry` for the entry and records it in that comment.

**Integration.** `MyListServiceReopenTests` and `MainDashboardServiceReopenTests` each gain a three-entry mixed case through the real `AiringWatchStatusService`. The My List one goes through the real repository, so after both commits it runs the lean read and the batched settle together. Detail and the schedule job get no new tests. They call `SettleAsync` exactly as before, with one entry and with the whole list, and cases 1–8 cover both sizes.

### D9. Two commits: N4 first, then R5 with the archive

- **N4 first:**
  - It changes no requirement, so it lands without the archive.
  - The three spec deltas describe R5, so archiving with R5 lands them with the code that makes them true, as earlier changes did.
- **They're independent.** `SettleAsync` reads only `AnimeId`, `Status`, `EpisodesWatched`, `Anime.AiringStatus` and `Anime.TotalEpisodes` from the caller's objects, and all five are in the lean read. It writes through its own tracked read, never through the caller's objects.
- **One test pass at the end covers both,** through `MyListServiceReopenTests` and the D3 parity test, which runs the real settle.

## Risks / Trade-offs

- **[A partly filled entity is mistaken for a full one]** A later change on one of the four pages reads, say, `Anime.Genres` and quietly gets null. → D3 fails when the output differs, and its guard forces every new column into the fixture. The interface doc comment names the contract and the test.
- **[A lean entity gets attached and blanks real columns]** `db.Update(entry)` or attaching `entry.Anime` would write defaults over stored columns. → Nothing on these paths attaches the caller's entries, and settling writes through its own tracked read. The doc comment says never to attach them.
- **[Parity is only as strong as its fixture]** A read whose effect the fixture's values don't show wouldn't be caught. → Every property gets a distinctive value, and D3 lists the branches the fixture must reach.
- **[Row order]** Neither query has an `ORDER BY`, so order is unspecified in both, and a narrower column list could get a different plan on Postgres. → Nothing visible depends on it. `MyListPage.tsx:471,477` sorts every mode with its comparator, and the server-side sorts on Home, Profile and Recap only fall back to row order on exact ties. The optional smoke check (tasks 3.4) compares real responses.
- **[The conflict path is only tested with a simulated exception]** InMemory can't reproduce `xmin` or a rollback. → The simulation matches what Postgres does, with nothing written on the failed attempt. The loop's correctness on Postgres rests on the two EF behaviours in D6, and the catch comment records them. A manual check on the dev database isn't worth it: making entries qualify there would push real changes to MyAnimeList.
- **[A conflict naming something unexpected now fails the page]** It used to be swallowed. → No settle caller has anything else pending. If one ever does, a visible error is better than a silently lost write.
- **[A failure that isn't a conflict now loses the whole batch]** A dropped connection, for example. Today the earlier entries were already saved. → Settling is idempotent, and it runs again on the next read and in the schedule job. Nothing is lost for good.
- **[Log rows from one batch can share a timestamp]** They're queued microseconds apart, where they used to be a round trip apart. → `ActivityLogRepository` orders by `Timestamp`, then `Id`, descending, so ties still order the same way every time. Each row is a different anime, so the feed's per-anime collapsing is unaffected.
- **[An ambient transaction would break the loop]** Under a caller's explicit transaction, a failed save isn't rolled back, so the retry would insert log rows twice. → No caller has one. A comment on the loop says it relies on `SaveChangesAsync`'s own transaction.

## Migration Plan

- There's no schema or API change.
- Deploy with the usual `docker compose build backend`.
- Each commit can be reverted on its own.

## Open Questions

- **`CompletedAt` isn't written back.** `IAiringWatchStatusService.cs:23-27` promises that settling writes `CompletedAt` back onto the caller's objects on a completion. The code writes back only `Status`. So the My List or dashboard response that auto-completes an entry shows it Completed with its previous finish date, usually none, until the next load. This change keeps today's behaviour, as the proposal asks. The options:
  - (a) Leave the code as it is, correct the doc comment in the R5 commit, and note it in the triage doc.
  - (b) Also write `CompletedAt` back. That's a small fix you can see, in its own commit.
  - (c) Leave both, and only note it.

  **To confirm before R5 is committed.** The default is (a).
