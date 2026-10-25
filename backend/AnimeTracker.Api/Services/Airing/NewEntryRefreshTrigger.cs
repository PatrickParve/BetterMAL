using System.Threading.Channels;

namespace AnimeTracker.Api.Services.Airing;

public class NewEntryRefreshTrigger : INewEntryRefreshTrigger
{
    private readonly Channel<int> _queue = Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(int animeId) => _queue.Writer.TryWrite(animeId);

    public async Task<IReadOnlyList<int>> WaitAsync(CancellationToken ct)
    {
        await _queue.Reader.WaitToReadAsync(ct);

        var animeIds = new List<int>();
        var seen = new HashSet<int>();
        while (_queue.Reader.TryRead(out var animeId))
            if (seen.Add(animeId))
                animeIds.Add(animeId);
        return animeIds;
    }
}
