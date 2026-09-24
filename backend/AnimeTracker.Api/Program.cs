using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Artwork;
using AnimeTracker.Api.Services.Airing.AniList;
using AnimeTracker.Api.Services.Dashboard;
using AnimeTracker.Api.Services.Detail;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.IdMapping;
using AnimeTracker.Api.Services.Import;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Library;
using AnimeTracker.Api.Services.ListBackup;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Auth;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Recap;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Search;
using AnimeTracker.Api.Services.Season;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Sync;
using AnimeTracker.Api.Services.Tmdb;
using AnimeTracker.Api.Services.Transfer;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Run natively (Development), the backend reads the repo-root .env that
// Docker Compose reads. Must come before anything reads configuration.
DevDotEnvOverlay.Apply(builder.Configuration, builder.Environment);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AnimeTrackerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IAnimeMetadataRepository, AnimeMetadataRepository>();
builder.Services.AddScoped<IUserAnimeEntryRepository, UserAnimeEntryRepository>();
builder.Services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
builder.Services.AddScoped<ITopAnimeSelectionRepository, TopAnimeSelectionRepository>();
builder.Services.AddScoped<ISeasonRepository, SeasonRepository>();
builder.Services.AddScoped<ITopAnimeRepository, TopAnimeRepository>();
builder.Services.AddScoped<IEpisodeAiringRepository, EpisodeAiringRepository>();

// --- MAL API integration ---
builder.Services.Configure<MalOptions>(builder.Configuration.GetSection(MalOptions.SectionName));

// Optional: with no key, TMDB access stays off and the rest of the app is
// unaffected (spec `tmdb-artwork`).
builder.Services.Configure<TmdbOptions>(builder.Configuration.GetSection(TmdbOptions.SectionName));

builder.Services.AddSingleton<MalRequestPacer>();
builder.Services.AddSingleton<MalSearchCache>();
builder.Services.AddSingleton<IAnimeSearchIndex, AnimeSearchIndexCache>();
builder.Services.AddSingleton<MalOAuthStateStore>();
builder.Services.AddSingleton<IMalTokenProvider, MalTokenProvider>();

builder.Services.AddScoped<IMalTokenStore, MalTokenStore>();
builder.Services.AddScoped<IMalOAuthService, MalOAuthService>();

builder.Services.AddTransient<MalAuthPacingHandler>();
builder.Services.AddHttpClient<IMalClient, MalClient>(client =>
{
    client.BaseAddress = new Uri("https://api.myanimelist.net/v2/");
}).AddHttpMessageHandler<MalAuthPacingHandler>();

// Plain client-factory client for OAuth token endpoint calls (different base
// address/host than the v2 API client above).
builder.Services.AddHttpClient();

builder.Services.AddHostedService<MalTokenRefreshBackgroundService>();

// --- Background jobs (shared lifecycle, design.md D1) ---
// One typed singleton per job, so DI and constructors stay typed rather than
// keyed. sync-now, reconciliation and the held-decision pair also run through
// the shared BackgroundJobRunner (design.md D4); the other three keep their
// existing trigger/background-service shape (Non-Goals: not moved onto the
// runner in this change).
builder.Services.AddSingleton<AiringFullRefreshProgress>();
builder.Services.AddSingleton<SeriesBulkBuildProgress>();
builder.Services.AddSingleton<SyncNowProgress>();
builder.Services.AddSingleton<ReconcileProgress>();
builder.Services.AddSingleton<HeldDecisionProgress>();
builder.Services.AddSingleton<BackgroundJobRunner>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<BackgroundJobRunner>());

// --- Initial import ---
builder.Services.AddSingleton<ListImportProgress>();
builder.Services.AddSingleton<IImportTrigger, ImportTrigger>();
builder.Services.AddScoped<IInitialImportService, InitialImportService>();
builder.Services.AddHostedService<InitialImportBackgroundService>();

// --- List editing & write-sync ---
builder.Services.AddSingleton<IEntrySyncScheduler, DebouncedEntrySyncScheduler>();
builder.Services.AddScoped<IUserAnimeEntryEditService, UserAnimeEntryEditService>();
builder.Services.AddScoped<IAiringWatchStatusService, AiringWatchStatusService>();

// --- Write-sync retry & reconciliation ---
builder.Services.AddScoped<IEntryPushService, EntryPushService>();
builder.Services.AddSingleton<ReconciliationRunGate>();
builder.Services.AddScoped<IReconciliationService, ReconciliationService>();
builder.Services.AddScoped<IHeldChangeService, HeldChangeService>();
builder.Services.AddScoped<IStartupPendingSyncHold, StartupPendingSyncHold>();
builder.Services.AddHostedService<PendingSyncRetryBackgroundService>();
builder.Services.AddHostedService<ReconciliationBackgroundService>();

