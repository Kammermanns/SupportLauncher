using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
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
        private readonly LauncherConfig _config;
        private readonly Label _status;
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
            Font = new Font("Segoe UI", 9.75f);
            ClientSize = new Size(420, 230);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { /* default icon */ }

            var logo = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(20, 16),
                Size = new Size(380, 80)
            };
            var logoPath = Path.Combine(baseDir, "logo.png");
            if (File.Exists(logoPath))
            {
                // Load via stream copy so the file is not locked.
                using (var fs = File.OpenRead(logoPath))
                    logo.Image = Image.FromStream(new MemoryStream(ReadAll(fs)));
            }

            var company = new Label
            {
                Text = config.CompanyName,
                Font = new Font(Font.FontFamily, 12f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 100),
                Size = new Size(380, 26)
            };

            _status = new Label
            {
                Text = config.StatusText,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 130),
                Size = new Size(380, 36)
            };

            _progress = new ProgressBar
            {
                Location = new Point(40, 172),
                Size = new Size(340, 16),
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30
            };

            _retry = new Button
            {
                Text = "Erneut versuchen",
                Location = new Point(135, 194),
                Size = new Size(150, 28),
                Visible = false
            };
            _retry.Click += async (s, e) => await RunAsync();

            Controls.AddRange(new Control[] { logo, company, _status, _progress, _retry });

            Shown += async (s, e) => await RunAsync();
            FormClosing += (s, e) => _cts.Cancel();
        }

        private async Task RunAsync()
        {
            _retry.Visible = false;
            _progress.Visible = true;
            _progress.Style = ProgressBarStyle.Marquee;
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
                return;
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

            if (Start(target))
            {
                await Task.Delay(800);
                Close();
            }
        }

        private async Task DownloadAsync(string url, string path, CancellationToken token)
        {
            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("SupportLauncher/1.0");

                using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    response.EnsureSuccessStatusCode();

                    var total = response.Content.Headers.ContentLength;
                    if (total.HasValue && total.Value > 0)
                    {
                        _progress.Style = ProgressBarStyle.Continuous;
                        _progress.Maximum = 100;
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
            }
        }

        private bool Start(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path)
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(path)
                });
                return true;
            }
            catch (Exception ex)
            {
                ShowError("Das Support-Modul konnte nicht gestartet werden.\n" + ex.Message);
                return false;
            }
        }

        private void ShowError(string message)
        {
            _progress.Visible = false;
            _status.Text = string.IsNullOrWhiteSpace(_config.SupportContact)
                ? message
                : message + "\n" + _config.SupportContact;
            _status.Size = new Size(380, 60);
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
