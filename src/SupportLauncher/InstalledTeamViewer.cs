using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using Microsoft.Win32;

namespace SupportLauncher
{
    /// <summary>
    /// A fully installed TeamViewer (service + tray program) blocks QuickSupport:
    /// QuickSupport then shows no ID or cannot be reached. This class stops the
    /// installed instance before QuickSupport starts and restarts the service
    /// afterwards, so the PC ends up in the same state as before.
    /// </summary>
    internal sealed class InstalledTeamViewer
    {
        private const string ServiceName = "TeamViewer";

        /// <summary>Folder of the installed TeamViewer, or null if not installed.</summary>
        public string InstallDir { get; private set; }

        /// <summary>True if the service was running and has been stopped by us.</summary>
        public bool ServiceStoppedByUs { get; private set; }

        public static InstalledTeamViewer Detect()
        {
            var tv = new InstalledTeamViewer();
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + ServiceName))
                {
                    var image = key?.GetValue("ImagePath") as string;
                    if (!string.IsNullOrWhiteSpace(image))
                    {
                        image = Environment.ExpandEnvironmentVariables(image.Trim());
                        if (image.StartsWith("\""))
                            image = image.Substring(1, image.IndexOf('"', 1) - 1);
                        else if (image.IndexOf(".exe", StringComparison.OrdinalIgnoreCase) > 0)
                            image = image.Substring(0, image.IndexOf(".exe", StringComparison.OrdinalIgnoreCase) + 4);
                        tv.InstallDir = Path.GetDirectoryName(image);
                    }
                }
            }
            catch { /* treat as not installed */ }
            return tv;
        }

        public bool IsInstalled => !string.IsNullOrEmpty(InstallDir);

        /// <summary>True if the service or a program from the install folder is running.</summary>
        public bool IsRunning()
        {
            if (!IsInstalled) return false;
            return ServiceStatus() == ServiceControllerStatus.Running || InstalledProcesses().Any();
        }

        /// <summary>Stops the service and closes the installed TeamViewer programs. Needs admin rights.</summary>
        public void Stop()
        {
            if (!IsInstalled) return;

            using (var sc = OpenService())
            {
                if (sc != null)
                {
                    sc.Refresh();
                    if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                    {
                        sc.Stop();
                        ServiceStoppedByUs = true;
                    }
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                }
            }

            // The tray program (TeamViewer.exe, tv_w32/tv_x64) keeps running without the service.
            foreach (var p in InstalledProcesses())
            {
                using (p)
                {
                    try
                    {
                        if (!p.CloseMainWindow() || !p.WaitForExit(3000))
                        {
                            p.Kill();
                            p.WaitForExit(5000);
                        }
                    }
                    catch { /* already gone or no access */ }
                }
            }
        }

        /// <summary>Starts the service again if we stopped it.</summary>
        public void RestoreService()
        {
            if (!ServiceStoppedByUs) return;
            try
            {
                using (var sc = OpenService())
                {
                    if (sc == null) return;
                    sc.Refresh();
                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    }
                }
                ServiceStoppedByUs = false;
            }
            catch { /* best effort; Windows starts it again at the next reboot */ }
        }

        /// <summary>
        /// True while a TeamViewer program runs that is NOT from the install folder,
        /// i.e. QuickSupport (it unpacks itself to the temp folder).
        /// </summary>
        public bool QuickSupportRunning(string[] processNamePrefixes)
        {
            foreach (var p in Process.GetProcesses())
            {
                using (p)
                {
                    if (!processNamePrefixes.Any(n => p.ProcessName.StartsWith(n, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    var path = TryGetPath(p);
                    if (path == null || !IsInInstallDir(path))
                        return true;
                }
            }
            return false;
        }

        private IEnumerable<Process> InstalledProcesses()
        {
            var own = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcesses())
            {
                if (p.Id != own)
                {
                    var path = TryGetPath(p);
                    if (path != null && IsInInstallDir(path))
                    {
                        yield return p;
                        continue;
                    }
                }
                p.Dispose();
            }
        }

        private bool IsInInstallDir(string path)
        {
            var dir = InstallDir.TrimEnd('\\') + "\\";
            return path.StartsWith(dir, StringComparison.OrdinalIgnoreCase);
        }

        private static string TryGetPath(Process p)
        {
            try { return p.MainModule?.FileName; }
            catch { return null; }   // system process, other user, or 32/64-bit mismatch
        }

        private static ServiceController OpenService()
        {
            try
            {
                var sc = new ServiceController(ServiceName);
                var _ = sc.Status;   // throws if the service does not exist
                return sc;
            }
            catch
            {
                return null;
            }
        }

        private static ServiceControllerStatus? ServiceStatus()
        {
            using (var sc = OpenService())
            {
                if (sc == null) return null;
                sc.Refresh();
                return sc.Status;
            }
        }
    }
}
