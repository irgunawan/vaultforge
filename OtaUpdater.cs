using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VaultForge
{
    internal static class OtaUpdater
    {
        internal sealed class Result
        {
            public bool Ok { get; set; }
            public string Error { get; set; }
        }

        public static async Task<Result> InstallAsync(UpdateInfo info, IProgress<string> progress)
        {
            var result = new Result();
            bool portable = PortableRuntime.IsPortable;
            string fileName = portable ? "VaultForge.exe" : "VaultForge-" + info.Version + ".msi";
            string download = Path.Combine(Path.GetTempPath(), fileName);
            string checksum = download + ".sha256";
            string url = portable ? info.ExeUrl : info.MsiUrl;
            string shaUrl = portable ? info.ExeShaUrl : info.MsiShaUrl;
            long expectedSize = portable ? info.ExeSize : info.MsiSize;

            try
            {
                if (string.IsNullOrWhiteSpace(url))
                    throw new Exception(portable
                        ? "Tautan EXE portable tidak ditemukan di release GitHub."
                        : "Tautan installer tidak ditemukan di release GitHub.");

                progress.Report("Mengunduh VaultForge v" + info.Version + "...");
                await DownloadAsync(url, download, progress);

                var file = new FileInfo(download);
                if (!file.Exists || file.Length < 1024)
                    throw new Exception("Berkas yang diunduh kosong atau tidak valid.");
                if (expectedSize > 0 && file.Length != expectedSize)
                    throw new Exception("Ukuran unduhan tidak cocok dengan release GitHub.");

                if (!string.IsNullOrWhiteSpace(shaUrl))
                {
                    progress.Report("Memverifikasi checksum unduhan...");
                    await DownloadAsync(shaUrl, checksum, new Progress<string>());
                    string expected = File.ReadAllText(checksum).Trim();
                    int space = expected.IndexOf(' ');
                    if (space > 0) expected = expected.Substring(0, space);
                    expected = expected.Trim().ToLowerInvariant();
                    string actual = Sha256(download);
                    if (expected.Length != 64 || expected != actual)
                        throw new Exception("SHA-256 unduhan tidak cocok. Pembaruan dibatalkan.");
                }

                progress.Report(portable ? "Menyiapkan penggantian EXE..." : "Menyiapkan pemasangan otomatis...");
                string script = portable
                    ? WritePortableHelper(download, Process.GetCurrentProcess().Id, Application.ExecutablePath)
                    : WriteHelper(download, Process.GetCurrentProcess().Id, Application.ExecutablePath);

                var psi = new ProcessStartInfo(
                    "powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + script + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);

                result.Ok = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }
        }

        private static async Task DownloadAsync(string url, string path, IProgress<string> progress)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (var client = new WebClient())
            {
                client.DownloadProgressChanged += (s, e) =>
                    progress.Report("Mengunduh " + e.ProgressPercentage + "% (" +
                                    (e.BytesReceived / 1024) + " KB)");
                await client.DownloadFileTaskAsync(new Uri(url), path);
            }
        }

        private static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(stream);
                var sb = new System.Text.StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static string WriteHelper(string msi, int appPid, string exe)
        {
            string path = Path.Combine(Path.GetTempPath(), "vaultforge-ota.ps1");
            string log = Path.Combine(Path.GetTempPath(), "vaultforge-ota.log");
            string script =
                "$appPid = " + appPid + "\r\n" +
                "$msi = '" + Ps(msi) + "'\r\n" +
                "$exe = '" + Ps(exe) + "'\r\n" +
                "$log = '" + Ps(log) + "'\r\n" +
                "$deadline = (Get-Date).AddMinutes(2)\r\n" +
                "while ((Get-Process -Id $appPid -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 250 }\r\n" +
                "$keys = @('HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*', 'HKLM:\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*')\r\n" +
                "$old = Get-ItemProperty $keys -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -eq 'VaultForge' } | Select-Object -First 1 -ExpandProperty PSChildName\r\n" +
                "if ($old) { Start-Process -FilePath \"$env:SystemRoot\\System32\\msiexec.exe\" -ArgumentList @('/x', $old, '/quiet', '/norestart') -Wait }\r\n" +
                "$p = Start-Process -FilePath \"$env:SystemRoot\\System32\\msiexec.exe\" -ArgumentList @('/i', $msi, '/quiet', '/norestart', '/log', $log) -Wait -PassThru\r\n" +
                "if ($p.ExitCode -eq 0 -and (Test-Path $exe)) { Start-Process -FilePath $exe }\r\n";
            File.WriteAllText(path, script, new System.Text.UTF8Encoding(false));
            return path;
        }

        private static string WritePortableHelper(string newExe, int appPid, string exe)
        {
            string path = Path.Combine(Path.GetTempPath(), "vaultforge-portable-update.ps1");
            string script =
                "$appPid = " + appPid + "\r\n" +
                "$newExe = '" + Ps(newExe) + "'\r\n" +
                "$exe = '" + Ps(exe) + "'\r\n" +
                "$deadline = (Get-Date).AddMinutes(2)\r\n" +
                "while ((Get-Process -Id $appPid -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 250 }\r\n" +
                "$ok = $false\r\n" +
                "for ($i = 0; $i -lt 40 -and -not $ok; $i++) { try { Copy-Item -LiteralPath $newExe -Destination $exe -Force -ErrorAction Stop; $ok = $true } catch { Start-Sleep -Milliseconds 300 } }\r\n" +
                "if ($ok) { Start-Process -FilePath $exe }\r\n";
            File.WriteAllText(path, script, new System.Text.UTF8Encoding(false));
            return path;
        }

        private static string Ps(string value)
        {
            return (value ?? "").Replace("'", "''");
        }
    }
}
