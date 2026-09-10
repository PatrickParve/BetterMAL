using AnimeTracker.Api.Services.Transfer;

namespace AnimeTracker.Api.Tests.Services.Transfer;

// device-transfer's device.name (design.md D6, tasks.md 5.4): a pure,
// version-free "OS · Browser" read from a real User-Agent string, or one
// part alone, or null.
public class DeviceNameTests
{
    public static TheoryData<string?, string?> Cases => new()
    {
        {
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Safari/605.1.15",
            "macOS · Safari"
        },
        {
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
            "macOS · Chrome"
        },
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36 Edg/124.0.0.0",
            "Windows · Edge"
        },
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:125.0) Gecko/20100101 Firefox/125.0",
            "Windows · Firefox"
        },
        {
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Mobile/15E148 Safari/604.1",
            "iOS · Safari"
        },
        {
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/124.0.6367.111 Mobile/15E148 Safari/604.1",
            "iOS · Chrome"
        },
        {
            "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Mobile Safari/537.36",
            "Android · Chrome"
        },
        {
            "Mozilla/5.0 (X11; Linux x86_64; rv:125.0) Gecko/20100101 Firefox/125.0",
            "Linux · Firefox"
        },
        { "SomeUnknownBot/1.0 (+https://example.com/bot)", null },
        { null, null },
        { "", null },
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            "Windows"
        },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void DescribeMatchesTheExpectedName(string? userAgent, string? expected)
    {
        Assert.Equal(expected, DeviceName.Describe(userAgent));
    }
}
