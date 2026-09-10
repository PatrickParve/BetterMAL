namespace AnimeTracker.Api.Services.Transfer;

/// <summary>An import refused for a reason about this device's own state,
/// not the file itself (design.md D2 steps 5-6): the initial list import
/// hasn't finished since the app started, or another import is already
/// running. Maps to 409 at the controller (task 9.1).</summary>
public class TransferImportBlockedException(string message) : Exception(message);
