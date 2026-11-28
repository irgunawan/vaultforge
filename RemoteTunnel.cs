using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace VaultForge
{
    internal enum RemoteState
    {
        Disconnected,
        Connecting,
        Connected,
        Error
    }

    /// <summary>
    /// Outbound reverse-SSH tunnel so Hermes can reach this laptop even behind
    /// NAT/router. Starts ssh.exe with a remote forward and keeps the UI lamp
    /// in sync with the process + local gateway health.
    /// </summary>
    internal sealed class RemoteTunnel : IDisposable
    {
        private readonly AppSettings _settings;
        private readonly object _gate = new object();
        private readonly Timer _poll;
        private Process _ssh;
        private bool _owned;
        private int _busy;
        private string _lastError = "";
        private string _lastMessage = "";
        private RemoteState _state = RemoteState.Disconnected;

        public event Action<RemoteState, string> StateChanged;

        public RemoteTunnel(AppSettings settings)
        {
            _settings = settings;
            _poll = new Timer(_ => Refresh(), null, 2500, 2500);
        }

        public bool OwnsTunnel { get { return _owned; } }

        public void Connect()
        {
            if (Interlocked.Exchange(ref _busy, 1) != 0) return;
            Task.Run(() =>
            {
                try { ConnectCore(); }
                catch (Exception ex) { Set(RemoteState.Error, "Gagal connect: " + ex.Message); }
                finally { Interlocked.Exchange(ref _busy, 0); }
            });
        }

        public void Disconnect()
        {
            if (Interlocked.Exchange(ref _busy, 1) != 0) return;
            Task.Run(() =>
            {
                try { DisconnectCore(); }
                catch (Exception ex) { Set(RemoteState.Error, "Gagal disconnect: " + ex.Message); }
                finally { Interlocked.Exchange(ref _busy, 0); }
            });
        }

        public void Refresh()
        {
            if (_busy != 0) return;
            bool running = FindTunnelPids().Length > 0;
            bool healthy = GatewayHealthy();
            if (running && healthy)
            {
                Set(RemoteState.Connected, "Terhubung ke server utama.");
            }
            else if (running)
            {
                Set(RemoteState.Connecting, "Tunnel aktif, menunggu gateway Hermes...");
            }
            else if (_state != RemoteState.Error)
            {
                Set(RemoteState.Disconnected, "Terputus. Tekan Connect untuk akses remote.");
            }
        }

        private void ConnectCore()
        {
            string key = ResolveKeyPath();
            if (!File.Exists(key))
            {
                Set(RemoteState.Error, "Kunci SSH tidak ditemukan: " + key);
                return;
            }

            Set(RemoteState.Connecting, "Memeriksa gateway Hermes di 127.0.0.1:" + _settings.LocalApiPort + "...");
            if (!GatewayHealthy())
            {
                Set(RemoteState.Connecting, "Menyalakan gateway Hermes...");
                TryStartGateway();
                for (int i = 0; i < 40 && !GatewayHealthy(); i++) Thread.Sleep(500);
            }

            if (!GatewayHealthy())
            {
                Set(RemoteState.Error, "Gateway Hermes tidak aktif di 127.0.0.1:" + _settings.LocalApiPort +
                    ". Jalankan Hermes lebih dulu.");
                return;
            }

            DeleteSentinel();

            if (FindTunnelPids().Length > 0)
            {
                Set(RemoteState.Connected, "Sudah terhubung (tunnel aktif).");
                return;
            }

            Set(RemoteState.Connecting, "Membuka tunnel keluar ke " + _settings.RemoteHost + "...");
            _lastError = "";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = SshPath(),
                    Arguments = BuildArguments(key),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                };
                var p = new Process { StartInfo = psi };
                p.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data)) _lastError = e.Data.Trim();
                };
                p.OutputDataReceived += (s, e) => { };
                p.Start();
                p.BeginErrorReadLine();
                p.BeginOutputReadLine();
                _ssh = p;
                _owned = true;

                for (int i = 0; i < 30; i++)
                {
                    Thread.Sleep(500);
                    if (p.HasExited)
                    {
                        _owned = false;
                        string detail = string.IsNullOrEmpty(_lastError) ? "exit " + p.ExitCode : _lastError;
                        Set(RemoteState.Error, "SSH gagal: " + detail);
                        return;
                    }
                    if (i >= 2)
                    {
                        Set(RemoteState.Connected, "Terhubung ke server utama.");
                        return;
                    }
                }
                Set(RemoteState.Connected, "Tunnel aktif.");
            }
            catch (Exception ex)
            {
                Set(RemoteState.Error, "Tidak bisa menjalankan ssh.exe: " + ex.Message);
            }
        }

        private void DisconnectCore()
        {
            Set(RemoteState.Disconnected, "Memutus tunnel...");
            CreateSentinel();
            try
            {
                Run("schtasks.exe", "/End /TN \"HermesFleetTunnel\"");
            }
            catch { }

            foreach (int pid in FindTunnelPids())
            {
                try
                {
                    using (var p = Process.GetProcessById(pid)) p.Kill();
                }
                catch { }
            }

            foreach (int pid in FindLoopPids())
            {
                try
                {
                    using (var p = Process.GetProcessById(pid)) p.Kill();
                }
                catch { }
            }

            if (_ssh != null)
            {
                try { if (!_ssh.HasExited) _ssh.Kill(); } catch { }
                _ssh = null;
            }
            _owned = false;
            Set(RemoteState.Disconnected, "Terputus. Akses remote dimatikan.");
        }

        private string BuildArguments(string key)
        {
            return string.Format(
                "-N -R {0}:127.0.0.1:{1} -i \"{2}\" -p {3} " +
                "-o ServerAliveInterval=30 -o ServerAliveCountMax=3 " +
                "-o ExitOnForwardFailure=yes -o StrictHostKeyChecking=accept-new " +
                "-o BatchMode=yes {4}@{5}",
                _settings.RemoteForwardPort,
                _settings.LocalApiPort,
                key,
                _settings.RemoteSshPort,
                _settings.RemoteUser,
                _settings.RemoteHost);
        }

        private string ResolveKeyPath()
        {
            string key = _settings.RemoteKeyPath;
            if (string.IsNullOrWhiteSpace(key))
            {
                key = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".ssh", "hermes_fleet_laptop");
            }
            return Environment.ExpandEnvironmentVariables(key);
        }

        private string SshPath()
        {
            string native = Path.Combine(Environment.SystemDirectory, "OpenSSH", "ssh.exe");
            if (File.Exists(native)) return native;
            return "ssh.exe";
        }

        private bool GatewayHealthy()
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(
                    "http://127.0.0.1:" + _settings.LocalApiPort + "/health");
                req.Method = "GET";
                req.Timeout = 2000;
                req.ReadWriteTimeout = 2000;
                req.Proxy = null;
                using (var resp = (HttpWebResponse)req.GetResponse())
                    return (int)resp.StatusCode == 200;
            }
            catch
            {
                return false;
            }
        }

        private void TryStartGateway()
        {
            string[] candidates =
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "hermes", "gateway-service", "Hermes_Gateway.vbs"),
                string.IsNullOrWhiteSpace(_settings.HermesHome) ? "" :
                    Path.Combine(_settings.HermesHome, "gateway-service", "Hermes_Gateway.vbs")
            };
            foreach (string vbs in candidates)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(vbs) || !File.Exists(vbs)) continue;
                    Process.Start(new ProcessStartInfo("wscript.exe", "\"" + vbs + "\"")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    return;
                }
                catch { }
            }
        }

        private int[] FindTunnelPids()
        {
            var pids = new List<int>();
            string needle = "-R " + _settings.RemoteForwardPort + ":127.0.0.1:" + _settings.LocalApiPort;
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId,CommandLine FROM Win32_Process WHERE Name='ssh.exe'"))
                {
                    foreach (ManagementBaseObject raw in searcher.Get())
                    {
                        using (raw)
                        {
                            string cmd = Convert.ToString(raw["CommandLine"]);
                            if (cmd.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                                pids.Add(Convert.ToInt32(raw["ProcessId"]));
                        }
                    }
                }
            }
            catch { }
            return pids.ToArray();
        }

        private int[] FindLoopPids()
        {
            var pids = new List<int>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId,CommandLine FROM Win32_Process WHERE Name='cmd.exe'"))
                {
                    foreach (ManagementBaseObject raw in searcher.Get())
                    {
                        using (raw)
                        {
                            string cmd = Convert.ToString(raw["CommandLine"]);
                            if (cmd.IndexOf("fleet-tunnel.cmd", StringComparison.OrdinalIgnoreCase) >= 0)
                                pids.Add(Convert.ToInt32(raw["ProcessId"]));
                        }
                    }
                }
            }
            catch { }
            return pids.ToArray();
        }

        private void CreateSentinel()
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "hermes");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "tunnel.disabled"), "disabled by VaultForge");
            }
            catch { }
        }

        private void DeleteSentinel()
        {
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "hermes", "tunnel.disabled");
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        private static void Run(string file, string args)
        {
            try
            {
                using (var p = Process.Start(new ProcessStartInfo(file, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                }))
                {
                    if (p != null) p.WaitForExit(5000);
                }
            }
            catch { }
        }

        private void Set(RemoteState state, string message)
        {
            if (_state == state && _lastMessage == message) return;
            _state = state;
            _lastMessage = message;
            var handler = StateChanged;
            if (handler == null) return;
            try { handler(state, message); } catch { }
        }

        public void Dispose()
        {
            try { _poll.Dispose(); } catch { }
        }
    }
}
