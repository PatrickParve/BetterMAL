using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Services.Setup;
using AnimeTracker.Api.Tests.Services.Setup;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Tests.Controllers;

// add-first-run-setup task 5.2: the two endpoints the request gate lets through while
// setup runs. (That the POST needs the X-Requested-With header is the cross-site
// guard's, in front of every mutating route: CrossSiteRequestGuardTests.)
public class SetupControllerTests
{
    [Fact]
    public async Task StatusServesTheStatusRead()
    {
        using var f = new SetupStatusFixture();
        f.MalOptions.ClientId = "";
        var controller = new SetupController(f.Service, f.Coordinator);

        var result = await controller.Status(CancellationToken.None);

        var dto = Assert.IsType<SetupStatusDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(["MAL_CLIENT_ID"], dto.MissingCredentials);
    }

    [Fact]
    public void RetryNowCallsTheCoordinatorAndAnswers204()
    {
        using var f = new SetupStatusFixture();
        var controller = new SetupController(f.Service, f.Coordinator);

        var result = controller.RetryNow();

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(1, f.Coordinator.RetryNowCalls);
    }
}
