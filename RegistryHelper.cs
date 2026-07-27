using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace QDSVersionLauncher
{
    /// <summary>
    /// Low-level, generic registry helpers. Two things live here:
    ///  1) Reading Windows "Programs and Features" (Uninstall) entries,
    ///     used by DesignerScanner as a secondary detection source.
    ///  2) Reading/writing values under HKCU\Software\Classes, used by
    ///     FileAssociation to make ".qsys" open through this launcher.
    /// Everything here only ever touches HKEY_CURRENT_USER (or reads
    /// HKEY_LOCAL_MACHINE), so no administrator rights are required and
    /// nothing here affects other user accounts on the machine.
    /// </summary>
    public static class RegistryHelper
    {
        public class UninstallEntry
        {
            public string DisplayName { get; set; }
            public string DisplayVersion { get; set; }
            public string InstallLocation { get; set; }
        }

        private const string ClassesRoot = @"Software\Classes";

        // --- Programs and Features (Uninstall) enumeration -----------------

        public static List<UninstallEntry> EnumerateUninstallEntries(string nameContains)
        {
            var results = new List<UninstallEntry>();

            var roots = new (RegistryKey Hive, string Path)[]
            {
                (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                (Registry.CurrentUser,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            };

            foreach (var (hive, path) in roots)
            {
                using var root = hive.OpenSubKey(path);
                if (root == null)
                    continue;

                foreach (var subKeyName in root.GetSubKeyNames())
                {
                    using var sub = root.OpenSubKey(subKeyName);
                    if (!(sub?.GetValue("DisplayName") is string displayName))
                        continue;

                    if (displayName.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    results.Add(new UninstallEntry
                    {
                        DisplayName = displayName,
                        DisplayVersion = sub.GetValue("DisplayVersion") as string,
                        InstallLocation = sub.GetValue("InstallLocation") as string
                    });
                }
            }

            return results;
        }

        // --- HKCU\Software\Classes helpers (used for file association) -----

        public static string ReadClassesDefaultValue(string subKeyPath)
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"{ClassesRoot}\{subKeyPath}");
            return key?.GetValue(null) as string;
        }

        public static string ReadClassesValue(string subKeyPath, string valueName)
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"{ClassesRoot}\{subKeyPath}");
            return key?.GetValue(valueName) as string;
        }

        public static void WriteClassesDefaultValue(string subKeyPath, string value)
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"{ClassesRoot}\{subKeyPath}");
            key.SetValue(null, value);
        }

        public static void WriteClassesValue(string subKeyPath, string valueName, string value)
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"{ClassesRoot}\{subKeyPath}");
            key.SetValue(valueName, value);
        }

        public static void DeleteClassesValue(string subKeyPath, string valueName)
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"{ClassesRoot}\{subKeyPath}", writable: true);
            key?.DeleteValue(valueName ?? "", throwOnMissingValue: false);
        }

        public static void DeleteClassesTree(string subKeyPath)
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"{ClassesRoot}\{subKeyPath}", throwOnMissingSubKey: false);
        }

        // --- Tell Explorer to pick up the association change immediately ---

        public static void NotifyShellAssociationsChanged()
            => SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private const int SHCNE_ASSOCCHANGED = 0x08000000;
        private const int SHCNF_IDLIST = 0x0000;
    }
}
