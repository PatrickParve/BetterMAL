using System.Reflection;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Data.Repositories;

/// <summary>Shared by <see cref="UserAnimeEntryRepositoryTests"/> and
/// <c>ListViewReadParityTests</c> (my-list-dashboard-read-performance design.md
/// D3/D4) so both pin the same field list against the same notion of
/// "default".</summary>
internal static class AnimeMetadataFixture
{
    /// <summary>Every public writable instance property of <see
    /// cref="AnimeMetadata"/> except the <see cref="AnimeMetadata.RelatedAnime"/>
    /// and <see cref="AnimeMetadata.UserEntry"/> navigations — the property set
    /// both the D4 default-check and the D3 fixture-coverage guard walk.</summary>
    internal static readonly PropertyInfo[] CheckedProperties = typeof(AnimeMetadata)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.CanWrite && p.Name is not (nameof(AnimeMetadata.RelatedAnime) or nameof(AnimeMetadata.UserEntry)))
        .ToArray();

    /// <summary>An <see cref="AnimeMetadata"/> with every property in <see
    /// cref="CheckedProperties"/> set to a distinctive non-default value, so a
    /// read that copies the wrong field — or drops one — shows up rather than
    /// coincidentally matching a shared default.</summary>
    internal static AnimeMetadata FullyPopulatedAnime(int id) => new()
    {
        Id = id,
        Title = $"TITLE-{id}",
        EnglishTitle = $"ENGLISH-{id}",
        SelectedPictureUrl = $"https://example.test/selected-{id}.jpg",
        SelectedPictureModifiedAt = new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(id),
        PictureUrl = $"https://example.test/picture-{id}.jpg",
        MalPictureUrl = $"https://example.test/mal-picture-{id}.jpg",
        PictureUrls = [$"https://example.test/picture-{id}-a.jpg", $"https://example.test/picture-{id}-b.jpg"],
        PicturesSyncedAt = new DateTimeOffset(2021, 2, 1, 0, 0, 0, TimeSpan.Zero).AddDays(id),
        MalScore = 6.5 + id * 0.1,
        MediaType = "tv",
        AiringStatus = "finished_airing",
        Rating = "pg_13",
        TotalEpisodes = 12 + id,
        MalTotalEpisodes = 12 + id,
        AniListTotalEpisodes = 13 + id,
        AiredFrom = new DateOnly(2018, 1, 1).AddDays(id),
        AiredTo = new DateOnly(2018, 4, 1).AddDays(id),
        Studio = $"STUDIO-{id}",
        BroadcastDayOfWeek = "mondays",
        BroadcastTime = new TimeOnly(23, 30),
        PopularityRank = 500 + id,
        Rank = 4200 + id,
        LastSyncedAt = new DateTimeOffset(2021, 3, 1, 0, 0, 0, TimeSpan.Zero).AddDays(id),
        LastRefreshFailedAt = new DateTimeOffset(2021, 4, 1, 0, 0, 0, TimeSpan.Zero).AddDays(id),
        LastScoreSyncedAt = new DateTimeOffset(2021, 5, 1, 0, 0, 0, TimeSpan.Zero).AddDays(id),
        Genres = [$"GENRE-{id}-A", $"GENRE-{id}-B"],
        Synopsis = $"SYNOPSIS-{id}",
        Background = $"BACKGROUND-{id}",
        AverageEpisodeDurationSeconds = 1400 + id,
        Source = "manga",
    };

    /// <summary>True for null, an empty collection, or a value equal to its
    /// type's zero value (0, false, <c>default(DateTimeOffset)</c>, and so
    /// on) — the one notion of "unset" both directions of the guard share.</summary>
    internal static bool IsDefaultOrEmpty(object? value)
    {
        if (value is null)
            return true;
        if (value is System.Collections.ICollection collection)
            return collection.Count == 0;

        var type = value.GetType();
        return type.IsValueType && value.Equals(Activator.CreateInstance(type));
    }
}

