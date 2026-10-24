using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SupportLauncher
{
    /// <summary>
    /// Settings read from launcher.ini next to the EXE.
    /// The installer can override values at install time (e.g. /URL=...).
    /// </summary>
    internal sealed class LauncherConfig
    {
        public string CompanyName { get; private set; } = "Support";
        public string WindowTitle { get; private set; } = "Remote Support";
        public string DownloadUrl { get; private set; } = "";
        public string FileName { get; private set; } = "QuickSupport.exe";
        /// <summary>Comma separated list. The signer's subject must contain one of these.</summary>
        public string[] AllowedPublishers { get; private set; } = { "TeamViewer Germany GmbH" };
        public string StatusText { get; private set; } = "Support module is being downloaded …";
        public string SupportContact { get; private set; } = "";
        public int TimeoutSeconds { get; private set; } = 60;
        /// <summary>Text while waiting for the support module's window.</summary>
        public string StartingText { get; private set; } = "TeamViewer wird gestartet, bitte einen Moment Geduld …";
        /// <summary>Process name prefix(es) whose window counts as "started" (comma separated).</summary>
        public string[] WindowProcessNames { get; private set; } = { "TeamViewer" };
        /// <summary>Maximum time to keep the launcher open while waiting for that window.</summary>
        public int StartWaitSeconds { get; private set; } = 120;
        /// <summary>
        /// Start the module without admin rights, so Windows does not show the
        /// User Account Control prompt. The supporter then cannot operate elevated
        /// windows until the customer confirms them.
        /// </summary>
        public bool RunWithoutAdmin { get; private set; } = false;
        /// <summary>
        /// Ask for administrator rights once when the launcher starts. QuickSupport,
        /// started by the elevated launcher, then runs with full rights without asking again.
        /// </summary>
        public bool RequestAdmin { get; private set; } = false;

        public static LauncherConfig Load(string path)
        {
            var cfg = new LauncherConfig();
            if (!File.Exists(path))
                return cfg;

            var values = ParseIni(ReadLines(path));

            cfg.CompanyName = Get(values, "CompanyName", cfg.CompanyName);
            cfg.WindowTitle = Get(values, "WindowTitle", cfg.WindowTitle);
            cfg.DownloadUrl = Get(values, "DownloadUrl", cfg.DownloadUrl);
            cfg.FileName = SafeFileName(Get(values, "FileName", cfg.FileName));
            cfg.StatusText = Get(values, "StatusText", cfg.StatusText);
            cfg.SupportContact = Get(values, "SupportContact", cfg.SupportContact);

            var publishers = Get(values, "AllowedPublishers", null);
            if (!string.IsNullOrWhiteSpace(publishers))
                cfg.AllowedPublishers = SplitList(publishers);

            if (int.TryParse(Get(values, "TimeoutSeconds", null), out var t) && t > 0)
                cfg.TimeoutSeconds = t;

            cfg.StartingText = Get(values, "StartingText", cfg.StartingText);
            var names = Get(values, "WindowProcessNames", null);
            if (!string.IsNullOrWhiteSpace(names))
                cfg.WindowProcessNames = SplitList(names);
            if (int.TryParse(Get(values, "StartWaitSeconds", null), out var w) && w > 0)
                cfg.StartWaitSeconds = w;

            cfg.RunWithoutAdmin = IsYes(Get(values, "RunWithoutAdmin", ""));
            cfg.RequestAdmin = IsYes(Get(values, "RequestAdmin", ""));

            return cfg;
        }

        /// <summary>
        /// Reads the file line by line, accepting UTF-8 (with or without BOM) and the
        /// Windows ANSI code page. Mixed files happen when the setup writes overrides
        /// with the Windows INI API into a UTF-8 file, so each line is decoded on its own.
        /// </summary>
        private static string[] ReadLines(string path)
        {
            var bytes = File.ReadAllBytes(path);
            int start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            var strictUtf8 = new UTF8Encoding(false, true);
            var lines = new List<string>();

            int lineStart = start;
            for (int i = start; i <= bytes.Length; i++)
            {
                if (i == bytes.Length || bytes[i] == (byte)'\n')
                {
                    int len = i - lineStart;
                    if (len > 0 && bytes[lineStart + len - 1] == (byte)'\r') len--;
                    string line;
                    try { line = strictUtf8.GetString(bytes, lineStart, len); }
                    catch (DecoderFallbackException) { line = Encoding.Default.GetString(bytes, lineStart, len); }
                    lines.Add(line);
                    lineStart = i + 1;
                }
            }
            return lines.ToArray();
        }

        private static Dictionary<string, string> ParseIni(string[] lines)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == ';' || line[0] == '#' || line[0] == '[')
                    continue;
                var eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;
                result[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            return result;
        }

        private static string Get(Dictionary<string, string> v, string key, string fallback)
            => v.TryGetValue(key, out var s) && !string.IsNullOrWhiteSpace(s) ? s : fallback;

        private static bool IsYes(string v)
            => v == "1" ||
               v.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               v.Equals("ja", StringComparison.OrdinalIgnoreCase) ||
               v.Equals("yes", StringComparison.OrdinalIgnoreCase);

        private static string[] SplitList(string s)
        {
            var parts = s.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var list = new List<string>();
            foreach (var p in parts)
                if (p.Trim().Length > 0) list.Add(p.Trim());
            return list.ToArray();
        }

        private static string SafeFileName(string name)
        {
            name = Path.GetFileName(name ?? "");
            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = "QuickSupport.exe";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
