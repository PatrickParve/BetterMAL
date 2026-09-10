namespace AnimeTracker.Api.Services.Transfer;

/// <summary>Accepts an import file: reads it, runs design.md D2's refusals
/// in order, then hands it to the background job (D1).</summary>
public interface ITransferImportService
{
    /// <summary>Runs D2's refusals, stopping at the first that applies, then
    /// offers the file to the background job and marks it pending. Throws
    /// <see cref="TransferFileRefusedException"/> for a file problem (steps
    /// 1-4, maps to 400) or <see cref="TransferImportBlockedException"/> for
    /// a state problem (steps 5-6, maps to 409). Nothing is written and no
    /// file is offered on a refusal. Returns the status snapshot once
    /// accepted — already <c>Running</c>.</summary>
    Task<TransferImportStatusSnapshot> AcceptAsync(byte[] bytes, CancellationToken ct = default);
}
