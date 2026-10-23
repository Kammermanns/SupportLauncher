using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace SupportLauncher
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // Only one launcher at a time (double clicks happen).
            using (var mutex = new Mutex(true, @"Local\SupportLauncher.SingleInstance", out var isFirst))
            {
                if (!isFirst)
                    return;

                // .NET Framework 4.8 defaults are OS dependent; make sure TLS 1.2+ is used.
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var config = LauncherConfig.Load(Path.Combine(baseDir, "launcher.ini"));

                Application.Run(new LauncherForm(config, baseDir));
            }
        }
    }
}
