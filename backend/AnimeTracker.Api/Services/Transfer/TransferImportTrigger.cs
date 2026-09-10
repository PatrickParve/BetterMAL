namespace AnimeTracker.Api.Services.Transfer;

public class TransferImportTrigger : ITransferImportTrigger
{
    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _signal = new(0, 1);
    private TransferFile? _pending;
    private bool _busy; // pending (offered, not yet picked up) or running

    public bool TryOffer(TransferFile file)
    {
        lock (_lock)
        {
            if (_busy)
                return false;
            _busy = true;
            _pending = file;
        }
        _signal.Release();
        return true;
    }

    public async Task<TransferFile> WaitAsync(CancellationToken ct)
    {
        await _signal.WaitAsync(ct);
        lock (_lock)
            return _pending!;
    }

    public void Release()
    {
        lock (_lock)
        {
            _busy = false;
            _pending = null;
        }
    }
}
