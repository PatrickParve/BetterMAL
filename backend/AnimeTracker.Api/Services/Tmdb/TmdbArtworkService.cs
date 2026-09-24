using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.IdMapping;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AnimeTracker.Api.Services.Tmdb;

/// <summary>See <see cref="ITmdbArtworkService"/>. Reads and writes the three
/// TMDB image stores through one uniform <see cref="TmdbSetKey"/>, so the due
/// check, the single-flight lock and the option lists are each written once
/// rather than three times. Nothing here logs a request URI or the key: the
/// client owns the logging of a failed request (design.md D4).</summary>
public class TmdbArtworkService(
    AnimeTrackerDbContext db,
    ITmdbClient client,
    RefreshGate refreshGate,
    IOptions<TmdbOptions> options,
    IAnimeIdMappingResolver idMappings) : ITmdbArtworkService
{
    /// <summary>A set last fetched longer ago than this is due again. What 30
    /// days mainly delays is extra posters added to an existing show: a new
    /// season is a new <c>(tv, n)</c> key, fetched on first need whatever the
    /// age, and "Refresh data" forces the rest (design.md D10).</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(30);

    /// <summary>The most sets one series follow-up fetches. Ties to the same
    /// "bounded, obviously terminating work on a page visit" reasoning as
    /// <c>PictureRefreshService.SeriesPictureFetchBudget</c>; a franchise with
    /// more due sets (the Gundam series: dozens of TV ids) fills over several
    /// visits (design.md D10).</summary>
    public const int SeriesFetchBudget = 20;

    // The fixed orders every group is built in (design.md D11).
    private static readonly string?[] LanguageOrder = [null, "ja", "en"];
    private static readonly TmdbImageKind[] KindOrder = [TmdbImageKind.Poster, TmdbImageKind.Backdrop];

    // --- Read side: cache only, no network ---

    public async Task<AnimeTmdbPicturesDto?> GetAnimePicturesAsync(int animeId, CancellationToken ct = default) =>
        await IsInMyListAsync(animeId, ct) ? await ReadAnimePicturesAsync(animeId, ct) : null;

    public async Task<SeriesTmdbPicturesDto> GetSeriesPicturesAsync(int seriesId, CancellationToken ct = default)
    {
        var (franchise, hasMapping) = await LoadFranchiseAsync(seriesId, ct);
        var keys = franchise.Keys.ToList();
        var sets = await LoadSetsAsync(keys, includeImages: true, ct);

        // A series' language groups mix its series, season and movie images
        // (design.md D8, D12), so there is one pass over all its keys.
        var languages = BuildLanguageGroups(keys, sets, new HashSet<string>(StringComparer.Ordinal));
        var configured = options.Value.IsConfigured;
        return new SeriesTmdbPicturesDto(configured, hasMapping, languages, configured ? DueKeys(keys, sets).Count : 0);
    }

    public async Task<bool> IsAnimeFetchDueAsync(int animeId, CancellationToken ct = default)
    {
        if (!options.Value.IsConfigured || !await IsInMyListAsync(animeId, ct))
            return false;

        var keys = TmdbAnimeScopes.For(await FindMappingAsync(animeId, ct)).Keys.ToList();
        return DueKeys(keys, await LoadSetsAsync(keys, includeImages: false, ct)).Count > 0;
    }

    public async Task<IReadOnlyList<string>> GetAnimeOptionUrlsAsync(int animeId, CancellationToken ct = default) =>
        (await ReadAnimePicturesAsync(animeId, ct)).Scopes
        .SelectMany(scope => scope.Languages)
        .SelectMany(group => group.Pictures)
        .Select(picture => picture.Url)
        .ToList();

    public async Task<IReadOnlyList<string>> GetSeriesOptionUrlsAsync(int seriesId, CancellationToken ct = default) =>
        (await GetSeriesPicturesAsync(seriesId, ct)).Languages
        .SelectMany(group => group.Pictures)
        .Select(picture => picture.Url)
        .ToList();

    // --- Fetch side ---

    public async Task RefreshAnimeAsync(int animeId, bool force, CancellationToken ct = default)
    {
        if (!options.Value.IsConfigured || !await IsInMyListAsync(animeId, ct))
            return;

        var keys = TmdbAnimeScopes.For(await FindMappingAsync(animeId, ct)).Keys.ToList();
        var toFetch = force ? keys : DueKeys(keys, await LoadSetsAsync(keys, includeImages: false, ct));
        await FetchAllAsync(toFetch, force, ct);
    }

    public async Task<int> RefreshSeriesAsync(int seriesId, int budget, bool force, CancellationToken ct = default)
    {
        if (!options.Value.IsConfigured)
            return 0;

        var (franchise, _) = await LoadFranchiseAsync(seriesId, ct);
        var keys = franchise.Keys.ToList();
        var due = force ? keys : DueKeys(keys, await LoadSetsAsync(keys, includeImages: false, ct));

        var toFetch = due.Take(budget).ToList();
        await FetchAllAsync(toFetch, force, ct);
        return due.Count - toFetch.Count;
    }

    private async Task FetchAllAsync(IEnumerable<TmdbSetKey> keys, bool force, CancellationToken ct)
    {
        foreach (var key in keys)
        {
            ct.ThrowIfCancellationRequested();
            await FetchAsync(key, force, ct);
        }
    }

    /// <summary>Fetches one set under the single-flight lock and applies what
    /// came back. Dueness is checked again inside the lock, so a page that
    /// queued behind another fetch of this same set finds it fresh and makes no
    /// request of its own (spec: "Two pages, one request"). A forced fetch
    /// skips that check: it asks for the set whatever its age.</summary>
    private async Task FetchAsync(TmdbSetKey key, bool force, CancellationToken ct)
    {
        using (await refreshGate.LockAsync(key.GateKey, ct))
        {
            if (!force && !await IsDueAsync(key, ct))
                return;

            var result = key.Scope switch
            {
                TmdbScope.Series => await client.GetTvImagesAsync(key.Id, ct),
                TmdbScope.Season => await client.GetSeasonImagesAsync(key.Id, key.SeasonNumber, ct),
                _ => await client.GetMovieImagesAsync(key.Id, ct),
            };

            switch (result)
            {
                case TmdbImagesResult.Images images:
                    await StoreAsync(key, ToStoredImages(images), ct);
                    break;

                // TMDB does not know the id: an answer, so the set is recorded
                // as fetched and empty and is not asked for again for 30 days.
                case TmdbImagesResult.NotFound:
                    await StoreAsync(key, [], ct);
                    break;

                // Failed: the client has logged why. The set is left as it
                // was, whether never fetched or still holding its previous
                // images, so a later visit retries it.
            }
        }
    }

    private async Task<bool> IsDueAsync(TmdbSetKey key, CancellationToken ct) =>
        DueKeys([key], await LoadSetsAsync([key], includeImages: false, ct)).Count > 0;

    // --- Writing a set ---

    private sealed record StoredImage(string FilePath, TmdbImageKind Kind, string? Language, int Width, int Height, int Position);

    private static List<StoredImage> ToStoredImages(TmdbImagesResult.Images images)
    {
        var stored = new List<StoredImage>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(IReadOnlyList<TmdbImage> list, TmdbImageKind kind)
        {
            for (var position = 0; position < list.Count; position++)
            {
                var image = list[position];
                // A set's rows are keyed on (set, file path), so a path TMDB
                // lists twice — in both arrays, say — is stored once, the first
                // occurrence winning, rather than failing the whole save.
                if (seen.Add(image.FilePath))
                    stored.Add(new StoredImage(image.FilePath, kind, image.Language, image.Width, image.Height, position));
            }
        }

        Add(images.Posters, TmdbImageKind.Poster);
        Add(images.Backdrops, TmdbImageKind.Backdrop);
        return stored;
    }

    /// <summary>Replaces one set's images wholesale and restamps its
    /// <c>FetchedAt</c>, in a single save (design.md D5). The set is loaded
    /// tracked, and the swap is a diff — new paths added, kept ones updated in
    /// place, vanished ones removed — rather than delete-then-insert: EF cannot
    /// track a deleted and an added row under one key in the same unit of
    /// work, and one save keeps a reader from ever seeing a half-replaced
    /// set.</summary>
    private async Task StoreAsync(TmdbSetKey key, IReadOnlyList<StoredImage> images, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        try
        {
            switch (key.Scope)
            {
                case TmdbScope.Series:
                {
                    var set = await db.TmdbTvImageSets.Include(s => s.Images).FirstOrDefaultAsync(s => s.TvId == key.Id, ct);
                    if (set is null)
                        db.TmdbTvImageSets.Add(set = new TmdbTvImageSet { TvId = key.Id });
                    set.FetchedAt = now;
                    ReplaceImages(set.Images, images, image => new TmdbTvImage { TvId = key.Id, FilePath = image.FilePath });
                    break;
                }

                case TmdbScope.Season:
                {
                    var set = await db.TmdbSeasonImageSets.Include(s => s.Images)
                        .FirstOrDefaultAsync(s => s.TvId == key.Id && s.SeasonNumber == key.SeasonNumber, ct);
                    if (set is null)
                        db.TmdbSeasonImageSets.Add(set = new TmdbSeasonImageSet { TvId = key.Id, SeasonNumber = key.SeasonNumber });
                    set.FetchedAt = now;
                    ReplaceImages(set.Images, images,
                        image => new TmdbSeasonImage { TvId = key.Id, SeasonNumber = key.SeasonNumber, FilePath = image.FilePath });
                    break;
                }

                default:
                {
                    var set = await db.TmdbMovieImageSets.Include(s => s.Images).FirstOrDefaultAsync(s => s.MovieId == key.Id, ct);
                    if (set is null)
                        db.TmdbMovieImageSets.Add(set = new TmdbMovieImageSet { MovieId = key.Id });
                    set.FetchedAt = now;
                    ReplaceImages(set.Images, images, image => new TmdbMovieImage { MovieId = key.Id, FilePath = image.FilePath });
                    break;
                }
            }

            await db.SaveChangesAsync(ct);
        }
        finally
        {
            // Leave nothing of this set on the scope's context. After a save
            // it would only go stale, and after a failed one it would be
            // staged for the next unrelated SaveChanges in the same scope to
            // retry. Only TMDB's own entities are detached: the context is
            // shared with whatever page or action asked for the fetch.
            DetachCachedEntities();
        }
    }

    private void ReplaceImages<TImage>(List<TImage> stored, IReadOnlyList<StoredImage> incoming, Func<StoredImage, TImage> create)
        where TImage : TmdbImageBase
    {
        var incomingPaths = incoming.Select(i => i.FilePath).ToHashSet(StringComparer.Ordinal);
        var storedByPath = stored.ToDictionary(i => i.FilePath, StringComparer.Ordinal);

        // Materialised first, so EF marking rows deleted cannot disturb the
        // collection being enumerated.
        db.Set<TImage>().RemoveRange(stored.Where(i => !incomingPaths.Contains(i.FilePath)).ToList());

        foreach (var image in incoming)
        {
            var isNew = !storedByPath.TryGetValue(image.FilePath, out var row);
            row ??= create(image);

            // EF writes only the columns whose value actually changed, so a
            // kept row that is the same as before costs no UPDATE.
            row.Kind = image.Kind;
            row.Language = image.Language;
            row.Width = image.Width;
            row.Height = image.Height;
            row.Position = image.Position;

            if (isNew)
                db.Set<TImage>().Add(row);
        }
    }

    private void DetachCachedEntities()
    {
        var entries = db.ChangeTracker.Entries()
            .Where(e => e.Entity is TmdbImageBase or TmdbTvImageSet or TmdbSeasonImageSet or TmdbMovieImageSet)
            .ToList();

        foreach (var entry in entries)
            entry.State = EntityState.Detached;
    }

    // --- Reading the cache ---

    /// <summary>A stored set as the read side needs it: when it was fetched,
    /// and (only when asked for) its images.</summary>
    private sealed record CachedSet(DateTimeOffset FetchedAt, IReadOnlyList<TmdbImageBase> Images);

    /// <summary>The stored sets among <paramref name="keys"/>: at most one
    /// query per store, and none for a store no key names. A key with no entry
    /// in the result has never been fetched. <paramref name="includeImages"/>
    /// off is the cheap form for a due check, which needs only the
    /// timestamps.</summary>
    private async Task<Dictionary<TmdbSetKey, CachedSet>> LoadSetsAsync(
        IReadOnlyCollection<TmdbSetKey> keys, bool includeImages, CancellationToken ct)
    {
        var sets = new Dictionary<TmdbSetKey, CachedSet>();
        var wanted = keys.ToHashSet();

        var tvIds = IdsOf(keys, TmdbScope.Series);
        if (tvIds.Count > 0)
        {
            IQueryable<TmdbTvImageSet> query = db.TmdbTvImageSets.AsNoTracking().Where(s => tvIds.Contains(s.TvId));
            if (includeImages)
                query = query.Include(s => s.Images);
            foreach (var set in await query.ToListAsync(ct))
                sets[TmdbSetKey.Tv(set.TvId)] = new CachedSet(set.FetchedAt, set.Images);
        }

        // A season key is (TV id, n), which EF cannot match as a tuple, so the
        // query takes every cached season of those shows and the exact keys
        // are picked out here.
        var seasonTvIds = IdsOf(keys, TmdbScope.Season);
        if (seasonTvIds.Count > 0)
        {
            IQueryable<TmdbSeasonImageSet> query = db.TmdbSeasonImageSets.AsNoTracking().Where(s => seasonTvIds.Contains(s.TvId));
            if (includeImages)
                query = query.Include(s => s.Images);
            foreach (var set in await query.ToListAsync(ct))
            {
                var key = TmdbSetKey.Season(set.TvId, set.SeasonNumber);
                if (wanted.Contains(key))
                    sets[key] = new CachedSet(set.FetchedAt, set.Images);
            }
        }

        var movieIds = IdsOf(keys, TmdbScope.Movie);
        if (movieIds.Count > 0)
        {
            IQueryable<TmdbMovieImageSet> query = db.TmdbMovieImageSets.AsNoTracking().Where(s => movieIds.Contains(s.MovieId));
            if (includeImages)
                query = query.Include(s => s.Images);
            foreach (var set in await query.ToListAsync(ct))
                sets[TmdbSetKey.Movie(set.MovieId)] = new CachedSet(set.FetchedAt, set.Images);
        }

        return sets;
    }

    private static List<int> IdsOf(IEnumerable<TmdbSetKey> keys, TmdbScope scope) =>
        keys.Where(k => k.Scope == scope).Select(k => k.Id).Distinct().ToList();

    /// <summary>The keys, in the order given, whose set is due: never fetched,
    /// or fetched longer ago than <see cref="StaleAfter"/>.</summary>
    private static List<TmdbSetKey> DueKeys(IEnumerable<TmdbSetKey> keys, IReadOnlyDictionary<TmdbSetKey, CachedSet> sets)
    {
        var now = DateTimeOffset.UtcNow;
        return keys.Where(key => !sets.TryGetValue(key, out var set) || now - set.FetchedAt > StaleAfter).ToList();
    }

    private Task<bool> IsInMyListAsync(int animeId, CancellationToken ct) =>
        db.UserAnimeEntries.AnyAsync(e => e.AnimeId == animeId, ct);

    // Every mapping read goes through the resolver, so a custom entry (design.md
    // D19) counts exactly as a synced one: it decides an anime's scopes, its
    // "TMDB has no match" note and whether a fetch is due.
    private Task<AnimeIdMapping?> FindMappingAsync(int animeId, CancellationToken ct) =>
        idMappings.FindAsync(animeId, ct);

    /// <summary>Whether a mapping names any TMDB id. An IMDb-only row, or no
    /// row at all, is no TMDB match: it is what the picker's "TMDB has no
    /// match" note rests on.</summary>
    private static bool HasTmdbId(AnimeIdMapping? mapping) =>
        mapping is not null && (mapping.TmdbTvId is not null || mapping.TmdbMovieIds.Count > 0);

    private async Task<AnimeTmdbPicturesDto> ReadAnimePicturesAsync(int animeId, CancellationToken ct)
    {
        var mapping = await FindMappingAsync(animeId, ct);
        var scopes = TmdbAnimeScopes.For(mapping);
        var sets = await LoadSetsAsync(scopes.Keys.ToList(), includeImages: true, ct);

        // One set of file paths across every scope: no picture is offered
        // twice, and where two scopes list the same image (a show's poster is
        // often also its season 1 poster) the earlier scope keeps it.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<TmdbScopeDto>();

        void Add(TmdbScope scope, int? seasonNumber, IReadOnlyList<TmdbSetKey> scopeKeys)
        {
            var languages = BuildLanguageGroups(scopeKeys, sets, seen);
            if (languages.Count > 0)
                result.Add(new TmdbScopeDto(scope, seasonNumber, languages));
        }

        if (scopes.Series is { } series)
            Add(TmdbScope.Series, null, [series]);
        if (scopes.Season is { } season)
            Add(TmdbScope.Season, season.SeasonNumber, [season]);
        if (scopes.Movie.Count > 0)
            Add(TmdbScope.Movie, null, scopes.Movie);

        return new AnimeTmdbPicturesDto(options.Value.IsConfigured, HasTmdbId(mapping), result);
    }

    /// <summary>Groups the images of <paramref name="keys"/> by language (none,
    /// then ja, then en). Within a group, every poster comes before any
    /// backdrop, each in the order of the keys and then TMDB's own order within
    /// a set. An image whose path is already in <paramref name="seen"/> is
    /// skipped and every path offered is added to it, so the caller decides how
    /// far "no option appears twice" reaches. Empty groups are left out.</summary>
    private static List<TmdbLanguageGroupDto> BuildLanguageGroups(
        IReadOnlyList<TmdbSetKey> keys, IReadOnlyDictionary<TmdbSetKey, CachedSet> sets, HashSet<string> seen)
    {
        var groups = new List<TmdbLanguageGroupDto>();

        foreach (var language in LanguageOrder)
        {
            var pictures = new List<TmdbPictureDto>();
            foreach (var kind in KindOrder)
            {
                foreach (var key in keys)
                {
                    if (!sets.TryGetValue(key, out var set))
                        continue;

                    foreach (var image in set.Images.Where(i => i.Language == language && i.Kind == kind).OrderBy(i => i.Position))
                    {
                        if (seen.Add(image.FilePath))
                            pictures.Add(new TmdbPictureDto(TmdbImageUrl.Original(image.FilePath), image.Kind, image.Width, image.Height));
                    }
                }
            }

            if (pictures.Count > 0)
                groups.Add(new TmdbLanguageGroupDto(language ?? "none", pictures));
        }

        return groups;
    }

    /// <summary>A series' franchise keys (design.md D8), and whether any member
    /// has a TMDB mapping at all. Members are read here — the main line in
    /// watch order, then the extras in the order the More section displays
    /// them (relation group, then position) — so every caller works from the
    /// same list.</summary>
    private async Task<(TmdbFranchiseKeys Franchise, bool HasMapping)> LoadFranchiseAsync(int seriesId, CancellationToken ct)
    {
        var members = await db.SeriesMembers.AsNoTracking()
            .Where(m => m.SeriesId == seriesId)
            .Select(m => new { m.AnimeId, m.IsMainLine, m.Order, m.RelationGroup })
            .ToListAsync(ct);

        var mainLine = members.Where(m => m.IsMainLine).OrderBy(m => m.Order).Select(m => m.AnimeId).ToList();
        var extras = members.Where(m => !m.IsMainLine)
            .OrderBy(m => SeriesRelationGroupOrder.GroupOf(SeriesService.ParseRelationGroup(m.RelationGroup)))
            .ThenBy(m => m.Order)
            .Select(m => m.AnimeId);
        List<int> allMembers = [.. mainLine, .. extras];

        var mappings = await idMappings.FindManyAsync(allMembers, ct);

        return (TmdbFranchiseKeys.For(mainLine, allMembers, mappings), mappings.Values.Any(m => HasTmdbId(m)));
    }
}
