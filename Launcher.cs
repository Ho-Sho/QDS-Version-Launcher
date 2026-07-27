using System;
using System.Diagnostics;
using System.IO;

namespace QDSVersionLauncher
{
    /// <summary>
    /// Starts a specific Q-SYS Designer executable, optionally opening a
    /// project file with it.
    /// </summary>
    public static class Launcher
    {
        public static bool TryLaunch(string exePath, string projectPath, out string error)
        {
            error = null;

            if (!File.Exists(exePath))
            {
                error = $"The Designer executable was not found:\n{exePath}\n\n" +
                        "It may have been moved or uninstalled since the last scan. Try Refresh.";
                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty
                };

                if (!string.IsNullOrEmpty(projectPath))
                {
                    // ArgumentList handles quoting for paths with spaces correctly,
                    // without needing to hand-build a quoted argument string.
                    startInfo.ArgumentList.Add(projectPath);
                }

                Process.Start(startInfo);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
