using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using PrintAgent.Printing;
using PrintAgent.Protocol;
using PrintAgent.Protocol.Handlers;
using Xunit;

namespace PrintAgent.Tests.Protocol;

public class GetJobStatusHandlerTests
{
    [Fact]
    public async Task Handle_KnownJob_ReturnsStatusName()
    {
        var jobs = Substitute.For<IPrintJobSubmitter>();
        var jobId = Guid.NewGuid();
        string? err;
        jobs.GetStatus(jobId, out err).Returns(call => { call[1] = null; return JobStatus.Completed; });
        var handler = new GetJobStatusHandler(jobs);
        var paramsJson = JsonSerializer.SerializeToElement(new { jobId = jobId.ToString() });

        var result = await handler.HandleAsync(paramsJson, new ConnectionContext { IsPaired = true }, CancellationToken.None);

        var json = JsonSerializer.SerializeToElement(result);
        json.GetProperty("status").GetString().Should().Be("Completed");
    }

    [Fact]
    public async Task Handle_UnknownJob_ThrowsRpcAppExceptionJobNotFound()
    {
        var jobs = Substitute.For<IPrintJobSubmitter>();
        string? err = null;
        jobs.GetStatus(Arg.Any<Guid>(), out err).Returns(call => { call[1] = null; return (JobStatus?)null; });
        var handler = new GetJobStatusHandler(jobs);
        var paramsJson = JsonSerializer.SerializeToElement(new { jobId = Guid.NewGuid().ToString() });

        var act = () => handler.HandleAsync(paramsJson, new ConnectionContext { IsPaired = true }, CancellationToken.None);

        await act.Should().ThrowAsync<RpcApplicationException>()
            .Where(e => e.Code == JsonRpcErrorCodes.JobNotFound);
    }
}
