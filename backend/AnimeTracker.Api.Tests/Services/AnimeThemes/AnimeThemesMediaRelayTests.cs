using System.Net;
using System.Net.Http.Headers;
using AnimeTracker.Api.Services.AnimeThemes;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.AnimeThemes;

// add-anime-themes-and-guess-the-theme task 7.1: the relay copies the first 768 KiB of a
// song's file from a.animethemes.moe and nothing else, against a fake media host.
public class AnimeThemesMediaRelayTests
{
    private const string Link = "https://a.animethemes.moe/DeathNote-OP1.ogg";

    /// <summary>What a request looked like to the media host.</summary>
    private sealed record MediaRequest(Uri Uri, RangeHeaderValue? Range, string UserAgent, HttpCompletionOption Completion);

    /// <summary>A body that counts what the relay took from it, and can stall after some bytes.</summary>
    private sealed class CountingStream(long length, long? stallAfter = null) : Stream
    {
        private long _position;
        public long BytesRead => _position;
        public bool Disposed { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (stallAfter is { } stall && _position >= stall)
            {
                await Task.Delay(Timeout.Infinite, ct);
                return 0;
            }

            var left = length - _position;
            var count = (int)Math.Min(Math.Min(buffer.Length, left), 8 * 1024);
            for (var i = 0; i < count; i++)
                buffer.Span[i] = (byte)((_position + i) % 251);
            _position += count;
            return count;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class MediaHandler : HttpMessageHandler
    {
        public List<MediaRequest> Requests { get; } = [];
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(new MediaRequest(request.RequestUri!, request.Headers.Range, request.Headers.UserAgent.ToString(), default));
            return Respond(request, ct);
        }

        public static HttpResponseMessage Body(Stream body, HttpStatusCode status = HttpStatusCode.PartialContent)
        {
            var response = new HttpResponseMessage(status) { Content = new StreamContent(body) };
            // What the real host sends, which must never reach the browser.
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("audio/ogg");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline") { FileName = "DeathNote-OP1.ogg" };
            response.Headers.TryAddWithoutValidation("Set-Cookie", "animethemes_session=secret; path=/");
            return response;
        }
    }

    private sealed class Factory(MediaHandler handler) : IHttpClientFactory
    {
        public string? AskedFor { get; private set; }

        public HttpClient CreateClient(string name)
        {
            AskedFor = name;
            var client = new HttpClient(handler, disposeHandler: false);
            AnimeThemesMediaRelay.Configure(client);
            return client;
        }
    }

    /// <summary>The relay with its waits set by the test, so none of it sits through seconds.</summary>
    private sealed class ShortRelay(IHttpClientFactory factory, TimeSpan answer, TimeSpan body, TimeSpan second)
        : AnimeThemesMediaRelay(factory, NullLogger<AnimeThemesMediaRelay>.Instance)
    {
        protected override TimeSpan AnswerLimit => answer;
        protected override TimeSpan BodyLimit => body;
        protected override TimeSpan SecondRead => second;
    }

    /// <summary>`limit` is both the wait for an answer and the time for the body; the second read
    /// is left at its real 8 seconds unless `secondAfter` says otherwise, so a test that counts
    /// requests isn't disturbed by one.</summary>
    private static (AnimeThemesMediaRelay Relay, MediaHandler Handler, Factory Factory) Make(
        TimeSpan? limit = null, TimeSpan? secondAfter = null)
    {
        var handler = new MediaHandler();
        var factory = new Factory(handler);
        AnimeThemesMediaRelay relay = limit is not null || secondAfter is not null
            ? new ShortRelay(factory, limit ?? AnimeThemesMediaRelay.AnswerTime, limit ?? AnimeThemesMediaRelay.BodyTime,
                secondAfter ?? AnimeThemesMediaRelay.SecondReadAfter)
            : new AnimeThemesMediaRelay(factory, NullLogger<AnimeThemesMediaRelay>.Instance);
        return (relay, handler, factory);
    }

    private static async Task<byte[]> CopyAsync(AnimeThemesClip clip)
    {
        var destination = new MemoryStream();
        await clip.CopyToAsync(destination);
        return destination.ToArray();
    }

    // ---- what is asked of the host ----

