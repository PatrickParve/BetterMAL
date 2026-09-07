using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Artwork;

/// <summary>Reads for an anime's picture state. Whether a picture is chosen
/// is stored, not derived — the old column-inequality comparison was only
/// meaningful inside the one database that made both writes.</summary>
public static class AnimePicture
{
    public static bool IsOverridden(AnimeMetadata anime) => anime.SelectedPictureUrl is not null;

    /// <summary>The option set a picker offers: the stored picture set plus
    /// MAL's main picture and the current selection, deduplicated by
    /// <see cref="PictureIdentity"/> (not raw URL equality — MAL sometimes
    /// serves the same photo under two extensions, e.g. <c>main_picture</c>
    /// as <c>.webp</c> while <c>pictures</c> lists it as <c>.jpg</c>, which
    /// would otherwise show as two tiles for one picture), MAL's order
    /// preserved. <c>MalPictureUrl</c>/<c>SelectedPictureUrl</c> are the
    /// identity-authoritative literals: when one of them shares an identity
    /// with an entry already added from <c>PictureUrls</c>, that slot is
    /// swapped to the authoritative literal rather than left as whichever
    /// extension <c>pictures</c> happened to list, so the current selection
    /// can still be matched by exact string elsewhere (the picker's "current"
    /// badge, <see cref="IsOverridden"/>). <c>SelectedPictureUrl</c> stands in
    /// for <c>PictureUrl</c> here — set-equivalent, since the displayed value
    /// is always one of the two already upserted, and it names what it
    /// means. The current selection is always
    /// included even when MAL has since dropped it, so a choice MAL no
    /// longer lists stays visible and replaceable rather than merely broken
    /// (design.md D9 / spec "A stored choice is never re-validated away").</summary>
    public static IReadOnlyList<string> Options(AnimeMetadata anime)
    {
        var options = new List<string>();
        var indexByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        void Upsert(string? url, bool preferOverExisting)
        {
            if (url is null) return;
            var key = PictureIdentity.KeyFor(url);
            if (indexByKey.TryGetValue(key, out var index))
            {
                if (preferOverExisting) options[index] = url;
                return;
            }
            indexByKey[key] = options.Count;
            options.Add(url);
        }

        if (anime.PictureUrls is not null)
            foreach (var url in anime.PictureUrls)
                Upsert(url, preferOverExisting: false);
        Upsert(anime.MalPictureUrl, preferOverExisting: true);
        Upsert(anime.SelectedPictureUrl, preferOverExisting: true);

        return options;
    }
}
