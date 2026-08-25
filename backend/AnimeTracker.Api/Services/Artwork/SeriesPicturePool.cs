using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Artwork;

/// <summary>A series' picture option set (design.md D6): the union of every
/// main-line member's artwork, deduplicated by <see cref="PictureIdentity"/>,
/// in main-line order and then MAL's own order within each member. Extras
/// contribute nothing — the series' picture describes the franchise's main
/// line.</summary>
public static class SeriesPicturePool
{
    /// <summary>Builds the pool from <paramref name="mainLineMembers"/> (given
    /// in main-line watch order): each member's displayed picture, its MAL
    /// picture, then its stored picture set. A member not in my list
    /// contributes only its main picture — the my-list-only fetch rule
    /// showing through (`artwork-selection`) — which is why the pool is never
    /// empty even for a franchise mostly outside my list. Callers append the
    /// series' own current selection when it isn't already covered, so a
    /// choice the pool no longer includes (its source member left, or MAL
    /// dropped it) stays visible and replaceable rather than merely broken
    /// (design.md D9 / spec "A stored choice is never re-validated away").</summary>
    public static List<string> Build(IEnumerable<SeriesMember> mainLineMembers)
    {
        var options = new List<string>();
        var indexByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Deduplicated by PictureIdentity, not raw URL equality: MAL
        // sometimes serves one photo under two extensions (main_picture as
        // .webp, the same photo listed in `pictures` as .jpg, or vice
        // versa), which byte-for-byte comparison would otherwise show as two
        // tiles for the same picture. A member's own displayed/MAL picture
        // is added before its picture set, so it wins the slot when both
        // describe the same photo.
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

        foreach (var member in mainLineMembers)
        {
            var anime = member.Anime;
            Upsert(anime.PictureUrl, preferOverExisting: true);
            Upsert(anime.MalPictureUrl, preferOverExisting: true);
            if (anime.PictureUrls is not null)
                foreach (var url in anime.PictureUrls)
                    Upsert(url, preferOverExisting: false);
        }

        return options;
    }
}
