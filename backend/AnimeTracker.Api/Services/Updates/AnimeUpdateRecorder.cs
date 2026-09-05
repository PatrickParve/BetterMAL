using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Updates;

public class AnimeUpdateRecorder(AnimeTrackerDbContext db, IAnimeUpdateRelevance relevance) : IAnimeUpdateRecorder
{
    private const AnimeUpdateKinds BecomingKnownKinds =
        AnimeUpdateKinds.Announced | AnimeUpdateKinds.EpisodeCountReleased | AnimeUpdateKinds.StartDateReleased;

    public async Task RecordAsync(
        AnimeMetadata anime,
        AnimeUpdateKinds kinds,
        ScheduleMoveDetails moves,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        if (kinds == 0)
            return;

        // The relevance gate (design.md D1) lives here rather than at each
        // call site because this is the only place an AnimeUpdate row is
        // created: gating here covers AnimeMetadataChangeDetector,
        // AnnouncementResolutionService and both EpisodeScheduleRefreshService
        // call sites at once, and covers any writer added later — the same
        // argument that put detection on the write functions themselves. It
        // runs after the kinds == 0 early-out so a caller with nothing to
        // report never pays for it.
        if (!await relevance.IsRelevantAsync(anime.Id, ct))
            return;

        var becomingKnown = kinds & BecomingKnownKinds;
        if (becomingKnown != 0)
        {
            var alreadyRecorded = await AlreadyRecordedBecomingKnownKindsAsync(anime.Id, ct);
            becomingKnown &= ~alreadyRecorded;
        }

        var scheduleChange = kinds & ~BecomingKnownKinds; // no dedupe: the diff that produced this is the guard
        var toRecord = becomingKnown | scheduleChange;
        if (toRecord == 0)
            return;

        db.AnimeUpdates.Add(new AnimeUpdate
        {
            AnimeId = anime.Id,
            DetectedAt = now,
            Kinds = toRecord,
            PreviousStartDate = (toRecord & AnimeUpdateKinds.StartDateChanged) != 0 ? moves.PreviousStartDate : null,
            PreviousBroadcastDayOfWeek = (toRecord & AnimeUpdateKinds.BroadcastSlotChanged) != 0 ? moves.PreviousBroadcastDayOfWeek : null,
            PreviousBroadcastTime = (toRecord & AnimeUpdateKinds.BroadcastSlotChanged) != 0 ? moves.PreviousBroadcastTime : null,
            MovedEpisode = (toRecord & AnimeUpdateKinds.EpisodesMoved) != 0 ? moves.MovedEpisode : null,
            PreviousEpisodeDate = (toRecord & AnimeUpdateKinds.EpisodesMoved) != 0 ? moves.PreviousEpisodeDate : null,
            NewEpisodeDate = (toRecord & AnimeUpdateKinds.EpisodesMoved) != 0 ? moves.NewEpisodeDate : null,
        });
    }

    // Becoming-known kinds are recorded at most once per anime, for all time
    // (design.md D2) — small enough per anime (three bits, ever) that reading
    // every existing row's Kinds and OR-ing them client-side is simpler and
    // just as cheap as expressing the check in SQL.
    private async Task<AnimeUpdateKinds> AlreadyRecordedBecomingKnownKindsAsync(int animeId, CancellationToken ct)
    {
        var existingKinds = await db.AnimeUpdates
            .Where(u => u.AnimeId == animeId)
            .Select(u => u.Kinds)
            .ToListAsync(ct);

        var combined = (AnimeUpdateKinds)0;
        foreach (var kinds in existingKinds)
            combined |= kinds;
        return combined & BecomingKnownKinds;
    }
}
