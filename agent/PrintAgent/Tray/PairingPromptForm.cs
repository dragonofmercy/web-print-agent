using System.Drawing;
using System.Windows.Forms;

namespace PrintAgent.Tray;

internal sealed class PairingPromptForm : Form
{
    public bool? Decision { get; private set; }

    public PairingPromptForm(string origin)
    {
        Text = "PrintAgent — autorisation";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 160);
        TopMost = true;

        var label = new Label
        {
            AutoSize = false,
            Location = new Point(16, 16),
            Size = new Size(388, 70),
            Text = $"\"{origin}\"\nveut accéder à vos imprimantes locales."
        };

        var allow = new Button
        {
            Text = "Autoriser",
            Location = new Point(220, 110),
            Size = new Size(90, 28),
            DialogResult = DialogResult.Yes
        };
        allow.Click += (_, _) => { Decision = true; Close(); };

        var refuse = new Button
        {
            Text = "Refuser",
            Location = new Point(316, 110),
            Size = new Size(90, 28),
            DialogResult = DialogResult.No
        };
        refuse.Click += (_, _) => { Decision = false; Close(); };

        Controls.Add(label);
        Controls.Add(allow);
        Controls.Add(refuse);

        AcceptButton = allow;
        CancelButton = refuse;
    }
}
