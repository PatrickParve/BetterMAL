using System.Net;
using System.Net.Http.Headers;

namespace AnimeTracker.Api.Services.AnimeThemes;

/// <summary>The relay of design D9, on a named client. It reads `a.animethemes.moe` and
/// `v.animethemes.moe` only, and only a link held on a game's round or in the catalogue (checked
/// here again, so a bad row can't aim it anywhere else). It asks for a `Range` of the file and
/// passes on what comes back, stopping at that many even if the host ignores `Range`. Nothing is
/// written to disk, and no upstream header leaves: the host's file name and `Content-Disposition` name the
/// show, and it sets session cookies, so the client keeps none of them either.
///
/// The host is slow to start answering and uneven about it: from under a second to over thirty
/// for the same read, with the bytes then arriving at once. So the relay is patient about the
/// answer (<see cref="AnswerTime"/>), sends a second identical read beside the first when it is
/// slow or couldn't connect (<see cref="SecondReadAfter"/>), and uses whichever answers first.
/// That is at most two reads of a song, and only when the first was slow.</summary>
public class AnimeThemesMediaRelay(IHttpClientFactory factory, ILogger<AnimeThemesMediaRelay> logger)
    : IAnimeThemesMediaRelay
{
    public const string HttpClientName = "AnimeThemesMedia";

    /// <summary>768 KiB: at the highest rate seen (about 45 KB per second) that is about 16
    /// seconds, enough for the latest onset measured plus the 10-second rung.</summary>
    public const long ClipBytes = 768 * 1024;

    /// <summary>From asking to the host's answer. Long, because the person is waiting for the song
    /// either way and a read given up on is one the host has done its work for; the real host took
    /// 10 to 25 seconds often enough, and over 30 three times in a short run.</summary>
    public static readonly TimeSpan AnswerTime = TimeSpan.FromSeconds(75);

    /// <summary>From the answer to the last byte passed on. Once the host starts, a clip comes in a
    /// fraction of a second, so a body slower than this has stalled.</summary>
    public static readonly TimeSpan BodyTime = TimeSpan.FromSeconds(30);

    /// <summary>How long the first read is given before a second is sent beside it.</summary>
    public static readonly TimeSpan SecondReadAfter = TimeSpan.FromSeconds(8);

    /// <summary>What Program.cs's registration applies to the named client. The time limits are
    /// the relay's own, so a body still streaming is held to them as well as the wait for the
    /// first byte; the client's timeout would cover only the latter.</summary>
    public static void Configure(HttpClient client)
    {
        client.Timeout = Timeout.InfiniteTimeSpan;
        client.DefaultRequestHeaders.UserAgent.ParseAdd(AnimeThemesClient.UserAgent);
    }

    /// <summary>No redirect is followed, which would leave the allow-listed host, and no cookie is
    /// kept. A redirect from the media host would show as a failed load.</summary>
    public static HttpMessageHandler CreateHandler() =>
        new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false };

    // Virtual so a test can use short limits rather than sit through them.
    protected virtual TimeSpan AnswerLimit => AnswerTime;
    protected virtual TimeSpan BodyLimit => BodyTime;
    protected virtual TimeSpan SecondRead => SecondReadAfter;

    public Task<AnimeThemesClip> OpenClipAsync(string audioUrl, CancellationToken ct = default)
    {
        // No request is made for a link the allow-list refuses: the song is as good as gone.
        var link = AnimeThemeChoice.AudioLink(audioUrl) ?? throw new AnimeThemesMediaGoneException();
        return OpenAsync(link, 0, ClipBytes - 1, askTwice: true, BodyLimit, ct);
    }

    public Task<AnimeThemesClip> OpenVideoAsync(string videoUrl, long firstByte, long lastByte, CancellationToken ct = default)
    {
        var link = AnimeThemeChoice.VideoLink(videoUrl) ?? throw new AnimeThemesMediaGoneException();

        // One read: a video is read ahead with nobody waiting on it, and a second read beside a
        // slow first only adds to what the host has to do. It has no time limit for its body either,
        // since it ends with the person watching: a long song read slowly is not a stalled one.
        return OpenAsync(link, firstByte, lastByte, askTwice: false, bodyLimit: null, ct);
    }

    private async Task<AnimeThemesClip> OpenAsync(
        string link, long firstByte, long lastByte, bool askTwice, TimeSpan? bodyLimit, CancellationToken ct)
    {
        HttpResponseMessage? response = null;
        CancellationTokenSource? body = null;
        try
        {
            using var answerBy = new CancellationTokenSource(AnswerLimit);
            using var waiting = CancellationTokenSource.CreateLinkedTokenSource(ct, answerBy.Token);
            response = askTwice
                ? await ReadFirstAnswerAsync(link, firstByte, lastByte, waiting.Token)
                : await SendAsync(link, firstByte, lastByte, waiting.Token);

            if (IsGone(response.StatusCode))
                throw new AnimeThemesMediaGoneException();
            if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.PartialContent))
            {
                logger.LogWarning("AnimeThemes media host answered {Status} for a file.", (int)response.StatusCode);
                throw new AnimeThemesMediaUnavailableException(response.StatusCode == HttpStatusCode.TooManyRequests
                    ? "AnimeThemes is busy right now."
                    : "AnimeThemes didn't give the song.");
            }

            // A host that ignored the `Range` sent the file from its start, which is only what was
            // asked for by a read from the start.
            if (firstByte > 0 && response.StatusCode != HttpStatusCode.PartialContent)
            {
                logger.LogWarning("AnimeThemes media host answered {Status} to a read from the middle of a file.", (int)response.StatusCode);
                throw new AnimeThemesMediaUnavailableException("AnimeThemes didn't give the song.");
            }

            // The time for the bytes starts here, with the answer.
            body = bodyLimit is { } limit ? new CancellationTokenSource(limit) : new CancellationTokenSource();
            var stream = await response.Content.ReadAsStreamAsync(body.Token);
            var wholeFile = response.Content.Headers.ContentRange?.Length ?? response.Content.Headers.ContentLength;
            return new AnimeThemesClip(stream, lastByte - firstByte + 1, body, response, wholeFile);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            response?.Dispose();
            body?.Dispose();
            logger.LogWarning("AnimeThemes media host took longer than {Seconds}s to answer for a file.", AnswerLimit.TotalSeconds);
            throw new AnimeThemesMediaUnavailableException("AnimeThemes took too long to answer.");
        }
        catch (HttpRequestException ex)
        {
            response?.Dispose();
            body?.Dispose();
            logger.LogWarning(ex, "AnimeThemes media host could not be reached for a file.");
            throw new AnimeThemesMediaUnavailableException("AnimeThemes couldn't be reached.", ex);
        }
        catch
        {
            response?.Dispose();
            body?.Dispose();
            throw;
        }
    }

    public async Task<AnimeThemesMediaState> ProbeAsync(string audioUrl, CancellationToken ct = default)
    {
        var link = AnimeThemeChoice.AudioLink(audioUrl);
        if (link is null)
            return AnimeThemesMediaState.Gone;

        using var deadline = new CancellationTokenSource(AnswerLimit);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        try
        {
            using var response = await SendAsync(link, 0, 0, linked.Token);

            if (IsGone(response.StatusCode))
                return AnimeThemesMediaState.Gone;

            return response.StatusCode is HttpStatusCode.OK or HttpStatusCode.PartialContent
                ? AnimeThemesMediaState.Present
                : AnimeThemesMediaState.Unreachable;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return AnimeThemesMediaState.Unreachable;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "AnimeThemes media host could not be reached to check a file.");
            return AnimeThemesMediaState.Unreachable;
        }
    }

    /// <summary>One read of the file's start, and a second beside it when the first is slow or
    /// failed to connect. The first answer that settles the matter is used: the file, or that it
    /// has gone. The read that wasn't used is cancelled. An answer that is neither (a refusal, a
    /// busy host) is not retried: asking again at once is what a busy host least wants, and the
    /// board's own Try again comes later. With every read ended and none answering, the last
    /// refusal is returned as it came, or the last failure thrown.</summary>
    private async Task<HttpResponseMessage> ReadFirstAnswerAsync(string link, long firstByte, long lastByte, CancellationToken giveUp)
    {
        var reads = new List<Read>();
        Read? refused = null;
        Exception? failure = null;

        try
        {
            reads.Add(Begin(link, firstByte, lastByte, giveUp));
            var secondDue = Task.Delay(SecondRead, giveUp);

            while (true)
            {
                giveUp.ThrowIfCancellationRequested();

                foreach (var read in reads.Where(r => r.Pending.IsCompleted && !r.Settled))
                {
                    read.Settled = true;
                    if (!read.Pending.IsCompletedSuccessfully)
                    {
                        failure = read.Pending.Exception?.GetBaseException();
                        continue;
                    }

                    var status = read.Pending.Result.StatusCode;
                    if (status is HttpStatusCode.OK or HttpStatusCode.PartialContent || IsGone(status))
                    {
                        read.Used = true;
                        return read.Pending.Result;
                    }

                    refused = read;
                }

                var running = reads.Where(r => !r.Pending.IsCompleted).Select(r => (Task)r.Pending).ToList();
                var canAskAgain = reads.Count < 2;

                // Slow, or it couldn't connect (and nothing else is on its way): a second read, once.
                if (canAskAgain && (secondDue.IsCompleted || (running.Count == 0 && failure is HttpRequestException)))
                {
                    reads.Add(Begin(link, firstByte, lastByte, giveUp));
                    continue;
                }

                if (running.Count == 0)
                    break;

                if (canAskAgain)
                    running.Add(secondDue);
                await Task.WhenAny(running);
            }

            // Nothing answered with the file: a refusal is passed on as the status it was.
            if (refused is not null)
            {
                refused.Used = true;
                return refused.Pending.Result;
            }

            throw failure ?? new HttpRequestException("AnimeThemes did not answer.");
        }
        finally
        {
            foreach (var read in reads)
                read.Finish();
        }
    }

    private Read Begin(string link, long firstByte, long lastByte, CancellationToken giveUp)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(giveUp);
        return new Read(cts, SendAsync(link, firstByte, lastByte, cts.Token));
    }

    /// <summary>A read in flight. The one that isn't used is cancelled, and whatever it brings is
    /// let go; the one that is used is left alone, since its body is still to be passed on.</summary>
    private sealed class Read(CancellationTokenSource cts, Task<HttpResponseMessage> pending)
    {
        public Task<HttpResponseMessage> Pending { get; } = pending;
        public bool Settled { get; set; }
        public bool Used { get; set; }

        public void Finish()
        {
            if (!Used)
            {
                cts.Cancel();
                _ = Discard(Pending);
            }

            // Disposing a source never cancels what it was given to, so the used read's body is safe.
            cts.Dispose();
        }

        private static async Task Discard(Task<HttpResponseMessage> pending)
        {
            try
            {
                (await pending).Dispose();
            }
            catch
            {
                // Cancelled or failed: nothing to let go of.
            }
        }
    }

    // Headers only: the body is read (or not) by the caller, so a probe never downloads a file.
    private Task<HttpResponseMessage> SendAsync(string link, long firstByte, long lastByte, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, link)
        {
            Headers = { Range = new RangeHeaderValue(firstByte, lastByte) },
        };

        return SendAndDisposeRequestAsync(request, ct);
    }

    private async Task<HttpResponseMessage> SendAndDisposeRequestAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using (request)
            return await factory.CreateClient(HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private static bool IsGone(HttpStatusCode status) => status is HttpStatusCode.NotFound or HttpStatusCode.Gone;
}
