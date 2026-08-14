using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace QDSVersionLauncher
{
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
    private bool _initializingSuppressCheckbox;
    private CheckBox _chkPinProject;

    // Shortcut selection RadioButtons
    private RadioButton _rbShortcutAlways;
    private RadioButton _rbShortcutCtrl;
    private RadioButton _rbShortcutShift;
    private RadioButton _rbShortcutCtrlShift;

    // Matches the assembly-version tag QDS embeds in a saved .qsys file,
    // e.g. "Q-Sys Designer, Version=10.0.2.0, Culture=...".
    private static readonly Regex SavedVersionRegex =
      new(@"Designer, Version=(\d+)\.(\d+)\.(\d+)\.(\d+)", RegexOptions.Compiled);

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
      AutoScaleMode = AutoScaleMode.Dpi;
      AutoScaleDimensions = new SizeF(96f, 96f);
      Text = "Q-SYS Version Launcher";
      StartPosition = FormStartPosition.CenterScreen;

      // Minimum width must fit Refresh + Cancel + Open side-by-side
      // without overlapping. At 96 DPI: panel padding (12+12) +
      // Refresh (100 + 8 margin) + Cancel (100 + 8 margin) + Open
      // (100) = 340px of content, plus buffer for window chrome.
      const int MinFormWidth = 380;
      // +40 over the old minimum to leave room for the new pin
      // checkbox row without squeezing the list too small.
      const int MinFormHeight = 440;
      Width = Math.Max(MinFormWidth, _settings.WindowWidth);
      Height = Math.Max(MinFormHeight, _settings.WindowHeight);
      MinimumSize = new Size(MinFormWidth, MinFormHeight);

      Icon appIcon = TryLoadIcon();
      if (appIcon != null)
        Icon = appIcon;

      // --- Header ---
      var headerPanel = new TableLayoutPanel
      {
        Dock = DockStyle.Top,
        AutoSize = true,
        ColumnCount = 2,
        RowCount = 2,
        Padding = new Padding(12, 6, 12, 6)
      };
      // Col 0: "Project" caption / filename (unchanged position).
      // Col 1: saved-version text, anchored to the right edge.
      headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
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
      var savedVersionLabel = new Label
      {
        Text = GetSavedVersionText(),
        AutoSize = true,
        ForeColor = SystemColors.GrayText,
        Anchor = AnchorStyles.Right,
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
      headerPanel.Controls.Add(savedVersionLabel, 1, 0);
      headerPanel.Controls.Add(projectLabel, 0, 1);
      headerPanel.SetColumnSpan(projectLabel, 2);

      // --- Hint ---
      _hintLabel = new Label
      {
        Dock = DockStyle.Top,
        AutoSize = true,
        Padding = new Padding(12, 4, 12, 4),
        ForeColor = Color.DarkOrange,
        Text = GetHintText(),
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

      // --- Pin this project to always use the selected version ---
      var pinPanel = new TableLayoutPanel
      {
        Dock = DockStyle.Bottom,
        AutoSize = true,
        ColumnCount = 1,
        RowCount = 1,
        Padding = new Padding(12, 4, 12, 4)
      };
      pinPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
      pinPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

      _chkPinProject = new CheckBox
      {
        Text = "Always use this version for this project",
        AutoSize = true,
        Checked = _settings.IsProjectPinned(_projectPath),
        Enabled = !string.IsNullOrEmpty(_projectPath),
        Margin = new Padding(0)
      };
      var pinToolTip = new ToolTip();
      pinToolTip.SetToolTip(_chkPinProject,
        "Skips the picker next time this project is opened, launching straight " +
        "into the version selected below. Hold Ctrl or Shift on the next launch " +
        "to bring the picker back up and change it.");
      pinPanel.Controls.Add(_chkPinProject, 0, 0);

      // --- Shortcut Selection Panel (Above Suppress Plugin) ---
      var shortcutPanel = new TableLayoutPanel
      {
        Dock = DockStyle.Bottom,
        AutoSize = true,
        ColumnCount = 1,
        RowCount = 5,
        Padding = new Padding(12, 8, 12, 8)
      };
      shortcutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
      for (int i = 0; i < 5; i++)
        shortcutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

      var lblShortcutTitle = new Label
      {
        Text = "Show picker on:",
        AutoSize = true,
        ForeColor = SystemColors.GrayText,
        Margin = new Padding(0, 0, 0, 4)
      };
      shortcutPanel.Controls.Add(lblShortcutTitle, 0, 0);

      _rbShortcutAlways = new RadioButton { Text = "Always", AutoSize = true, Margin = new Padding(0, 2, 0, 2) };
      _rbShortcutCtrl = new RadioButton { Text = "Ctrl is held", AutoSize = true, Margin = new Padding(0, 2, 0, 2) };
      _rbShortcutShift = new RadioButton { Text = "Shift is held", AutoSize = true, Margin = new Padding(0, 2, 0, 2) };
      _rbShortcutCtrlShift = new RadioButton { Text = "Ctrl + Shift are held", AutoSize = true, Margin = new Padding(0, 2, 0, 2) };

      shortcutPanel.Controls.Add(_rbShortcutAlways, 0, 1);
      shortcutPanel.Controls.Add(_rbShortcutCtrl, 0, 2);
      shortcutPanel.Controls.Add(_rbShortcutShift, 0, 3);
      shortcutPanel.Controls.Add(_rbShortcutCtrlShift, 0, 4);

      // Wire up events to save immediately. Each radio button writes its
      // mode straight to Settings.ForceSelectionMode and saves right
      // away (no separate "Apply" step), same pattern as the suppress checkbox below.
      _rbShortcutAlways.CheckedChanged += (s, e) => { if (_rbShortcutAlways.Checked) { _settings.ForceSelectionMode = 0; _settings.Save(); UpdateHintLabel(); } };
      _rbShortcutCtrl.CheckedChanged += (s, e) => { if (_rbShortcutCtrl.Checked) { _settings.ForceSelectionMode = 1; _settings.Save(); UpdateHintLabel(); } };
      _rbShortcutShift.CheckedChanged += (s, e) => { if (_rbShortcutShift.Checked) { _settings.ForceSelectionMode = 2; _settings.Save(); UpdateHintLabel(); } };
      _rbShortcutCtrlShift.CheckedChanged += (s, e) => { if (_rbShortcutCtrlShift.Checked) { _settings.ForceSelectionMode = 3; _settings.Save(); UpdateHintLabel(); } };

      SetShortcutRadioButtons();

      // --- Suppress-plugin-folder toggle ---
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

      _initializingSuppressCheckbox = true;
      _chkSuppressPlugins.Checked = _settings.SuppressPlugins;
      _initializingSuppressCheckbox = false;
      _chkSuppressPlugins.CheckedChanged += (s, e) => OnSuppressPluginsCheckedChanged();

      // --- Bottom buttons ---
      int btnWidth = 100;
      var buttonPanel = new TableLayoutPanel
      {
        Dock = DockStyle.Bottom,
        AutoSize = true,
        ColumnCount = 4,
        RowCount = 2,
        Padding = new Padding(12, 8, 12, 12)
      };
      buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Col 0: Left
      buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Col 1: Spacer
      buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Col 2: Right
      buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Col 3: Right
      buttonPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      buttonPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

      _btnRefresh = new Button { Text = "Refresh", Width = btnWidth, Margin = new Padding(0, 0, 8, 8) };
      _btnCancel = new Button { Text = "Cancel", Width = btnWidth, Margin = new Padding(0, 0, 8, 8) };
      _btnOpen = new Button { Text = "Open", Width = btnWidth, Margin = new Padding(0, 0, 0, 8) };
      _btnManagePaths = new Button { Text = "Manage Folders...", Width = btnWidth, Margin = new Padding(0, 0, 0, 0) };

      _btnRefresh.Click += (s, e) => RefreshVersions();
      _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
      _btnOpen.Click += (s, e) => OpenSelected();
      _btnManagePaths.Click += (s, e) => OpenManagePaths();

      buttonPanel.Controls.Add(_btnRefresh, 0, 0);
      buttonPanel.Controls.Add(_btnCancel, 2, 0);
      buttonPanel.Controls.Add(_btnOpen, 3, 0);
      buttonPanel.Controls.Add(_btnManagePaths, 0, 1);

      // --- App version, bottom-right, read from <Version> in the .csproj ---
      string appVersionText = GetAppVersionText();
      if (!string.IsNullOrEmpty(appVersionText))
      {
        var lblVersion = new Label
        {
          Text = appVersionText,
          AutoSize = true,
          ForeColor = SystemColors.GrayText,
          Font = new Font(Font.FontFamily, 7.5f),
          Anchor = AnchorStyles.Right,
          Margin = new Padding(0, 8, 0, 0)
        };
        buttonPanel.Controls.Add(lblVersion, 3, 1);
      }

      AcceptButton = _btnOpen;
      CancelButton = _btnCancel;

      Controls.Add(_listView);
      Controls.Add(pinPanel);
      Controls.Add(shortcutPanel);
      Controls.Add(suppressPanel);
      Controls.Add(buttonPanel);
      Controls.Add(_hintLabel);
      Controls.Add(headerPanel);

      float scaleFactorY = CurrentAutoScaleDimensions.Height / 96f;
      _listView.SmallImageList = new ImageList
      {
        ImageSize = new Size(1, (int)(16 * scaleFactorY))
      };
    }

    private string GetShortcutDisplayText()
    {
      switch (_settings.ForceSelectionMode)
      {
        case 0: return ""; // Always
        case 1: return "Ctrl";
        case 2: return "Shift";
        case 3: return "Ctrl + Shift";
        default: return "Ctrl";
      }
    }

    private string GetHintText()
    {
      string shortcutText = GetShortcutDisplayText();
      return string.IsNullOrEmpty(shortcutText)
        ? "Always showing version picker."
        : $"{shortcutText} held: choose a version manually.";
    }

    private void UpdateHintLabel()
    {
      _hintLabel.Text = GetHintText();
    }

    // App's own version, assembly's version metadata (set via <Version> in the .csproj).
    private static string GetAppVersionText()
    {
      var version = Assembly.GetExecutingAssembly().GetName().Version;
      return version != null ? $"ver{version.Major}.{version.Minor}.{version.Build}" : null;
    }

    // Text shown next to "Project", telling the version this .qsys file was last saved.
    private string GetSavedVersionText()
    {
      if (string.IsNullOrEmpty(_projectPath))
        return string.Empty;
      string version = TryReadSavedVersion(_projectPath);
      return version != null ? $"Saved version: {version}" : "Saved version: unknown";
    }

    private static string TryReadSavedVersion(string projectPath)
    {
      if (!File.Exists(projectPath))
        return null;

      try
      {
        byte[] bytes = File.ReadAllBytes(projectPath);
        string text = Encoding.UTF8.GetString(bytes);
        var match = SavedVersionRegex.Match(text);
        if (!match.Success)
          return null;

        int major = int.Parse(match.Groups[1].Value);
        int minor = int.Parse(match.Groups[2].Value);
        int patch = int.Parse(match.Groups[3].Value);

        // major 1 -> "10.x", major 10 -> "10.minor.patch", else as-is.
        if (major == 1)
          return $"10.{patch}";
        if (major == 10)
          return $"10.{minor}.{patch}";
        return $"{major}.{minor}.{patch}";
      }
      catch
      {
        return null;
      }
    }

    private void SetShortcutRadioButtons()
    {
      int mode = _settings.ForceSelectionMode;
      _rbShortcutAlways.Checked = (mode == 0);
      _rbShortcutCtrl.Checked = (mode == 1);
      _rbShortcutShift.Checked = (mode == 2);
      _rbShortcutCtrlShift.Checked = (mode == 3);

      // Fallback for invalid values
      if (!_rbShortcutAlways.Checked && !_rbShortcutCtrl.Checked && !_rbShortcutShift.Checked && !_rbShortcutCtrlShift.Checked)
        _rbShortcutCtrl.Checked = true;
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
      _settings.SetProjectPinned(_projectPath, _chkPinProject.Checked);
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

      // Force the dialog to open at the same top-left position as the main form
      dialog.StartPosition = FormStartPosition.Manual;
      dialog.Location = new Point(this.Left, this.Top);

      dialog.ShowDialog(this);
      RefreshVersions();
    }

    private static Icon TryLoadIcon()
    {
      try
      {
        // Prefer app.ico next to the EXE.
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

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
      _settings.WindowWidth = Width;
      _settings.WindowHeight = Height;
      _settings.Save();
      base.OnFormClosing(e);
    }
  }
}