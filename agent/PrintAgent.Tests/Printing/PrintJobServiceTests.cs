using FluentAssertions;
using NSubstitute;
using PrintAgent.Printing;
using PrintAgent.Protocol.Events;
using PrintAgent.Tests.Helpers;
using Xunit;

namespace PrintAgent.Tests.Printing;

public class PrintJobServiceTests
{
    private static byte[] MinimalPdfBytes()
    {
        // Smallest reasonable PDF that begins with %PDF- and has %%EOF.
        return System.Text.Encoding.ASCII.GetBytes(
            "%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n" +
            "2 0 obj<</Type/Pages/Count 0/Kids[]>>endobj\n" +
            "trailer<</Root 1 0 R>>\n%%EOF\n");
    }

    private sealed class FakeRunner : ISumatraRunner
    {
        public int ExitCode { get; set; } = 0;
        public string Stderr { get; set; } = string.Empty;
        public List<string> RunsRecorded { get; } = new();

        public Task<SumatraPdfRunner.RunResult> RunAsync(string printer, string pdf, PrintOptions opts, CancellationToken ct)
        {
            RunsRecorded.Add(pdf);
            return Task.FromResult(new SumatraPdfRunner.RunResult(ExitCode, Stderr));
        }
    }

    [Fact]
    public async Task Submit_ValidPdf_WritesTempFileAndQueuesJobAndReturnsJobId()
    {
        using var temp = new TempDirectory();
        var publisher = new JobEventPublisher();
        var runner = new FakeRunner();
        var svc = new PrintJobService(publisher, runner, tempDirectory: temp.Path, maxJobsPerConnection: 5, maxQueuedJobs: 100);

        var jobId = await svc.SubmitAsync("HP", MinimalPdfBytes(), new PrintOptions(),
            connectionId: Guid.NewGuid(), CancellationToken.None);

        jobId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Submit_NotPdfMagicBytes_ThrowsArgumentExceptionWithDecodeMessage()
    {
        using var temp = new TempDirectory();
        var svc = new PrintJobService(new JobEventPublisher(), new FakeRunner(),
            tempDirectory: temp.Path, maxJobsPerConnection: 5, maxQueuedJobs: 100);

        var act = () => svc.SubmitAsync("HP", new byte[] { 0x00, 0x01, 0x02 }, new PrintOptions(),
            Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*PDF*");
    }

    [Fact]
    public async Task Submit_ExceedsMaxConcurrentJobs_ThrowsRpcApplicationExceptionRateLimited()
    {
        using var temp = new TempDirectory();
        var publisher = new JobEventPublisher();
        var runner = new HangingRunner();
        var svc = new PrintJobService(publisher, runner, tempDirectory: temp.Path, maxJobsPerConnection: 2, maxQueuedJobs: 100);
        var conn = Guid.NewGuid();

        await svc.SubmitAsync("HP", MinimalPdfBytes(), new PrintOptions(), conn, CancellationToken.None);
        await svc.SubmitAsync("HP", MinimalPdfBytes(), new PrintOptions(), conn, CancellationToken.None);

        var act = () => svc.SubmitAsync("HP", MinimalPdfBytes(), new PrintOptions(), conn, CancellationToken.None);

        await act.Should().ThrowAsync<PrintAgent.Protocol.RpcApplicationException>()
            .Where(e => e.Code == PrintAgent.Protocol.JsonRpcErrorCodes.RateLimited);
    }

    [Fact]
    public async Task RunOneJob_RunnerSucceeds_EmitsSubmittedPrintingCompletedAndDeletesTempFile()
    {
        using var temp = new TempDirectory();
        var events = new List<JobEvent>();
        var publisher = new JobEventPublisher();
        var conn = Guid.NewGuid();
        using var sub = publisher.Subscribe(conn, (ev, _) => { events.Add(ev); return Task.CompletedTask; });
        var runner = new FakeRunner();
        var svc = new PrintJobService(publisher, runner, tempDirectory: temp.Path, maxJobsPerConnection: 5, maxQueuedJobs: 100);

        var jobId = await svc.SubmitAsync("HP", MinimalPdfBytes(), new PrintOptions(),
            conn, CancellationToken.None);

        await svc.WaitForJobCompletionAsync(jobId, TimeSpan.FromSeconds(2));

        events.Select(e => e.Status).Should()
            .ContainInOrder(JobStatus.Submitted, JobStatus.Printing, JobStatus.Completed);

        runner.RunsRecorded.Should().ContainSingle();
        File.Exists(runner.RunsRecorded[0]).Should().BeFalse();
    }

    [Fact]
    public async Task RunOneJob_RunnerFails_EmitsFailedWithErrorAndDeletesTempFile()
    {
        using var temp = new TempDirectory();
        var events = new List<JobEvent>();
        var publisher = new JobEventPublisher();
        var conn = Guid.NewGuid();
        using var sub = publisher.Subscribe(conn, (ev, _) => { events.Add(ev); return Task.CompletedTask; });
        var runner = new FakeRunner { ExitCode = 1, Stderr = "spool error" };
        var svc = new PrintJobService(publisher, runner, tempDirectory: temp.Path, maxJobsPerConnection: 5, maxQueuedJobs: 100);

        var jobId = await svc.SubmitAsync("HP", MinimalPdfBytes(), new PrintOptions(),
            conn, CancellationToken.None);

        await svc.WaitForJobCompletionAsync(jobId, TimeSpan.FromSeconds(2));

        events.Last().Status.Should().Be(JobStatus.Failed);
        events.Last().Error.Should().Contain("spool error");
    }

    private sealed class HangingRunner : ISumatraRunner
    {
        public Task<SumatraPdfRunner.RunResult> RunAsync(string printer, string pdf, PrintOptions opts, CancellationToken ct)
            => Task.Delay(Timeout.Infinite, ct).ContinueWith(_ => new SumatraPdfRunner.RunResult(0, ""));
    }
}
