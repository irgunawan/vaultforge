using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VaultForgeSetup
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!IsAdmin())
            {
                try
                {
                    Process.Start(new ProcessStartInfo(Application.ExecutablePath)
                    {
                        Verb = "runas",
                        UseShellExecute = true
                    });
                }
                catch
                {
                    MessageBox.Show("Instalasi dibatalkan.", "VaultForge Setup",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }

            Application.Run(new SetupForm());
        }

        internal static bool IsAdmin()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }

    internal sealed class SetupForm : Form
    {
        private readonly Label _status;
        private readonly ProgressBar _bar;

        public SetupForm()
        {
            Text = "VaultForge Setup";
            ClientSize = new Size(430, 140);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = SystemColors.Control;
            Font = new Font("Segoe UI", 9f);

            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
            _bar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                Dock = DockStyle.Top,
                Height = 18,
                Margin = new Padding(0, 8, 0, 0)
            };
            _status = new Label
            {
                Text = "Menyiapkan...",
                Dock = DockStyle.Top,
                Height = 54,
                AutoSize = false
            };
            panel.Controls.Add(_bar);
            panel.Controls.Add(_status);
            Controls.Add(panel);

            Shown += (s, e) => ThreadPool.QueueUserWorkItem(_ => Install());
        }

        private void SetStatus(string text)
        {
            if (InvokeRequired) BeginInvoke((Action)(() => _status.Text = text));
            else _status.Text = text;
        }

        private void Install()
        {
            string temp = Path.Combine(Path.GetTempPath(), "VaultForge-Setup-" + Guid.NewGuid().ToString("N"));
            string log = Path.Combine(Path.GetTempPath(), "vaultforge-setup.log");
            try
            {
                Directory.CreateDirectory(temp);
                string msi = Path.Combine(temp, "VaultForge.msi");
                SetStatus("Mengekstrak installer...");
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("VaultForgeSetup.VaultForge.msi"))
                {
                    if (stream == null) throw new Exception("Installer internal tidak ditemukan.");
                    using (var file = File.Create(msi)) stream.CopyTo(file);
                }

                StopRunningApp();

                string oldProduct = FindProductCode();
                if (!string.IsNullOrEmpty(oldProduct))
                {
                    SetStatus("Menghapus versi lama...");
                    RunMsi("/x " + oldProduct + " /quiet /norestart /log \"" + log + "\"");
                }

                SetStatus("Memasang VaultForge...");
                int code = RunMsi("/i \"" + msi + "\" /quiet /norestart /log \"" + log + "\"");
                if (code != 0)
                {
                    ShowError("Instalasi gagal (kode " + code + ").\n\nLog: " + log);
                    return;
                }

                string exe = @"C:\Program Files (x86)\VaultForge\VaultForge.exe";
                if (!File.Exists(exe)) exe = @"C:\Program Files\VaultForge\VaultForge.exe";
                if (!File.Exists(exe))
                {
                    ShowError("Instalasi selesai tetapi VaultForge.exe tidak ditemukan.\n\nLog: " + log);
                    return;
                }

                SetStatus("Selesai. Membuka VaultForge...");
                try { Process.Start(exe); } catch { }
                Thread.Sleep(600);
                CloseOnUi();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message + "\n\nLog: " + log);
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }

        private static void StopRunningApp()
        {
            foreach (var process in Process.GetProcessesByName("VaultForge"))
            {
                try { process.Kill(); process.WaitForExit(5000); }
                catch { }
                finally { process.Dispose(); }
            }
            Thread.Sleep(400);
        }

        private static string FindProductCode()
        {
            string[] roots =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };
            foreach (var root in roots)
            {
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(root))
                    {
                        if (key == null) continue;
                        foreach (var name in key.GetSubKeyNames())
                        {
                            using (var sub = key.OpenSubKey(name))
                            {
                                if (sub == null) continue;
                                if (Convert.ToString(sub.GetValue("DisplayName")) == "VaultForge")
                                    return name;
                            }
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        private static int RunMsi(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo("msiexec.exe", arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var process = Process.Start(psi))
                {
                    process.WaitForExit(300000);
                    return process.HasExited ? process.ExitCode : -1;
                }
            }
            catch
            {
                return -1;
            }
        }

        private void ShowError(string message)
        {
            if (InvokeRequired)
            {
                Invoke((Action)(() => ShowError(message)));
                return;
            }
            MessageBox.Show(this, message, "VaultForge Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }

        private void CloseOnUi()
        {
            if (InvokeRequired) BeginInvoke((Action)Close);
            else Close();
        }
    }
}
