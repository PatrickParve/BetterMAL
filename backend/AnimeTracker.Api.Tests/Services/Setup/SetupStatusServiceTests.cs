using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Airing.AniList;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Season;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Setup;
using Microsoft.Extensions.DependencyInjection;

namespace AnimeTracker.Api.Tests.Services.Setup;

// add-first-run-setup task 10.1 (design D17): the DTO the setup screen and Settings'
// Library data entry read. Counts come from the database, everything else from the
// coordinator's snapshot, the two services' health and the credentials. The read
// makes no outside call: the service has no MAL or AniList client to call.
public class SetupStatusServiceTests
{
    private static SetupSnapshot Snapshot(
        bool listRead = false, int read = 0, int? total = null, bool reconnect = false, bool draining = false,
        SetupStepState? list = null, SetupStepState? details = null, SetupStepState? series = null, SetupStepState? airing = null,
        IEnumerable<int>? noSeries = null, IEnumerable<UnrecognizedStatusSkip>? unrecognized = null) =>
        new(listRead, read, total, reconnect, draining,
            list ?? SetupStepState.Waiting, details ?? SetupStepState.Waiting, series ?? SetupStepState.Waiting, airing ?? SetupStepState.Waiting,
            (noSeries ?? []).ToHashSet(), (unrecognized ?? []).ToList());

    private static SetupStepState Running(double? eta = null, SetupRetryWait? waiting = null) =>
        new(SetupStepPhase.Running, waiting, eta);

    // --- the screen states ---

    [Fact]
    public async Task WithACredentialMissingItNamesWhatIsMissing()
    {
        using var f = new SetupStatusFixture();
        f.MalOptions.ClientSecret = "";
        f.Tokens.Token = null;

        var dto = await f.Service.GetAsync();

        Assert.Equal(["MAL_CLIENT_SECRET"], dto.MissingCredentials);
        Assert.False(dto.Finished);
        Assert.Equal("NotConnected", dto.Connection.State);
    }

    [Fact]
    public async Task WithBothCredentialsAndNoLoginItIsTheConnectScreensState()
    {
        using var f = new SetupStatusFixture();
        f.Tokens.Token = null;

        var dto = await f.Service.GetAsync();

        Assert.Empty(dto.MissingCredentials);
        Assert.Equal(("NotConnected", (DateTimeOffset?)null), (dto.Connection.State, dto.Connection.LostAt));
        Assert.False(dto.Finished);
        Assert.All(StepsOf(dto), step => Assert.Equal(SetupStepPhase.Waiting, step.Phase));
        Assert.Null(dto.Steps.List.Total); // a list read that hasn't begun doesn't know its total
    }

    [Fact]
    public async Task NothingIsDoneJustBecauseNothingHasBeenStoredYet()
    {
        using var f = new SetupStatusFixture(); // an empty database and a list that hasn't been read

        var dto = await f.Service.GetAsync();

        // 0 of 0 would read as complete, but the list hasn't been read: nothing is known yet.
        Assert.All(StepsOf(dto), step => Assert.Equal(SetupStepPhase.Waiting, step.Phase));
    }

    [Fact]
    public async Task AnEmptyListThatHasBeenReadFinishesEveryStepAtOnce()
    {
        using var f = new SetupStatusFixture();
        f.Coordinator.Snapshot = Snapshot(listRead: true, read: 0, total: 0);

        var dto = await f.Service.GetAsync();

        Assert.All(StepsOf(dto), step => Assert.Equal(SetupStepPhase.Done, step.Phase));
        Assert.Equal(new SetupProgressDto(0, 0), dto.AiringPriority);
    }

