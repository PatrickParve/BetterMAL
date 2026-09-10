using AnimeTracker.Api.Services.Transfer;

namespace AnimeTracker.Api.Tests.Services.Transfer;

// TransferImportTrigger (device-transfer design.md D1, tasks.md 4.4): the
// single-slot handoff from the accepting request to the background job.
public class TransferImportTriggerTests
{
    private static TransferFile File() => new(
        1, new TransferDevice(Guid.NewGuid(), "Device"), DateTimeOffset.UtcNow,
        new TransferRanking([], null), [], [], []);

    [Fact]
    public void TryOfferSucceedsWhenNothingIsPending()
    {
        var trigger = new TransferImportTrigger();

        Assert.True(trigger.TryOffer(File()));
    }

    [Fact]
    public void TryOfferRefusesASecondFileWhileOneIsPending()
    {
        var trigger = new TransferImportTrigger();
        trigger.TryOffer(File());

        Assert.False(trigger.TryOffer(File()));
    }

    [Fact]
    public async Task TryOfferRefusesWhileTheFirstIsRunningEvenAfterWaitAsyncPicksItUp()
    {
        var trigger = new TransferImportTrigger();
        trigger.TryOffer(File());
        await trigger.WaitAsync(CancellationToken.None); // picked up; still "running" until Release

        Assert.False(trigger.TryOffer(File()));
    }

    [Fact]
    public async Task TryOfferAcceptsAgainOnceTheSlotIsReleased()
    {
        var trigger = new TransferImportTrigger();
        trigger.TryOffer(File());
        await trigger.WaitAsync(CancellationToken.None);
        trigger.Release();

        Assert.True(trigger.TryOffer(File()));
    }

    [Fact]
    public async Task WaitAsyncReturnsTheOfferedFile()
    {
        var trigger = new TransferImportTrigger();
        var file = File();
        trigger.TryOffer(file);

        var received = await trigger.WaitAsync(CancellationToken.None);

        Assert.Same(file, received);
    }
}
