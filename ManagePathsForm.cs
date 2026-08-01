using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace QDSVersionLauncher
{
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

            Icon appIcon = TryLoadIcon();
            if (appIcon != null)
                Icon = appIcon;

            Width = 380;
            Height = 360;
            MinimumSize = new Size(340, 300);

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
                Padding = new Padding(12, 8, 12, 12),
            };

            // Explicitly set Width and Height to ensure perfect alignment across all DPIs
            int btnWidth = 90;
            int btnHeight = 32;

            _btnAdd = new Button 
            { 
                Text = "Add...", 
                AutoSize = false, 
                Width = btnWidth, 
                Height = btnHeight,
                Margin = new Padding(0, 0, 8, 0) 
            };
            _btnRemove = new Button 
            { 
                Text = "Remove", 
                AutoSize = false, 
                Width = btnWidth, 
                Height = btnHeight,
                Margin = new Padding(0, 0, 16, 0) 
            };
            _btnClose = new Button 
            { 
                Text = "Close", 
                AutoSize = false, 
                Width = btnWidth, 
                Height = btnHeight,
                Margin = new Padding(0, 0, 0, 0)
            };

            _btnAdd.Click += (s, e) => AddFolder();
            _btnRemove.Click += (s, e) => RemoveSelected();
            _btnClose.Click += (s, e) => Close();

            buttonPanel.Controls.Add(_btnAdd);
            buttonPanel.Controls.Add(_btnRemove);
            buttonPanel.Controls.Add(_btnClose);

            CancelButton = _btnClose;

            Controls.Add(_listBox);
            Controls.Add(buttonPanel);
            Controls.Add(hint);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (this.Owner != null)
            {
                this.StartPosition = FormStartPosition.Manual;

                int offsetX = (this.Owner.Width - this.Width) / 2;
                int offsetY = (this.Owner.Height - this.Height) / 2 - 40;

                if (offsetY < 0)
                    offsetY = 0;

                this.Location = new Point(this.Owner.Left + offsetX, this.Owner.Top + offsetY);
            }
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

        private static Icon TryLoadIcon()
        {
            try
            {
                // Prefer app.ico next to the EXE; fallback to extracting from the EXE itself.
                string appIconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
                if (File.Exists(appIconPath))
                    return new Icon(appIconPath);

                string exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    return Icon.ExtractAssociatedIcon(exePath);
            }
            catch
            {
                // Final fallback
                try
                {
                    string legacyIconPath = Path.Combine(AppContext.BaseDirectory, "qdv.ico");
                    return File.Exists(legacyIconPath) ? new Icon(legacyIconPath) : null;
                }
                catch { }
            }
            return null;
        }
    }
}