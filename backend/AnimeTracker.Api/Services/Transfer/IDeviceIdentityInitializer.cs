namespace AnimeTracker.Api.Services.Transfer;

/// <summary>Creates this database's device identity on first start. Must run
/// from Program.cs's synchronous startup scope, after Database.Migrate() and
/// before app.Run() — the only point with a guarantee that no hosted
/// service, controller, or debounce timer has started yet (design.md D6,
/// same reasoning as IStartupPendingSyncHold).</summary>
public interface IDeviceIdentityInitializer
{
    /// <summary>Idempotent: adds one <c>DeviceIdentity</c> row with a fresh
    /// GUID when none exists. Does nothing when one already does, so the id
    /// never changes once created.</summary>
    Task EnsureAsync(CancellationToken ct = default);
}
