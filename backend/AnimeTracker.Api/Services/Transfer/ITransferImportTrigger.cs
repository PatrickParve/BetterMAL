namespace AnimeTracker.Api.Services.Transfer;

/// <summary>Hands an accepted import file to the background job — a
/// single-slot queue, unlike <c>ISeriesBulkBuildTrigger</c>'s payload-less
/// signal: the background service needs the parsed file itself, not just a
/// wake-up.</summary>
public interface ITransferImportTrigger
{
    /// <summary>Offers <paramref name="file"/> to be imported. Returns false,
    /// accepting nothing, while a file is already pending or an import is
    /// running.</summary>
    bool TryOffer(TransferFile file);

    /// <summary>Waits for and returns the next offered file.</summary>
    Task<TransferFile> WaitAsync(CancellationToken ct);

    /// <summary>Frees the slot once a run has finished — success or failure
    /// — so the next <see cref="TryOffer"/> can succeed (design.md D1: "the
    /// slot is released when the run ends").</summary>
    void Release();
}