    [Fact]
    public async Task ARunningSetupReportsEachStepsCountsFromTheDatabase()
    {
        using var f = new SetupStatusFixture();
        // Ten list anime: four fully fetched, one MyAnimeList answered 404 for, five still basic rows.
        foreach (var id in Enumerable.Range(1, 4)) await f.SeedAsync(id, fetched: true);
        await f.SeedAsync(5, notOnMal: true);
        foreach (var id in Enumerable.Range(6, 5)) await f.SeedAsync(id);
        // Two of them belong to a stored series, and a build found that anime 3 belongs to none.
        await f.SeedSeriesAsync(1, memberIds: [1, 2]);
        // Six have their airing-fetched mark.
        foreach (var id in Enumerable.Range(1, 6))
            f.Db.AnimeAiringSyncs.Add(new AnimeAiringSync { AnimeId = id, AniListId = 900 + id, LastFetchedAt = DateTimeOffset.UtcNow });
        await f.Db.SaveChangesAsync();
        f.Coordinator.Snapshot = Snapshot(
            listRead: true, read: 10, total: 10,
            details: Running(eta: 120), series: SetupStepState.Waiting, airing: Running(eta: 30),
            noSeries: [3]);

        var dto = await f.Service.GetAsync();

        Assert.Equal(SetupStepPhase.Done, dto.Steps.List.Phase);
        Assert.Equal((10, 10), (dto.Steps.List.Done, dto.Steps.List.Total));

        Assert.Equal(SetupStepPhase.Running, dto.Steps.Details.Phase);
        Assert.Equal((5, 10), (dto.Steps.Details.Done, dto.Steps.Details.Total)); // four fetched plus the one skipped for good
        Assert.Equal(120, dto.Steps.Details.EtaSeconds);

        Assert.Equal(SetupStepPhase.Waiting, dto.Steps.Series.Phase); // the coordinator says it waits for the details
        Assert.Equal((3, 9), (dto.Steps.Series.Done, dto.Steps.Series.Total)); // two covered plus one with no series; the skipped one isn't a target

        Assert.Equal(SetupStepPhase.Running, dto.Steps.Airing.Phase);
        Assert.Equal((6, 10), (dto.Steps.Airing.Done, dto.Steps.Airing.Total));
        Assert.Equal(30, dto.Steps.Airing.EtaSeconds);
    }

    [Fact]
    public async Task AStepWithNothingLeftIsDoneOnceTheListHasBeenRead_WhateverTheCoordinatorLastSaid()
    {
        using var f = new SetupStatusFixture();
        foreach (var id in Enumerable.Range(1, 3)) await f.SeedAsync(id, fetched: true, marked: true);
        await f.SeedSeriesAsync(1, memberIds: [1, 2, 3]);
        f.Coordinator.Snapshot = Snapshot(
            listRead: true, read: 3, total: 3,
            details: Running(eta: 5), series: Running(eta: 5), airing: Running(eta: 5, waiting: new SetupRetryWait(1, DateTimeOffset.UtcNow)));

        var dto = await f.Service.GetAsync();

        Assert.All(StepsOf(dto), step => Assert.Equal(SetupStepPhase.Done, step.Phase));
        Assert.All(StepsOf(dto), step => Assert.Null(step.EtaSeconds)); // no estimate for a step that is finished
        Assert.All(StepsOf(dto), step => Assert.Null(step.WaitingRetry));
    }

    [Fact]
    public async Task APartialSeriesOrOneBuiltUnderSupersededRulesIsNotCovered()
    {
        using var f = new SetupStatusFixture();
        foreach (var id in Enumerable.Range(1, 4)) await f.SeedAsync(id, fetched: true);
        await f.SeedSeriesAsync(1, partial: true, memberIds: [1]);
        await f.SeedSeriesAsync(2, builtAt: SeriesGraphBuilder.ClassificationRevisedAt.AddDays(-1), memberIds: [2]);
        await f.SeedSeriesAsync(3, memberIds: [3]);
        f.Coordinator.Snapshot = Snapshot(listRead: true, read: 4, total: 4);

        var dto = await f.Service.GetAsync();

        Assert.Equal((1, 4), (dto.Steps.Series.Done, dto.Steps.Series.Total));
    }

