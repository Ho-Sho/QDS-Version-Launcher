using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace QDSVersionLauncher
{
    /// <summary>
    /// Describes one detected Q-SYS Designer installation.
    /// </summary>
    public class DesignerVersionInfo
    {
        /// <summary>Numeric version, e.g. "9.13.1" (at most Major.Minor.Patch).</summary>
        public string Version { get; set; }

        /// <summary>True if this version is on the user-maintained "known LTS" list.</summary>
        public bool IsLts { get; set; }

        /// <summary>Full path to the Designer executable.</summary>
        public string ExePath { get; set; }

        /// <summary>Folder the executable was found in.</summary>
        public string InstallFolder { get; set; }

        /// <summary>Text shown in the picker list, e.g. "9.13.1 LTS".</summary>
        public string DisplayName => IsLts ? $"{Version} LTS" : Version;
    }

    /// <summary>
    /// Finds Q-SYS Designer installations on this machine.
    ///
    /// Q-SYS does not install multiple versions side-by-side automatically —
    /// QSC's own instructions have users rename the default
    /// "QSC Audio\Q-SYS Designer" folder before installing another version,
    /// so folder names are NOT a reliable way to know which version lives
    /// where. Instead, this scanner reads the *actual* file version from
    /// each candidate "*designer*.exe" it finds, via Win32 file version info.
    /// Windows "Programs and Features" (uninstall registry) entries are used
    /// as a secondary source, mainly to catch installs done into a custom
    /// (non-default) folder.
    /// </summary>
    public class DesignerScanner
    {
        private readonly Settings _settings;

        public DesignerScanner(Settings settings)
        {
            _settings = settings;
        }

        public List<DesignerVersionInfo> ScanInstalledVersions()
        {
            // Key = full exe path, used to de-duplicate the same install found
            // via more than one candidate root.
            var found = new Dictionary<string, DesignerVersionInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var root in GetCandidateRoots())
            {
                ScanRoot(root, found);
            }

            // Secondary source: anything registered under "Programs and Features"
            // whose install location we haven't already scanned above.
            var uninstallEntries = RegistryHelper.EnumerateUninstallEntries("Designer")
                .Where(e => e.DisplayName.IndexOf("Q-SYS", StringComparison.OrdinalIgnoreCase) >= 0
                         || e.DisplayName.IndexOf("QSC", StringComparison.OrdinalIgnoreCase) >= 0);

            foreach (var entry in uninstallEntries)
            {
                if (!string.IsNullOrEmpty(entry.InstallLocation) && Directory.Exists(entry.InstallLocation))
                {
                    ScanRoot(entry.InstallLocation, found, maxDepth: 1);
                }
            }

            var list = found.Values.ToList();
            foreach (var info in list)
            {
                info.IsLts = _settings.KnownLtsVersions.Contains(info.Version);
            }

            list.Sort((a, b) => CompareVersionsDescending(a.Version, b.Version));
            return list;
        }

        private IEnumerable<string> GetCandidateRoots()
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            // Default install location documented by QSC, in both the 64-bit
            // and 32-bit Program Files trees (older Designer releases were 32-bit).
            yield return Path.Combine(programFiles, "QSC Audio");
            yield return Path.Combine(programFilesX86, "QSC Audio");
            yield return Path.Combine(programFiles, "QSC");
            yield return Path.Combine(programFilesX86, "QSC");

            // Anything the user has added manually via "Add folder..." in the picker.
            foreach (var custom in _settings.CustomScanPaths)
            {
                yield return custom;
            }
        }

        /// <summary>
        /// Looks for a Designer executable directly inside <paramref name="root"/>,
        /// and inside each of its immediate subfolders (that is where
        /// side-by-side installs usually end up).
        /// </summary>
        private void ScanRoot(string root, Dictionary<string, DesignerVersionInfo> found, int maxDepth = 2)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return;

            var dirsToCheck = new List<string> { root };

            if (maxDepth > 1)
            {
                try
                {
                    dirsToCheck.AddRange(Directory.GetDirectories(root));
                }
                catch
                {
                    // Ignore folders we can't list (permissions, etc.).
                }
            }

            foreach (var dir in dirsToCheck)
            {
                string[] exeFiles;
                try
                {
                    exeFiles = Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    continue;
                }

                foreach (var exe in exeFiles)
                {
                    if (found.ContainsKey(exe))
                        continue;

                    string name = Path.GetFileName(exe);
                    if (name.IndexOf("designer", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    string version = GetProductVersion(exe);
                    if (string.IsNullOrEmpty(version))
                        continue;

                    found[exe] = new DesignerVersionInfo
                    {
                        Version = version,
                        ExePath = exe,
                        InstallFolder = dir
                    };
                }
            }
        }

        /// <summary>
        /// Reads the real version from the executable itself, rather than
        /// trusting the folder name (which may have been renamed by hand).
        /// </summary>
        private static string GetProductVersion(string exePath)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(exePath);

                // ProductVersion is usually the clean marketing version
                // (e.g. "9.13.1"); FileVersion is the fallback.
                string raw = !string.IsNullOrWhiteSpace(info.ProductVersion)
                    ? info.ProductVersion
                    : info.FileVersion;

                if (string.IsNullOrWhiteSpace(raw))
                    return null;

                // Drop any trailing build metadata after a space or '+'.
                int cut = raw.IndexOfAny(new[] { ' ', '+' });
                if (cut > 0)
                    raw = raw.Substring(0, cut);

                // Keep only Major.Minor.Patch for a clean display string.
                var parts = raw.Split('.');
                if (parts.Length > 3)
                    raw = string.Join(".", parts.Take(3));

                return raw;
            }
            catch
            {
                return null;
            }
        }

        private static int CompareVersionsDescending(string a, string b)
        {
            if (Version.TryParse(a, out var va) && Version.TryParse(b, out var vb))
                return vb.CompareTo(va);

            return string.Compare(b, a, StringComparison.OrdinalIgnoreCase);
        }
    }
}
