namespace AnimeTracker.Api.Services.Transfer;

/// <summary>A file this build cannot or should not read (design.md D2 steps
/// 1-4): not JSON, not an object, a required member missing or of the wrong
/// kind, a newer format, or exported from this very device. Maps to 400 at
/// the controller (task 9.1) — a file problem, checked and reported before
/// anything about this device's own state.</summary>
public class TransferFileRefusedException(string message) : Exception(message);
