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
            RegistryHelper.WriteClassesDefaultValue($@"{ProgId}\DefaultIcon", $"\"{exePath}\",0");
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
        }

        public static void Unregister()
        {
            string previous = RegistryHelper.ReadClassesValue(Extension, PreviousProgIdValueName);

            if (!string.IsNullOrEmpty(previous))
            {
                RegistryHelper.WriteClassesDefaultValue(Extension, previous);
            }
            else
            {
                RegistryHelper.DeleteClassesValue(Extension, null);
            }

            RegistryHelper.DeleteClassesTree(ProgId);

            string exeFileName = Path.GetFileName(Process.GetCurrentProcess().MainModule?.FileName ?? "");
            if (!string.IsNullOrEmpty(exeFileName))
                RegistryHelper.DeleteClassesTree($@"Applications\{exeFileName}");

            RegistryHelper.NotifyShellAssociationsChanged();
        }

        public static bool IsRegistered()
            => RegistryHelper.ReadClassesDefaultValue(Extension) == ProgId;
    }
}