    [Fact]
    public async Task ASeriesOfAnAnimeOutsideMyListDoesNotCount()
    {
        using var f = new SetupStatusFixture();
        await f.SeedAsync(1, fetched: true);
        f.Db.AnimeMetadata.Add(new AnimeMetadata { Id = 99, Title = "A franchise anime the series build brought in" });
        await f.Db.SaveChangesAsync();
        await f.SeedSeriesAsync(1, memberIds: [99]);
        f.Coordinator.Snapshot = Snapshot(listRead: true, read: 1, total: 1);

        var dto = await f.Service.GetAsync();

        Assert.Equal((0, 1), (dto.Steps.Series.Done, dto.Steps.Series.Total));
    }

    [Fact]
    public async Task APausedStepShowsItsRetryWaitAndNoEstimate()
    {
        using var f = new SetupStatusFixture();
        await f.SeedAsync(1);
        var next = f.Now.AddMinutes(5);
        f.Coordinator.Snapshot = Snapshot(
            listRead: true, read: 1, total: 1,
            details: new SetupStepState(SetupStepPhase.Paused, new SetupRetryWait(3, next), null));
        for (var i = 0; i < ServiceHealth.DownAfterFailures; i++)
            f.Mal.RecordTemporaryFailure();

        var dto = await f.Service.GetAsync();

        Assert.Equal(SetupStepPhase.Paused, dto.Steps.Details.Phase);
        Assert.Null(dto.Steps.Details.EtaSeconds);
        Assert.Equal(new SetupWaitingRetryDto(3, next), dto.Steps.Details.WaitingRetry);
        var mal = Assert.Single(dto.Services, s => s.Name == "MyAnimeList");
        Assert.True(mal.Down);
        Assert.Equal(f.Mal.NextTryAt, mal.NextTryAt);
        Assert.False(Assert.Single(dto.Services, s => s.Name == "AniList").Down);
    }

    [Fact]
    public async Task AThrottledServiceShowsWhenItResumes()
    {
        using var f = new SetupStatusFixture();
        var until = f.Now.AddSeconds(30);
        f.AniList.SetThrottledUntil(until);

        var dto = await f.Service.GetAsync();

        var aniList = Assert.Single(dto.Services, s => s.Name == "AniList");
        Assert.Equal(until, aniList.ThrottledUntil);
        Assert.False(aniList.Down);
        Assert.Null(aniList.NextTryAt);
        Assert.Null(Assert.Single(dto.Services, s => s.Name == "MyAnimeList").ThrottledUntil);

        f.Now = until.AddSeconds(1); // the wait is over

        Assert.Null(Assert.Single((await f.Service.GetAsync()).Services, s => s.Name == "AniList").ThrottledUntil);
    }

    // Found in 12.4, on a finished install in headless Chrome: health is fed by every request the
    // app makes, so an outage an ordinary job (a series visit, the hourly check) met read in Library
    // data as "MyAnimeList appears to be down. Setup retries automatically." with a Retry now that
    // woke nothing, and stayed until some later request happened to succeed.
    [Fact]
    public async Task AfterHomeMalsTroubleIsNotSetupsToShow()
    {
        using var f = new SetupStatusFixture();
        await f.Gate.MarkFinishedAsync();
        for (var i = 0; i < ServiceHealth.DownAfterFailures; i++)
            f.Mal.RecordTemporaryFailure();
        f.Mal.SetThrottledUntil(f.Now.AddSeconds(30));

        var mal = Assert.Single((await f.Service.GetAsync()).Services, s => s.Name == "MyAnimeList");

        Assert.False(mal.Down);
        Assert.Null(mal.NextTryAt);
        Assert.Null(mal.ThrottledUntil);
    }

