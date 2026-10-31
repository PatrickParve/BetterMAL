namespace AnimeTracker.Api.Services.AnimeThemes;

/// <summary>What one opening or ending is held as: the one version and video
/// chosen for it (<see cref="AnimeThemeChoice"/>). <c>Episodes</c> is every
/// version's, the flags are the chosen version's, and a link is null unless it
/// passed the host allow-list.</summary>
public record ChosenTheme(
    string? Episodes,
    bool Nsfw,
    bool Spoiler,
    bool Clean,
    string? VideoUrl,
    string? AudioUrl);

/// <summary>Which version and which video of a theme the catalogue uses
/// (spec `anime-themes`, add-anime-themes-and-guess-the-theme design.md D4), and
/// the allow-list that decides which links are kept. Pure functions.
/// <para>A theme has versions, each with videos. The row and the game use one of
/// each, so the flags on a row describe what would really play:</para>
/// <list type="bullet">
/// <item><description>Version: the lowest-numbered of those that have a video with audio, preferring
/// one with no flag, then one flagged only NSFW, then any other. So a spoiler-free cut wins
/// whenever one exists.</description></item>
/// <item><description>Video: a clean one (no credits over it) first, then AnimeThemes' own
/// <c>priority</c>, highest first.</description></item>
/// <item><description>Episodes: every version's, in order, because a row says where the song plays,
/// not where this cut plays.</description></item>
/// </list>
/// A theme none of whose versions has a video with audio still gets a version, from
/// those with any video and then from all of them, so it is held with what it
/// has rather than dropped.</summary>
public static class AnimeThemeChoice
{
    public const string VideoHost = "v.animethemes.moe";
    public const string AudioHost = "a.animethemes.moe";

    public static ChosenTheme Choose(IReadOnlyList<AnimeThemesVersion> versions)
    {
        var ordered = versions.OrderBy(v => v.Number).ToList();
        if (ordered.Count == 0)
            return new ChosenTheme(null, false, false, false, null, null);

        var episodes = string.Join(", ", ordered
            .Select(v => v.Episodes?.Trim())
            .Where(e => !string.IsNullOrEmpty(e)));

        var pool = ordered.Where(v => v.Videos.Any(HasAudio)).ToList();
        if (pool.Count == 0)
            pool = ordered.Where(v => v.Videos.Count > 0).ToList();
        if (pool.Count == 0)
            pool = ordered;

        // OrderBy is stable, and the pool is already in version order, so the
        // lowest number wins among versions with the same flags.
        var version = pool.OrderBy(Flagging).First();

        var videos = version.Videos.Where(HasAudio).ToList();
        if (videos.Count == 0)
            videos = [.. version.Videos];
        var video = videos
            .OrderByDescending(v => v.Clean)
            .ThenByDescending(v => v.Priority)
            .FirstOrDefault();

        return new ChosenTheme(
            episodes.Length == 0 ? null : episodes,
            version.Nsfw,
            version.Spoiler,
            video?.Clean ?? false,
            VideoLink(video?.VideoUrl),
            AudioLink(video?.AudioUrl));
    }

    /// <summary>The link if it is `https` on the video host, else null.</summary>
    public static string? VideoLink(string? link) => Allowed(link, VideoHost);

    /// <summary>The link if it is `https` on the audio host, else null. The backend
    /// fetches this one, and it came from outside, so this is what stops a bad row
    /// from aiming the relay anywhere else: the relay checks it again before
    /// every read.</summary>
    public static string? AudioLink(string? link) => Allowed(link, AudioHost);

    // 0 no flag, 1 NSFW only, 2 anything flagged spoiler, whether or not it is
    // also NSFW: the order of preference between versions.
    private static int Flagging(AnimeThemesVersion version) =>
        version.Spoiler ? 2 : version.Nsfw ? 1 : 0;

    private static bool HasAudio(AnimeThemesVideo video) => !string.IsNullOrWhiteSpace(video.AudioUrl);

    // Absolute, `https`, exactly the host, the default port and no credentials.
    // The link returned is the normalised one, so what is stored is what is
    // fetched.
    private static string? Allowed(string? link, string host)
    {
        if (string.IsNullOrWhiteSpace(link) || !Uri.TryCreate(link.Trim(), UriKind.Absolute, out var uri))
            return null;

        if (uri.Scheme != Uri.UriSchemeHttps
            || !uri.IsDefaultPort
            || uri.UserInfo.Length > 0
            || !string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase))
            return null;

        return uri.AbsoluteUri;
    }
}
