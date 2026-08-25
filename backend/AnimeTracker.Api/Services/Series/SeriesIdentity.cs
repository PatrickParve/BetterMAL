namespace AnimeTracker.Api.Services.Series;

/// <summary>The single resolution of "what is this series called and what
/// does it look like" (design.md D7 / spec `series-identity` "resolved in one
/// place") — every surface that names or pictures a series routes through
/// this instead of projecting its root member directly.</summary>
public static class SeriesIdentity
{
    /// <summary>Resolves a series' displayed title, English title, and
    /// picture. A chosen title suppresses the English title: the client's
    /// title-preference rule picks between the two, so leaving the root's
    /// English title in place beside a chosen title would let some surfaces
    /// render the very title that was rejected.</summary>
    public static (string Title, string? EnglishTitle, string? PictureUrl) Resolve(
        string? selectedTitle, string? selectedPictureUrl, string rootTitle, string? rootEnglishTitle, string? rootPictureUrl) =>
        selectedTitle is not null
            ? (selectedTitle, null, selectedPictureUrl ?? rootPictureUrl)
            : (rootTitle, rootEnglishTitle, selectedPictureUrl ?? rootPictureUrl);
}
