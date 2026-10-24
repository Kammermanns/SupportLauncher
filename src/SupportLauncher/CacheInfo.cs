using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SupportLauncher
{
    /// <summary>
    /// Remembers which server file the cached module came from (URL, ETag,
    /// Last-Modified, size), so the next start can ask the server whether it changed
    /// instead of downloading it again. Stored next to the cached EXE.
    /// </summary>
    internal sealed class CacheInfo
    {
        public string Url = "";
        public string ETag = "";
        public string LastModified = "";
        public long Length = -1;

        public static CacheInfo Load(string path)
        {
            var info = new CacheInfo();
            try
            {
                if (!File.Exists(path)) return info;
                foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    var eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = line.Substring(0, eq);
                    var value = line.Substring(eq + 1);
                    switch (key)
                    {
                        case "Url": info.Url = value; break;
                        case "ETag": info.ETag = value; break;
                        case "LastModified": info.LastModified = value; break;
                        case "Length": long.TryParse(value, out info.Length); break;
                    }
                }
            }
            catch { /* treat as empty */ }
            return info;
        }

        public void Save(string path)
        {
            try
            {
                File.WriteAllLines(path, new List<string>
                {
                    "Url=" + Url,
                    "ETag=" + ETag,
                    "LastModified=" + LastModified,
                    "Length=" + Length
                }, new UTF8Encoding(false));
            }
            catch { /* cache info is optional */ }
        }

        public static void Delete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        /// <summary>Same file on the server: same address and same ETag, or same date and size.</summary>
        public bool Matches(CacheInfo other)
        {
            if (other == null || !SameAddress(Url, other.Url)) return false;
            if (ETag.Length > 0 && other.ETag.Length > 0)
                return ETag == other.ETag;
            if (LastModified.Length > 0 && other.LastModified.Length > 0 && Length > 0 && other.Length > 0)
                return LastModified == other.LastModified && Length == other.Length;
            return false;   // not enough information: download to be safe
        }

        /// <summary>Compares scheme, host and path; query strings (often one-time tokens) are ignored.</summary>
        public static bool SameAddress(string a, string b)
        {
            Uri ua, ub;
            if (!Uri.TryCreate(a, UriKind.Absolute, out ua) || !Uri.TryCreate(b, UriKind.Absolute, out ub))
                return false;
            return string.Equals(ua.GetLeftPart(UriPartial.Path), ub.GetLeftPart(UriPartial.Path),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
