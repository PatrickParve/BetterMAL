namespace AnimeTracker.Api.Services.Mal;

public class MalOptions
{
    public const string SectionName = "Mal";

    public string ClientId { get; set; } = "";
    public string? ClientSecret { get; set; }

    /// <summary>Host port the backend is published on. Used to build the OAuth
    /// redirect URI (http://localhost:{CallbackPort}/callback), which must match
    /// the redirect URI registered with the MAL app.</summary>
    public int CallbackPort { get; set; } = 5000;
}
