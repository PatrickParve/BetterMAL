namespace AnimeTracker.Api.Services.Mal;

public class MalOptions
{
    public const string SectionName = "Mal";

    public string ClientId { get; set; } = "";
    public string? ClientSecret { get; set; }

    /// <summary>Host port the backend is published on. Used to build the OAuth
    /// redirect URI (http://localhost:{CallbackPort}/callback), which must match
    /// the redirect URI registered with the MAL app.</summary>
    public int CallbackPort { get; set; } = 5050;

    /// <summary>Port the frontend is reached on at localhost (FRONTEND_PORT).
    /// MalAuthController.Callback sends the browser back into the app at
    /// http://localhost:{FrontendPort} after sign-in, instead of rendering a page
    /// of its own. It is the port Docker Compose publishes the frontend on, and
    /// the one the native Vite dev server listens on.</summary>
    public int FrontendPort { get; set; } = 5173;
}
