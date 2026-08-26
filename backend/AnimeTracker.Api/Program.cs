using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Artwork;
using AnimeTracker.Api.Services.Airing.AniList;
using AnimeTracker.Api.Services.Dashboard;
using AnimeTracker.Api.Services.Detail;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Import;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Library;
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
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddSingleton<MalRequestPacer>();
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

// --- Initial import ---
builder.Services.AddSingleton<IImportProgressTracker, ImportProgressTracker>();
builder.Services.AddSingleton<IImportTrigger, ImportTrigger>();
builder.Services.AddScoped<IInitialImportService, InitialImportService>();
builder.Services.AddHostedService<InitialImportBackgroundService>();

// --- List editing & write-sync ---
builder.Services.AddSingleton<IEntrySyncScheduler, DebouncedEntrySyncScheduler>();
builder.Services.AddScoped<IUserAnimeEntryEditService, UserAnimeEntryEditService>();
builder.Services.AddScoped<ICompletedEntryReopenService, CompletedEntryReopenService>();

// --- Write-sync retry & reconciliation ---
builder.Services.AddScoped<IEntryPushService, EntryPushService>();
builder.Services.AddScoped<IReconciliationService, ReconciliationService>();
builder.Services.AddHostedService<PendingSyncRetryBackgroundService>();
builder.Services.AddHostedService<ReconciliationBackgroundService>();

// --- Corrective full re-sync (one-time, manually triggered) ---
builder.Services.AddSingleton<IResyncProgressTracker, ResyncProgressTracker>();
builder.Services.AddSingleton<IResyncTrigger, ResyncTrigger>();
builder.Services.AddScoped<IResyncService, ResyncService>();
builder.Services.AddHostedService<ResyncBackgroundService>();

// --- Metadata & score refresh ---
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

// Manual "refresh all airing data" (settings page) — mirrors the corrective
// MAL re-sync's trigger/progress-tracker/background-service shape.
builder.Services.AddSingleton<IAiringFullRefreshTrigger, AiringFullRefreshTrigger>();
builder.Services.AddSingleton<IAiringFullRefreshProgressTracker, AiringFullRefreshProgressTracker>();
builder.Services.AddHostedService<AiringFullRefreshBackgroundService>();

// Manual "build all series from my list" (settings page) — mirrors the same
// trigger/progress-tracker/background-service shape (design.md decision 6).
builder.Services.AddSingleton<ISeriesBulkBuildTrigger, SeriesBulkBuildTrigger>();
builder.Services.AddSingleton<ISeriesBulkBuildProgressTracker, SeriesBulkBuildProgressTracker>();
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

using (var startupScope = app.Services.CreateScope())
{
    var db = startupScope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
    db.Database.Migrate();
}

app.Run();
