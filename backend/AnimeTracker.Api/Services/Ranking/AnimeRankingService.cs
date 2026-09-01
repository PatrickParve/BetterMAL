using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Profile;

namespace AnimeTracker.Api.Services.Ranking;

public class AnimeRankingService(
    IUserAnimeEntryRepository entryRepository,
    ITopAnimeSelectionRepository topAnimeSelectionRepository) : IAnimeRankingService
{
    public async Task<AnimeRankingSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        var storedOrder = await topAnimeSelectionRepository.GetOrderedAnimeIdsAsync(ct);
        return AnimeRankingSnapshot.Build(entries, storedOrder);
    }

    public async Task<List<AnimeRankingScoreCountDto>> GetScoreCountsAsync(string mediaTypeScope, CancellationToken ct = default)
    {
        var snapshot = await GetSnapshotAsync(ct);
        return HandOrderableByScore(snapshot, mediaTypeScope)
            .Select(g => new AnimeRankingScoreCountDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Score)
            .ToList();
    }

    public async Task<AnimeRankingTierDto?> GetTierAsync(int score, string mediaTypeScope, CancellationToken ct = default)
    {
        var snapshot = await GetSnapshotAsync(ct);
        return BuildTier(snapshot, score, mediaTypeScope);
    }

    private static AnimeRankingTierDto? BuildTier(AnimeRankingSnapshot snapshot, int score, string mediaTypeScope)
    {
        var members = HandOrderableByScore(snapshot, mediaTypeScope)
            .FirstOrDefault(g => g.Key == score)
            ?.Select(e => ToMember(e, snapshot))
            .ToList();

        return members is { Count: > 0 } ? new AnimeRankingTierDto(score, members) : null;
    }

    // Grouping preserves each group's internal encounter order, and
    // RankedEntries is already in full ranking order (score desc, band asc,
    // position asc within hand-ordered, title asc) — so filtering to the
    // hand-ordered band before grouping by score yields each score's
    // hand-ordered members already in ranking order, with no re-sort needed.
    private static IEnumerable<IGrouping<int, UserAnimeEntry>> HandOrderableByScore(
        AnimeRankingSnapshot snapshot, string mediaTypeScope) =>
        snapshot.RankedEntries
            .Where(e => RankBandResolver.Resolve(e) == RankBand.HandOrdered
                && TopAnimeMediaTypeScope.Matches(mediaTypeScope, e.Anime.MediaType))
            .GroupBy(e => e.MyScore!.Value);

    private static AnimeRankingMemberDto ToMember(UserAnimeEntry e, AnimeRankingSnapshot snapshot) =>
        new(e.AnimeId, e.Anime.Title, e.Anime.EnglishTitle, e.Anime.PictureUrl, snapshot.RankOf(e.AnimeId)!.Value);

    public async Task ApplyTierOrderAsync(List<AnimeRankingTierOrderRequest> tiers, CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        var positionByAnimeId = await LoadPositionMapAsync(ct);

        var handOrderableByScore = HandOrderableByScoreMap(entries);

        var mismatchedIds = tiers
            .SelectMany(tier =>
            {
                var tierMemberIds = (handOrderableByScore.GetValueOrDefault(tier.Score) ?? [])
                    .Select(e => e.AnimeId)
                    .ToHashSet();
                return tier.AnimeIds.Where(id => !tierMemberIds.Contains(id));
            })
            .ToList();
        if (mismatchedIds.Count > 0)
            throw new AnimeRankingTierScoreMismatchException(mismatchedIds);

        // design.md D5: build one combined sequence of every edited tier's
        // full hand-orderable membership, each tier's own new order intact —
        // the slot-preserving merge that assigns these back onto the stored
        // list lives in TopAnimeSelectionRepository.ReplaceOrderAsync.
        var editedIds = new List<int>();

        foreach (var tier in tiers.OrderByDescending(t => t.Score))
        {
            var tierMembers = handOrderableByScore.GetValueOrDefault(tier.Score) ?? [];
            var effectiveOrder = AnimeRankingKey.OrderEntries(tierMembers, positionByAnimeId)
                .Select(e => e.AnimeId)
                .ToList();

            var visibleIds = tier.AnimeIds;
            var visibleIndexes = effectiveOrder
                .Select((animeId, index) => (animeId, index))
                .Where(x => visibleIds.Contains(x.animeId))
                .Select(x => x.index)
                .OrderBy(index => index)
                .ToList();

            var merged = effectiveOrder.ToList();
            for (var i = 0; i < visibleIndexes.Count; i++)
                merged[visibleIndexes[i]] = visibleIds[i];

            editedIds.AddRange(merged);
        }

        await topAnimeSelectionRepository.ReplaceOrderAsync(editedIds, ct);
    }

    public async Task MoveAdjacentAsync(int promotedAnimeId, int demotedAnimeId, CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        var byId = entries.ToDictionary(e => e.AnimeId);
        if (!byId.TryGetValue(promotedAnimeId, out var promoted) || !byId.TryGetValue(demotedAnimeId, out var demoted))
            return;
        if (promoted.MyScore != demoted.MyScore
            || RankBandResolver.Resolve(promoted) != RankBand.HandOrdered
            || RankBandResolver.Resolve(demoted) != RankBand.HandOrdered)
            return;

        var positionByAnimeId = await LoadPositionMapAsync(ct);
        var tierMembers = entries.Where(e => e.MyScore == promoted.MyScore && RankBandResolver.Resolve(e) == RankBand.HandOrdered);
        var tierOrder = AnimeRankingKey.OrderEntries(tierMembers, positionByAnimeId).Select(e => e.AnimeId).ToList();

        // Remove-then-insert-after (not a swap): everyone strictly between
        // the two original positions shifts by one slot, and demoted lands
        // immediately after promoted — which is where it needs to be
        // whichever originally came first, since both directions of the
        // series-page reorder route through this same call.
        tierOrder.Remove(demotedAnimeId);
        var promotedIndex = tierOrder.IndexOf(promotedAnimeId);
        tierOrder.Insert(promotedIndex + 1, demotedAnimeId);

        await topAnimeSelectionRepository.ReplaceOrderAsync(tierOrder, ct);
    }

    public async Task PlaceLastInTierAsync(int animeId, int score, CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        var positionByAnimeId = await LoadPositionMapAsync(ct);

        // animeId is placed unconditionally, whatever its own current band —
        // a dropped or plan-to-watch anime still gets a reserved slot at the
        // tail of its score's hand-ordered order, which is what lets it fall
        // back into the same place once it becomes hand-orderable again
        // (design.md D6/"Dropping and un-dropping keeps my placement").
        var otherHandOrderedMembers = entries
            .Where(e => e.AnimeId != animeId && e.MyScore == score && RankBandResolver.Resolve(e) == RankBand.HandOrdered);

        var sequence = AnimeRankingKey.OrderEntries(otherHandOrderedMembers, positionByAnimeId)
            .Select(e => e.AnimeId)
            .Append(animeId)
            .ToList();

        await topAnimeSelectionRepository.ReplaceOrderAsync(sequence, ct);
    }

    private async Task<Dictionary<int, int>> LoadPositionMapAsync(CancellationToken ct)
    {
        var existingOrder = await topAnimeSelectionRepository.GetOrderedAnimeIdsAsync(ct);
        return existingOrder
            .Select((animeId, index) => (animeId, index))
            .ToDictionary(x => x.animeId, x => x.index);
    }

    private static Dictionary<int, List<UserAnimeEntry>> HandOrderableByScoreMap(List<UserAnimeEntry> entries) =>
        entries
            .Where(e => RankBandResolver.Resolve(e) == RankBand.HandOrdered)
            .GroupBy(e => e.MyScore!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
}