    [Fact]
    public async Task AfterHomeAniListsTroubleShowsOnlyWhileTheLeftoverAiringWorkIsDraining()
    {
        using var f = new SetupStatusFixture();
        await f.Gate.MarkFinishedAsync();
        for (var i = 0; i < ServiceHealth.DownAfterFailures; i++)
            f.AniList.RecordTemporaryFailure();
        var until = f.Now.AddSeconds(30);
        f.AniList.SetThrottledUntil(until);

        f.Coordinator.Snapshot = Snapshot(listRead: true, draining: true, airing: Running());
        var draining = Assert.Single((await f.Service.GetAsync()).Services, s => s.Name == "AniList");
        Assert.True(draining.Down);
        Assert.Equal(f.AniList.NextTryAt, draining.NextTryAt);
        Assert.Equal(until, draining.ThrottledUntil);

        // After a restart nothing drains: the hourly catch-up owns the rest, and its outage isn't setup's.
        f.Coordinator.Snapshot = Snapshot();
        var idle = Assert.Single((await f.Service.GetAsync()).Services, s => s.Name == "AniList");
        Assert.False(idle.Down);
        Assert.Null(idle.NextTryAt);
        Assert.Null(idle.ThrottledUntil);
    }

    [Fact]
    public async Task ALostLoginShowsTheReconnectFlagAndWhenTheLoginWasLost()
    {
        using var f = new SetupStatusFixture();
        var lostAt = new DateTimeOffset(2026, 9, 28, 9, 0, 0, TimeSpan.Zero);
        f.Tokens.Token = FakeTokenStore.StoredToken(lostAt);
        f.Coordinator.Snapshot = Snapshot(listRead: false, reconnect: true, list: new SetupStepState(SetupStepPhase.Paused, null, null));

        var dto = await f.Service.GetAsync();

        Assert.True(dto.WaitingForReconnect);
        Assert.Equal(("Lost", (DateTimeOffset?)lostAt), (dto.Connection.State, dto.Connection.LostAt));
        Assert.Equal(SetupStepPhase.Paused, dto.Steps.List.Phase);
    }

    [Fact]
    public async Task TheListStepShowsItsRunningCountWithATotalOnlyOnceItIsKnown()
    {
        using var f = new SetupStatusFixture();
        f.Coordinator.Snapshot = Snapshot(read: 200, total: null, list: Running());

        var dto = await f.Service.GetAsync();

        Assert.Equal(SetupStepPhase.Running, dto.Steps.List.Phase);
        Assert.Equal((200, (int?)null), (dto.Steps.List.Done, dto.Steps.List.Total));
    }

    [Fact]
    public async Task ARepeatedListReadEndsWithTheNumberReadWhenMyAnimeListNeverStatedATotal()
    {
        using var f = new SetupStatusFixture();
        f.Coordinator.Snapshot = Snapshot(listRead: true, read: 624, total: null);

        var dto = await f.Service.GetAsync();

        Assert.Equal((SetupStepPhase.Done, 624, (int?)624), (dto.Steps.List.Phase, dto.Steps.List.Done, dto.Steps.List.Total));
    }

    [Fact]
    public async Task AFinishedInstallWithLeftoverAiringWorkShowsTheDrain()
    {
        using var f = new SetupStatusFixture();
        await f.Gate.MarkFinishedAsync();
        foreach (var id in Enumerable.Range(1, 4)) await f.SeedAsync(id, fetched: true, marked: id <= 2);
        await f.SeedSeriesAsync(1, memberIds: [1, 2, 3, 4]);
        f.Coordinator.Snapshot = Snapshot(listRead: true, read: 4, total: 4, draining: true, airing: Running(eta: 60));

        var dto = await f.Service.GetAsync();

        Assert.True(dto.Finished);
        Assert.True(dto.AiringDraining);
        Assert.Equal(SetupStepPhase.Done, dto.Steps.Details.Phase);
        Assert.Equal(SetupStepPhase.Running, dto.Steps.Airing.Phase);
        Assert.Equal((2, 4), (dto.Steps.Airing.Done, dto.Steps.Airing.Total));
        Assert.Equal(60, dto.Steps.Airing.EtaSeconds);
    }

