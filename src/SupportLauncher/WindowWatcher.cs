using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SupportLauncher
{
    /// <summary>
    /// Detects when the support module shows its window. QuickSupport unpacks
    /// itself and starts a second process, so instead of watching one process,
    /// any visible top-level window of a matching process counts.
    /// </summary>
    internal static class WindowWatcher
    {
        public static bool HasVisibleWindow(int startedProcessId, string[] processNamePrefixes)
        {
            var ownPid = (uint)Process.GetCurrentProcess().Id;
            var nameCache = new Dictionary<uint, string>();
            bool found = false;

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd) || GetWindowTextLength(hWnd) == 0)
                    return true;

                GetWindowThreadProcessId(hWnd, out var pid);
                if (pid == 0 || pid == ownPid)
                    return true;

                if (pid == startedProcessId)
                {
                    found = true;
                    return false;
                }

                if (!nameCache.TryGetValue(pid, out var name))
                {
                    try { using (var p = Process.GetProcessById((int)pid)) name = p.ProcessName; }
                    catch { name = ""; }
                    nameCache[pid] = name;
                }

                foreach (var prefix in processNamePrefixes)
                {
                    if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);

            return found;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }
}
