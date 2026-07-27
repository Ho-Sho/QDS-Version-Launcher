using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace QDSVersionLauncher
{
    /// <summary>
    /// The version-picker dialog shown when a .qsys file (or the EXE itself)
    /// is double-clicked. Shows every detected Designer install, with the
    /// most-recently-used one starred and pinned to the top.
    /// </summary>
    public class MainForm : Form
    {
        private readonly string _projectPath;
        private readonly Settings _settings;
        private readonly bool _forceSelection;
        private List<DesignerVersionInfo> _versions;

        private ListView _listView;
        private Button _btnOpen;
        private Button _btnRefresh;
        private Button _btnCancel;
        private Button _btnManagePaths;
        private Label _hintLabel;
        private CheckBox _chkSuppressPlugins;
        private Label _lblSuppressInfo;
        // Guards CheckedChanged so setting Checked to reflect a loaded
        // Settings value doesn't itself trigger a folder move.
        private bool _initializingSuppressCheckbox;

        public MainForm(string projectPath, List<DesignerVersionInfo> versions, Settings settings, bool forceSelection = false)
        {
            _projectPath = projectPath;
            _versions = versions;
            _settings = settings;
            _forceSelection = forceSelection;
            BuildUi();
            PopulateList();
        }

        private void BuildUi()
        {
            // Enable DPI auto-scaling. AutoScaleDimensions is set to the design-time DPI (96).
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            Text = "Q-SYS Launcher";
            StartPosition = FormStartPosition.CenterScreen;
            
            Width = Math.Max(320, _settings.WindowWidth);
            Height = Math.Max(480, _settings.WindowHeight);
            MinimumSize = new Size(320, 400);
            
            Icon appIcon = TryLoadIcon();
            if (appIcon != null)
                Icon = appIcon;

            // --- Header: which project this is for ---
            var headerPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(12, 6, 12, 6)
            };
            headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            headerPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            headerPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var captionLabel = new Label
            {
                Text = "Project",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 0, 0, 4)
            };
            var projectLabel = new Label
            {
                Text = string.IsNullOrEmpty(_projectPath) ? "(No project file selected)" : Path.GetFileName(_projectPath),
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 0)
            };
            headerPanel.Controls.Add(captionLabel, 0, 0);
            headerPanel.Controls.Add(projectLabel, 0, 1);

            // --- Hint shown only when Ctrl forced this dialog to appear ---
            _hintLabel = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(12, 4, 12, 4),
                ForeColor = Color.DarkOrange,
                Text = "Ctrl held: choose a version manually.",
                Visible = _forceSelection
            };

            // --- Version list ---
            _listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                GridLines = false,
                Margin = new Padding(12)
            };
            _listView.Columns.Add("Version", -1, HorizontalAlignment.Left);
            _listView.DoubleClick += (s, e) => OpenSelected();
            _listView.Resize += (s, e) => AutoSizeColumn();

            // --- Bottom buttons ---
            var buttonPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                ColumnCount = 4,
                RowCount = 2,
                Padding = new Padding(12, 8, 12, 12)
            };
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _btnOpen = new Button { Text = "Open", AutoSize = true, Margin = new Padding(0, 0, 8, 8) };
            _btnRefresh = new Button { Text = "Refresh", AutoSize = true, Margin = new Padding(0, 0, 8, 8) };
            _btnCancel = new Button { Text = "Cancel", AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
            _btnManagePaths = new Button { Text = "Manage Folders...", AutoSize = true, Margin = new Padding(0, 0, 0, 0) };

            _btnOpen.Click += (s, e) => OpenSelected();
            _btnRefresh.Click += (s, e) => RefreshVersions();
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            _btnManagePaths.Click += (s, e) => OpenManagePaths();

            buttonPanel.Controls.Add(_btnOpen, 0, 0);
            buttonPanel.Controls.Add(_btnRefresh, 1, 0);
            buttonPanel.Controls.Add(_btnCancel, 2, 0);
            buttonPanel.Controls.Add(_btnManagePaths, 0, 1);

            AcceptButton = _btnOpen;
            CancelButton = _btnCancel;

            // --- Suppress-plugin-folder toggle: checkbox + description/paths ---
            var suppressPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(12, 4, 12, 4)
            };
            suppressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            suppressPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            suppressPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _chkSuppressPlugins = new CheckBox
            {
                Text = "Suppress plugin folder",
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            var suppressToolTip = new ToolTip();
            suppressToolTip.SetToolTip(_chkSuppressPlugins,
                "While checked, Designer's Plugins and Assets folders are moved out of the way " +
                "(a temporary safe mode with no custom content loaded). Unchecking moves them back.");
            
            _lblSuppressInfo = new Label
            {
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Font = new Font(Font.FontFamily, 7.5f),
                Text =
                    "While checked, moves these folders aside and back when unchecked:\n" +
                    $"Plugins -> {PluginSuppression.PluginsHiddenDir}\n" +
                    $"Assets  -> {PluginSuppression.AssetsHiddenDir}",
                Margin = new Padding(0, 0, 0, 0)
            };

            suppressPanel.Controls.Add(_chkSuppressPlugins, 0, 0);
            suppressPanel.Controls.Add(_lblSuppressInfo, 0, 1);

            // Reflect the saved state without triggering a folder move --
            // Program.cs already reconciled the actual folders at startup.
            _initializingSuppressCheckbox = true;
            _chkSuppressPlugins.Checked = _settings.SuppressPlugins;
            _initializingSuppressCheckbox = false;
            _chkSuppressPlugins.CheckedChanged += (s, e) => OnSuppressPluginsCheckedChanged();

            // Add controls in reverse dock order (Fill -> Bottom -> Top(inner) -> Top(outer))
            Controls.Add(_listView);
            Controls.Add(suppressPanel);
            Controls.Add(buttonPanel);
            Controls.Add(_hintLabel);
            Controls.Add(headerPanel);

            // High DPI fix for ListView item height
            float scaleFactorY = CurrentAutoScaleDimensions.Height / 96f;
            _listView.SmallImageList = new ImageList 
            { 
                ImageSize = new Size(1, (int)(16 * scaleFactorY)) 
            };
        }

        private void PopulateList()
        {
            _listView.Items.Clear();
            if (_versions.Count == 0)
            {
                var empty = new ListViewItem("No Q-SYS Designer installations found.")
                {
                    ForeColor = SystemColors.GrayText
                };
                _listView.Items.Add(empty);
                _btnOpen.Enabled = false;
                return;
            }

            _btnOpen.Enabled = true;
            string mostRecent = _settings.GetMostRecentVersion();
            var recentMatch = _versions.FirstOrDefault(v => v.Version == mostRecent);
            var ordered = new List<DesignerVersionInfo>();
            if (recentMatch != null)
                ordered.Add(recentMatch);
            ordered.AddRange(_versions.Where(v => v != recentMatch));

            foreach (var v in ordered)
            {
                bool isRecent = v == recentMatch;
                string text = (isRecent ? "\u2605 " : "   ") + v.DisplayName;
                _listView.Items.Add(new ListViewItem(text) { Tag = v });
            }

            _listView.Items[0].Selected = true;
            _listView.Items[0].Focused = true;
            AutoSizeColumn();
        }

        private void AutoSizeColumn()
        {
            if (_listView.Columns.Count > 0)
                _listView.Columns[0].Width = Math.Max(100, _listView.ClientSize.Width - 4);
        }

        private void OpenSelected()
        {
            if (_listView.SelectedItems.Count == 0)
                return;
            if (_listView.SelectedItems[0].Tag is not DesignerVersionInfo info)
                return;

            if (!Launcher.TryLaunch(info.ExePath, _projectPath, out string error))
            {
                MessageBox.Show(this,
                    $"Failed to launch Q-SYS Designer {info.DisplayName}.\n\n{error}",
                    "Q-SYS Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _settings.SetRememberedVersion(_projectPath, info.Version);
            _settings.Save();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnSuppressPluginsCheckedChanged()
        {
            if (_initializingSuppressCheckbox)
                return;
            bool wantSuppressed = _chkSuppressPlugins.Checked;
            if (PluginSuppression.IsDesignerRunning())
            {
                MessageBox.Show(this,
                    "Please close all running Q-SYS Designer windows first, then try again.\n\n" +
                    "Moving the Plugins/Assets folders isn't safe to do while Designer already has them open.",
                    "Q-SYS Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                RevertSuppressCheckbox(!wantSuppressed);
                return;
            }
            try
            {
                if (wantSuppressed)
                    PluginSuppression.Hide();
                else
                    PluginSuppression.Restore();
                _settings.SuppressPlugins = wantSuppressed;
                _settings.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    $"Couldn't {(wantSuppressed ? "hide" : "restore")} the Plugins/Assets folders.\n\n{ex.Message}",
                    "Q-SYS Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RevertSuppressCheckbox(!wantSuppressed);
            }
        }

        private void RevertSuppressCheckbox(bool value)
        {
            _initializingSuppressCheckbox = true;
            _chkSuppressPlugins.Checked = value;
            _initializingSuppressCheckbox = false;
        }

        private void RefreshVersions()
        {
            var scanner = new DesignerScanner(_settings);
            _versions = scanner.ScanInstalledVersions();
            PopulateList();
        }

        private void OpenManagePaths()
        {
            using var dialog = new ManagePathsForm(_settings);
            dialog.ShowDialog(this);
            RefreshVersions();
        }

        private static Icon TryLoadIcon()
        {
            try
            {
                string iconPath = Path.Combine(AppContext.BaseDirectory, "qdv.ico");
                return File.Exists(iconPath) ? new Icon(iconPath) : null;
            }
            catch
            {
                return null;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _settings.WindowWidth = Width;
            _settings.WindowHeight = Height;
            _settings.Save();
            base.OnFormClosing(e);
        }
    }
}