    [Fact]
    public async Task OpenClipReadsTheFirst768KiBWithARangeRequestOnTheLink()
    {
        var (relay, handler, factory) = Make();
        handler.Respond = (_, _) => Task.FromResult(MediaHandler.Body(new CountingStream(1000)));

        await using var clip = await relay.OpenClipAsync(Link);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(Link, request.Uri.AbsoluteUri);
        Assert.Equal(0, request.Range!.Ranges.Single().From);
        Assert.Equal(768 * 1024 - 1, request.Range.Ranges.Single().To);
        Assert.Equal(AnimeThemesMediaRelay.HttpClientName, factory.AskedFor);
    }

    [Fact]
    public async Task EveryRequestNamesTheApp()
    {
        var (relay, handler, _) = Make();
        handler.Respond = (_, _) => Task.FromResult(MediaHandler.Body(new CountingStream(10)));

        await using var clip = await relay.OpenClipAsync(Link);
        await relay.ProbeAsync(Link);

        Assert.All(handler.Requests, r => Assert.Contains("BetterMAL", r.UserAgent));
    }

    // ---- a video ----

    private const string VideoLink = "https://v.animethemes.moe/DeathNote-OP1.webm";

    private static HttpResponseMessage VideoBody(Stream body, long first, long last, long whole)
    {
        var response = MediaHandler.Body(body);
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(first, last, whole);
        return response;
    }

    [Fact]
    public async Task AVideoIsReadForTheBytesAskedFor()
    {
        var (relay, handler, _) = Make();
        handler.Respond = (_, _) => Task.FromResult(VideoBody(new CountingStream(1000), 5000, 5999, 40_000_000));

        await using var clip = await relay.OpenVideoAsync(VideoLink, 5000, 5999);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(VideoLink, request.Uri.AbsoluteUri);
        Assert.Equal(5000, request.Range!.Ranges.Single().From);
        Assert.Equal(5999, request.Range.Ranges.Single().To);
    }

    [Fact]
    public async Task AVideoReadSaysHowBigTheWholeFileIs()
    {
        var (relay, handler, _) = Make();
        handler.Respond = (_, _) => Task.FromResult(VideoBody(new CountingStream(1000), 0, 999, 40_000_000));

        await using var clip = await relay.OpenVideoAsync(VideoLink, 0, 999);

        Assert.Equal(40_000_000, clip.WholeFileBytes);
    }

    [Fact]
    public async Task AVideoReadStopsAtTheBytesAskedForWhateverTheHostSends()
    {
        var (relay, handler, _) = Make();
        handler.Respond = (_, _) => Task.FromResult(VideoBody(new CountingStream(50_000), 0, 49_999, 50_000));

        await using var clip = await relay.OpenVideoAsync(VideoLink, 0, 999);

        Assert.Equal(1000, (await CopyAsync(clip)).Length);
    }

