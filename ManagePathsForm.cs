using System;
using System.Drawing;
using System.Windows.Forms;

namespace QDSVersionLauncher
{
    /// <summary>
    /// Small dialog for viewing, adding, and removing the extra folders
    /// DesignerScanner scans in addition to the default QDS locations
    /// (Settings.CustomScanPaths). Edits the list in place and saves
    /// immediately on each change; MainForm re-scans after this closes.
    /// </summary>
    public class ManagePathsForm : Form
    {
        private readonly Settings _settings;
        private ListBox _listBox;
        private Button _btnAdd;
        private Button _btnRemove;
        private Button _btnClose;

        public ManagePathsForm(Settings settings)
        {
            _settings = settings;
            BuildUi();
            PopulateList();
        }

        private void BuildUi()
        {
            Text = "Manage Search Folders";
            StartPosition = FormStartPosition.CenterParent;
            Width = 460;
            Height = 340;
            MinimumSize = new Size(360, 260);

            var hint = new Label
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(12, 8, 12, 0),
                Text = "These folders are scanned for Designer installs, in addition to the default QDS install locations."
            };

            _listBox = new ListBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(12),
                IntegralHeight = false
            };
            _listBox.SelectedIndexChanged += (s, e) => UpdateRemoveEnabled();

            var buttonPanel = new Panel { Dock = DockStyle.Bottom, Height = 48 };
            _btnAdd = new Button { Text = "Add...", Width = 90 };
            _btnRemove = new Button { Text = "Remove", Width = 90 };
            _btnClose = new Button { Text = "Close", Width = 90 };

            _btnAdd.Click += (s, e) => AddFolder();
            _btnRemove.Click += (s, e) => RemoveSelected();
            _btnClose.Click += (s, e) => Close();

            buttonPanel.Controls.Add(_btnAdd);
            buttonPanel.Controls.Add(_btnRemove);
            buttonPanel.Controls.Add(_btnClose);

            void LayoutButtons(object sender, EventArgs e)
            {
                const int margin = 12;
                const int gap = 8;
                int right = buttonPanel.ClientSize.Width - margin;

                _btnClose.Location = new Point(right - _btnClose.Width, 8);
                right -= _btnClose.Width + gap;

                _btnRemove.Location = new Point(right - _btnRemove.Width, 8);

                _btnAdd.Location = new Point(margin, 8);
            }
            buttonPanel.Resize += LayoutButtons;

            CancelButton = _btnClose;

            // Same Fill -> Bottom -> Top add order as MainForm (WinForms
            // docks in reverse of add order).
            Controls.Add(_listBox);
            Controls.Add(buttonPanel);
            Controls.Add(hint);

            LayoutButtons(this, EventArgs.Empty);
        }

        private void PopulateList()
        {
            _listBox.Items.Clear();
            foreach (var path in _settings.CustomScanPaths)
                _listBox.Items.Add(path);

            UpdateRemoveEnabled();
        }

        private void UpdateRemoveEnabled()
            => _btnRemove.Enabled = _listBox.SelectedIndex >= 0;

        private void AddFolder()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select a folder that contains a Q-SYS Designer installation"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            if (!_settings.CustomScanPaths.Contains(dialog.SelectedPath, StringComparer.OrdinalIgnoreCase))
            {
                _settings.CustomScanPaths.Add(dialog.SelectedPath);
                _settings.Save();
                PopulateList();
            }
        }

        private void RemoveSelected()
        {
            if (_listBox.SelectedItem is not string path)
                return;

            _settings.CustomScanPaths.Remove(path);
            _settings.Save();
            PopulateList();
        }
    }
}
