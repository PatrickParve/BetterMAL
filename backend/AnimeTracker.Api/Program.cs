using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Dashboard;
using AnimeTracker.Api.Services.Detail;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Import;
using AnimeTracker.Api.Services.Library;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Auth;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Search;
using AnimeTracker.Api.Services.Season;
using AnimeTracker.Api.Services.Sync;
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
builder.Services.AddScoped<IMetadataRefreshService, MetadataRefreshService>();
builder.Services.AddHostedService<MetadataRefreshBackgroundService>();

// --- Timezone conversion ---
builder.Services.AddSingleton<IBroadcastLocalTimeConverter, BroadcastLocalTimeConverter>();

// --- Search ---
builder.Services.AddScoped<IAnimeSearchService, AnimeSearchService>();

// --- Main dashboard ---
builder.Services.AddScoped<IMainDashboardService, MainDashboardService>();

// --- Airing schedule ---
builder.Services.AddScoped<IAiringScheduleService, AiringScheduleService>();

// --- Season browsing ---
builder.Services.AddScoped<ISeasonBrowseService, SeasonBrowseService>();

// --- Library views (my list / top anime) ---
builder.Services.AddScoped<IMyListService, MyListService>();
builder.Services.AddScoped<ITopAnimeService, TopAnimeService>();

// --- Profile stats ---
builder.Services.AddScoped<IProfileService, ProfileService>();

// --- Single anime detail page ---
builder.Services.AddScoped<IAnimeDetailService, AnimeDetailService>();

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
