using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Entries;

namespace AnimeTracker.Api.Services.Ranking;

/// <summary>design.md D2: the four bands the ranking sorts a score tier
/// into, ascending. Bands 0-2 are the ranked set (they receive a rank
/// number); Unranked receives none. <see cref="AnimeRankingKey.OrderByRanking"/>
/// and <c>Data/Repositories/SeasonRepository.cs</c>'s my-score sort both
/// inline this same band rule in SQL, since EF cannot translate a call into
/// <see cref="RankBandResolver.Resolve"/> — every copy must move
/// together.</summary>
public enum RankBand
{
    HandOrdered = 0,
    ShortForm = 1,
    Dropped = 2,
    Unranked = 3,
}

public static class RankBandResolver
{
    private static readonly HashSet<string> ShortFormMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "music", "cm", "pv",
    };

    /// <summary>Dropped is checked before short form (design.md D2), which
    /// settles the dropped-music-video case in favour of the very bottom.
    /// The aired test reuses <see cref="AiredEpisodeGate.HasAired"/> with no
    /// known aired-episode count, since a <see cref="UserAnimeEntry"/> alone
    /// carries none — this is the same approximation design.md D3 accepts
    /// for the SQL side, and the app's own editing rules keep the two
    /// implementations from ever actually disagreeing (scoring an anime
    /// already requires it to have aired).</summary>
    public static RankBand Resolve(UserAnimeEntry entry)
    {
        if (entry.MyScore is null)
            return RankBand.Unranked;

        if (entry.Status == WatchStatus.PlanToWatch)
            return RankBand.Unranked;

        if (!AiredEpisodeGate.HasAired(entry.Anime, episodesAired: null))
            return RankBand.Unranked;

        if (entry.Status == WatchStatus.Dropped)
            return RankBand.Dropped;

        if (entry.Anime.MediaType is { } mediaType && ShortFormMediaTypes.Contains(mediaType))
            return RankBand.ShortForm;

        return RankBand.HandOrdered;
    }
}
