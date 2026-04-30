using System.Windows.Forms;
using PrintAgent.Security;

namespace PrintAgent.Tray;

public sealed class WinFormsPairingUi : IPairingUi
{
    private readonly Control _uiThread;

    public WinFormsPairingUi(Control uiThreadAnchor) => _uiThread = uiThreadAnchor;

    public Task<PairingDecision> PromptAsync(string origin, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<PairingDecision>(TaskCreationOptions.RunContinuationsAsynchronously);

        _uiThread.BeginInvoke(new Action(() =>
        {
            using var form = new PairingPromptForm(origin);
            using var timer = new System.Windows.Forms.Timer { Interval = (int)timeout.TotalMilliseconds };
            timer.Tick += (_, _) => { form.Close(); };
            timer.Start();

            form.ShowDialog();

            if (form.Decision == true) tcs.TrySetResult(PairingDecision.Approved);
            else if (form.Decision == false) tcs.TrySetResult(PairingDecision.Refused);
            else tcs.TrySetResult(PairingDecision.TimedOut);
        }));

        cancellationToken.Register(() => tcs.TrySetResult(PairingDecision.TimedOut));
        return tcs.Task;
    }
}
