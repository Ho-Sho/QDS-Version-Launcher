using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QDSVersionLauncher
{
    internal static class Program
    {
        // Explorer does not tell a launched program which modifier keys were
        // held during the double-click, so we ask Windows directly for the
        // current key state at process start. This is timing-sensitive: it
        // only works if the user is still physically holding Ctrl in the
        // brief moment the process is starting up.
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKeyCode);

        private const int VK_CONTROL = 0x11;

        [STAThread]
        private static void Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Internal maintenance commands, run via register.bat / unregister.bat
            // (or manually), not meant to be typed by end users day-to-day.
            if (args.Length > 0)
            {
                if (string.Equals(args[0], "--register-association", StringComparison.OrdinalIgnoreCase))
                {
                    FileAssociation.Register();
                    MessageBox.Show(".qsys files are now opened with Q-SYS Launcher.",
                        "Q-SYS Launcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (string.Equals(args[0], "--unregister-association", StringComparison.OrdinalIgnoreCase))
                {
                    FileAssociation.Unregister();
                    MessageBox.Show("Q-SYS Launcher has been removed as the handler for .qsys files.",
                        "Q-SYS Launcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            bool forceSelection = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            string projectPath = args.Length > 0 ? args[0] : null;

            var settings = Settings.Load();

            // Make the actual Plugins/Assets folder locations match whatever
            // the "Suppress plugin folder" checkbox was last set to, before
            // anything else runs. This is a plain state toggle (see
            // PluginSuppression), not something tied to this particular
            // launch, so a couple of cheap existence checks here is all it
            // takes -- no waiting on Designer required either way.
            PluginSuppression.EnsureStateMatches(settings.SuppressPlugins);

            var scanner = new DesignerScanner(settings);
            var versions = scanner.ScanInstalledVersions();

            // Fast path: if we already know which version this exact project
            // was opened with last time, and the user isn't forcing the
            // picker (Ctrl), launch straight into it instead of asking again.
            if (!forceSelection && !string.IsNullOrEmpty(projectPath))
            {
                string remembered = settings.GetRememberedVersion(projectPath);
                var match = versions.Find(v => v.Version == remembered);

                if (match != null && Launcher.TryLaunch(match.ExePath, projectPath, out _))
                {
                    settings.SetRememberedVersion(projectPath, match.Version);
                    settings.Save();
                    return;
                }
                // If there's no remembered version, or launching it failed
                // (e.g. it was since uninstalled), fall through to the picker.
            }

            // Only sweep out dead per-project entries when we're about to show
            // a dialog anyway (see Settings.PruneMissingProjectsIfDue) -- never
            // on the fast path above, so a stale entry can't add latency to a
            // normal double-click launch.
            settings.PruneMissingProjectsIfDue();

            using var form = new MainForm(projectPath, versions, settings, forceSelection);
            Application.Run(form);
        }
    }
}