    [Fact]
    public async Task AFinishedIdleInstallReportsEveryStepDoneFromItsDataAlone()
    {
        using var f = new SetupStatusFixture();
        await f.Gate.MarkFinishedAsync();
        foreach (var id in Enumerable.Range(1, 3)) await f.SeedAsync(id, fetched: true, marked: true);
        await f.SeedSeriesAsync(1, memberIds: [1, 2, 3]);
        // Nothing has run in this process (a restart after setup): the snapshot is idle.

        var dto = await f.Service.GetAsync();

        Assert.True(dto.Finished);
        Assert.False(dto.AiringDraining);
        Assert.All(StepsOf(dto), step => Assert.Equal(SetupStepPhase.Done, step.Phase));
        Assert.Equal((3, 3), (dto.Steps.List.Done, dto.Steps.List.Total));
    }

    [Fact]
    public async Task AFinishedIdleInstallCountsItsLoneAnimeAsSettledSoSeriesReadsDone()
    {
        // The anime a build found to belong to no series are remembered only in the process that
        // built them, so after a restart, or on an install upgraded past setup, they look unbuilt.
        // The step finished when setup did, so it must not read as unfinished.
        using var f = new SetupStatusFixture();
        await f.Gate.MarkFinishedAsync();
        foreach (var id in Enumerable.Range(1, 5)) await f.SeedAsync(id, fetched: true, marked: true);
        await f.SeedSeriesAsync(1, memberIds: [1, 2]); // 3, 4 and 5 belong to no series and have no row anywhere

        var dto = await f.Service.GetAsync();

        Assert.Equal((SetupStepPhase.Done, 5, (int?)5), (dto.Steps.Series.Phase, dto.Steps.Series.Done, dto.Steps.Series.Total));
        Assert.All(StepsOf(dto), step => Assert.Equal(SetupStepPhase.Done, step.Phase));
    }

    [Fact]
    public async Task ASkippedAnimeIsNotCountedInAFinishedIdleInstallsSeriesTotal()
    {
        using var f = new SetupStatusFixture();
        await f.Gate.MarkFinishedAsync();
        foreach (var id in Enumerable.Range(1, 3)) await f.SeedAsync(id, fetched: true, marked: true);
        await f.SeedAsync(4, notOnMal: true);

        var dto = await f.Service.GetAsync();

        Assert.Equal((3, 3), (dto.Steps.Series.Done, dto.Steps.Series.Total));
    }

    // --- the estimate (task 10.2, design D17) ---
    //
    // The workers publish an estimate from their recent pace. The read leaves it out while it would
    // be a guess at how long something else takes: paused, a throttled service, a refused login.

    [Fact]
    public async Task AThrottledMalLeavesTheListDetailsAndSeriesEstimatesOut_ButNotAiringsAndItReturnsWhenTheThrottleEnds()
    {
        using var f = new SetupStatusFixture();
        await f.SeedAsync(1);
        await f.SeedAsync(2);
        f.Coordinator.Snapshot = Snapshot(
            listRead: false, read: 2, total: 10,
            list: Running(eta: 9), details: Running(eta: 120), series: Running(eta: 90), airing: Running(eta: 30));
        var until = f.Now.AddSeconds(30);
        f.Mal.SetThrottledUntil(until);

        var throttled = await f.Service.GetAsync();

        Assert.Null(throttled.Steps.List.EtaSeconds);
        Assert.Null(throttled.Steps.Details.EtaSeconds);
        Assert.Null(throttled.Steps.Series.EtaSeconds);
        Assert.Equal(30, throttled.Steps.Airing.EtaSeconds); // AniList is a different service

        f.Now = until.AddSeconds(1); // the wait is over

        var resumed = await f.Service.GetAsync();
        Assert.Equal(9, resumed.Steps.List.EtaSeconds);
        Assert.Equal(120, resumed.Steps.Details.EtaSeconds);
        Assert.Equal(90, resumed.Steps.Series.EtaSeconds);
    }

