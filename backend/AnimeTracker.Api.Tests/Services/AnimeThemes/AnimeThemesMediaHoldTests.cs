using AnimeTracker.Api.Services.AnimeThemes;

namespace AnimeTracker.Api.Tests.Services.AnimeThemes;

// What is held in memory ahead of being wanted (add-anime-themes-and-guess-the-theme design.md
// D10), against a stand-in for the relay whose reads end when a test says so.
public class AnimeThemesMediaHoldTests
{
    private const string AudioA = "https://a.animethemes.moe/A-OP1.ogg";
    private const string VideoA = "https://v.animethemes.moe/A-OP1.webm";
    private const string AudioB = "https://a.animethemes.moe/B-OP1.ogg";
    private const string VideoB = "https://v.animethemes.moe/B-OP1.webm";
    private const string VideoC = "https://v.animethemes.moe/C-OP1.webm";

    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(5);

    /// <summary>A read in progress at the host. It answers when <see cref="Answer"/> is called.</summary>
    private sealed class Read(string link, long first, long last, CancellationToken ct)
    {
        private readonly TaskCompletionSource<AnimeThemesClip> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Link { get; } = link;
        public long First { get; } = first;
        public long Last { get; } = last;
        public CancellationToken Token { get; } = ct;
        public Task<AnimeThemesClip> Answered => _answer.Task;

        public void Answer(byte[] bytes, long? wholeFile = null) =>
            _answer.TrySetResult(new AnimeThemesClip(
                new MemoryStream(bytes), bytes.Length, new CancellationTokenSource(), owner: null, wholeFile ?? bytes.Length));

        public void Answer(Stream body) =>
            _answer.TrySetResult(new AnimeThemesClip(body, long.MaxValue, new CancellationTokenSource()));

        public void Fail(Exception failure) => _answer.TrySetException(failure);
    }

    /// <summary>Every read that has reached the host, in order; a test waits for one by its link.</summary>
    private sealed class HeldBackRelay : IAnimeThemesMediaRelay
    {
        private readonly object _gate = new();
        private readonly List<Read> _reads = [];
        private readonly SemaphoreSlim _arrived = new(0);

        public IReadOnlyList<Read> Reads
        {
            get
            {
                lock (_gate)
                    return [.. _reads];
            }
        }

        public int ReadsOf(string link) => Reads.Count(r => r.Link == link);

        public async Task<Read> ReadOfAsync(string link)
        {
            using var timeout = new CancellationTokenSource(Soon);
            while (true)
            {
                var read = Reads.LastOrDefault(r => r.Link == link);
                if (read is not null)
                    return read;
                await _arrived.WaitAsync(timeout.Token);
            }
        }

        private Task<AnimeThemesClip> Begin(string link, long first, long last, CancellationToken ct)
        {
            var read = new Read(link, first, last, ct);
            lock (_gate)
                _reads.Add(read);
            _arrived.Release();
            ct.Register(() => read.Fail(new OperationCanceledException(ct)));
            return read.Answered;
        }

        public Task<AnimeThemesClip> OpenClipAsync(string audioUrl, CancellationToken ct = default) =>
            Begin(audioUrl, 0, AnimeThemesMediaRelay.ClipBytes - 1, ct);

        public Task<AnimeThemesClip> OpenVideoAsync(string videoUrl, long firstByte, long lastByte, CancellationToken ct = default) =>
            Begin(videoUrl, firstByte, lastByte, ct);

        public Task<AnimeThemesMediaState> ProbeAsync(string audioUrl, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly byte[] Bytes = [1, 2, 3, 4, 5];

    /// <summary>A body that gives some bytes and then fails, as a dropped connection does.</summary>
    private sealed class FailingBody : Stream
    {
        private bool _gave;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (_gave)
                throw new IOException("The connection dropped.");
            _gave = true;
            return ValueTask.FromResult(1);
        }
    }

    // ---- one read of a file ----

