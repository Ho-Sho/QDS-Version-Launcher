using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace QDSVersionLauncher
{
    /// <summary>
    /// Implements the "Suppress plugin folder" checkbox: a persistent
    /// on/off toggle (its state lives in Settings.SuppressPlugins) rather
    /// than something tied to a single launch. While "on", Designer's
    /// Plugins and Assets folders are moved to sibling "-suppressed"
    /// folders, so Designer starts with none of the user's custom content
    /// loaded -- like a temporary safe mode. Turning the checkbox back off
    /// moves them back.
    ///
    /// Because the move happens exactly when the checkbox changes (not
    /// around a Designer launch), the launcher itself doesn't need to
    /// track or wait on the Designer process at all -- Open still just
    /// starts Designer and exits immediately, same as before this feature
    /// existed.
    /// </summary>
    public static class PluginSuppression
    {
        private const string PluginsFolderName = "Plugins";
        private const string AssetsFolderName = "Assets";
        private const string SuppressedSuffix = "-suppressed";

        private static string DesignerUserDataDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "QSC", "Q-Sys Designer");

        public static string PluginsLiveDir => Path.Combine(DesignerUserDataDir, PluginsFolderName);
        public static string PluginsHiddenDir => Path.Combine(DesignerUserDataDir, PluginsFolderName + SuppressedSuffix);
        public static string AssetsLiveDir => Path.Combine(DesignerUserDataDir, AssetsFolderName);
        public static string AssetsHiddenDir => Path.Combine(DesignerUserDataDir, AssetsFolderName + SuppressedSuffix);

        /// <summary>
        /// True if any Designer process is currently running. Moving these
        /// folders while a running Designer instance already has them open
        /// isn't safe, so callers should refuse to change the suppression
        /// state while this is true and ask the user to close Designer first.
        /// </summary>
        public static bool IsDesignerRunning()
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (proc.ProcessName.IndexOf("Designer", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
                catch
                {
                    // Some processes refuse to report their name (permissions,
                    // already exited, etc.) -- just skip those.
                }
                finally
                {
                    proc.Dispose();
                }
            }
            return false;
        }

        /// <summary>Moves Plugins and Assets to their "-suppressed" siblings.</summary>
        public static void Hide()
        {
            MoveIfNeeded(PluginsLiveDir, PluginsHiddenDir);
            MoveIfNeeded(AssetsLiveDir, AssetsHiddenDir);
        }

        /// <summary>Moves Plugins and Assets back from their "-suppressed" siblings.</summary>
        public static void Restore()
        {
            MoveIfNeeded(PluginsHiddenDir, PluginsLiveDir);
            MoveIfNeeded(AssetsHiddenDir, AssetsLiveDir);
        }

        /// <summary>
        /// Called once at startup, before any launch happens: makes the
        /// actual folder locations match whatever suppression state was
        /// last saved in settings.json. This is what keeps things correct
        /// even if this app was killed mid-move, or the folders were
        /// touched by hand outside the launcher since the last run.
        /// </summary>
        public static void EnsureStateMatches(bool suppressed)
        {
            if (suppressed)
                Hide();
            else
                Restore();
        }

        private static void MoveIfNeeded(string from, string to)
        {
            if (!Directory.Exists(from))
                return; // nothing to move

            if (!Directory.Exists(to))
            {
                Directory.Move(from, to);
                return;
            }

            // "to" already exists. Most commonly this is Designer having
            // quietly recreated an empty Plugins/Assets folder the moment it
            // didn't find one -- in that case just replace it outright. If
            // it actually has content in it, merge item-by-item instead of
            // silently doing nothing (the old behavior, which is what left
            // the suppressed backup stranded on uncheck).
            if (IsDirectoryEmpty(to))
            {
                Directory.Delete(to);
                Directory.Move(from, to);
                return;
            }

            MergeInto(from, to);
        }

        private static bool IsDirectoryEmpty(string path)
            => !Directory.EnumerateFileSystemEntries(path).Any();

        /// <summary>
        /// Moves every item from <paramref name="from"/> into
        /// <paramref name="to"/>, recursing into subfolders that exist on
        /// both sides. Deletes <paramref name="from"/> afterward if that
        /// emptied it out completely. Name collisions on files are left in
        /// place on the source side rather than overwritten or discarded --
        /// rare enough that manual cleanup is an acceptable outcome.
        /// </summary>
        private static void MergeInto(string from, string to)
        {
            foreach (var entry in Directory.GetFileSystemEntries(from))
            {
                string name = Path.GetFileName(entry);
                string destination = Path.Combine(to, name);

                if (Directory.Exists(entry))
                {
                    if (Directory.Exists(destination))
                        MergeInto(entry, destination);
                    else
                        Directory.Move(entry, destination);
                }
                else if (!File.Exists(destination))
                {
                    File.Move(entry, destination);
                }
            }

            if (IsDirectoryEmpty(from))
                Directory.Delete(from);
        }
    }
}