    [Theory]
    [InlineData("https://a.animethemes.moe/DeathNote-OP1.ogg")]
    [InlineData("https://evil.example/DeathNote-OP1.webm")]
    [InlineData("http://v.animethemes.moe/DeathNote-OP1.webm")]
    public async Task AVideoReadRefusesAnyLinkButTheVideoHostAndAsksNothing(string link)
    {
        var (relay, handler, _) = Make();

        await Assert.ThrowsAsync<AnimeThemesMediaGoneException>(() => relay.OpenVideoAsync(link, 0, 999));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AVideoThatHasGoneIsGone()
    {
        var (relay, handler, _) = Make();
        handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

        await Assert.ThrowsAsync<AnimeThemesMediaGoneException>(() => relay.OpenVideoAsync(VideoLink, 0, 999));
    }

    [Fact]
    public async Task AReadFromTheMiddleOfAFileThatCameBackFromItsStartIsRefused()
    {
        var (relay, handler, _) = Make();
        handler.Respond = (_, _) => Task.FromResult(MediaHandler.Body(new CountingStream(1000), HttpStatusCode.OK));

        await Assert.ThrowsAsync<AnimeThemesMediaUnavailableException>(() => relay.OpenVideoAsync(VideoLink, 5000, 5999));
    }

    [Fact]
    public async Task AReadFromTheStartThatIgnoredRangeIsStillTheStartOfTheFile()
    {
        var (relay, handler, _) = Make();
        handler.Respond = (_, _) => Task.FromResult(MediaHandler.Body(new CountingStream(5000), HttpStatusCode.OK));

        await using var clip = await relay.OpenVideoAsync(VideoLink, 0, 999);

        Assert.Equal(1000, (await CopyAsync(clip)).Length);
    }

    [Fact]
    public async Task AVideoIsNeverReadTwiceHoweverSlowTheHost()
    {
        var (relay, handler, _) = Make(limit: TimeSpan.FromSeconds(5), secondAfter: TimeSpan.FromMilliseconds(10));
        var answer = new TaskCompletionSource<HttpResponseMessage>();
        handler.Respond = (_, _) => answer.Task;

        var opening = relay.OpenVideoAsync(VideoLink, 0, 999);
        await Task.Delay(100);
        answer.SetResult(VideoBody(new CountingStream(1000), 0, 999, 1000));
        await using var clip = await opening;

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task AVideoBeingWatchedHasNoTimeLimitOnItsBody()
    {
        var (relay, handler, _) = Make(limit: TimeSpan.FromMilliseconds(50));
        handler.Respond = (_, _) => Task.FromResult(VideoBody(new CountingStream(1000), 0, 999, 1000));

        await using var clip = await relay.OpenVideoAsync(VideoLink, 0, 999);
        await Task.Delay(150);

        Assert.Equal(1000, (await CopyAsync(clip)).Length);
        Assert.False(clip.TimedOut);
    }

    [Fact]
    public async Task AHostThatNeverAnswersAVideoReadIsAFailureWithAReason()
    {
        var (relay, handler, _) = Make(limit: TimeSpan.FromMilliseconds(50));
        handler.Respond = async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        };

        var failure = await Assert.ThrowsAsync<AnimeThemesMediaUnavailableException>(
            () => relay.OpenVideoAsync(VideoLink, 0, 999).WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.Equal("AnimeThemes took too long to answer.", failure.Message);
    }

    // ---- what is passed on ----

    [Fact]
    public async Task TheBytesAreCopiedAsTheyCame()
    {
        var (relay, handler, _) = Make();
        var expected = new byte[40_000];
        for (var i = 0; i < expected.Length; i++)
            expected[i] = (byte)(i % 251);
        handler.Respond = (_, _) => Task.FromResult(MediaHandler.Body(new CountingStream(expected.Length)));

        await using var clip = await relay.OpenClipAsync(Link);

        Assert.Equal(expected, await CopyAsync(clip));
    }

    [Fact]
    public async Task ARangeAnswerOfFewerBytesThanTheCapIsPassedWhole()
    {
        var (relay, handler, _) = Make();
        handler.Respond = (_, _) => Task.FromResult(MediaHandler.Body(new CountingStream(300_000)));

        await using var clip = await relay.OpenClipAsync(Link);

        Assert.Equal(300_000, (await CopyAsync(clip)).Length);
    }

    [Fact]
    public async Task ItStopsAtTheCapEvenIfTheHostIgnoresRange()
    {
        var (relay, handler, _) = Make();
        var upstream = new CountingStream(5 * 1024 * 1024);
        // A 200 for the whole file, as a host that ignores Range would send.
        handler.Respond = (_, _) => Task.FromResult(MediaHandler.Body(upstream, HttpStatusCode.OK));

        await using var clip = await relay.OpenClipAsync(Link);
        var copied = await CopyAsync(clip);

        Assert.Equal(AnimeThemesMediaRelay.ClipBytes, copied.Length);
        // Nor does it read on past what it passes on.
        Assert.Equal(AnimeThemesMediaRelay.ClipBytes, upstream.BytesRead);
    }

    [Fact]
    public async Task TheBodyIsStreamedNotReadBeforeTheCallerAsksForIt()
    {
        var (relay, handler, _) = Make();
        var upstream = new CountingStream(500_000);
        handler.Respond = (_, _) => Task.FromResult(MediaHandler.Body(upstream));

        await using var clip = await relay.OpenClipAsync(Link);

        Assert.Equal(0, upstream.BytesRead);
    }

    [Fact]
    public async Task TheBodyIsPassedOnInSmallChunks()
    {
        var (relay, handler, _) = Make();
        handler.Respond = (_, _) => Task.FromResult(MediaHandler.Body(new CountingStream(200_000)));
        await using var clip = await relay.OpenClipAsync(Link);
        var destination = new RecordingStream();

        await clip.CopyToAsync(destination);

        Assert.Equal(200_000, destination.Total);
        Assert.True(destination.Writes.Count > 10, "one big write would mean the body was held whole");
        Assert.All(destination.Writes, size => Assert.InRange(size, 1, 16 * 1024));
    }

    private sealed class RecordingStream : Stream
    {
        public List<int> Writes { get; } = [];
        public long Total => Writes.Sum(w => (long)w);
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => Writes.Add(count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        {
            Writes.Add(buffer.Length);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public void OnlyTheBytesComeThroughTheClip()
    {
        // No upstream header can reach the response, because the clip has nowhere to carry
        // one: the host's file name and Content-Disposition name the show. The size of the file is
        // the one thing that comes through, as a number, since a range of a video has to state it,
        // and the video is only served once the song has been revealed.
        var members = typeof(AnimeThemesClip).GetMembers(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(m => m.MemberType is System.Reflection.MemberTypes.Method or System.Reflection.MemberTypes.Property or System.Reflection.MemberTypes.Field)
            .Where(m => !m.Name.StartsWith("get_"))
            .Select(m => m.Name)
            .Order()
            .ToList();

        Assert.Equal(["CopyToAsync", "DisposeAsync", "TimedOut", "WholeFileBytes"], members);
    }

    [Fact]
    public async Task DisposingTheClipReleasesTheUpstreamBody()
    {
        var (relay, handler, _) = Make();
        var upstream = new CountingStream(100);
        handler.Respond = (_, _) => Task.FromResult(MediaHandler.Body(upstream));
        var clip = await relay.OpenClipAsync(Link);

        await clip.DisposeAsync();

        Assert.True(upstream.Disposed);
    }

    // ---- the answers of the host, told apart ----

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task AFileThatIsGoneIsToldApartFromAFailure(HttpStatusCode status)
    {
        var (relay, handler, _) = Make();
        handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(status));

        await Assert.ThrowsAsync<AnimeThemesMediaGoneException>(() => relay.OpenClipAsync(Link));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.MovedPermanently)]
    [InlineData(HttpStatusCode.RequestedRangeNotSatisfiable)]
    public async Task AnyOtherAnswerIsAFailureNotAMissingFile(HttpStatusCode status)
    {
        var (relay, handler, _) = Make();
        handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(status));

        var ex = await Assert.ThrowsAsync<AnimeThemesMediaUnavailableException>(() => relay.OpenClipAsync(Link));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public async Task ARateLimitSaysAnimeThemesIsBusy()
    {
        var (relay, handler, _) = Make();
        handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var ex = await Assert.ThrowsAsync<AnimeThemesMediaUnavailableException>(() => relay.OpenClipAsync(Link));

        Assert.Contains("busy", ex.Message);
    }

    [Fact]
    public async Task ARefusedConnectionIsAFailureWithAReason()
    {
        var (relay, handler, _) = Make();
        handler.Respond = (_, _) => throw new HttpRequestException("connection refused");

        var ex = await Assert.ThrowsAsync<AnimeThemesMediaUnavailableException>(() => relay.OpenClipAsync(Link));

        Assert.Contains("couldn't be reached", ex.Message);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task AHostThatNeverAnswersTimesOutAsAFailure()
    {
        var (relay, handler, _) = Make(TimeSpan.FromMilliseconds(80));
        handler.Respond = async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage();
        };

        var ex = await Assert.ThrowsAsync<AnimeThemesMediaUnavailableException>(() => relay.OpenClipAsync(Link).WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.Contains("too long", ex.Message);
    }

    [Fact]
    public async Task ACallerWhoGivesUpIsNotMistakenForATimeout()
    {
        var (relay, handler, _) = Make();
        handler.Respond = async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage();
        };
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => relay.OpenClipAsync(Link, cts.Token));
    }

    [Fact]
    public async Task ABodyThatStallsMidwayIsStoppedByTheTimeLimit()
    {
        var (relay, handler, _) = Make(TimeSpan.FromMilliseconds(150));
        handler.Respond = (_, _) => Task.FromResult(MediaHandler.Body(new CountingStream(500_000, stallAfter: 20_000)));
        await using var clip = await relay.OpenClipAsync(Link);
        var destination = new MemoryStream();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => clip.CopyToAsync(destination).WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.True(clip.TimedOut);
        Assert.True(destination.Length >= 20_000);
    }

    // ---- only the audio host, only a link that passes ----

    public static TheoryData<string> LinksTheRelayMustNotFetch =>
    [
        "http://a.animethemes.moe/DeathNote-OP1.ogg",
        "https://v.animethemes.moe/DeathNote-OP1.webm",
        "https://animethemes.moe/DeathNote-OP1.ogg",
        "https://a.animethemes.moe.example.com/DeathNote-OP1.ogg",
        "https://example.com/a.animethemes.moe/DeathNote-OP1.ogg",
        "https://user:pass@a.animethemes.moe/DeathNote-OP1.ogg",
        "https://a.animethemes.moe:8443/DeathNote-OP1.ogg",
        "//a.animethemes.moe/DeathNote-OP1.ogg",
        "/DeathNote-OP1.ogg",
        "file:///etc/passwd",
        "",
        "   ",
    ];

    [Theory]
    [MemberData(nameof(LinksTheRelayMustNotFetch))]
    public async Task NoRequestIsMadeForALinkOnAnotherHost(string link)
    {
        var (relay, handler, _) = Make();

        await Assert.ThrowsAsync<AnimeThemesMediaGoneException>(() => relay.OpenClipAsync(link));
        var probed = await relay.ProbeAsync(link);

        Assert.Empty(handler.Requests);
        Assert.Equal(AnimeThemesMediaState.Gone, probed);
    }

    [Fact]
    public async Task TheAudioHostIsMatchedWhateverTheCase()
    {
        var (relay, handler, _) = Make();
        handler.Respond = (_, _) => Task.FromResult(MediaHandler.Body(new CountingStream(10)));

        await using var clip = await relay.OpenClipAsync("https://A.AnimeThemes.Moe/DeathNote-OP1.ogg");

        Assert.Single(handler.Requests);
    }

    // ---- the one-byte probe ----

    [Fact]
    public async Task AProbeAsksForOneByte()
    {
        var (relay, handler, _) = Make();
        handler.Respond = (_, _) => Task.FromResult(MediaHandler.Body(new CountingStream(1)));

        await relay.ProbeAsync(Link);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(0, request.Range!.Ranges.Single().From);
        Assert.Equal(0, request.Range.Ranges.Single().To);
    }

    [Theory]
    [InlineData(HttpStatusCode.PartialContent, AnimeThemesMediaState.Present)]
    [InlineData(HttpStatusCode.OK, AnimeThemesMediaState.Present)]
    [InlineData(HttpStatusCode.NotFound, AnimeThemesMediaState.Gone)]
    [InlineData(HttpStatusCode.Gone, AnimeThemesMediaState.Gone)]
    [InlineData(HttpStatusCode.InternalServerError, AnimeThemesMediaState.Unreachable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, AnimeThemesMediaState.Unreachable)]
    [InlineData(HttpStatusCode.TooManyRequests, AnimeThemesMediaState.Unreachable)]
    [InlineData(HttpStatusCode.Forbidden, AnimeThemesMediaState.Unreachable)]
    [InlineData(HttpStatusCode.Found, AnimeThemesMediaState.Unreachable)]
    public async Task AProbeTellsPresentFromGoneFromAnythingElse(HttpStatusCode status, AnimeThemesMediaState expected)
    {
        var (relay, handler, _) = Make();
        handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(status));

        Assert.Equal(expected, await relay.ProbeAsync(Link));
    }

    [Fact]
    public async Task AProbeThatCannotConnectCannotSay()
    {
        var (relay, handler, _) = Make();
        handler.Respond = (_, _) => throw new HttpRequestException("no route");

        Assert.Equal(AnimeThemesMediaState.Unreachable, await relay.ProbeAsync(Link));
    }

    [Fact]
    public async Task AProbeThatTimesOutCannotSay()
    {
        var (relay, handler, _) = Make(TimeSpan.FromMilliseconds(80));
        handler.Respond = async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage();
        };

        Assert.Equal(AnimeThemesMediaState.Unreachable, await relay.ProbeAsync(Link).WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task AProbeDoesNotReadTheBody()
    {
        var (relay, handler, _) = Make();
        var upstream = new CountingStream(1_000_000);
        handler.Respond = (_, _) => Task.FromResult(MediaHandler.Body(upstream));

        await relay.ProbeAsync(Link);

        Assert.Equal(0, upstream.BytesRead);
    }

    // ---- how the client is built ----

    [Fact]
    public void TheNamedClientFollowsNoRedirectAndKeepsNoCookie()
    {
        // The media host sets session cookies and could redirect off the allow-listed host.
        using var handler = Assert.IsType<SocketsHttpHandler>(AnimeThemesMediaRelay.CreateHandler());

        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseCookies);
    }

    [Fact]
    public void TheClientNamesTheAppAndLeavesTheTimeToTheRelay()
    {
        using var client = new HttpClient();

        AnimeThemesMediaRelay.Configure(client);

        Assert.Contains("BetterMAL", client.DefaultRequestHeaders.UserAgent.ToString());
        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
    }

    [Fact]
    public void TheHostGetsSeventyFiveSecondsToAnswerAndThirtyToSendTheBytes()
    {
        // The real host was seen taking over 30 seconds to start answering, so the old 30 for
        // both gave up on reads the host would have finished.
        Assert.Equal(TimeSpan.FromSeconds(75), AnimeThemesMediaRelay.AnswerTime);
        Assert.Equal(TimeSpan.FromSeconds(30), AnimeThemesMediaRelay.BodyTime);
        Assert.Equal(TimeSpan.FromSeconds(8), AnimeThemesMediaRelay.SecondReadAfter);
        Assert.Equal(786_432, AnimeThemesMediaRelay.ClipBytes);
    }

    // ---- a host that is slow to answer ----

    [Fact]
    public async Task AnAnswerSlowerThanTheOldThirtySecondLimitIsStillServed()
    {
        // The wait for the answer and the time for the bytes are separate: a slow start doesn't
        // use up the bytes' time. (Scaled down: 400 ms to answer, 300 ms for the body.)
        var handler = new MediaHandler();
        var relay = new ShortRelay(new Factory(handler), TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(60));
        handler.Respond = async (_, ct) =>
        {
            await Task.Delay(400, ct);
            return MediaHandler.Body(new CountingStream(50_000));
        };

        await using var clip = await relay.OpenClipAsync(Link);

        Assert.Equal(50_000, (await CopyAsync(clip)).Length);
        Assert.False(clip.TimedOut);
    }

    [Fact]
    public async Task ASecondReadIsSentWhenTheFirstIsSlowAndTheFirstToAnswerIsUsed()
    {
        var (relay, handler, _) = Make(secondAfter: TimeSpan.FromMilliseconds(60));
        var calls = 0;
        CancellationToken firstToken = default;
        handler.Respond = async (_, ct) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                firstToken = ct;
                await Task.Delay(Timeout.Infinite, ct);
            }

            return MediaHandler.Body(new CountingStream(1_000));
        };

        await using var clip = await relay.OpenClipAsync(Link).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(1_000, (await CopyAsync(clip)).Length);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Equal(Link, r.Uri.AbsoluteUri));
        Assert.True(firstToken.IsCancellationRequested, "the read that wasn't used is dropped");
    }

