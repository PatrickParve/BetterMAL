namespace AnimeTracker.Api.Services.AnimeThemes;

/// <summary>Whether a song's audio file is still on AnimeThemes' media host, as a one-byte read
/// found it. <see cref="Unreachable"/> is every answer that doesn't say: a timeout, a refusal, a
/// server error. Only <see cref="Gone"/> lets a song be replaced.</summary>
public enum AnimeThemesMediaState
{
    Present,
    Gone,
    Unreachable,
}

/// <summary>The file isn't there any more: AnimeThemes answered 404 or 410, or the link isn't
/// one the relay may fetch, so it never could be. The controller answers 410, and the board asks
/// for the song to be replaced.</summary>
public class AnimeThemesMediaGoneException() : Exception("The song's file is no longer there.");

/// <summary>AnimeThemes didn't give the file: it couldn't be reached, took too long, or answered
/// with something other than the file. The message says why, for the player's notice. The
/// controller answers 502.</summary>
public class AnimeThemesMediaUnavailableException(string reason, Exception? inner = null)
    : Exception(reason, inner);

/// <summary>Fetches a song's files for Guess the Opening/Ending: the start of its audio, because the
/// file's name and `Content-Disposition` name the show and the host sends no CORS headers, and its
/// video, so it can be read ahead of the reveal (add-anime-themes-and-guess-the-theme design.md D9,
/// D10). It copies the bytes and nothing else, and reads only AnimeThemes' two media hosts, never an
/// address a request supplies.</summary>
public interface IAnimeThemesMediaRelay
{
    /// <summary>Opens the first <see cref="AnimeThemesMediaRelay.ClipBytes"/> of the file with a
    /// `Range` read. The response headers are read here, so a missing file or a failure is known
    /// before anything is written to the caller's response. Throws
    /// <see cref="AnimeThemesMediaGoneException"/> and <see cref="AnimeThemesMediaUnavailableException"/>.
    /// The clip is the caller's to dispose.</summary>
    Task<AnimeThemesClip> OpenClipAsync(string audioUrl, CancellationToken ct = default);

    /// <summary>Opens that part of a video file, both ends included, with a `Range` read. The
    /// response headers are read here, as with a clip. It is read once, with no limit on the time
    /// its body takes, and the size of the whole file is on the clip. Throws what
    /// <see cref="OpenClipAsync"/> does. The clip is the caller's to dispose.</summary>
    Task<AnimeThemesClip> OpenVideoAsync(string videoUrl, long firstByte, long lastByte, CancellationToken ct = default);

    /// <summary>A one-byte read, to find out whether the file is still there. Never throws for
    /// anything AnimeThemes does.</summary>
    Task<AnimeThemesMediaState> ProbeAsync(string audioUrl, CancellationToken ct = default);
}

/// <summary>The body of an opened clip, held until the caller has copied it. Nothing about the
/// upstream response but its bytes is available through it: no header, no name, no address.
/// Streamed, never buffered whole, and stopped at the cap even if the host sent more.</summary>
public sealed class AnimeThemesClip : IAsyncDisposable
{
    private readonly Stream _source;
    private readonly long _maxBytes;
    private readonly CancellationTokenSource _deadline;
    private readonly IDisposable? _owner;

    /// <summary>The size of the whole file, as the host gave it, whatever part this clip is.</summary>
    public long? WholeFileBytes { get; }

    /// <param name="source">The upstream body.</param>
    /// <param name="maxBytes">The most to pass on, whatever the host sends.</param>
    /// <param name="deadline">Cancelled when the relay's time limit runs out; owned by the clip.</param>
    /// <param name="owner">What to dispose with the clip, such as the response.</param>
    /// <param name="wholeFileBytes">The size of the whole file, where the host said.</param>
    public AnimeThemesClip(
        Stream source, long maxBytes, CancellationTokenSource deadline, IDisposable? owner = null, long? wholeFileBytes = null)
    {
        WholeFileBytes = wholeFileBytes;
        _source = source;
        _maxBytes = maxBytes;
        _deadline = deadline;
        _owner = owner;
    }

    /// <summary>Passes the bytes on a chunk at a time. Throws
    /// <see cref="OperationCanceledException"/> if the caller gives up or the time limit runs
    /// out, which leaves a truncated body the browser can't decode and the board treats as a
    /// failed load.</summary>
    public async Task CopyToAsync(Stream destination, CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _deadline.Token);
        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            var remaining = _maxBytes;
            while (remaining > 0)
            {
                var wanted = (int)Math.Min(buffer.Length, remaining);
                var read = await _source.ReadAsync(buffer.AsMemory(0, wanted), linked.Token);
                if (read == 0)
                    break;

                await destination.WriteAsync(buffer.AsMemory(0, read), linked.Token);
                remaining -= read;
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Whether the relay's time limit, not the caller, is what ended the copy.</summary>
    public bool TimedOut => _deadline.IsCancellationRequested;

    public async ValueTask DisposeAsync()
    {
        await _source.DisposeAsync();
        _owner?.Dispose();
        _deadline.Dispose();
    }
}