// hold-startup-pending-sync-for-review tasks.md 9.8 / design.md D14:
// GetSyncStatusAsync counts held entries into HeldCount and excludes them
// from PendingCount, so the two figures never conflate "in flight" with
// "waiting on me".
public class UserAnimeEntryRepositoryTests
{
    private static readonly HashSet<string> LeanAnimeFieldNames =
    [
        nameof(AnimeMetadata.Id), nameof(AnimeMetadata.Title), nameof(AnimeMetadata.EnglishTitle),
        nameof(AnimeMetadata.PictureUrl), nameof(AnimeMetadata.MediaType), nameof(AnimeMetadata.TotalEpisodes),
        nameof(AnimeMetadata.AiringStatus), nameof(AnimeMetadata.MalScore), nameof(AnimeMetadata.PopularityRank),
        nameof(AnimeMetadata.AiredFrom), nameof(AnimeMetadata.AverageEpisodeDurationSeconds),
    ];

    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // my-list-dashboard-read-performance design.md D4: pins GetAllForListViewAsync's
    // shape directly — the one place that fails if the default interface method
    // (design.md D2) is ever left un-overridden, since GetAllAsync and
    // GetAllForListViewAsync would then return identical results.
    [Fact]
    public async Task LeanReadCarriesExactlyTheListViewFields()
    {
        using var db = CreateDb();

        var anime1 = AnimeMetadataFixture.FullyPopulatedAnime(101);
        var anime2 = new AnimeMetadata
        {
            Id = 102, Title = "Two", EnglishTitle = "Two EN", PictureUrl = "https://example.test/2.jpg",
            MediaType = "movie", TotalEpisodes = 1, AiringStatus = "finished_airing", MalScore = 7.2,
            PopularityRank = 50, AiredFrom = new DateOnly(2020, 5, 1), AverageEpisodeDurationSeconds = 6000,
        };
        var anime3 = new AnimeMetadata
        {
            Id = 103, Title = "Three", EnglishTitle = "Three EN", PictureUrl = "https://example.test/3.jpg",
            MediaType = "tv", TotalEpisodes = 24, AiringStatus = "currently_airing", MalScore = 8.1,
            PopularityRank = 12, AiredFrom = new DateOnly(2022, 7, 1), AverageEpisodeDurationSeconds = 1380,
        };
        db.AnimeMetadata.AddRange(anime1, anime2, anime3);

        // Every entry field is non-default on all three, so a copy that drops
        // or mismatches any one of the ten would be caught.
        db.UserAnimeEntries.AddRange(
            new UserAnimeEntry
            {
                AnimeId = 101, Anime = anime1, Status = WatchStatus.Rewatching, EpisodesWatched = 12, MyScore = 9,
                StartedAt = new DateOnly(2019, 1, 1), CompletedAt = new DateOnly(2019, 3, 1), RewatchCount = 2,
                PendingSync = true, LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-1), HeldForReviewAt = DateTimeOffset.UtcNow.AddDays(-2),
            },
            new UserAnimeEntry
            {
                AnimeId = 102, Anime = anime2, Status = WatchStatus.Completed, EpisodesWatched = 1, MyScore = 6,
                StartedAt = new DateOnly(2020, 5, 1), CompletedAt = new DateOnly(2020, 5, 1), RewatchCount = 1,
                PendingSync = true, LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-3), HeldForReviewAt = DateTimeOffset.UtcNow.AddDays(-4),
            },
            new UserAnimeEntry
            {
                AnimeId = 103, Anime = anime3, Status = WatchStatus.Watching, EpisodesWatched = 5, MyScore = 7,
                StartedAt = new DateOnly(2022, 7, 1), CompletedAt = new DateOnly(2022, 7, 2), RewatchCount = 3,
                PendingSync = true, LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-5), HeldForReviewAt = DateTimeOffset.UtcNow.AddDays(-6),
            });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var repository = new UserAnimeEntryRepository(db);
        var lean = await repository.GetAllForListViewAsync();
        var full = await repository.GetAllAsync();

        Assert.Equal(full.Select(e => e.AnimeId).ToHashSet(), lean.Select(e => e.AnimeId).ToHashSet());

        var fullByAnimeId = full.ToDictionary(e => e.AnimeId);
        foreach (var leanEntry in lean)
        {
            var fullEntry = fullByAnimeId[leanEntry.AnimeId];

            Assert.Equal(fullEntry.Status, leanEntry.Status);
            Assert.Equal(fullEntry.EpisodesWatched, leanEntry.EpisodesWatched);
            Assert.Equal(fullEntry.MyScore, leanEntry.MyScore);
            Assert.Equal(fullEntry.StartedAt, leanEntry.StartedAt);
            Assert.Equal(fullEntry.CompletedAt, leanEntry.CompletedAt);
            Assert.Equal(fullEntry.RewatchCount, leanEntry.RewatchCount);
            Assert.Equal(fullEntry.PendingSync, leanEntry.PendingSync);
            Assert.Equal(fullEntry.LastSyncedAt, leanEntry.LastSyncedAt);
            Assert.Equal(fullEntry.HeldForReviewAt, leanEntry.HeldForReviewAt);

            Assert.Equal(fullEntry.Anime.Id, leanEntry.Anime.Id);
            Assert.Equal(fullEntry.Anime.Title, leanEntry.Anime.Title);
            Assert.Equal(fullEntry.Anime.EnglishTitle, leanEntry.Anime.EnglishTitle);
            Assert.Equal(fullEntry.Anime.PictureUrl, leanEntry.Anime.PictureUrl);
            Assert.Equal(fullEntry.Anime.MediaType, leanEntry.Anime.MediaType);
            Assert.Equal(fullEntry.Anime.TotalEpisodes, leanEntry.Anime.TotalEpisodes);
            Assert.Equal(fullEntry.Anime.AiringStatus, leanEntry.Anime.AiringStatus);
            Assert.Equal(fullEntry.Anime.MalScore, leanEntry.Anime.MalScore);
            Assert.Equal(fullEntry.Anime.PopularityRank, leanEntry.Anime.PopularityRank);
            Assert.Equal(fullEntry.Anime.AiredFrom, leanEntry.Anime.AiredFrom);
            Assert.Equal(fullEntry.Anime.AverageEpisodeDurationSeconds, leanEntry.Anime.AverageEpisodeDurationSeconds);

            Assert.Empty(leanEntry.Anime.RelatedAnime);
            Assert.Null(leanEntry.Anime.UserEntry);

            foreach (var property in AnimeMetadataFixture.CheckedProperties)
            {
                if (LeanAnimeFieldNames.Contains(property.Name))
                    continue;

                Assert.True(
                    AnimeMetadataFixture.IsDefaultOrEmpty(property.GetValue(leanEntry.Anime)),
                    $"Expected {property.Name} to be left at its default on the lean read, but it wasn't. " +
                    "If this page now reads that column, extend GetAllForListViewAsync's projection (and its doc comment) rather than loosening this check.");
            }
        }

        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task HeldEntriesAreCountedInHeldCountAndExcludedFromPendingCount()
    {
        using var db = CreateDb();
        var unheldAnime = new AnimeMetadata { Id = 1, Title = "Unheld" };
        var heldAnime = new AnimeMetadata { Id = 2, Title = "Held" };
        var heldRemovalAnime = new AnimeMetadata { Id = 3, Title = "Held Removal" };
        db.AnimeMetadata.AddRange(unheldAnime, heldAnime, heldRemovalAnime);
        db.UserAnimeEntries.AddRange(
            new UserAnimeEntry { AnimeId = 1, Anime = unheldAnime, Status = WatchStatus.Watching, PendingSync = true },
            new UserAnimeEntry { AnimeId = 2, Anime = heldAnime, Status = WatchStatus.Watching, PendingSync = true, HeldForReviewAt = DateTimeOffset.UtcNow });
        db.PendingEntryDeletions.Add(new PendingEntryDeletion
        {
            AnimeId = 3, Anime = heldRemovalAnime, RequestedAt = DateTimeOffset.UtcNow, HeldForReviewAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var (pendingCount, heldCount, _) = await new UserAnimeEntryRepository(db).GetSyncStatusAsync();

        Assert.Equal(1, pendingCount);
        Assert.Equal(2, heldCount); // one held entry + one held removal
    }
}
