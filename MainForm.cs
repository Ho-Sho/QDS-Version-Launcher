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
            Text = "Q-SYS Launcher";
            StartPosition = FormStartPosition.CenterScreen;
            
            // Adjusted default and minimum width to be more compact and eliminate excess right margin
            Width = Math.Max(220, _settings.WindowWidth);
            Height = Math.Max(360, _settings.WindowHeight);
            MinimumSize = new Size(220, 320);

            Icon appIcon = TryLoadIcon();
            if (appIcon != null)
                Icon = appIcon;

            // --- Header: which project this is for ---
            var headerPanel = new Panel { Dock = DockStyle.Top, Height = 56 };
            var captionLabel = new Label
            {
                Text = "Project",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Location = new Point(12, 6)
            };
            var projectLabel = new Label
            {
                Text = string.IsNullOrEmpty(_projectPath) ? "(No project file selected)" : Path.GetFileName(_projectPath),
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Location = new Point(12, 24)
            };
            headerPanel.Controls.Add(captionLabel);
            headerPanel.Controls.Add(projectLabel);

            // --- Hint shown only when Ctrl forced this dialog to appear ---
            _hintLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = _forceSelection ? 20 : 0,
                Padding = new Padding(12, 0, 12, 0),
                ForeColor = Color.DarkOrange,
                Text = "Ctrl/Shift held: choose a version manually.",
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
            _listView.Columns.Add("Version", 250, HorizontalAlignment.Left);
            _listView.DoubleClick += (s, e) => OpenSelected();
            _listView.Resize += (s, e) => AutoSizeColumn();

            // --- Bottom buttons (Height increased to 90 to accommodate 2 rows) ---
            var buttonPanel = new Panel { Dock = DockStyle.Bottom, Height = 90 };

            _btnManagePaths = new Button { Text = "Manage Folders...", Width = 130 };
            _btnOpen = new Button { Text = "Open", Width = 90 };
            _btnRefresh = new Button { Text = "Refresh", Width = 90 };
            _btnCancel = new Button { Text = "Cancel", Width = 90 };

            _btnOpen.Click += (s, e) => OpenSelected();
            _btnRefresh.Click += (s, e) => RefreshVersions();
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            _btnManagePaths.Click += (s, e) => OpenManagePaths();

            buttonPanel.Controls.Add(_btnManagePaths);
            buttonPanel.Controls.Add(_btnOpen);
            buttonPanel.Controls.Add(_btnRefresh);
            buttonPanel.Controls.Add(_btnCancel);

            void LayoutButtons(object sender, EventArgs e)
            {
                const int margin = 12;
                const int gap = 8;
                
                // Start positioning from the left margin
                int left = margin;

                // Top row: Open, Refresh, Cancel (packed to the left)
                _btnOpen.Location = new Point(left, 8);
                left += _btnOpen.Width + gap;

                _btnRefresh.Location = new Point(left, 8);
                left += _btnRefresh.Width + gap;

                _btnCancel.Location = new Point(left, 8);

                // Bottom row: Manage Folders (alone, left-aligned)
                _btnManagePaths.Location = new Point(margin, 46);
            }
            buttonPanel.Resize += LayoutButtons;

            AcceptButton = _btnOpen;
            CancelButton = _btnCancel;

            // --- Suppress-plugin-folder toggle: checkbox + description/paths ---
            var suppressPanel = new Panel { Dock = DockStyle.Bottom, Height = 68, Padding = new Padding(12, 4, 12, 4) };

            _chkSuppressPlugins = new CheckBox
            {
                Text = "Suppress plugin folder",
                AutoSize = true,
                Dock = DockStyle.Top
            };

            var suppressToolTip = new ToolTip();
            suppressToolTip.SetToolTip(_chkSuppressPlugins,
                "While checked, Designer's Plugins and Assets folders are moved out of the way " +
                "(a temporary safe mode with no custom content loaded). Unchecking moves them back.");

            _lblSuppressInfo = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = SystemColors.GrayText,
                Font = new Font(Font.FontFamily, 7.5f),
                Text =
                    "While checked, moves these folders aside and back when unchecked:\n" +
                    $"Plugins -> {PluginSuppression.PluginsHiddenDir}\n" +
                    $"Assets  -> {PluginSuppression.AssetsHiddenDir}"
            };

            // Fill added first, Top added after, so the checkbox stays
            // pinned to the top of this panel and the label fills the rest
            // below it (same reverse-add-order pattern as the outer form).
            suppressPanel.Controls.Add(_lblSuppressInfo);
            suppressPanel.Controls.Add(_chkSuppressPlugins);

            // Reflect the saved state without triggering a folder move --
            // Program.cs already reconciled the actual folders at startup.
            _initializingSuppressCheckbox = true;
            _chkSuppressPlugins.Checked = _settings.SuppressPlugins;
            _initializingSuppressCheckbox = false;

            _chkSuppressPlugins.CheckedChanged += (s, e) => OnSuppressPluginsCheckedChanged();

            // Controls are added Fill -> Bottom -> Top(inner) -> Top(outer),
            // since WinForms docks in reverse of add order: whichever Top
            // control is added last ends up as the outermost (topmost) band.
            // Among the two Bottom-docked panels, suppressPanel is added
            // first so buttonPanel (added after) stays the true bottom edge,
            // with suppressPanel sitting just above it.
            Controls.Add(_listView);
            Controls.Add(suppressPanel);
            Controls.Add(buttonPanel);
            Controls.Add(_hintLabel);
            Controls.Add(headerPanel);

            LayoutButtons(this, EventArgs.Empty);
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
                string text = (isRecent ? "\u2605 " : "   ") + v.DisplayName; // ★ prefix for the recent one
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

            // Paths may have been added/removed either way, so always rescan.
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