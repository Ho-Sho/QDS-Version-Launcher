using System;
using System.Diagnostics;
using System.IO;

namespace QDSVersionLauncher
{
  /// <summary>
  /// Registers/unregisters this launcher as the handler for ".qsys" files
  /// (per-user, under HKCU, so no administrator rights are needed).
  /// The previous handler (if any) is remembered so Unregister() can put
  /// it back, e.g. if the user wants to go back to double-click opening a
  /// single fixed Designer version.
  /// </summary>
  public static class FileAssociation
  {
    private const string Extension = ".qsys";
    private const string ProgId = "QDSVersionLauncher.Project";
    private const string PreviousProgIdValueName = "QDSVersionLauncher.PreviousProgId";

    // Shown by Windows in "Open with" pickers and the file Properties
    // dialog's "Opens with:" line. Without this, Windows falls back to
    // showing the raw exe filename there, even though the exe's own
    // FileDescription (set via <AssemblyTitle> in the .csproj) is
    // already correct -- the two are resolved through different paths.
    private const string FriendlyAppName = "QDS Version Launcher";

    public static void Register(string exePathOverride = null)
    {
      string exePath = exePathOverride ?? Process.GetCurrentProcess().MainModule?.FileName;
      if (string.IsNullOrEmpty(exePath))
        return;

      RegistryHelper.WriteClassesDefaultValue(ProgId, "Q-SYS Design File");

      // Use qsys.ico for the file type icon if it exists next to the EXE;
      // otherwise fall back to the EXE's own icon resource.
      string qsysIconPath = Path.Combine(AppContext.BaseDirectory, "qsys.ico");
      string iconValue = File.Exists(qsysIconPath)
        ? $"\"{qsysIconPath}\""
        : $"\"{exePath}\",0";
      RegistryHelper.WriteClassesDefaultValue($@"{ProgId}\DefaultIcon", iconValue);

      RegistryHelper.WriteClassesDefaultValue($@"{ProgId}\shell\open\command", $"\"{exePath}\" \"%1\"");

      string exeFileName = Path.GetFileName(exePath);
      RegistryHelper.WriteClassesValue($@"Applications\{exeFileName}", "FriendlyAppName", FriendlyAppName);

      // Remember whatever ".qsys" used to point to, so we can restore
      // it later if the user unregisters.
      string previous = RegistryHelper.ReadClassesDefaultValue(Extension);
      if (!string.IsNullOrEmpty(previous) && previous != ProgId)
      {
        RegistryHelper.WriteClassesValue(Extension, PreviousProgIdValueName, previous);
      }

      RegistryHelper.WriteClassesDefaultValue(Extension, ProgId);
      RegistryHelper.NotifyShellAssociationsChanged();

      // Registry values alone don't force Explorer to redraw icons --
      // a stale (often the EXE's own) icon can keep showing until the
      // shell icon cache is cleared, especially when re-registering
      // after the DefaultIcon value has changed.
      ClearShellIconCache();
    }

    public static void Unregister()
    {
      string previous = RegistryHelper.ReadClassesValue(Extension, PreviousProgIdValueName);

      // Restore the previous handler if we recorded one, otherwise just
      // clear the default value so ".qsys" goes back to unassociated.
      if (!string.IsNullOrEmpty(previous))
      {
        RegistryHelper.WriteClassesDefaultValue(Extension, previous);
      }
      else
      {
        RegistryHelper.DeleteClassesValue(Extension, null);
      }

      // Remove the ProgId itself.
      RegistryHelper.DeleteClassesTree(ProgId);

      string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
      string exeFileName = Path.GetFileName(exePath);

      // Remove the "Applications\<exe>" entry.
      if (!string.IsNullOrEmpty(exeFileName))
      {
        RegistryHelper.DeleteClassesTree($@"Applications\{exeFileName}");
      }

      // Clean MuiCache to prevent ghost items in "Recommended Apps".
      if (!string.IsNullOrEmpty(exePath))
      {
        string muiCache = @"Local Settings\Software\Microsoft\Windows\Shell\MuiCache";
        RegistryHelper.DeleteClassesValue(muiCache, exePath + ".FriendlyAppName");
        RegistryHelper.DeleteClassesValue(muiCache, exePath + ".ApplicationCompany");
      }

      RegistryHelper.NotifyShellAssociationsChanged();
      ClearShellIconCache();
    }

    public static bool IsRegistered()
      => RegistryHelper.ReadClassesDefaultValue(Extension) == ProgId;

    /// <summary>
    /// Forces Windows Explorer to drop its cached file-type icons, so a
    /// newly-set (or removed) DefaultIcon shows up right away instead of
    /// needing a manual cache clear, an Explorer restart, or a reboot.
    /// Best-effort only: a stale icon is cosmetic, never worth failing
    /// registration over.
    /// </summary>
    private static void ClearShellIconCache()
    {
      try
      {
        var startInfo = new ProcessStartInfo
        {
          FileName = "ie4uinit.exe",
          Arguments = "-ClearIconCache",
          UseShellExecute = false,
          CreateNoWindow = true
        };
        using var process = Process.Start(startInfo);
        process?.WaitForExit(3000);
      }
      catch
      {
        // Icon cache clearing is a nice-to-have; ignore failures silently.
      }
    }
  }
}