using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Data;

public class AnimeTrackerDbContext(DbContextOptions<AnimeTrackerDbContext> options) : DbContext(options)
{
    public DbSet<AnimeMetadata> AnimeMetadata => Set<AnimeMetadata>();
    public DbSet<UserAnimeEntry> UserAnimeEntries => Set<UserAnimeEntry>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<OAuthToken> OAuthTokens => Set<OAuthToken>();
    public DbSet<TopAnimeSelection> TopAnimeSelections => Set<TopAnimeSelection>();
    public DbSet<RankingState> RankingStates => Set<RankingState>();
    public DbSet<DeviceIdentity> DeviceIdentities => Set<DeviceIdentity>();
    public DbSet<PendingEntryDeletion> PendingEntryDeletions => Set<PendingEntryDeletion>();
    public DbSet<PendingReconciliationDiff> PendingReconciliationDiffs => Set<PendingReconciliationDiff>();
    public DbSet<PendingReconciliationDiffEntry> PendingReconciliationDiffEntries => Set<PendingReconciliationDiffEntry>();
    public DbSet<SeasonFetchLog> SeasonFetchLogs => Set<SeasonFetchLog>();
    public DbSet<TopAnimeRankingEntry> TopAnimeRankingEntries => Set<TopAnimeRankingEntry>();
    public DbSet<TopAnimeFetchLog> TopAnimeFetchLogs => Set<TopAnimeFetchLog>();
    public DbSet<SeasonAnimeListing> SeasonAnimeListings => Set<SeasonAnimeListing>();
    public DbSet<ReconciliationRunLog> ReconciliationRunLogs => Set<ReconciliationRunLog>();
    public DbSet<EpisodeAiring> EpisodeAirings => Set<EpisodeAiring>();
    public DbSet<AnimeAiringSync> AnimeAiringSyncs => Set<AnimeAiringSync>();
    public DbSet<AiringRefreshState> AiringRefreshStates => Set<AiringRefreshState>();
    public DbSet<AnimeRelatedAnime> AnimeRelatedAnime => Set<AnimeRelatedAnime>();
    public DbSet<Series> Series => Set<Series>();
    public DbSet<SeriesMember> SeriesMembers => Set<SeriesMember>();
    public DbSet<AniListRelation> AniListRelations => Set<AniListRelation>();
    public DbSet<RelationDiscovery> RelationDiscoveries => Set<RelationDiscovery>();
    public DbSet<AnimeUpdate> AnimeUpdates => Set<AnimeUpdate>();
    public DbSet<AnimeIdMapping> AnimeIdMappings => Set<AnimeIdMapping>();
    public DbSet<AnimeIdMappingSyncState> AnimeIdMappingSyncStates => Set<AnimeIdMappingSyncState>();
    public DbSet<TmdbTvImageSet> TmdbTvImageSets => Set<TmdbTvImageSet>();
    public DbSet<TmdbTvImage> TmdbTvImages => Set<TmdbTvImage>();
    public DbSet<TmdbSeasonImageSet> TmdbSeasonImageSets => Set<TmdbSeasonImageSet>();
    public DbSet<TmdbSeasonImage> TmdbSeasonImages => Set<TmdbSeasonImage>();
    public DbSet<TmdbMovieImageSet> TmdbMovieImageSets => Set<TmdbMovieImageSet>();
    public DbSet<TmdbMovieImage> TmdbMovieImages => Set<TmdbMovieImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnimeMetadata>(entity =>
        {
            // Id is the MAL anime id, supplied by the application — not DB-generated.
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<UserAnimeEntry>(entity =>
        {
            entity.HasKey(e => e.AnimeId);
            entity.HasOne(e => e.Anime)
                .WithOne(a => a.UserEntry)
                .HasForeignKey<UserAnimeEntry>(e => e.AnimeId);
            entity.Property(e => e.Status).HasConversion<string>();
            // Maps Postgres's built-in xmin system column as a concurrency token
            // (no migration needed for the column itself — every row already has
            // one). Lets the debounced push detect, via
            // DbUpdateConcurrencyException, that an edit landed between its read
            // and its save, so it can leave PendingSync set instead of silently
            // clearing it for values it never actually pushed to MAL.
            entity.Property<uint>("RowVersion")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.HasOne(e => e.Anime)
                .WithMany()
                .HasForeignKey(e => e.AnimeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.ChangeType).HasConversion<string>();
            entity.HasIndex(e => e.Timestamp);
            // No default value: EF always sends the object's EventId, so a
            // database default would never fire for the app, and an
            // EF-scaffolded zero-GUID default would turn a forgotten value
            // into a unique-index collision on the second row (design.md D5).
            entity.HasIndex(e => e.EventId).IsUnique();
        });

        modelBuilder.Entity<TopAnimeSelection>(entity =>
        {
            entity.HasKey(e => e.AnimeId);
            entity.HasOne(e => e.Anime)
                .WithMany()
                .HasForeignKey(e => e.AnimeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PendingEntryDeletion>(entity =>
        {
            entity.HasKey(e => e.AnimeId);
            entity.HasOne(e => e.Anime)
                .WithMany()
                .HasForeignKey(e => e.AnimeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PendingReconciliationDiff>(entity =>
        {
            entity.HasMany(e => e.Entries)
                .WithOne(e => e.Diff)
                .HasForeignKey(e => e.PendingReconciliationDiffId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PendingReconciliationDiffEntry>(entity =>
        {
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.ChangeType).HasConversion<string>();
        });

        modelBuilder.Entity<SeasonFetchLog>(entity =>
        {
            entity.HasKey(e => new { e.Year, e.Season });
        });

        modelBuilder.Entity<SeasonAnimeListing>(entity =>
        {
            entity.HasKey(e => new { e.Year, e.Season, e.AnimeId });
            entity.HasOne(e => e.Anime)
                .WithMany()
                .HasForeignKey(e => e.AnimeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TopAnimeRankingEntry>(entity =>
        {
            entity.HasKey(e => new { e.RankingType, e.AnimeId });
            entity.HasOne(e => e.Anime)
                .WithMany()
                .HasForeignKey(e => e.AnimeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TopAnimeFetchLog>(entity =>
        {
            entity.HasKey(e => e.RankingType);
        });

        modelBuilder.Entity<EpisodeAiring>(entity =>
        {
            entity.HasKey(e => new { e.AnimeId, e.Episode });
            entity.HasOne<AnimeMetadata>()
                .WithMany()
                .HasForeignKey(e => e.AnimeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.AnimeId, e.AirsAtUtc });
        });

        modelBuilder.Entity<AnimeAiringSync>(entity =>
        {
            entity.HasKey(e => e.AnimeId);
            entity.HasOne<AnimeMetadata>()
                .WithMany()
                .HasForeignKey(e => e.AnimeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AnimeRelatedAnime>(entity =>
        {
            entity.HasKey(e => new { e.AnimeId, e.RelatedAnimeId, e.RelationType });
            entity.HasOne(e => e.Anime)
                .WithMany(a => a.RelatedAnime)
                .HasForeignKey(e => e.AnimeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.AnimeId);
            // Reverse lookup: relations that point *at* a given anime. Every
            // detail-page read needs this direction now (relation-confidence
            // union of outgoing/incoming edges), not just rare series builds.
            entity.HasIndex(e => e.RelatedAnimeId);
        });

        modelBuilder.Entity<AniListRelation>(entity =>
        {
            entity.HasKey(e => new { e.AnimeId, e.RelatedAnimeId, e.RelationType });
            entity.HasOne<AnimeMetadata>()
                .WithMany()
                .HasForeignKey(e => e.AnimeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.AnimeId);
            entity.HasIndex(e => e.RelatedAnimeId);
        });

        modelBuilder.Entity<RelationDiscovery>(entity =>
        {
            entity.HasOne(e => e.Anime)
                .WithMany()
                .HasForeignKey(e => e.AnimeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.DiscoveredAt);
            entity.HasIndex(e => e.ProcessedAt); // the "still unprocessed" query
        });

        modelBuilder.Entity<AnimeUpdate>(entity =>
        {
            entity.HasOne(e => e.Anime)
                .WithMany()
                .HasForeignKey(e => e.AnimeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.DetectedAt);
            entity.HasIndex(e => e.AnimeId);
        });

        modelBuilder.Entity<Series>(entity =>
        {
            // Id is the root entry's MAL id, supplied by the builder — the
            // uniqueness the RootAnimeId index used to guarantee is now the
            // primary key's job.
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<SeriesMember>(entity =>
        {
            entity.HasKey(e => new { e.SeriesId, e.AnimeId });
            entity.HasOne<Series>()
                .WithMany(s => s.Members)
                .HasForeignKey(e => e.SeriesId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Anime)
                .WithMany()
                .HasForeignKey(e => e.AnimeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.SeriesId);
            // Anime -> series lookups (FindSeriesAsync/IsPrimary resolution,
            // RelationResolver, ArtworkSelectionService, ...) no longer hit the
            // primary key now that it's composite (split-series-by-version
            // tasks 1.2/8.*).
            entity.HasIndex(e => e.AnimeId);
            // Every membership recorded before this column existed was a
            // core one (rebuild-series-by-story-component design.md decision
            // D8) — VersionSlotKey/BranchHeadAnimeId need no default of their
            // own, since null is already the correct "not an alternative"
            // value for pre-existing rows.
            entity.Property(e => e.MembershipKind).HasDefaultValue("Core");
        });

        modelBuilder.Entity<AnimeIdMapping>(entity =>
        {
            // The MAL id, supplied by the sync — not DB-generated. Deliberately
            // no FK to AnimeMetadata (see the entity's comment). The array
            // columns (integer[], text[]) come from the List<int>/List<string>
            // properties, as PictureUrls and Genres do.
            entity.HasKey(e => e.AnimeId);
            entity.Property(e => e.AnimeId).ValueGeneratedNever();
        });

        // The three TMDB image caches share one shape: a fetch-log set row and
        // image rows that cascade-delete with it (design.md D5). The set ids are
        // TMDB's own, so never DB-generated; Kind is stored as its name, as
        // RelationGroup is. Image rows share their columns through a plain base
        // class that is not in the model, so each table gets them as its own.
        modelBuilder.Entity<TmdbTvImageSet>(entity =>
        {
            entity.HasKey(e => e.TvId);
            entity.Property(e => e.TvId).ValueGeneratedNever();
            entity.HasMany(e => e.Images)
                .WithOne()
                .HasForeignKey(e => e.TvId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TmdbTvImage>(entity =>
        {
            entity.HasKey(e => new { e.TvId, e.FilePath });
            entity.Property(e => e.Kind).HasConversion<string>();
        });

        modelBuilder.Entity<TmdbSeasonImageSet>(entity =>
        {
            entity.HasKey(e => new { e.TvId, e.SeasonNumber });
            entity.HasMany(e => e.Images)
                .WithOne()
                .HasForeignKey(e => new { e.TvId, e.SeasonNumber })
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TmdbSeasonImage>(entity =>
        {
            entity.HasKey(e => new { e.TvId, e.SeasonNumber, e.FilePath });
            entity.Property(e => e.Kind).HasConversion<string>();
        });

        modelBuilder.Entity<TmdbMovieImageSet>(entity =>
        {
            entity.HasKey(e => e.MovieId);
            entity.Property(e => e.MovieId).ValueGeneratedNever();
            entity.HasMany(e => e.Images)
                .WithOne()
                .HasForeignKey(e => e.MovieId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TmdbMovieImage>(entity =>
        {
            entity.HasKey(e => new { e.MovieId, e.FilePath });
            entity.Property(e => e.Kind).HasConversion<string>();
        });
    }
}
