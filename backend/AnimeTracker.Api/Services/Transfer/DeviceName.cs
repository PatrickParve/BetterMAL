namespace AnimeTracker.Api.Services.Transfer;

/// <summary>Turns a request's <c>User-Agent</c> into a readable, version-free
/// description such as <c>"macOS · Safari"</c> (design.md D6). Describes the
/// browser and never identifies the device — the device identity does that.
/// </summary>
public static class DeviceName
{
    // First match wins. Order matters: iPhone/iPad must be checked before
    // Macintosh, since iPad Safari's desktop-site user agent also contains
    // "Macintosh".
    private static readonly (string Token, string Name)[] OperatingSystems =
    [
        ("iPhone", "iOS"),
        ("iPad", "iPadOS"),
        ("Android", "Android"),
        ("CrOS", "ChromeOS"),
        ("Windows", "Windows"),
        ("Macintosh", "macOS"),
        ("Mac OS X", "macOS"),
        ("Linux", "Linux"),
    ];

    // Checked in this order because each later token also appears in the
    // user agents of the earlier browsers (design.md D6): Edge and Opera
    // embed "Chrome/" and "Safari/"; Chrome embeds "Safari/"; Firefox on iOS
    // is "FxiOS/", not "Firefox/".
    private static readonly (string Token, string Name)[] Browsers =
    [
        ("Edg", "Edge"),
        ("OPR/", "Opera"),
        ("Firefox/", "Firefox"),
        ("FxiOS/", "Firefox"),
        ("Chrome/", "Chrome"),
        ("CriOS/", "Chrome"),
        ("Safari/", "Safari"),
    ];

    public static string? Describe(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return null;

        var os = Match(userAgent, OperatingSystems);
        var browser = Match(userAgent, Browsers);

        if (os is not null && browser is not null)
            return $"{os} · {browser}";
        return os ?? browser;
    }

    private static string? Match(string userAgent, (string Token, string Name)[] table)
    {
        foreach (var (token, name) in table)
            if (userAgent.Contains(token, StringComparison.Ordinal))
                return name;
        return null;
    }
}