// --- Metadata & score refresh ---
builder.Services.AddScoped<IAnimeUpdateRelevance, AnimeUpdateRelevance>();
builder.Services.AddScoped<IAnimeUpdateRecorder, AnimeUpdateRecorder>();
builder.Services.AddScoped<IAnimeMetadataChangeDetector, AnimeMetadataChangeDetector>();
builder.Services.AddScoped<IAnnouncementResolutionService, AnnouncementResolutionService>();
builder.Services.AddScoped<IAnimeUpdateService, AnimeUpdateService>();
builder.Services.AddScoped<IMetadataRefreshService, MetadataRefreshService>();
builder.Services.AddHostedService<MetadataRefreshBackgroundService>();
builder.Services.AddScoped<IPictureRefreshService, PictureRefreshService>();
builder.Services.AddScoped<IArtworkSelectionService, ArtworkSelectionService>();

// Single-flight lock shared by every visit-triggered live fetch (season,
// top-anime ranking, anime detail) so concurrent requests for the same
// subject collapse into one MAL fetch instead of racing.
builder.Services.AddSingleton<RefreshGate>();

// --- Timezone conversion ---
builder.Services.AddSingleton<IBroadcastLocalTimeConverter, BroadcastLocalTimeConverter>();

// --- Search ---
builder.Services.AddScoped<SeriesSearchLookup>();
builder.Services.AddScoped<IAnimeSearchService, AnimeSearchService>();

// --- Series ranking (Top series) ---
builder.Services.AddScoped<SeriesRankingLookup>();

// --- Series list (Series page) ---
builder.Services.AddScoped<SeriesListService>();

// --- Main dashboard ---
builder.Services.AddScoped<IMainDashboardService, MainDashboardService>();

