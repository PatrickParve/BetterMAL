using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Transfer;

public class DeviceIdentityInitializer(AnimeTrackerDbContext db) : IDeviceIdentityInitializer
{
    public async Task EnsureAsync(CancellationToken ct = default)
    {
        if (await db.DeviceIdentities.AnyAsync(ct))
            return;

        db.DeviceIdentities.Add(new DeviceIdentity { DeviceId = Guid.NewGuid() });
        await db.SaveChangesAsync(ct);
    }
}
