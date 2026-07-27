using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace QDSVersionLauncher
{
    /// <summary>
    /// App settings, persisted as a plain JSON file so the tool stays
    /// portable. Saved next to the EXE when that folder is writable
    /// (e.g. a USB stick or a normal folder); otherwise falls back to
    /// %AppData%\QDSVersionLauncher (e.g. when installed under
    /// "Program Files", which is read-only for normal users).
    /// </summary>
    public class Settings
    {
        /// <summary>Extra folders to scan, added via "Add folder..." in the picker.</summary>
        public List<string> CustomScanPaths { get; set; } = new();

        /// <summary>
        /// Versions QSC has designated "LTS". Detection can't tell this from
        /// the EXE alone, so this list is user-maintained -- add new LTS
        /// releases here as QSC announces them.
        /// </summary>
        public List<string> KnownLtsVersions { get; set; } = new() { "9.13.1", "9.13.2" };

        /// <summary>Per-project remembered version (key = normalized full file path).</summary>
        public Dictionary<string, string> RememberedVersionByProject { get; set; } = new();

        /// <summary>Most-recently-used versions first; the front entry gets the star.</summary>
        public List<string> GlobalMru { get; set; } = new();

        public int WindowWidth { get; set; } = 420;
        public int WindowHeight { get; set; } = 480;

        /// <summary>
        /// When the dead-project sweep (PruneMissingProjectsIfDue) last
        /// actually ran. Used to throttle how often it does the File.Exists
        /// pass, rather than doing it on every single launch.
        /// </summary>
        public DateTime LastPruneUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// State of the "Suppress plugin folder" checkbox. When true, the
        /// Plugins and Assets folders are kept moved out of the way (see
        /// PluginSuppression) until the user unchecks it again.
        /// </summary>
        public bool SuppressPlugins { get; set; } = false;

        // --- Load / Save ----------------------------------------------------

        public static Settings Load()
        {
            try
            {
                string path = ResolveSettingsPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var loaded = JsonSerializer.Deserialize<Settings>(json);
                    if (loaded != null)
                        return loaded;
                }
            }
            catch
            {
                // Corrupt or unreadable settings file: fall back to defaults
                // rather than crashing the launcher.
            }

            return new Settings();
        }

        public void Save()
        {
            try
            {
                string path = ResolveSettingsPath();
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch
            {
                // Settings are a convenience, not critical to the launcher's
                // core job of finding and starting Designer, so failures here
                // are silently ignored.
            }
        }

        // --- Convenience accessors ------------------------------------------

        public string GetRememberedVersion(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath))
                return null;

            string key = NormalizeProjectKey(projectPath);
            return RememberedVersionByProject.TryGetValue(key, out var version) ? version : null;
        }

        public void SetRememberedVersion(string projectPath, string version)
        {
            if (!string.IsNullOrEmpty(projectPath))
            {
                string key = NormalizeProjectKey(projectPath);
                RememberedVersionByProject[key] = version;
            }

            GlobalMru.Remove(version);
            GlobalMru.Insert(0, version);
            if (GlobalMru.Count > 20)
                GlobalMru.RemoveRange(20, GlobalMru.Count - 20);
        }

        public string GetMostRecentVersion() => GlobalMru.Count > 0 ? GlobalMru[0] : null;

        /// <summary>Minimum time between sweeps, so a normal day of launches never triggers one.</summary>
        private static readonly TimeSpan PruneInterval = TimeSpan.FromDays(7);

        /// <summary>
        /// Drops RememberedVersionByProject entries whose project file no
        /// longer exists at its recorded path (moved, renamed, or deleted),
        /// so the dictionary doesn't grow forever as projects come and go.
        ///
        /// Deliberately NOT called from Load(): the caller should only call
        /// this right before showing the version-picker dialog, never on the
        /// fast "remembered version, launch immediately" path, so a stale
        /// entry can never add latency to a normal double-click launch.
        /// Also throttled to at most once per PruneInterval, and skips any
        /// path on a network share (a disconnected UNC path can make
        /// File.Exists hang for several seconds waiting on a network
        /// timeout -- not worth the risk just to tidy up the file).
        /// </summary>
        public void PruneMissingProjectsIfDue()
        {
            if (DateTime.UtcNow - LastPruneUtc < PruneInterval)
                return;

            var missingKeys = RememberedVersionByProject.Keys
                .Where(IsMissingLocalFile)
                .ToList();

            foreach (var key in missingKeys)
                RememberedVersionByProject.Remove(key);

            // Stamp the time regardless of whether anything was removed,
            // otherwise an all-clean sweep would count as "never happened"
            // and we'd end up re-scanning on every picker display instead
            // of respecting PruneInterval.
            LastPruneUtc = DateTime.UtcNow;
            Save();
        }

        private static bool IsMissingLocalFile(string path)
        {
            // UNC network paths (\\server\share\...) are skipped entirely --
            // see the PruneMissingProjectsIfDue summary above for why.
            if (path.StartsWith(@"\\", StringComparison.Ordinal))
                return false;

            return !File.Exists(path);
        }

        private static string NormalizeProjectKey(string projectPath)
        {
            try
            {
                return Path.GetFullPath(projectPath).ToLowerInvariant();
            }
            catch
            {
                return projectPath.ToLowerInvariant();
            }
        }

        private static string ResolveSettingsPath()
        {
            string exeDir = AppContext.BaseDirectory;

            if (IsWritable(exeDir))
                return Path.Combine(exeDir, "settings.json");

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "QDSVersionLauncher", "settings.json");
        }

        private static bool IsWritable(string dir)
        {
            try
            {
                string probe = Path.Combine(dir, $".write_test_{Guid.NewGuid():N}");
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
