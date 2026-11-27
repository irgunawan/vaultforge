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
            string msi = Path.Combine(Path.GetTempPath(), "VaultForge-" + info.Version + ".msi");
            string checksum = msi + ".sha256";

            try
            {
                if (string.IsNullOrWhiteSpace(info.MsiUrl))
                    throw new Exception("Tautan installer tidak ditemukan di release GitHub.");

                progress.Report("Mengunduh VaultForge v" + info.Version + "...");
                await DownloadAsync(info.MsiUrl, msi, progress);

                var file = new FileInfo(msi);
                if (!file.Exists || file.Length < 1024)
                    throw new Exception("Installer yang diunduh kosong atau tidak valid.");
                if (info.MsiSize > 0 && file.Length != info.MsiSize)
                    throw new Exception("Ukuran installer tidak cocok dengan release GitHub.");

                if (!string.IsNullOrWhiteSpace(info.MsiShaUrl))
                {
                    progress.Report("Memverifikasi checksum unduhan...");
                    await DownloadAsync(info.MsiShaUrl, checksum, new Progress<string>());
                    string expected = File.ReadAllText(checksum).Trim();
                    int space = expected.IndexOf(' ');
                    if (space > 0) expected = expected.Substring(0, space);
                    expected = expected.Trim().ToLowerInvariant();
                    string actual = Sha256(msi);
                    if (expected.Length != 64 || expected != actual)
                        throw new Exception("SHA-256 installer tidak cocok. Unduhan dibatalkan.");
                }

                progress.Report("Menyiapkan pemasangan otomatis...");
                string script = WriteHelper(msi, Process.GetCurrentProcess().Id, Application.ExecutablePath);

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
                "$p = Start-Process -FilePath \"$env:SystemRoot\\System32\\msiexec.exe\" -ArgumentList @('/i', \"`\"$msi`\"\", '/quiet', '/norestart', '/log', \"`\"$log`\"\") -Wait -PassThru\r\n" +
                "if ($p.ExitCode -eq 0 -and (Test-Path $exe)) { Start-Process -FilePath $exe }\r\n";
            File.WriteAllText(path, script, new System.Text.UTF8Encoding(false));
            return path;
        }

        private static string Ps(string value)
        {
            return (value ?? "").Replace("'", "''");
        }
    }
}
