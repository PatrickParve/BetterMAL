namespace AnimeTracker.Api.Services.AnimeThemes;

/// <summary>What is wanted of one song: either link may be null for a file that isn't.</summary>
public record HeldSong(string? AudioUrl, string? VideoUrl);

/// <summary>The start of a video, with the size of the whole file.</summary>
public record AnimeThemesVideoStart(byte[] Bytes, long WholeFileBytes);

/// <summary>Reads songs' files ahead of when they're wanted, and holds what it read in memory for as
/// long as the game has a use for it (add-anime-themes-and-guess-the-theme design.md D10). AnimeThemes'
/// media host takes seconds to start answering any read, however small, and then sends the bytes at
/// once, so the wait can only be moved, not shortened: a song read during the round before it has
/// nothing left to wait for when it's played.</summary>
public interface IAnimeThemesMediaHold
{
    /// <summary>Holds these songs and nothing else: what isn't read yet starts now, in the order
    /// given, and anything else is let go, with a read in progress stopped. Never waits.</summary>
    void Keep(IEnumerable<HeldSong> songs);

    /// <summary>The first <see cref="AnimeThemesMediaRelay.ClipBytes"/> of an audio file, waiting for
    /// the read already under way or starting one. Throws
    /// <see cref="AnimeThemesMediaGoneException"/> and <see cref="AnimeThemesMediaUnavailableException"/>.
    /// A caller giving up leaves the read going, for whoever asks next.</summary>
    Task<byte[]> ClipAsync(string audioUrl, CancellationToken ct = default);

    /// <summary>The first <see cref="AnimeThemesMediaHold.VideoStartBytes"/> of a video file, on the
    /// same terms as <see cref="ClipAsync"/>.</summary>
    Task<AnimeThemesVideoStart> VideoStartAsync(string videoUrl, CancellationToken ct = default);
}

/// <summary>The one store of read-ahead media, a singleton. It holds files by their link, so a song
/// is the same song in whichever round or game it turns up, and a file is only ever read once however
/// many ask for it: the read ahead, the browser and a second request all share one read.
///
/// <para>Memory only: nothing is written to disk, and it survives no restart. <see cref="Keep"/>
/// keeps it to the songs a game has in hand, about 34 MiB at most; <see cref="MostHeld"/> and
/// <see cref="IdleLimit"/> bound it whatever else happens, so a game abandoned in a tab holds nothing
/// for long. A file let go while a response is still sending it stays alive for that response.</para>
///
/// <para>Audio reads start at once, since someone is usually about to want them. Video reads take
/// turns, <see cref="MostVideoReads"/> at a time: the host copes badly with many reads together (of
/// ten sent at once, six took 10s and four came back without the file's size).</para></summary>
public sealed class AnimeThemesMediaHold : IAnimeThemesMediaHold, IDisposable
{
    /// <summary>16 MiB of video, under half of the 37 MiB the files sampled averaged. Their bitrates
    /// ran from about 210 to 710 KB/s, so that is 23 to 77 seconds of the song, which is the time the
    /// rest of the file has to arrive in before playback catches up, and the host's slowest answers
    /// seen were about 25 seconds. A song never reached costs this much and no more.</summary>
    public const int VideoStartBytes = 16 * 1024 * 1024;

    public const int MostVideoReads = 2;

    /// <summary>A game has four files in hand at most, a song's audio and video twice, so this only
    /// bounds what a run of other requests can do: past it the least recently wanted file goes.</summary>
    public const int MostHeld = 8;

    /// <summary>How long a file nothing has asked for stays. Long enough that no round runs it out.</summary>
    public static readonly TimeSpan IdleLimit = TimeSpan.FromMinutes(30);

    private static readonly TimeSpan SweepEvery = TimeSpan.FromMinutes(1);

    private readonly IAnimeThemesMediaRelay _relay;
    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private readonly Dictionary<string, Held> _held = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _videoReads = new(MostVideoReads);
    private readonly ITimer _sweep;

    public AnimeThemesMediaHold(IAnimeThemesMediaRelay relay, TimeProvider? clock = null)
    {
        _relay = relay;
        _clock = clock ?? TimeProvider.System;
        _sweep = _clock.CreateTimer(_ => Sweep(), null, SweepEvery, SweepEvery);
    }

    public void Keep(IEnumerable<HeldSong> songs)
    {
        var wanted = songs
            .SelectMany(song => new[] { (Link: song.AudioUrl, Video: false), (Link: song.VideoUrl, Video: true) })
            .Where(file => file.Link is not null)
            .ToList();

        lock (_gate)
        {
            var links = wanted.Select(file => file.Link).ToHashSet(StringComparer.Ordinal);
            foreach (var held in _held.Values.Where(held => !links.Contains(held.Link)).ToList())
                Drop(held);

            foreach (var (link, video) in wanted)
                Ensure(link!, video);
        }
    }

