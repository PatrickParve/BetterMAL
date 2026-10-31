using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.AnimeThemes;
using AnimeTracker.Api.Services.Games;
using Microsoft.EntityFrameworkCore;
using static AnimeTracker.Api.Tests.Services.Games.HigherOrLowerTestKit;

namespace AnimeTracker.Api.Tests.Services.Games;

// What the Guess the Opening/Ending fixtures share (add-anime-themes-and-guess-the-theme
// sections 5-7): settings to play with, a stored song to seed, and a game in progress
// written straight to its tables. The database, the clock and the plain eligible anime are
// HigherOrLowerTestKit's.
internal static class GuessTheThemeTestKit
{
    public static readonly GuessTheThemeSettings AnyTheme =
        new(null, null, null, GameListScope.All, false, GameThemeChoice.All);

    /// <summary>One stored opening or ending that is usable unless a parameter says otherwise:
    /// a song, a video and an audio file, no flag. Pass <c>songId: null</c>, <c>video: false</c>
    /// or <c>audio: false</c> for the three things that make one unusable. The theme's own id is the song's, which keeps two songs of one anime off each other's key.</summary>
    public static AnimeTheme Song(
        int animeId,
        int? songId,
        AnimeThemeKind kind = AnimeThemeKind.Opening,
        int sequence = 1,
        int? themeId = null,
        bool spoiler = false,
        bool nsfw = false,
        bool video = true,
        bool audio = true)
    {
        var id = themeId ?? songId ?? 99_999;
        return new AnimeTheme
        {
            AnimeId = animeId,
            ThemeId = id,
            Kind = kind,
            Sequence = sequence,
            Label = $"{(kind == AnimeThemeKind.Opening ? "OP" : "ED")}{sequence}",
            SongId = songId,
            SongTitle = songId is null ? null : $"Song {songId}",
            Artists = ["Artist A", "Artist B"],
            Episodes = "1-12",
            Nsfw = nsfw,
            Spoiler = spoiler,
            Clean = true,
            VideoUrl = video ? $"https://v.animethemes.moe/T{id}.webm" : null,
            AudioUrl = audio ? $"https://a.animethemes.moe/T{id}.ogg" : null,
        };
    }

    /// <summary>A game in progress with its current song, written straight to the tables so a
    /// test can put the game wherever it needs without playing there. The song is the one
    /// <paramref name="song"/> names, copied as a draw copies it; nothing about the catalogue
    /// has to exist for it.</summary>
    public static async Task<(GuessTheThemeRun Run, GuessTheThemeRound Round)> AddThemeRunAsync(
        AnimeTrackerDbContext db,
        int animeId,
        int songId = 500,
        int step = 0,
        GuessTheThemePhase phase = GuessTheThemePhase.Listening,
        int[]? roundGuesses = null,
        int[]? played = null,
        int[]? accepted = null,
        GuessTheThemeSettings? settings = null,
        int moves = 0,
        int round = 1)
    {
        var s = settings ?? AnyTheme;
        var run = new GuessTheThemeRun
        {
            Moves = moves,
            Round = round,
            Step = step,
            Phase = phase,
            RoundGuesses = [.. roundGuesses ?? []],
            PlayedSongIds = [.. played ?? [songId]],
            YearFrom = s.YearFrom, YearTo = s.YearTo, MediaTypes = s.MediaTypes, ListScope = s.List,
            HideHentai = s.HideHentai, Themes = s.Themes,
        };
        db.GuessTheThemeRuns.Add(run);
        await db.SaveChangesAsync();

        var current = AddRound(db, run.Id, round, animeId, songId, accepted);
        await db.SaveChangesAsync();
        return (run, current);
    }

    /// <summary>Adds a drawn song to a run, unsaved.</summary>
    public static GuessTheThemeRound AddRound(
        AnimeTrackerDbContext db, int runId, int number, int animeId, int songId, int[]? accepted = null)
    {
        var round = new GuessTheThemeRound
        {
            RunId = runId,
            Number = number,
            AnimeId = animeId,
            ThemeId = songId,
            Kind = AnimeThemeKind.Opening,
            Label = "OP1",
            SongId = songId,
            SongTitle = $"Song {songId}",
            Artists = ["Artist A"],
            AudioUrl = $"https://a.animethemes.moe/T{songId}.ogg",
            VideoUrl = $"https://v.animethemes.moe/T{songId}.webm",
            AcceptedAnimeIds = [.. accepted ?? [animeId]],
        };
        db.GuessTheThemeRounds.Add(round);
        return round;
    }

