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
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            Text = "Manage Search Folders";
            StartPosition = FormStartPosition.CenterParent;
            
            // Narrowed the default and minimum width for a more compact look
            Width = 420;
            Height = 360;
            MinimumSize = new Size(300, 300);

            var hint = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(12, 8, 12, 4),
                Text = "These folders are scanned for Designer installs,\n" + 
                       "in addition to the default QDS install locations."
            };

            _listBox = new ListBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(12),
                IntegralHeight = false
            };
            _listBox.SelectedIndexChanged += (s, e) => UpdateRemoveEnabled();

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(12, 8, 12, 12)
            };

            // Adjusted margins to bring Remove and Close closer together cleanly
            _btnAdd = new Button { Text = "Add...", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
            _btnRemove = new Button { Text = "Remove", AutoSize = true, Margin = new Padding(0, 0, 16, 0) };
            _btnClose = new Button { Text = "Close", AutoSize = true };

            _btnAdd.Click += (s, e) => AddFolder();
            _btnRemove.Click += (s, e) => RemoveSelected();
            _btnClose.Click += (s, e) => Close();

            buttonPanel.Controls.Add(_btnAdd);
            buttonPanel.Controls.Add(_btnRemove);
            buttonPanel.Controls.Add(_btnClose);

            CancelButton = _btnClose;

            // Add in reverse dock order: Fill -> Bottom -> Top
            Controls.Add(_listBox);
            Controls.Add(buttonPanel);
            Controls.Add(hint);
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