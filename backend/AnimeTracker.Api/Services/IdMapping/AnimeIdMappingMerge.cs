using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.IdMapping;

/// <summary>How a custom entry applies on top of the synced mapping (design.md
/// D19, spec `external-id-mapping` "A custom mapping file supplements and
/// corrects the synced mapping"). Pure, so the rules can be tested without a
/// file or a database.
///
/// An entry provides up to two groups, decided separately: its TMDB ids
/// together with the season (the season means nothing without its show), and
/// its IMDb ids. A group the entry provides applies when the synced mapping
/// holds nothing for that group, or when the entry is an override. A group it
/// does not provide is read from the synced mapping. So a source that catches
/// up wins with no edit to the file, and an IMDb-only source row can still take
/// a TMDB id from it.</summary>
public static class AnimeIdMappingMerge
{
    /// <summary>The mapping a reader should see: <paramref name="synced"/> with
    /// <paramref name="custom"/> applied. Returns <paramref name="synced"/>
    /// itself when the entry changes nothing, and otherwise a new mapping, never
    /// the synced one modified.</summary>
    public static AnimeIdMapping? Apply(AnimeIdMapping? synced, CustomIdMapping? custom)
    {
        if (custom is null)
            return synced;

        var entry = custom.Mapping;
        var takeTmdb = HasTmdb(entry) && (custom.Override || !HasTmdb(synced));
        var takeImdb = HasImdb(entry) && (custom.Override || !HasImdb(synced));
        if (!takeTmdb && !takeImdb)
            return synced;

        var tmdb = takeTmdb ? entry : synced;
        var imdb = takeImdb ? entry : synced;
        return new AnimeIdMapping
        {
            AnimeId = entry.AnimeId,
            TmdbTvId = tmdb?.TmdbTvId,
            TmdbSeasonNumber = tmdb?.TmdbSeasonNumber,
            TmdbMovieIds = [.. tmdb?.TmdbMovieIds ?? []],
            ImdbIds = [.. imdb?.ImdbIds ?? []],
        };
    }

    /// <summary>True when applying <paramref name="custom"/> would leave the
    /// mapping exactly as the source has it: a default entry whose groups the
    /// source now holds, or an override the source now agrees with. Such an
    /// entry can be deleted from the file.</summary>
    public static bool IsUnnecessary(AnimeIdMapping? synced, CustomIdMapping custom) =>
        Same(Apply(synced, custom), synced);

    /// <summary>Whether the mapping names any TMDB id, TV or movie.</summary>
    public static bool HasTmdb(AnimeIdMapping? mapping) =>
        mapping is not null && (mapping.TmdbTvId is not null || mapping.TmdbMovieIds.Count > 0);

    public static bool HasImdb(AnimeIdMapping? mapping) =>
        mapping is not null && mapping.ImdbIds.Count > 0;

    private static bool Same(AnimeIdMapping? a, AnimeIdMapping? b)
    {
        if (a is null || b is null)
            return a is null && b is null;

        return a.TmdbTvId == b.TmdbTvId
            && a.TmdbSeasonNumber == b.TmdbSeasonNumber
            && a.TmdbMovieIds.SequenceEqual(b.TmdbMovieIds)
            && a.ImdbIds.SequenceEqual(b.ImdbIds);
    }
}