    [Fact]
    public async Task AThrottledAniListLeavesOnlyTheAiringEstimateOut()
    {
        using var f = new SetupStatusFixture();
        await f.SeedAsync(1);
        f.Coordinator.Snapshot = Snapshot(
            listRead: true, read: 1, total: 1, details: Running(eta: 120), airing: Running(eta: 30));
        f.AniList.SetThrottledUntil(f.Now.AddSeconds(60));

        var dto = await f.Service.GetAsync();

        Assert.Null(dto.Steps.Airing.EtaSeconds);
        Assert.Equal(120, dto.Steps.Details.EtaSeconds);
    }

    [Fact]
    public async Task AListReadWaitingForReconnectHasNoEstimate_ButTheStepsThatNeedNoLoginKeepTheirs()
    {
        using var f = new SetupStatusFixture();
        await f.SeedAsync(1);
        f.Coordinator.Snapshot = Snapshot(
            listRead: false, read: 3, total: 10, reconnect: true,
            list: Running(eta: 9), details: Running(eta: 120));

        var dto = await f.Service.GetAsync();

        Assert.Null(dto.Steps.List.EtaSeconds);
        Assert.Equal(120, dto.Steps.Details.EtaSeconds); // details and series use the client id, not the login
    }

    [Fact]
    public async Task APausedStepNeverShowsAnEstimateEvenWhenOneWasStillStored()
    {
        using var f = new SetupStatusFixture();
        await f.SeedAsync(1);
        f.Coordinator.Snapshot = Snapshot(
            listRead: true, read: 1, total: 1,
            details: new SetupStepState(SetupStepPhase.Paused, null, 300));

        var dto = await f.Service.GetAsync();

        Assert.Equal(SetupStepPhase.Paused, dto.Steps.Details.Phase);
        Assert.Null(dto.Steps.Details.EtaSeconds);
    }

    // --- no outside call ---

    [Fact]
    public void TheReadHasNoClientToCallAnOutsideServiceWith()
    {
        // The read is served once a second to every open tab, so it must be able to do nothing but
        // read the database, the in-memory snapshot and the services' health. The way to be sure it
        // never calls out is that it cannot: nothing it is given can make a request.
        Type[] canMakeARequest =
        [
            typeof(IMalClient), typeof(IMalSetupClient), typeof(IAniListClient), typeof(IEpisodeScheduleRefreshService),
            typeof(ISeriesService), typeof(HttpClient), typeof(IHttpClientFactory), typeof(IServiceProvider), typeof(IServiceScopeFactory),
        ];

        var dependencies = typeof(SetupStatusService).GetConstructors().Single().GetParameters().Select(p => p.ParameterType).ToList();

        Assert.DoesNotContain(dependencies, d => canMakeARequest.Any(c => c.IsAssignableFrom(d)));
    }

    // --- the priority set ---

    [Fact]
    public async Task ThePriorityNoteCountsTheAiringPrioritySetAndHowMuchOfItIsDone()
    {
        using var f = new SetupStatusFixture();
        var (year, season) = SeasonCalendar.GetSeasonFor(DateOnly.FromDateTime(DateTime.UtcNow));
        var (nextYear, nextSeason) = SeasonCalendar.Shift(year, season, 1);
        var (lastYear, lastSeason) = SeasonCalendar.Shift(year, season, -1);

        await f.SeedAsync(1, airingStatus: "currently_airing", marked: true);                                    // tier 1, done
        await f.SeedAsync(2, airingStatus: "currently_airing");                                                   // tier 1
        await f.SeedAsync(3, airingStatus: "not_yet_aired", airedFrom: SeasonCalendar.SeasonStart(nextYear, nextSeason)); // tier 2
        await f.SeedAsync(4, airingStatus: "finished_airing", airedFrom: SeasonCalendar.SeasonStart(lastYear, lastSeason), marked: true); // tier 3, done
        await f.SeedAsync(5, airingStatus: "finished_airing", airedFrom: new DateOnly(2015, 4, 1));               // tier 4: not in the set
        await f.SeedAsync(6, airingStatus: "not_yet_aired");                                                      // tier 4: no start date
        f.Coordinator.Snapshot = Snapshot(listRead: true, read: 6, total: 6);

        var dto = await f.Service.GetAsync();

        Assert.Equal(new SetupProgressDto(Done: 2, Total: 4), dto.AiringPriority);
    }

