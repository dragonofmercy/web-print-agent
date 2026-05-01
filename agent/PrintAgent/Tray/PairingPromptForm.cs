using System.Drawing;
using System.Windows.Forms;
using PrintAgent.Localization;

namespace PrintAgent.Tray;

public sealed class PairingPromptForm : Form
{
    public bool? Decision { get; private set; }

    public static string FormatOriginForDisplay(string origin)
    {
        // Strip C0/C1/format/bidi-override codepoints first so they can never reach the label.
        var stripped = new string((origin ?? string.Empty).Where(c =>
        {
            var cat = char.GetUnicodeCategory(c);
            return cat != System.Globalization.UnicodeCategory.Control
                && cat != System.Globalization.UnicodeCategory.Format
                && cat != System.Globalization.UnicodeCategory.Surrogate;
        }).ToArray());

        if (Uri.TryCreate(stripped, UriKind.Absolute, out var uri))
        {
            try
            {
                var puny = new System.Globalization.IdnMapping().GetAscii(uri.Host);
                var defaultPort = uri.Scheme == Uri.UriSchemeHttps ? 443 : 80;
                return uri.Port == defaultPort
                    ? $"{uri.Scheme}://{puny}"
                    : $"{uri.Scheme}://{puny}:{uri.Port}";
            }
            catch (ArgumentException)
            {
                /* fall through */
            }
        }
        return stripped;
    }

    public PairingPromptForm(string origin)
    {
        var displayOrigin = FormatOriginForDisplay(origin);
        Text = Strings.PairingTitle;
        Icon = Icons.LoadFull();
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
            Text = Strings.PairingMessage(displayOrigin)
        };

        var allow = new Button
        {
            Text = Strings.PairingAllow,
            Location = new Point(220, 110),
            Size = new Size(90, 28),
            DialogResult = DialogResult.Yes
        };
        allow.Click += (_, _) => { Decision = true; Close(); };

        var refuse = new Button
        {
            Text = Strings.PairingRefuse,
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
