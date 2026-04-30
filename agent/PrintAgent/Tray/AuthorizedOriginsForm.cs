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
            FullRowSelect = true,
            MultiSelect = true,
            HideSelection = false,
            GridLines = false,
            Dock = DockStyle.Fill,
        };
        _listView.Columns.Add(Strings.OriginsHeaderOrigin, -2, HorizontalAlignment.Left);
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

        var buttonRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            Height = 48,
            Padding = new Padding(12, 6, 12, 12),
            WrapContents = false,
        };
        buttonRow.Controls.Add(btnClose);
        buttonRow.Controls.Add(_btnRemoveAll);
        buttonRow.Controls.Add(_btnRemoveSelected);
        buttonRow.Controls.Add(btnRefresh);

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
