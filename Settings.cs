using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace VaultForge
{
    internal sealed class AppSettings
    {
        public string HermesExe { get; set; }
        public string PythonExe { get; set; }
        public string HermesHome { get; set; }
        public bool CheckUpdates { get; set; }
        public bool AutoInstallUpdates { get; set; }
        public bool DarkTheme { get; set; }

        public AppSettings()
        {
            CheckUpdates = true;
            AutoInstallUpdates = false;
            DarkTheme = false;
        }

        private static string SettingsDir
        {
            get
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                if (PortableRuntime.IsWritable(exeDir)) return exeDir;
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "VaultForge");
            }
        }

        private static string SettingsFile
        {
            get { return Path.Combine(SettingsDir, "settings.json"); }
        }

        private static string LegacySettingsFile
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "HermesVaultManager", "settings.json");
            }
        }

        public static AppSettings Load()
        {
            AppSettings s = null;
            try
            {
                string source = SettingsFile;
                if (!File.Exists(source) && File.Exists(LegacySettingsFile))
                {
                    source = LegacySettingsFile;
                }
                if (File.Exists(source))
                {
                    var json = File.ReadAllText(source);
                    s = new JavaScriptSerializer().Deserialize<AppSettings>(json);
                }
            }
            catch
            {
            }

            if (s == null)
            {
                s = new AppSettings();
            }

            if (string.IsNullOrWhiteSpace(s.HermesExe))
            {
                s.HermesExe = FindOnPath("hermes.exe") ?? FindOnPath("hermes") ?? "hermes.exe";
            }

            if (string.IsNullOrWhiteSpace(s.HermesHome))
            {
                s.HermesHome = HermesRoot(s.HermesExe)
                    ?? Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hermes");
            }

            if (string.IsNullOrWhiteSpace(s.PythonExe))
            {
                s.PythonExe = FindHermesPython(s.HermesExe, s.HermesHome)
                    ?? FindOnPath("python.exe") ?? "python.exe";
            }

            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                var json = new JavaScriptSerializer().Serialize(this);
                File.WriteAllText(SettingsFile, json);
            }
            catch
            {
            }
        }

        private static string HermesRoot(string hermesExe)
        {
            try
            {
                var dir = new DirectoryInfo(Path.GetDirectoryName(hermesExe) ?? "");
                while (dir != null)
                {
                    if (File.Exists(Path.Combine(dir.FullName, "config.yaml")))
                        return dir.FullName;
                    dir = dir.Parent;
                }
            }
            catch
            {
            }
            return null;
        }

        private static string FindHermesPython(string hermesExe, string hermesHome)
        {
            string sibling = FindSiblingPython(hermesExe);
            if (!string.IsNullOrWhiteSpace(sibling)) return sibling;

            string root = hermesHome;
            if (string.IsNullOrWhiteSpace(root)) root = HermesRoot(hermesExe);
            if (!string.IsNullOrWhiteSpace(root))
            {
                string installs = Path.Combine(root, "installs");
                if (Directory.Exists(installs))
                {
                    try
                    {
                        string best = Directory.GetFiles(installs, "python.exe", SearchOption.AllDirectories)
                            .Where(p => p.IndexOf("venv\\Scripts\\", StringComparison.OrdinalIgnoreCase) >= 0)
                            .OrderByDescending(p => Directory.GetLastWriteTimeUtc(Path.GetDirectoryName(p) ?? ""))
                            .FirstOrDefault();
                        if (best != null) return best;
                    }
                    catch
                    {
                    }
                }
                string tools = Path.Combine(root, "tools");
                if (Directory.Exists(tools))
                {
                    try
                    {
                        string best = Directory.GetFiles(tools, "python.exe", SearchOption.AllDirectories)
                            .OrderByDescending(p => File.GetLastWriteTimeUtc(p))
                            .FirstOrDefault();
                        if (best != null) return best;
                    }
                    catch
                    {
                    }
                }
            }
            return null;
        }

        private static string FindSiblingPython(string hermesExe)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(hermesExe) && File.Exists(hermesExe))
                {
                    var dir = Path.GetDirectoryName(hermesExe);
                    var candidate = Path.Combine(dir ?? "", "python.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private static string FindOnPath(string fileName)
        {
            try
            {
                var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';');
                foreach (var raw in paths)
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    var candidate = Path.Combine(raw.Trim(), fileName);
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch
            {
            }
            return null;
        }
    }
}
