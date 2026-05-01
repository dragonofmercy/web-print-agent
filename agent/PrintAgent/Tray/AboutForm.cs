using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using PrintAgent.Localization;

namespace PrintAgent.Tray;

internal sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = Strings.AboutTitle;
        Icon = Icons.LoadFull();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(360, 200);

        var iconBox = new PictureBox
        {
            Image = Icons.LoadAt(new Size(48, 48))?.ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(20, 20),
            Size = new Size(48, 48),
        };

        var nameLabel = new Label
        {
            Text = Strings.AppName,
            Font = new Font(Font.FontFamily, 12, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(84, 22),
        };

        var versionLabel = new Label
        {
            Text = Strings.AboutVersion(AppInfo.Version),
            AutoSize = true,
            Location = new Point(84, 50),
        };

        var copyright = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;
        var copyLabel = new Label
        {
            Text = copyright,
            AutoSize = false,
            Location = new Point(20, 96),
            Size = new Size(320, 40),
            ForeColor = SystemColors.GrayText,
        };

        var closeBtn = new Button
        {
            Text = Strings.OriginsClose,
            Location = new Point(254, 152),
            Size = new Size(86, 28),
            DialogResult = DialogResult.Cancel,
        };
        closeBtn.Click += (_, _) => Close();

        Controls.Add(iconBox);
        Controls.Add(nameLabel);
        Controls.Add(versionLabel);
        Controls.Add(copyLabel);
        Controls.Add(closeBtn);

        AcceptButton = closeBtn;
        CancelButton = closeBtn;
    }
}
