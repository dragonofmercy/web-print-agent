using System.Drawing;
using System.Windows.Forms;
using PrintAgent.Localization;
using PrintAgent.Storage;

namespace PrintAgent.Tray;

internal sealed class AuthorizedOriginsForm : Form
{
    private readonly ConfigStore _configStore;
    private readonly ListView _listView;
    private readonly Label _emptyLabel;
    private readonly Button _btnRemoveSelected;
    private readonly Button _btnRemoveAll;

    public AuthorizedOriginsForm(ConfigStore configStore)
    {
        _configStore = configStore;

        Text = Strings.OriginsTitle;
        Icon = Icons.LoadFull();
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(420, 280);
        ClientSize = new Size(560, 380);
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = true;
        MinimizeBox = true;
        ShowInTaskbar = true;

        _listView = new ListView
        {
            View = View.Details,
            HeaderStyle = ColumnHeaderStyle.None,
            FullRowSelect = true,
            MultiSelect = true,
            HideSelection = false,
            GridLines = false,
            Dock = DockStyle.Fill,
        };
        _listView.Columns.Add(string.Empty, -2, HorizontalAlignment.Left);
        _listView.SelectedIndexChanged += (_, _) => UpdateButtonState();

        _emptyLabel = new Label
        {
            Text = Strings.OriginsEmpty,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(24),
            ForeColor = SystemColors.GrayText,
            Visible = false,
        };

        var listContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 12, 12, 6) };
        listContainer.Controls.Add(_listView);
        listContainer.Controls.Add(_emptyLabel);

        var btnRefresh = new Button
        {
            Text = Strings.OriginsRefresh,
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4),
        };
        btnRefresh.Click += (_, _) => Reload();

        _btnRemoveSelected = new Button
        {
            Text = Strings.OriginsRemoveSelected,
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4),
        };
        _btnRemoveSelected.Click += (_, _) => RemoveSelected();

        _btnRemoveAll = new Button
        {
            Text = Strings.OriginsRemoveAll,
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4),
        };
        _btnRemoveAll.Click += (_, _) => RemoveAll();

        var btnClose = new Button
        {
            Text = Strings.OriginsClose,
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4),
            DialogResult = DialogResult.Cancel,
        };
        btnClose.Click += (_, _) => Close();

        // Refresh on the left (secondary), destructive + close on the right.
        // Both groups align with the inner edges of the ListView (same 12 px gutter).
        var leftButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Left,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0),
        };
        leftButtons.Controls.Add(btnRefresh);

        var rightButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0),
        };
        rightButtons.Controls.Add(btnClose);
        rightButtons.Controls.Add(_btnRemoveAll);
        rightButtons.Controls.Add(_btnRemoveSelected);

        var buttonRow = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            Padding = new Padding(12, 6, 12, 12),
        };
        buttonRow.Controls.Add(leftButtons);
        buttonRow.Controls.Add(rightButtons);

        Controls.Add(listContainer);
        Controls.Add(buttonRow);

        CancelButton = btnClose;

        Load += (_, _) => Reload();
        Resize += (_, _) => ResizeColumn();
    }

    private void Reload()
    {
        _listView.BeginUpdate();
        try
        {
            _listView.Items.Clear();
            foreach (var origin in _configStore.GetAllowedOrigins())
                _listView.Items.Add(new ListViewItem(origin));
        }
        finally
        {
            _listView.EndUpdate();
        }

        var hasItems = _listView.Items.Count > 0;
        _listView.Visible = hasItems;
        _emptyLabel.Visible = !hasItems;
        ResizeColumn();
        UpdateButtonState();
    }

    private void ResizeColumn()
    {
        if (_listView.Columns.Count > 0)
            _listView.Columns[0].Width = Math.Max(200, _listView.ClientSize.Width - 4);
    }

    private void UpdateButtonState()
    {
        _btnRemoveSelected.Enabled = _listView.SelectedItems.Count > 0;
        _btnRemoveAll.Enabled = _listView.Items.Count > 0;
    }

    private void RemoveSelected()
    {
        var selected = _listView.SelectedItems.Cast<ListViewItem>().Select(i => i.Text).ToList();
        if (selected.Count == 0) return;

        var result = MessageBox.Show(
            this,
            Strings.OriginsConfirmRemove(selected.Count),
            "PrintAgent",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (result != DialogResult.Yes) return;

        foreach (var origin in selected)
            _configStore.RemoveAllowedOrigin(origin);
        Reload();
    }

    private void RemoveAll()
    {
        if (_listView.Items.Count == 0) return;

        var result = MessageBox.Show(
            this,
            Strings.OriginsConfirmRemoveAll,
            "PrintAgent",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;

        _configStore.ClearAllowedOrigins();
        Reload();
    }
}
