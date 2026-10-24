using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SupportLauncher
{
    /// <summary>
    /// Small branded window: downloads the current support module,
    /// verifies its signature, starts it and closes itself.
    /// </summary>
    internal sealed class LauncherForm : Form
    {
        private const int ContentWidth = 480;

        private readonly LauncherConfig _config;
        private readonly Label _status;
        private readonly Label _contact;
        private readonly ProgressBar _progress;
        private readonly Button _retry;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public LauncherForm(LauncherConfig config, string baseDir)
        {
            _config = config;

            Text = config.WindowTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10f);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(24, 20, 24, 20);

            // app.ico next to the EXE (installed by the setup) wins over the built-in icon.
            try
            {
                var icoPath = Path.Combine(baseDir, "app.ico");
                Icon = File.Exists(icoPath)
                    ? new Icon(icoPath)
                    : Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { /* keep default icon */ }

            // Vertical stack; every row grows with its text, so nothing gets cut off.
            var layout = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ContentWidth));

            var logoPath = Path.Combine(baseDir, "logo.png");
            if (File.Exists(logoPath))
            {
                var logo = new PictureBox
                {
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Size = new Size(ContentWidth, 100),
                    Margin = new Padding(0, 0, 0, 12)
                };
                // Load via stream copy so the file is not locked.
                using (var fs = File.OpenRead(logoPath))
                    logo.Image = Image.FromStream(new MemoryStream(ReadAll(fs)));
                layout.Controls.Add(logo);
            }

            layout.Controls.Add(MakeLabel(config.CompanyName, new Font(Font.FontFamily, 14f, FontStyle.Bold), Color.FromArgb(30, 30, 30)));

            _status = MakeLabel(config.StatusText, Font, Color.FromArgb(60, 60, 60));
            _status.Margin = new Padding(0, 10, 0, 10);
            layout.Controls.Add(_status);

            _progress = new ProgressBar
            {
                Size = new Size(ContentWidth - 40, 18),
                Anchor = AnchorStyles.None,
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Margin = new Padding(20, 4, 20, 8)
            };
            layout.Controls.Add(_progress);

            _contact = MakeLabel(config.SupportContact, new Font(Font.FontFamily, 9.5f), Color.FromArgb(90, 90, 90));
            _contact.Visible = false;
            layout.Controls.Add(_contact);

            _retry = new Button
            {
                Text = "Erneut versuchen",
                AutoSize = true,
                Padding = new Padding(12, 4, 12, 4),
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 10, 0, 0),
                Visible = false
            };
            _retry.Click += async (s, e) => await RunAsync();
            layout.Controls.Add(_retry);

            Controls.Add(layout);

            Shown += async (s, e) => await RunAsync();
            FormClosing += (s, e) => _cts.Cancel();
        }

        private static Label MakeLabel(string text, Font font, Color color)
        {
            return new Label
            {
                Text = text ?? "",
                Font = font,
                ForeColor = color,
                AutoSize = true,
                MaximumSize = new Size(ContentWidth, 0),   // wrap instead of cutting off
                MinimumSize = new Size(ContentWidth, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0)
            };
        }

        private async Task RunAsync()
        {
            _retry.Visible = false;
            _contact.Visible = false;
            _progress.Visible = true;
            _progress.Style = ProgressBarStyle.Marquee;
            _status.ForeColor = Color.FromArgb(60, 60, 60);
            _status.Text = _config.StatusText;

            var cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SupportLauncher");
            Directory.CreateDirectory(cacheDir);

            var target = Path.Combine(cacheDir, _config.FileName);
            var temp = target + ".download";

            string error = null;
            try
            {
                if (string.IsNullOrWhiteSpace(_config.DownloadUrl))
                    throw new InvalidOperationException("Kein Download-Link konfiguriert.");

                await DownloadAsync(_config.DownloadUrl, temp, _cts.Token);

                string details;
                if (!SignatureVerifier.IsTrusted(temp, _config.AllowedPublishers, true, out details))
                {
                    TryDelete(temp);
                    throw new InvalidOperationException("Die heruntergeladene Datei ist nicht vertrauenswürdig.\n" + details);
                }

                // Replace the cached copy only after a successful check.
                TryDelete(target);
                File.Move(temp, target);
            }
            catch (OperationCanceledException)
            {
                if (_cts.IsCancellationRequested) return;
                error = "Zeitüberschreitung beim Download.";
                TryDelete(temp);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                TryDelete(temp);
            }

            if (error != null)
            {
                // Offline fallback: last verified copy (re-checked, without online revocation lookup).
                string ignored;
                if (File.Exists(target) &&
                    SignatureVerifier.IsTrusted(target, _config.AllowedPublishers, false, out ignored))
                {
                    _status.Text = "Keine Verbindung – letzte Version wird gestartet …";
                }
                else
                {
                    ShowError(error);
                    return;
                }
            }

            var pid = Start(target);
            if (pid < 0)
                return;

            // Stay open until the support window is visible, so the customer
            // sees that something is happening on slow PCs.
            _progress.Style = ProgressBarStyle.Marquee;
            _status.ForeColor = Color.FromArgb(60, 60, 60);
            _status.Text = _config.StartingText;

            var deadline = DateTime.UtcNow.AddSeconds(_config.StartWaitSeconds);
            while (DateTime.UtcNow < deadline)
            {
                if (_cts.IsCancellationRequested) return;
                bool visible;
                try { visible = WindowWatcher.HasVisibleWindow(pid, _config.WindowProcessNames); }
                catch { visible = true; }   // never keep the window open because of a detection error
                if (visible)
                    break;
                try { await Task.Delay(400, _cts.Token); } catch (OperationCanceledException) { return; }
            }

            await Task.Delay(1000);   // let the support window come to the front
            Close();
        }

        /// <summary>
        /// Downloads the module. Links like https://get.teamviewer.com/xyz return an HTML
        /// page instead of the EXE; in that case the EXE link is taken from the page
        /// (up to two hops). The signature check afterwards decides whether it is started.
        /// </summary>
        private async Task DownloadAsync(string url, string path, CancellationToken token)
        {
            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds) })
            {
                // Browser-like user agent: some download pages serve different content otherwise.
                http.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) SupportLauncher/1.1");

                var current = new Uri(url);
                for (int hop = 0; hop < 3; hop++)
                {
                    using (var response = await http.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, token))
                    {
                        response.EnsureSuccessStatusCode();

                        var mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
                        if (mediaType.IndexOf("html", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var html = await response.Content.ReadAsStringAsync();
                            var baseUri = response.RequestMessage?.RequestUri ?? current;
                            var next = FindExeLink(html, baseUri);
                            if (next == null)
                                throw new InvalidOperationException(
                                    "Unter dem Download-Link wurde keine Programmdatei gefunden.\n" + baseUri);
                            current = next;
                            continue;
                        }

                        await SaveAsync(response, path, token);
                        if (!LooksLikeExe(path))
                            throw new InvalidOperationException(
                                "Die heruntergeladene Datei ist keine Programmdatei.\n" + current);
                        return;
                    }
                }
                throw new InvalidOperationException("Zu viele Weiterleitungen beim Download.");
            }
        }

        private static readonly Regex ExeLink = new Regex(
            @"(?:https?:)?//[^\s""'<>]+?\.exe(?:\?[^\s""'<>]*)?|[^\s""'<>=]+?\.exe(?:\?[^\s""'<>]*)?(?=[""'])",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Finds the first link to an .exe on a teamviewer.com host.</summary>
        private static Uri FindExeLink(string html, Uri baseUri)
        {
            html = html.Replace("\\/", "/").Replace("&amp;", "&");
            foreach (Match m in ExeLink.Matches(html))
            {
                Uri candidate;
                var value = m.Value;
                if (value.StartsWith("//")) value = baseUri.Scheme + ":" + value;
                if (!Uri.TryCreate(baseUri, value, out candidate))
                    continue;
                if (candidate.Scheme != Uri.UriSchemeHttps)
                    continue;
                var host = candidate.Host.ToLowerInvariant();
                if (host == "teamviewer.com" || host.EndsWith(".teamviewer.com"))
                    return candidate;
            }
            return null;
        }

        private async Task SaveAsync(HttpResponseMessage response, string path, CancellationToken token)
        {
            var total = response.Content.Headers.ContentLength;
            if (total.HasValue && total.Value > 0)
            {
                _progress.Style = ProgressBarStyle.Continuous;
                _progress.Maximum = 100;
                _progress.Value = 0;
            }

            using (var input = await response.Content.ReadAsStreamAsync())
            using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                long done = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    await output.WriteAsync(buffer, 0, read, token);
                    done += read;
                    if (total.HasValue && total.Value > 0)
                        _progress.Value = (int)Math.Min(100, done * 100 / total.Value);
                }
            }
        }

        private static bool LooksLikeExe(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                    return fs.Length > 2 && fs.ReadByte() == 'M' && fs.ReadByte() == 'Z';
            }
            catch { return false; }
        }

        /// <summary>Starts the module; returns its process id, or -1 on failure.</summary>
        private int Start(string path)
        {
            try
            {
                using (var p = Process.Start(new ProcessStartInfo(path)
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(path)
                }))
                {
                    return p?.Id ?? 0;
                }
            }
            catch (Exception ex)
            {
                ShowError("Das Support-Modul konnte nicht gestartet werden.\n" + ex.Message);
                return -1;
            }
        }

        private void ShowError(string message)
        {
            _progress.Visible = false;
            _status.ForeColor = Color.FromArgb(170, 30, 30);
            _status.Text = message;
            _contact.Visible = !string.IsNullOrWhiteSpace(_config.SupportContact);
            _retry.Visible = true;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
        }

        private static byte[] ReadAll(Stream s)
        {
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                return ms.ToArray();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _cts.Dispose();
            base.Dispose(disposing);
        }
    }
}
