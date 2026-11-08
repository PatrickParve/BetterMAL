namespace AnimeTracker.Api.Models;

/// <summary>What the app has learned about one studio's page on AniList, keyed by
/// the name MyAnimeList credits (studio-links, design.md D7). A row with an
/// <see cref="AniListStudioId"/> is matched, and that id is never overwritten or
/// cleared. A row without one means a followed link asked AniList about anime
/// crediting the studio and the studios it listed held none of that name: a click on
/// its link goes to AniList's search without asking again, and a later lookup that
/// does match the name fills the id in. A lookup never writes such a row, since what
/// one anime's AniList record leaves out says nothing about the studio. No row means
/// the studio has not been matched, and has not been given up on either.
/// <para>Derived data, learned from AniList: neither the list backup nor the
/// device transfer carries it, and a device with none learns it again. Deliberately
/// no foreign key to <see cref="AnimeMetadata"/>, since a studio outlives any one
/// anime and other anime can credit it.</para></summary>
public class StudioLink
{
    public required string Name { get; set; }

    /// <summary>AniList's own id for the studio, or null when a followed link found
    /// none among the studios AniList lists for the anime asked about.</summary>
    public int? AniListStudioId { get; set; }
}
