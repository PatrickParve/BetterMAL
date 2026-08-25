namespace AnimeTracker.Api.Services.Artwork;

/// <summary>Thrown when a picture or title choice fails validation — an
/// out-of-set URL, an anime not on my list, or a title that fails
/// <see cref="SeriesTitleRule"/>. Maps to 400 at the controller (design.md D13).</summary>
public class ArtworkSelectionRejectedException(string message) : Exception(message);