    // --- the skipped list ---

    [Fact]
    public async Task SkippedAnimeAreListedWithTheirTitleAndReasonAndDropOutWhenLaterFetched()
    {
        using var f = new SetupStatusFixture();
        await f.SeedAsync(1, "Zeta", notOnMal: true);
        var alpha = await f.SeedAsync(2, "alpha", notOnMal: true);
        await f.SeedAsync(3, "Fetched Fine", fetched: true);
        f.Coordinator.Snapshot = Snapshot(
            listRead: true, read: 3, total: 3,
            unrecognized: [new UnrecognizedStatusSkip(77, "Mystery Show", "watching_but_odd")]);

        var dto = await f.Service.GetAsync();

        Assert.Equal(
            [
                new SetupSkippedDto(2, "alpha", SetupSkipReason.NotOnMal, null),
                new SetupSkippedDto(77, "Mystery Show", SetupSkipReason.UnrecognizedStatus, "watching_but_odd"),
                new SetupSkippedDto(1, "Zeta", SetupSkipReason.NotOnMal, null),
            ],
            dto.Skipped);

        // The scheduled refresh later fetches "alpha" successfully: it is no longer skipped.
        alpha.LastSyncedAt = DateTimeOffset.UtcNow;
        await f.Db.SaveChangesAsync();

        Assert.DoesNotContain(2, (await f.Service.GetAsync()).Skipped.Select(s => s.AnimeId));
    }

    [Fact]
    public async Task ANotFoundAnimeCountsAsDoneInTheDetailsStepButNotAsATargetOfTheSeriesStep()
    {
        using var f = new SetupStatusFixture();
        await f.SeedAsync(1, fetched: true);
        await f.SeedAsync(2, notOnMal: true);
        f.Coordinator.Snapshot = Snapshot(listRead: true, read: 2, total: 2);

        var dto = await f.Service.GetAsync();

        Assert.Equal((2, 2), (dto.Steps.Details.Done, dto.Steps.Details.Total));
        Assert.Equal((0, 1), (dto.Steps.Series.Done, dto.Steps.Series.Total));
    }

    [Fact]
    public async Task TheDtoSerializesWithTheShapeTheFrontendReads()
    {
        using var f = new SetupStatusFixture();
        f.Coordinator.Snapshot = Snapshot(read: 5, total: 10, list: Running(eta: 12));

        var json = System.Text.Json.JsonSerializer.Serialize(
            await f.Service.GetAsync(),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
            });

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("finished").GetBoolean());
        Assert.Equal(0, root.GetProperty("missingCredentials").GetArrayLength());
        Assert.Equal("Connected", root.GetProperty("connection").GetProperty("state").GetString());
        var list = root.GetProperty("steps").GetProperty("list");
        Assert.Equal("Running", list.GetProperty("phase").GetString());
        Assert.Equal(5, list.GetProperty("done").GetInt32());
        Assert.Equal(10, list.GetProperty("total").GetInt32());
        Assert.Equal(12, list.GetProperty("etaSeconds").GetDouble());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, list.GetProperty("waitingRetry").ValueKind);
        Assert.Equal(2, root.GetProperty("services").GetArrayLength());
        Assert.Equal("MyAnimeList", root.GetProperty("services")[0].GetProperty("name").GetString());
        Assert.True(root.TryGetProperty("airingPriority", out _));
        Assert.True(root.TryGetProperty("airingDraining", out _));
        Assert.True(root.TryGetProperty("waitingForReconnect", out _));
        Assert.True(root.TryGetProperty("skipped", out _));
    }

    private static IEnumerable<SetupStepDto> StepsOf(SetupStatusDto dto) =>
        [dto.Steps.List, dto.Steps.Details, dto.Steps.Series, dto.Steps.Airing];
}
