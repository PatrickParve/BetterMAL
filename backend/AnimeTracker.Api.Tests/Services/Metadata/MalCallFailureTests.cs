using System.Net;
using System.Net.Http;
using System.Text.Json;
using AnimeTracker.Api.Services.Metadata;

namespace AnimeTracker.Api.Tests.Services.Metadata;

// MalCallFailure (design D2): classifies a failed MAL call for the scheduled
// refresh job's pass-ending and not-found handling — local to that job, not
// a general-purpose classifier for every IMalClient caller.
public class MalCallFailureTests
{
    [Fact]
    public void Classify_NoResponseIsOutage() =>
        Assert.Equal(MalCallFailureKind.Outage, MalCallFailure.Classify(new HttpRequestException("no response")));

    [Fact]
    public void Classify_HttpIOExceptionIsOutage() =>
        Assert.Equal(
            MalCallFailureKind.Outage,
            MalCallFailure.Classify(new HttpIOException(HttpRequestError.ResponseEnded, "connection dropped mid-body")));

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Forbidden)]
    public void Classify_OutageStatusesAreOutage(HttpStatusCode status) =>
        Assert.Equal(MalCallFailureKind.Outage, MalCallFailure.Classify(new HttpRequestException("failed", null, status)));

    [Fact]
    public void Classify_TimeoutIsOutage() =>
        Assert.Equal(
            MalCallFailureKind.Outage,
            MalCallFailure.Classify(new TaskCanceledException("timed out", new TimeoutException())));

    [Fact]
    public void Classify_AnimeMetadataNotFoundExceptionIsNotFound() =>
        Assert.Equal(MalCallFailureKind.NotFound, MalCallFailure.Classify(new AnimeMetadataNotFoundException(1)));

    [Fact]
    public void Classify_404IsNotFound() =>
        Assert.Equal(
            MalCallFailureKind.NotFound,
            MalCallFailure.Classify(new HttpRequestException("not found", null, HttpStatusCode.NotFound)));

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public void Classify_OtherStatusesAreOther(HttpStatusCode status) =>
        Assert.Equal(MalCallFailureKind.Other, MalCallFailure.Classify(new HttpRequestException("failed", null, status)));

    [Fact]
    public void Classify_UnreadableBodyIsOther() =>
        Assert.Equal(MalCallFailureKind.Other, MalCallFailure.Classify(new JsonException("invalid body")));

    [Fact]
    public void Classify_InvalidOperationExceptionIsOther() =>
        Assert.Equal(MalCallFailureKind.Other, MalCallFailure.Classify(new InvalidOperationException()));
}
