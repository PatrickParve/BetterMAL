namespace AnimeTracker.Api.Services.Artwork;

/// <summary>MAL sometimes serves the exact same photo under two different
/// URLs that differ only by file extension — <c>main_picture</c> returning
/// the <c>.webp</c> rendition of a picture while the <c>pictures</c> array
/// lists the identical photo as <c>.jpg</c> (or vice versa). Byte-for-byte
/// URL equality treats those as two different pictures, which is what makes
/// a picker occasionally show "the same" picture twice. This key strips the
/// trailing file extension so both renditions of one photo collapse to the
/// same identity, while two genuinely different photos (different numeric
/// filename) still key apart.</summary>
public static class PictureIdentity
{
    private static readonly string[] KnownExtensions = [".webp", ".jpg", ".jpeg", ".png", ".gif"];

    public static string KeyFor(string url)
    {
        foreach (var extension in KnownExtensions)
            if (url.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                return url[..^extension.Length];

        return url;
    }
}