    /// <summary>Marks the catalogue as having been refreshed once, which is what makes the
    /// setup screen's count believable.</summary>
    public static void MarkSynced(AnimeTrackerDbContext db) =>
        db.AnimeThemesSyncStates.Add(new AnimeThemesSyncState
        {
            LastSyncedAt = DateTimeOffset.UtcNow,
            LastAttemptAt = DateTimeOffset.UtcNow,
        });
}

/// <summary>Stands in for AnimeThemes' media host behind <see cref="IAnimeThemesMediaRelay"/>: a
/// probe answers <see cref="ProbeResult"/> and is recorded, and a clip or a read of a video is the
/// bytes it is given or the failure it is told to raise.</summary>
internal sealed class FakeMediaRelay : IAnimeThemesMediaRelay
{
    public AnimeThemesMediaState ProbeResult { get; set; } = AnimeThemesMediaState.Gone;
    public List<string> Probed { get; } = [];
    public List<string> Opened { get; } = [];

    /// <summary>What a clip holds; ignored if <see cref="OpenFailure"/> is set.</summary>
    public byte[] Bytes { get; set; } = [1, 2, 3, 4, 5];
    public Exception? OpenFailure { get; set; }

    /// <summary>A body to pass on instead of <see cref="Bytes"/>, such as one that fails part way.</summary>
    public Stream? Source { get; set; }

    /// <summary>Runs inside a probe, such as another tab's move landing while this one waits.</summary>
    public Func<Task>? OnProbe { get; set; }

    /// <summary>The whole of a video file, of which a read is given the part it asks for.</summary>
    public byte[] Video { get; set; } = [];
    public List<(string Link, long First, long Last)> VideoReads { get; } = [];
    public Exception? VideoFailure { get; set; }

    /// <summary>A body to pass on for a read from that byte instead of <see cref="Video"/>'s, such as one
    /// that fails part way.</summary>
    public Func<long, Stream>? VideoBody { get; set; }

    public Task<AnimeThemesClip> OpenClipAsync(string audioUrl, CancellationToken ct = default)
    {
        Opened.Add(audioUrl);
        if (OpenFailure is not null)
            return Task.FromException<AnimeThemesClip>(OpenFailure);

        return Task.FromResult(Source is not null
            ? new AnimeThemesClip(Source, AnimeThemesMediaRelay.ClipBytes, new CancellationTokenSource())
            : new AnimeThemesClip(new MemoryStream(Bytes), Bytes.Length, new CancellationTokenSource()));
    }

    public Task<AnimeThemesClip> OpenVideoAsync(string videoUrl, long firstByte, long lastByte, CancellationToken ct = default)
    {
        VideoReads.Add((videoUrl, firstByte, lastByte));
        if (VideoFailure is not null)
            return Task.FromException<AnimeThemesClip>(VideoFailure);

        var start = (int)Math.Min(firstByte, Video.Length);
        var end = (int)Math.Min(lastByte + 1, Video.Length);
        Stream body = VideoBody is { } make ? make(firstByte) : new MemoryStream(Video[start..end]);
        return Task.FromResult(new AnimeThemesClip(
            body, lastByte - firstByte + 1, new CancellationTokenSource(), owner: null, wholeFileBytes: Video.Length));
    }

    public async Task<AnimeThemesMediaState> ProbeAsync(string audioUrl, CancellationToken ct = default)
    {
        Probed.Add(audioUrl);
        if (OnProbe is not null)
            await OnProbe();
        return ProbeResult;
    }
}

/// <summary>Stands in for <see cref="IAnimeThemesMediaHold"/> behind the game: records what the game
/// asked to have held, each time it asked.</summary>
internal sealed class FakeMediaHold : IAnimeThemesMediaHold
{
    public List<List<HeldSong>> Kept { get; } = [];

    public void Keep(IEnumerable<HeldSong> songs) => Kept.Add(songs.ToList());

    public Task<byte[]> ClipAsync(string audioUrl, CancellationToken ct = default) =>
        throw new NotSupportedException("The game never reads a file itself.");

    public Task<AnimeThemesVideoStart> VideoStartAsync(string videoUrl, CancellationToken ct = default) =>
        throw new NotSupportedException("The game never reads a file itself.");
}