// --- External id mapping (MAL -> TMDB/IMDb) ---
// Synced weekly as a step of the airing tick (EpisodeScheduleRefreshBackgroundService),
// in a scope of its own; a plain named client, since it talks to GitHub, not an API.
builder.Services.AddHttpClient(AnimeIdMappingSyncService.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddScoped<IAnimeIdMappingSyncService, AnimeIdMappingSyncService>();

// The hand-edited custom mapping (design.md D19): read on top of the synced
// mapping by everything that reads the mapping, never written into it. The file
// is custom/id-mapping.json, looked for in the working directory and the two
// folders above it (backend/custom from the project folder, /app/custom in the
// image) unless IdMapping:CustomFile (IdMapping__CustomFile) says otherwise, and
// is re-read whenever it changes.
builder.Services.AddSingleton<ICustomIdMappings>(services => new CustomIdMappings(
    CustomIdMappings.CandidatePaths(builder.Configuration["IdMapping:CustomFile"], Directory.GetCurrentDirectory()),
    services.GetRequiredService<ILogger<CustomIdMappings>>()));
builder.Services.AddScoped<IAnimeIdMappingResolver, AnimeIdMappingResolver>();

// --- TMDB images (spec `tmdb-artwork`) ---
// The v3 key travels in the query string, so nothing may log a request URI
// (design.md D4). IHttpClientFactory's own request logging prints URIs but,
// since .NET 9, redacts query strings; System.Net.Http.DisableUriRedaction
// must stay off.
builder.Services.AddHttpClient<ITmdbClient, TmdbClient>(client =>
{
    client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<ITmdbArtworkService, TmdbArtworkService>();

// TMDB's terms cap how long its data may be cached (design.md D20): sets not
// refetched for 150 days are deleted, as a step of the airing tick like the
// mapping sync above, in a scope of its own.
builder.Services.AddScoped<ITmdbCachePurgeService, TmdbCachePurgeService>();

// --- Airing schedule ---
builder.Services.AddScoped<IAiringScheduleService, AiringScheduleService>();

// Per-episode air dates from AniList (break-aware), persisted in
// EpisodeAiring rows and refreshed in the background — no in-memory cache and
// no cadence-estimate fallback; a value not backed by a stored row is unknown.
builder.Services.AddSingleton<AniListRequestPacer>();
builder.Services.AddHttpClient<IAniListClient, AniListClient>(client =>
{
    client.BaseAddress = new Uri("https://graphql.anilist.co/");
});
builder.Services.AddScoped<IEpisodeScheduleService, EpisodeScheduleService>();
builder.Services.AddScoped<AniListRelationStore>();
builder.Services.AddScoped<IEpisodeScheduleRefreshService, EpisodeScheduleRefreshService>();
builder.Services.AddHostedService<EpisodeScheduleRefreshBackgroundService>();
builder.Services.AddSingleton<IAiringRefreshTrigger, AiringRefreshTrigger>();
builder.Services.AddHostedService<AiringRefreshTriggerBackgroundService>();

// Relation confidence: resolves prequel/sequel/parent-story references and
// classifies edge confidence; adjudication fetches AniList relations for
// anime that would otherwise never get an AniList lookup at all.
builder.Services.AddScoped<IRelationResolver, RelationResolver>();
builder.Services.AddScoped<IRelationAdjudicationService, RelationAdjudicationService>();
builder.Services.AddHostedService<RelationAdjudicationBackgroundService>();

// Background series build, scheduled from search when a top match has no
// stored series yet (design.md decision 6) — same trigger shape as above.
builder.Services.AddSingleton<ISeriesBuildTrigger, SeriesBuildTrigger>();
builder.Services.AddHostedService<SeriesBuildTriggerBackgroundService>();

// Manual "refresh all airing data" (settings page) — mirrors the
// build-all-series trigger/progress-tracker/background-service shape.
builder.Services.AddSingleton<IAiringFullRefreshTrigger, AiringFullRefreshTrigger>();
builder.Services.AddHostedService<AiringFullRefreshBackgroundService>();

// Manual "build all series from my list" (settings page) — mirrors the same
// trigger/progress-tracker/background-service shape (design.md decision 6).
builder.Services.AddSingleton<ISeriesBulkBuildTrigger, SeriesBulkBuildTrigger>();
builder.Services.AddHostedService<SeriesBulkBuildBackgroundService>();

// --- Season browsing ---
builder.Services.AddScoped<ISeasonBrowseService, SeasonBrowseService>();

// --- Library views (my list / top anime) ---
builder.Services.AddScoped<IMyListService, MyListService>();
builder.Services.AddScoped<ITopAnimeService, TopAnimeService>();

// --- Anime ranking ---
builder.Services.AddScoped<IAnimeRankingService, AnimeRankingService>();

// --- Profile stats ---
builder.Services.AddScoped<IProfileService, ProfileService>();

// --- Recap ---
builder.Services.AddScoped<IRecapService, RecapService>();
builder.Services.AddScoped<IRecapAvailabilityService, RecapAvailabilityService>();

// --- Single anime detail page ---
builder.Services.AddScoped<IAnimeDetailService, AnimeDetailService>();

// --- Series page ---
builder.Services.AddScoped<SeriesGraphBuilder>();
builder.Services.AddScoped<ISeriesService, SeriesService>();

// --- Device-to-device transfer ---
builder.Services.AddScoped<IDeviceIdentityInitializer, DeviceIdentityInitializer>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<IListBackupService, ListBackupService>();
builder.Services.AddSingleton<ITransferImportProgressTracker, TransferImportProgressTracker>();
builder.Services.AddSingleton<ITransferImportTrigger, TransferImportTrigger>();
builder.Services.AddScoped<ITransferImportService, TransferImportService>();
builder.Services.AddScoped<TransferImportRunner>();
builder.Services.AddHostedService<TransferImportBackgroundService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// The automatic UseRouting selects an endpoint ahead of user middleware, but
// endpoints only execute in the automatic UseEndpoints at the very end of the
// pipeline, so this still refuses a request before any controller/service
// runs (design.md D4).
app.Use(CrossSiteRequestGuard.Invoke);

app.UseAuthorization();

app.MapControllers();

using (var startupScope = app.Services.CreateScope())
{
    var db = startupScope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
    db.Database.Migrate();

    // Right after the migration, so a device identity exists before
    // anything — a hosted service, a controller, an export request — could
    // read it (export-data-mal-cannot-carry design.md D6).
    var deviceIdentityInitializer = startupScope.ServiceProvider.GetRequiredService<IDeviceIdentityInitializer>();
    await deviceIdentityInitializer.EnsureAsync();

    // Stamped here, immediately after the migration and before app.Run(), so
    // it is committed before any hosted service, controller, or debounce
    // timer can push a pending row (design.md D2).
    var startupHold = startupScope.ServiceProvider.GetRequiredService<IStartupPendingSyncHold>();
    await startupHold.ApplyAsync();

    // Read the custom mapping once now, so that its "loaded" line and any
    // warning about an entry appear at start-up rather than at the first page
    // that happens to read the mapping.
    _ = startupScope.ServiceProvider.GetRequiredService<ICustomIdMappings>().Current;
}

app.Run();