    public async Task<byte[]> ClipAsync(string audioUrl, CancellationToken ct = default)
    {
        var file = await Want(audioUrl, video: false).WaitAsync(ct);
        return file.Bytes;
    }

    public async Task<AnimeThemesVideoStart> VideoStartAsync(string videoUrl, CancellationToken ct = default)
    {
        var file = await Want(videoUrl, video: true).WaitAsync(ct);
        return new AnimeThemesVideoStart(file.Bytes, file.WholeFileBytes);
    }

    private Task<HeldFile> Want(string link, bool video)
    {
        lock (_gate)
            return Ensure(link, video).Read;
    }

    /// <summary>Lets go of anything no one has asked for in <see cref="IdleLimit"/>. The timer's work;
    /// a test calls it to sweep at a moment of its choosing.</summary>
    public void Sweep()
    {
        var oldest = _clock.GetUtcNow() - IdleLimit;
        lock (_gate)
        {
            foreach (var held in _held.Values.Where(held => held.LastWanted <= oldest).ToList())
                Drop(held);
        }
    }

    public void Dispose()
    {
        _sweep.Dispose();
        lock (_gate)
        {
            foreach (var held in _held.Values.ToList())
                Drop(held);
        }
    }

    // Under the lock. A read is started on the pool, so nothing it does runs inside the lock.
    private Held Ensure(string link, bool video)
    {
        var now = _clock.GetUtcNow();
        if (_held.TryGetValue(link, out var held))
        {
            held.LastWanted = now;
            return held;
        }

        held = new Held(link, now);
        var added = held;
        held.Read = Task.Run(() => ReadAsync(added, video));
        _held[link] = held;

        while (_held.Count > MostHeld)
            Drop(_held.Values.MinBy(entry => entry.LastWanted)!);

        return held;
    }

    // Under the lock. Whoever is waiting on the read is told the file is no longer on its way.
    private void Drop(Held held)
    {
        _held.Remove(held.Link);
        held.Stop.Cancel();
    }

    private async Task<HeldFile> ReadAsync(Held held, bool video)
    {
        var stop = held.Stop.Token;
        try
        {
            return video ? await ReadVideoStartAsync(held.Link, stop) : await ReadClipAsync(held.Link, stop);
        }
        catch (Exception ex)
        {
            // Forgotten, so asking again reads the file afresh rather than failing the same way.
            lock (_gate)
            {
                if (_held.TryGetValue(held.Link, out var current) && ReferenceEquals(current, held))
                    _held.Remove(held.Link);
            }

            throw ex switch
            {
                AnimeThemesMediaGoneException or AnimeThemesMediaUnavailableException => ex,
                OperationCanceledException when stop.IsCancellationRequested =>
                    new AnimeThemesMediaUnavailableException("The song is no longer wanted."),
                _ => new AnimeThemesMediaUnavailableException("AnimeThemes stopped sending the song.", ex),
            };
        }
    }

    private async Task<HeldFile> ReadClipAsync(string link, CancellationToken stop)
    {
        await using var clip = await _relay.OpenClipAsync(link, stop);
        return await TakeAsync(clip, (int)AnimeThemesMediaRelay.ClipBytes, stop);
    }

    private async Task<HeldFile> ReadVideoStartAsync(string link, CancellationToken stop)
    {
        await _videoReads.WaitAsync(stop);
        try
        {
            await using var clip = await _relay.OpenVideoAsync(link, 0, VideoStartBytes - 1, stop);
            return await TakeAsync(clip, VideoStartBytes, stop);
        }
        finally
        {
            _videoReads.Release();
        }
    }

    private static async Task<HeldFile> TakeAsync(AnimeThemesClip clip, int size, CancellationToken stop)
    {
        using var bytes = new MemoryStream(size);
        await clip.CopyToAsync(bytes, stop);

        var read = bytes.Length == size ? bytes.GetBuffer() : bytes.ToArray();
        return new HeldFile(read, clip.WholeFileBytes ?? read.Length);
    }

    private sealed record HeldFile(byte[] Bytes, long WholeFileBytes);

    private sealed class Held(string link, DateTimeOffset wanted)
    {
        public string Link { get; } = link;
        public CancellationTokenSource Stop { get; } = new();
        public Task<HeldFile> Read { get; set; } = null!;
        public DateTimeOffset LastWanted { get; set; } = wanted;
    }
}
