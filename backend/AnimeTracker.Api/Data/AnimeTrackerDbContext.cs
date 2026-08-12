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
            entity.HasKey(e => e.AnimeId);
            entity.HasOne(e => e.Anime)
                .WithMany()
                .HasForeignKey(e => e.AnimeId)
                .OnDelete(DeleteBehavior.Cascade);
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
        });

        modelBuilder.Entity<Series>(entity =>
        {
            entity.HasIndex(e => e.RootAnimeId).IsUnique();
        });

        modelBuilder.Entity<SeriesMember>(entity =>
        {
            entity.HasKey(e => e.AnimeId);
            entity.HasOne<Series>()
                .WithMany(s => s.Members)
                .HasForeignKey(e => e.SeriesId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Anime)
                .WithMany()
                .HasForeignKey(e => e.AnimeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.SeriesId);
        });
    }
}
