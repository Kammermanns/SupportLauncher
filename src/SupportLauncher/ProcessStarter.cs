using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace SupportLauncher
{
    /// <summary>
    /// Starts the verified support module without the Windows
    /// "Open File - Security Warning" ("Do you want to run this file?").
    /// The launcher has already checked the Authenticode signature and publisher,
    /// so the customer only has to confirm once (when starting the launcher).
    /// </summary>
    internal static class ProcessStarter
    {
        /// <summary>Removes the "downloaded from the internet" marker (Mark of the Web).</summary>
        public static void RemoveZoneIdentifier(string path)
        {
            try { DeleteFile(path + ":Zone.Identifier"); } catch { /* not present or not NTFS */ }
        }

        /// <summary>Starts the file and returns its process id (0 if unknown).</summary>
        public static int Start(string path)
        {
            RemoveZoneIdentifier(path);

            var info = new SHELLEXECUTEINFO
            {
                cbSize = Marshal.SizeOf(typeof(SHELLEXECUTEINFO)),
                fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NOZONECHECKS,
                lpVerb = "open",
                lpFile = path,
                lpDirectory = Path.GetDirectoryName(path),
                nShow = SW_SHOWNORMAL
            };

            if (!ShellExecuteEx(ref info))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            if (info.hProcess == IntPtr.Zero)
                return 0;
            try { return (int)GetProcessId(info.hProcess); }
            finally { CloseHandle(info.hProcess); }
        }

        private const uint SEE_MASK_NOCLOSEPROCESS = 0x00000040;
        private const uint SEE_MASK_NOZONECHECKS = 0x00800000;
        private const int SW_SHOWNORMAL = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHELLEXECUTEINFO
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpVerb;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpFile;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpParameters;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpDirectory;
            public int nShow;
            public IntPtr hInstApp;
            public IntPtr lpIDList;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpClass;
            public IntPtr hkeyClass;
            public uint dwHotKey;
            public IntPtr hIcon;
            public IntPtr hProcess;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetProcessId(IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool DeleteFile(string path);
    }
}
