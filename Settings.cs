using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace QDSVersionLauncher
{
    public class Settings
    {
        public List<string> CustomScanPaths { get; set; } = new();
        public List<string> KnownLtsVersions { get; set; } = new() { "9.13.1", "9.13.2" };
        public Dictionary<string, string> RememberedVersionByProject { get; set; } = new();

        /// <summary>
        /// Projects (normalized keys) that skip the picker and launch
        /// straight into their remembered version, regardless of
        /// ForceSelectionMode. Per-project override on top of the app-wide
        /// "Show picker on:" setting; toggled via the "Always use this
        /// version for this project" checkbox in the picker.
        /// </summary>
        public List<string> PinnedProjects { get; set; } = new();

        public List<string> GlobalMru { get; set; } = new();
        public int WindowWidth { get; set; } = 420;
        public int WindowHeight { get; set; } = 480;
        public DateTime LastPruneUtc { get; set; } = DateTime.MinValue;
        public bool SuppressPlugins { get; set; } = false;
        
        /// <summary>
        /// Mode for triggering the version picker dialog.
        /// 0: Always show, 1: Ctrl, 2: Shift, 3: Ctrl + Shift
        /// Default is 1 (Ctrl).
        /// </summary>
        public int ForceSelectionMode { get; set; } = 1;

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
                    {
                        if (loaded.ForceSelectionKeys != null && loaded.ForceSelectionKeys.Count > 0)
                        {
                            if (loaded.ForceSelectionKeys.Contains(17) && loaded.ForceSelectionKeys.Contains(16))
                                loaded.ForceSelectionMode = 3; // Ctrl + Shift
                            else if (loaded.ForceSelectionKeys.Contains(16))
                                loaded.ForceSelectionMode = 2; // Shift
                            else
                                loaded.ForceSelectionMode = 1; // Ctrl
                            loaded.ForceSelectionKeys = null;
                            loaded.Save();
                        }
                        return loaded;
                    }
                }
            }
            catch { }
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
            catch { }
        }

        public string GetRememberedVersion(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath)) return null;
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

        public bool IsProjectPinned(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath)) return false;
            return PinnedProjects.Contains(NormalizeProjectKey(projectPath));
        }

        public void SetProjectPinned(string projectPath, bool pinned)
        {
            if (string.IsNullOrEmpty(projectPath)) return;
            string key = NormalizeProjectKey(projectPath);
            if (pinned)
            {
                if (!PinnedProjects.Contains(key))
                    PinnedProjects.Add(key);
            }
            else
            {
                PinnedProjects.Remove(key);
            }
        }
        
        private static readonly TimeSpan PruneInterval = TimeSpan.FromDays(7);
        
        public void PruneMissingProjectsIfDue()
        {
            if (DateTime.UtcNow - LastPruneUtc < PruneInterval) return;
            var missingKeys = RememberedVersionByProject.Keys.Where(IsMissingLocalFile).ToList();
            foreach (var key in missingKeys)
                RememberedVersionByProject.Remove(key);
            PinnedProjects.RemoveAll(IsMissingLocalFile);
            LastPruneUtc = DateTime.UtcNow;
            Save();
        }
        
        private static bool IsMissingLocalFile(string path)
        {
            if (path.StartsWith(@"\\", StringComparison.Ordinal)) return false;
            return !File.Exists(path);
        }
        
        private static string NormalizeProjectKey(string projectPath)
        {
            try { return Path.GetFullPath(projectPath).ToLowerInvariant(); }
            catch { return projectPath.ToLowerInvariant(); }
        }
        
        private static string ResolveSettingsPath()
        {
            string exeDir = AppContext.BaseDirectory;
            if (IsWritable(exeDir)) return Path.Combine(exeDir, "settings.json");
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
            catch { return false; }
        }

        // --- Legacy property for migration ---
        public List<int> ForceSelectionKeys { get; set; }
    }
}