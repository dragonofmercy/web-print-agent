using System.Text.Json;
using PrintAgent.Printing;

namespace PrintAgent.Protocol.Handlers;

public sealed class GetJobStatusHandler : IRpcHandler
{
    public string Method => "getJobStatus";
    public bool RequiresPairedConnection => true;

    private readonly IPrintJobSubmitter _jobs;

    public GetJobStatusHandler(IPrintJobSubmitter jobs) => _jobs = jobs;

    public Task<object?> HandleAsync(JsonElement? @params, ConnectionContext connection, CancellationToken ct)
    {
        if (@params is null || !@params.Value.TryGetProperty("jobId", out var idEl) || idEl.ValueKind != JsonValueKind.String)
            throw new ArgumentException("Missing or invalid 'jobId'.");

        if (!Guid.TryParse(idEl.GetString(), out var jobId))
            throw new ArgumentException("Invalid jobId format.");

        var status = _jobs.GetStatus(jobId, out var error);
        if (status is null)
            throw new RpcApplicationException(JsonRpcErrorCodes.JobNotFound, $"Job {jobId} not found.");

        return Task.FromResult<object?>(new { status = status.Value.ToString(), error });
    }
}
