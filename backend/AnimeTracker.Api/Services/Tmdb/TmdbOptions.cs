namespace AnimeTracker.Api.Services.Tmdb;

public class TmdbOptions
{
    public const string SectionName = "Tmdb";

    /// <summary>TMDB v3 API key (themoviedb.org → Settings → API). Optional:
    /// blank means TMDB access is off, and every TMDB call site becomes a
    /// no-op. It travels in the request query string, so nothing may log a
    /// request URI (design.md D4).</summary>
    public string ApiKey { get; set; } = "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
