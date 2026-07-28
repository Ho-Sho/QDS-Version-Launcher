using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QDSVersionLauncher
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKeyCode);

        private const int VK_CONTROL = 0x11;
        private const int VK_SHIFT = 0x10;

        [STAThread]
        private static void Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
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
            
            string projectPath = args.Length > 0 ? args[0] : null;
            var settings = Settings.Load();
            
            PluginSuppression.EnsureStateMatches(settings.SuppressPlugins);
            
            var scanner = new DesignerScanner(settings);
            var versions = scanner.ScanInstalledVersions();
            
            // Check modifier keys based on the configured mode
            bool ctrlPressed = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            bool shiftPressed = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
            bool forceSelection = CheckConfiguredModifiers(settings.ForceSelectionMode, ctrlPressed, shiftPressed);

            // A pinned project skips the picker even when the mode would
            // normally force it open (e.g. "Always") -- unless Ctrl or
            // Shift is held, which still forces the picker so a pinned
            // project's version can be changed later.
            if (settings.IsProjectPinned(projectPath) && !ctrlPressed && !shiftPressed)
                forceSelection = false;
            
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
            }
            
            settings.PruneMissingProjectsIfDue();
            
            using var form = new MainForm(projectPath, versions, settings, forceSelection);
            Application.Run(form);
        }
        
        private static bool CheckConfiguredModifiers(int mode, bool ctrlPressed, bool shiftPressed)
        {
            switch (mode)
            {
                // If mode == 0 (Always), we should return true to force the picker.
                case 0: return true; 
                case 1: return ctrlPressed && !shiftPressed; // Ctrl only
                case 2: return shiftPressed && !ctrlPressed; // Shift only
                case 3: return ctrlPressed && shiftPressed;  // Ctrl + Shift
                default: return ctrlPressed;
            }
        }
    }
}