using System;
using System.Collections.Generic;
using System.IO;
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

            if (string.IsNullOrWhiteSpace(s.HermesHome))
            {
                s.HermesHome = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hermes");
            }

            if (string.IsNullOrWhiteSpace(s.HermesExe))
            {
                s.HermesExe = FindOnPath("hermes.exe") ?? FindOnPath("hermes") ?? "hermes.exe";
            }

            if (string.IsNullOrWhiteSpace(s.PythonExe))
            {
                s.PythonExe = FindSiblingPython(s.HermesExe) ?? FindOnPath("python.exe") ?? "python.exe";
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