    [Fact]
    public async Task KeepStartsTheReadsWithNobodyAsking()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);

        hold.Keep([new HeldSong(AudioA, VideoA)]);

        await relay.ReadOfAsync(AudioA);
        await relay.ReadOfAsync(VideoA);
    }

    [Fact]
    public async Task ALinkLeftOutIsNotRead()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);

        hold.Keep([new HeldSong(AudioA, null), new HeldSong(null, VideoB)]);

        await relay.ReadOfAsync(AudioA);
        await relay.ReadOfAsync(VideoB);
        Assert.Equal(2, relay.Reads.Count);
    }

    [Fact]
    public async Task AClipAskedForDuringItsReadWaitsForThatRead()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);
        hold.Keep([new HeldSong(AudioA, null)]);
        var read = await relay.ReadOfAsync(AudioA);

        var asked = hold.ClipAsync(AudioA);
        Assert.False(asked.IsCompleted);
        read.Answer(Bytes);

        Assert.Equal(Bytes, await asked.WaitAsync(Soon));
        Assert.Equal(1, relay.ReadsOf(AudioA));
    }

    [Fact]
    public async Task AFileAlreadyReadIsThereAtOnceAndIsNotReadAgain()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);
        var first = hold.ClipAsync(AudioA);
        (await relay.ReadOfAsync(AudioA)).Answer(Bytes);
        await first.WaitAsync(Soon);

        Assert.Equal(Bytes, await hold.ClipAsync(AudioA).WaitAsync(Soon));
        Assert.Equal(1, relay.ReadsOf(AudioA));
    }

    [Fact]
    public async Task KeepingAFileAlreadyHeldDoesNotReadItAgain()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);
        hold.Keep([new HeldSong(AudioA, null)]);
        (await relay.ReadOfAsync(AudioA)).Answer(Bytes);

        hold.Keep([new HeldSong(AudioA, null), new HeldSong(AudioB, null)]);
        await relay.ReadOfAsync(AudioB);

        Assert.Equal(1, relay.ReadsOf(AudioA));
    }

    [Fact]
    public async Task ThePersonWhoStopsWaitingLeavesTheReadGoing()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);
        using var leaving = new CancellationTokenSource();
        var asked = hold.ClipAsync(AudioA, leaving.Token);
        var read = await relay.ReadOfAsync(AudioA);

        await leaving.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => asked);

        Assert.False(read.Token.IsCancellationRequested);
        read.Answer(Bytes);
        Assert.Equal(Bytes, await hold.ClipAsync(AudioA).WaitAsync(Soon));
        Assert.Equal(1, relay.ReadsOf(AudioA));
    }

    // ---- the video's start ----

    [Fact]
    public async Task AVideoIsReadForItsFirstSixteenMebibytes()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);

        hold.Keep([new HeldSong(null, VideoA)]);

        var read = await relay.ReadOfAsync(VideoA);
        Assert.Equal(0, read.First);
        Assert.Equal(16 * 1024 * 1024 - 1, read.Last);
    }

    [Fact]
    public async Task AVideoStartComesWithTheSizeOfTheWholeFile()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);
        var asked = hold.VideoStartAsync(VideoA);

        (await relay.ReadOfAsync(VideoA)).Answer(Bytes, wholeFile: 41_000_000);

        var start = await asked.WaitAsync(Soon);
        Assert.Equal(Bytes, start.Bytes);
        Assert.Equal(41_000_000, start.WholeFileBytes);
    }

    [Fact]
    public async Task ThreeVideosAreReadTwoAtATimeInTheOrderGiven()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);

        hold.Keep([new HeldSong(null, VideoA), new HeldSong(null, VideoB), new HeldSong(null, VideoC)]);

        var a = await relay.ReadOfAsync(VideoA);
        await relay.ReadOfAsync(VideoB);
        await Task.Delay(50);
        Assert.Equal(0, relay.ReadsOf(VideoC));

        a.Answer(Bytes);

        await relay.ReadOfAsync(VideoC);
    }

    [Fact]
    public async Task AudioIsNeverHeldUpBehindVideos()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);
        hold.Keep([new HeldSong(null, VideoA), new HeldSong(null, VideoB), new HeldSong(AudioA, null)]);

        await relay.ReadOfAsync(AudioA);
    }

    [Fact]
    public async Task AVideoLetGoWhileWaitingItsTurnIsNeverRead()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);
        hold.Keep([new HeldSong(null, VideoA), new HeldSong(null, VideoB), new HeldSong(null, VideoC)]);
        var a = await relay.ReadOfAsync(VideoA);
        await relay.ReadOfAsync(VideoB);

        hold.Keep([new HeldSong(null, VideoA), new HeldSong(null, VideoB)]);
        a.Answer(Bytes);
        await Task.Delay(50);

        Assert.Equal(0, relay.ReadsOf(VideoC));
    }

    [Fact]
    public async Task AVideoReadThatEndsGivesItsTurnBackHoweverItEnded()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);
        hold.Keep([new HeldSong(null, VideoA), new HeldSong(null, VideoB)]);
        var a = await relay.ReadOfAsync(VideoA);
        var b = await relay.ReadOfAsync(VideoB);

        a.Fail(new AnimeThemesMediaUnavailableException("AnimeThemes is busy right now."));
        b.Answer(Bytes);

        hold.Keep([new HeldSong(null, VideoB), new HeldSong(null, VideoC)]);
        await relay.ReadOfAsync(VideoC);
    }

    // ---- letting go ----

    [Fact]
    public async Task KeepLetsGoOfEverythingElseAndStopsItsReads()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);
        hold.Keep([new HeldSong(AudioA, VideoA)]);
        var audio = await relay.ReadOfAsync(AudioA);
        var video = await relay.ReadOfAsync(VideoA);

        hold.Keep([new HeldSong(AudioB, null)]);

        Assert.True(audio.Token.IsCancellationRequested);
        Assert.True(video.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task KeepingNothingStopsEverything()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);
        hold.Keep([new HeldSong(AudioA, null)]);
        var read = await relay.ReadOfAsync(AudioA);

        hold.Keep([]);

        Assert.True(read.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task KeepingAFileLeavesItsReadRunning()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);
        hold.Keep([new HeldSong(AudioA, VideoA)]);
        var video = await relay.ReadOfAsync(VideoA);

        hold.Keep([new HeldSong(null, VideoA)]);

        Assert.False(video.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task SomeoneWaitingOnAFileLetGoIsToldItIsNotComing()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);
        var asked = hold.ClipAsync(AudioA);
        await relay.ReadOfAsync(AudioA);

        hold.Keep([]);

        var failure = await Assert.ThrowsAsync<AnimeThemesMediaUnavailableException>(() => asked.WaitAsync(Soon));
        Assert.Equal("The song is no longer wanted.", failure.Message);
    }

    [Fact]
    public async Task AFileLetGoIsReadAfreshWhenAskedForAgain()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);
        var first = hold.ClipAsync(AudioA);
        (await relay.ReadOfAsync(AudioA)).Answer(Bytes);
        await first.WaitAsync(Soon);

        hold.Keep([]);
        var again = hold.ClipAsync(AudioA);
        while (relay.ReadsOf(AudioA) < 2)
            await Task.Delay(5);
        (await relay.ReadOfAsync(AudioA)).Answer([9, 9]);

        Assert.Equal([9, 9], await again.WaitAsync(Soon));
    }

    [Fact]
    public async Task AFileNobodyHasWantedForTheLimitIsLetGo()
    {
        var clock = new Clock();
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay, clock);
        hold.Keep([new HeldSong(AudioA, null)]);
        var read = await relay.ReadOfAsync(AudioA);

        clock.Now += AnimeThemesMediaHold.IdleLimit;
        hold.Sweep();

        Assert.True(read.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task AFileWantedSinceStaysPastTheLimit()
    {
        var clock = new Clock();
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay, clock);
        hold.Keep([new HeldSong(AudioA, null)]);
        var read = await relay.ReadOfAsync(AudioA);

        clock.Now += AnimeThemesMediaHold.IdleLimit - TimeSpan.FromMinutes(1);
        hold.Keep([new HeldSong(AudioA, null)]);
        clock.Now += TimeSpan.FromMinutes(2);
        hold.Sweep();

        Assert.False(read.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task PastTheMostHeldTheLeastRecentlyWantedGoes()
    {
        var clock = new Clock();
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay, clock);
        var links = Enumerable.Range(0, AnimeThemesMediaHold.MostHeld + 1)
            .Select(i => $"https://a.animethemes.moe/{i}.ogg")
            .ToList();

        foreach (var link in links)
        {
            _ = hold.ClipAsync(link);
            clock.Now += TimeSpan.FromSeconds(1);
        }

        var oldest = await relay.ReadOfAsync(links[0]);
        var next = await relay.ReadOfAsync(links[1]);
        Assert.True(oldest.Token.IsCancellationRequested);
        Assert.False(next.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task DisposingStopsTheReadsInProgress()
    {
        var relay = new HeldBackRelay();
        var hold = new AnimeThemesMediaHold(relay);
        hold.Keep([new HeldSong(AudioA, null)]);
        var read = await relay.ReadOfAsync(AudioA);

        hold.Dispose();

        Assert.True(read.Token.IsCancellationRequested);
    }

    // ---- reads that fail ----

    [Fact]
    public async Task AFileThatHasGoneIsSaidToHaveGone()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);
        var asked = hold.ClipAsync(AudioA);

        (await relay.ReadOfAsync(AudioA)).Fail(new AnimeThemesMediaGoneException());

        await Assert.ThrowsAsync<AnimeThemesMediaGoneException>(() => asked.WaitAsync(Soon));
    }

    [Fact]
    public async Task AReadThatFailedIsForgottenSoAskingAgainReadsAfresh()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);
        var asked = hold.ClipAsync(AudioA);
        (await relay.ReadOfAsync(AudioA)).Fail(new AnimeThemesMediaUnavailableException("AnimeThemes is busy right now."));
        await Assert.ThrowsAsync<AnimeThemesMediaUnavailableException>(() => asked.WaitAsync(Soon));

        var again = hold.ClipAsync(AudioA);
        while (relay.ReadsOf(AudioA) < 2)
            await Task.Delay(5);
        (await relay.ReadOfAsync(AudioA)).Answer(Bytes);

        Assert.Equal(Bytes, await again.WaitAsync(Soon));
    }

    [Fact]
    public async Task AReadThatStopsPartWayIsAFailureNotAShortFile()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);
        var asked = hold.ClipAsync(AudioA);

        (await relay.ReadOfAsync(AudioA)).Answer(new FailingBody());

        var failure = await Assert.ThrowsAsync<AnimeThemesMediaUnavailableException>(() => asked.WaitAsync(Soon));
        Assert.Equal("AnimeThemes stopped sending the song.", failure.Message);
    }

    [Fact]
    public async Task AReadThatFailsWithNobodyWaitingHarmsNothing()
    {
        var relay = new HeldBackRelay();
        using var hold = new AnimeThemesMediaHold(relay);
        hold.Keep([new HeldSong(AudioA, null)]);

        (await relay.ReadOfAsync(AudioA)).Fail(new AnimeThemesMediaUnavailableException("AnimeThemes is busy right now."));

        var asked = hold.ClipAsync(AudioA);
        while (relay.ReadsOf(AudioA) < 2)
            await Task.Delay(5);
        (await relay.ReadOfAsync(AudioA)).Answer(Bytes);
        Assert.Equal(Bytes, await asked.WaitAsync(Soon));
    }
}