    [Fact]
    public async Task ASlowFirstReadThatAnswersBeforeTheSecondOneDoesIsUsed()
    {
        var (relay, handler, _) = Make(secondAfter: TimeSpan.FromMilliseconds(60));
        var calls = 0;
        CancellationToken secondToken = default;
        handler.Respond = async (_, ct) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                // Long enough that the second read, sent after 60 ms, is on its way first.
                await Task.Delay(1_000, ct);
                return MediaHandler.Body(new CountingStream(2_000));
            }

            secondToken = ct;
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage();
        };

        await using var clip = await relay.OpenClipAsync(Link).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(2_000, (await CopyAsync(clip)).Length);
        Assert.True(secondToken.IsCancellationRequested);
    }

    [Fact]
    public async Task NoSecondReadIsSentWhenTheFirstAnswersInTime()
    {
        var (relay, handler, _) = Make(secondAfter: TimeSpan.FromSeconds(5));
        handler.Respond = (_, _) => Task.FromResult(MediaHandler.Body(new CountingStream(500)));

        await using var clip = await relay.OpenClipAsync(Link);
        await Task.Delay(50);

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task TheBodyOfTheReadThatWasUsedIsNotCutOffWhenTheOtherIsDropped()
    {
        // Dropping the loser must not touch the winner's stream, which is still to be copied.
        var (relay, handler, _) = Make(secondAfter: TimeSpan.FromMilliseconds(40));
        var calls = 0;
        handler.Respond = async (_, ct) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new HttpResponseMessage();
            }

            return MediaHandler.Body(new CountingStream(300_000));
        };

        await using var clip = await relay.OpenClipAsync(Link).WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(100);

        Assert.Equal(300_000, (await CopyAsync(clip)).Length);
    }

    [Fact]
    public async Task AConnectionFailureIsTriedOnceMoreAtOnce()
    {
        // Not after the 8 seconds a slow answer waits: it failed, so there is nothing to wait on.
        var (relay, handler, _) = Make(secondAfter: TimeSpan.FromSeconds(30));
        var calls = 0;
        handler.Respond = (_, _) =>
            Interlocked.Increment(ref calls) == 1
                ? throw new HttpRequestException("connection reset")
                : Task.FromResult(MediaHandler.Body(new CountingStream(700)));

        await using var clip = await relay.OpenClipAsync(Link).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(700, (await CopyAsync(clip)).Length);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task TwoConnectionFailuresAreAFailureWithAReasonAndNoThirdRead()
    {
        var (relay, handler, _) = Make(secondAfter: TimeSpan.FromSeconds(30));
        handler.Respond = (_, _) => throw new HttpRequestException("no route");

        var ex = await Assert.ThrowsAsync<AnimeThemesMediaUnavailableException>(
            () => relay.OpenClipAsync(Link).WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.Contains("couldn't be reached", ex.Message);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ARefusalIsReportedAtOnceAndNotAskedAgain(HttpStatusCode status)
    {
        // A busy host is the last one to ask twice. The wait is the second-read timer's: had the
        // relay waited for it, this would not come back within the test's two seconds.
        var (relay, handler, _) = Make(secondAfter: TimeSpan.FromSeconds(30));
        handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(status));

        await Assert.ThrowsAsync<AnimeThemesMediaUnavailableException>(
            () => relay.OpenClipAsync(Link).WaitAsync(TimeSpan.FromSeconds(2)));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task AFileThatHasGoneAnswersAtOnceToo()
    {
        var (relay, handler, _) = Make(secondAfter: TimeSpan.FromSeconds(30));
        handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

        await Assert.ThrowsAsync<AnimeThemesMediaGoneException>(
            () => relay.OpenClipAsync(Link).WaitAsync(TimeSpan.FromSeconds(2)));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ASlowFirstReadThatIsRefusedLeavesTheSecondToAnswer()
    {
        var (relay, handler, _) = Make(secondAfter: TimeSpan.FromMilliseconds(40));
        var calls = 0;
        handler.Respond = async (_, ct) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                await Task.Delay(150, ct);
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            }

            await Task.Delay(300, ct);
            return MediaHandler.Body(new CountingStream(900));
        };

        await using var clip = await relay.OpenClipAsync(Link).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(900, (await CopyAsync(clip)).Length);
    }

    [Fact]
    public async Task ACallerWhoGivesUpDropsBothReads()
    {
        var (relay, handler, _) = Make(secondAfter: TimeSpan.FromMilliseconds(40));
        var tokens = new List<CancellationToken>();
        handler.Respond = async (_, ct) =>
        {
            lock (tokens)
                tokens.Add(ct);
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage();
        };
        using var cts = new CancellationTokenSource();

        var open = relay.OpenClipAsync(Link, cts.Token);
        // Both reads are on their way before the caller gives up: waited for, not timed.
        var started = DateTime.UtcNow;
        while (true)
        {
            lock (tokens)
                if (tokens.Count == 2)
                    break;
            Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(10), "the second read was never sent");
            await Task.Delay(10);
        }

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => open.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, t => Assert.True(t.IsCancellationRequested));
    }

    [Fact]
    public async Task AHostThatNeverAnswersIsGivenBothReadsAndThenGivenUpOn()
    {
        var (relay, handler, _) = Make(limit: TimeSpan.FromMilliseconds(1_500), secondAfter: TimeSpan.FromMilliseconds(40));
        handler.Respond = async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage();
        };

        var ex = await Assert.ThrowsAsync<AnimeThemesMediaUnavailableException>(
            () => relay.OpenClipAsync(Link).WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.Contains("too long", ex.Message);
        Assert.Equal(2, handler.Requests.Count);
    }
}
