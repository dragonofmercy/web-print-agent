using System.Collections.Concurrent;
using System.Threading.Channels;
using PrintAgent.Protocol;
using PrintAgent.Protocol.Events;

namespace PrintAgent.Printing;

public sealed class PrintJobService : IDisposable
{
    private readonly JobEventPublisher _publisher;
    private readonly ISumatraRunner _runner;
    private readonly string _tempDirectory;
    private readonly int _maxJobsPerConnection;
    private readonly ConcurrentDictionary<Guid, int> _activeJobsByConnection = new();
    private readonly ConcurrentDictionary<Guid, JobState> _jobs = new();
    private readonly Channel<PrintJob> _queue = Channel.CreateUnbounded<PrintJob>();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _workerTask;

    public PrintJobService(JobEventPublisher publisher, ISumatraRunner runner, string tempDirectory, int maxJobsPerConnection)
    {
        _publisher = publisher;
        _runner = runner;
        _tempDirectory = tempDirectory;
        _maxJobsPerConnection = maxJobsPerConnection;
        Directory.CreateDirectory(_tempDirectory);
        _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token));
    }

    public async Task<Guid> SubmitAsync(string printerName, byte[] pdfBytes, PrintOptions options, Guid connectionId, CancellationToken ct)
    {
        if (pdfBytes.Length < 5 || pdfBytes[0] != (byte)'%' || pdfBytes[1] != (byte)'P' || pdfBytes[2] != (byte)'D' || pdfBytes[3] != (byte)'F' || pdfBytes[4] != (byte)'-')
            throw new ArgumentException("PdfDecodeFailed: payload is not a valid PDF.");

        var current = _activeJobsByConnection.AddOrUpdate(connectionId, 1, (_, n) => n + 1);
        if (current > _maxJobsPerConnection)
        {
            _activeJobsByConnection.AddOrUpdate(connectionId, 0, (_, n) => Math.Max(0, n - 1));
            throw new RpcApplicationException(JsonRpcErrorCodes.RateLimited,
                "Too many concurrent print jobs for this connection.");
        }

        var jobId = Guid.NewGuid();
        var pdfPath = Path.Combine(_tempDirectory, $"printagent-{jobId:N}.pdf");
        await File.WriteAllBytesAsync(pdfPath, pdfBytes, ct);

        var job = new PrintJob(jobId, printerName, pdfPath, options, connectionId);
        _jobs[jobId] = new JobState { Status = JobStatus.Submitted };

        await _queue.Writer.WriteAsync(job, ct);
        await _publisher.PublishAsync(connectionId, new JobEvent(jobId, JobStatus.Submitted), ct);
        return jobId;
    }

    public JobStatus? GetStatus(Guid jobId, out string? error)
    {
        error = null;
        if (!_jobs.TryGetValue(jobId, out var state)) return null;
        error = state.Error;
        return state.Status;
    }

    public async Task WaitForJobCompletionAsync(Guid jobId, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_jobs.TryGetValue(jobId, out var state)
                && (state.Status == JobStatus.Completed || state.Status == JobStatus.Failed))
                return;
            await Task.Delay(20);
        }
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        await foreach (var job in _queue.Reader.ReadAllAsync(ct))
        {
            await ProcessJobAsync(job, ct);
        }
    }

    private async Task ProcessJobAsync(PrintJob job, CancellationToken ct)
    {
        try
        {
            _jobs[job.JobId] = _jobs[job.JobId] with { Status = JobStatus.Printing };
            await _publisher.PublishAsync(job.SubmittingConnectionId,
                new JobEvent(job.JobId, JobStatus.Printing), ct);

            var result = await _runner.RunAsync(job.PrinterName, job.PdfPath, job.Options, ct);

            if (result.ExitCode == 0 && string.IsNullOrWhiteSpace(result.StandardError))
            {
                _jobs[job.JobId] = _jobs[job.JobId] with { Status = JobStatus.Completed };
                await _publisher.PublishAsync(job.SubmittingConnectionId,
                    new JobEvent(job.JobId, JobStatus.Completed), ct);
            }
            else
            {
                var msg = string.IsNullOrWhiteSpace(result.StandardError)
                    ? $"SumatraPDF exited with code {result.ExitCode}"
                    : result.StandardError.Trim();
                _jobs[job.JobId] = _jobs[job.JobId] with { Status = JobStatus.Failed, Error = msg };
                await _publisher.PublishAsync(job.SubmittingConnectionId,
                    new JobEvent(job.JobId, JobStatus.Failed, msg), ct);
            }
        }
        catch (Exception ex)
        {
            _jobs[job.JobId] = _jobs[job.JobId] with { Status = JobStatus.Failed, Error = ex.Message };
            await _publisher.PublishAsync(job.SubmittingConnectionId,
                new JobEvent(job.JobId, JobStatus.Failed, ex.Message), ct);
        }
        finally
        {
            try { File.Delete(job.PdfPath); } catch { /* ignore */ }
            _activeJobsByConnection.AddOrUpdate(job.SubmittingConnectionId, 0, (_, n) => Math.Max(0, n - 1));
        }
    }

    public void Dispose()
    {
        _queue.Writer.TryComplete();
        _cts.Cancel();
        try { _workerTask.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts.Dispose();
    }

    private sealed record JobState
    {
        public JobStatus Status { get; init; }
        public string? Error { get; init; }
    }
}
