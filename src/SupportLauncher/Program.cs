using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace SupportLauncher
{
    internal static class Program
    {
        private const string ElevatedArg = "--elevated";

        [STAThread]
        private static void Main(string[] args)
        {
            var isElevatedRestart = Array.Exists(args, a => a == ElevatedArg);

            // Only one launcher at a time (double clicks happen).
            using (var mutex = new Mutex(false, @"Local\SupportLauncher.SingleInstance"))
            {
                bool owns;
                try
                {
                    // The elevated restart waits briefly until the first instance has exited.
                    owns = mutex.WaitOne(isElevatedRestart ? TimeSpan.FromSeconds(10) : TimeSpan.Zero);
                }
                catch (AbandonedMutexException)
                {
                    owns = true;
                }
                if (!owns)
                    return;

                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var config = LauncherConfig.Load(Path.Combine(baseDir, "launcher.ini"));

                    // Ask for administrator rights once, here. Programs started by an
                    // elevated launcher inherit the rights, so QuickSupport does not ask again.
                    // Stopping an installed TeamViewer service also needs admin rights.
                    var needsAdmin = config.RequestAdmin ||
                                     (config.StopInstalledTeamViewer && InstalledTeamViewer.Detect().IsRunning());

                    if (needsAdmin && !isElevatedRestart && !IsElevated())
                    {
                        if (RestartElevated())
                            return;
                        // Declined: continue with normal rights (QuickSupport may ask itself).
                    }

                    // .NET Framework 4.8 defaults are OS dependent; make sure TLS 1.2+ is used.
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new LauncherForm(config, baseDir));
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        private static bool IsElevated()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Starts this EXE again with the User Account Control prompt. True if started.</summary>
        private static bool RestartElevated()
        {
            try
            {
                Process.Start(new ProcessStartInfo(Application.ExecutablePath, ElevatedArg)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                })?.Dispose();
                return true;
            }
            catch (Win32Exception)
            {
                return false;   // user clicked "No" (ERROR_CANCELLED) or elevation unavailable
            }
        }
    }
}
