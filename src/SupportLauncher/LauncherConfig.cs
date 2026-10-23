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

        public static LauncherConfig Load(string path)
        {
            var cfg = new LauncherConfig();
            if (!File.Exists(path))
                return cfg;

            var values = ParseIni(File.ReadAllLines(path, Encoding.UTF8));

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

            return cfg;
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
