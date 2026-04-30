namespace PrintAgent.Security;

public enum PairingDecision { Approved, Refused, TimedOut }

public interface IPairingUi
{
    Task<PairingDecision> PromptAsync(string origin, TimeSpan timeout, CancellationToken cancellationToken);
}
