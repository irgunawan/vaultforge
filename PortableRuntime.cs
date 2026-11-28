using System;
using System.IO;
using System.Reflection;

namespace VaultForge
{
    // Portable build: one EXE, helper files extracted beside it (or to AppData
    // when the folder is read-only, e.g. a per-machine install).
    internal static class PortableRuntime
    {
        public static bool IsPortable { get; private set; }
        public static string RuntimeDir { get; private set; }

        public static void Ensure()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            IsPortable = IsWritable(exeDir);
            RuntimeDir = IsPortable
                ? Path.Combine(exeDir, "VaultForge-data")
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VaultForge", "runtime");

            try { Directory.CreateDirectory(RuntimeDir); }
            catch { }

            Extract("VaultForge.vaultforge_bridge.ps1", "vaultforge_bridge.ps1");
            Extract("VaultForge.vaultforge_helper.py", "vaultforge_helper.py");
        }

        public static string BridgePath
        {
            get { return Path.Combine(RuntimeDir, "vaultforge_bridge.ps1"); }
        }

        private static void Extract(string resource, string fileName)
        {
            try
            {
                string target = Path.Combine(RuntimeDir, fileName);
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                {
                    if (stream == null) return;
                    using (var file = File.Create(target)) stream.CopyTo(file);
                }
            }
            catch
            {
            }
        }

        internal static bool IsWritable(string dir)
        {
            try
            {
                string probe = Path.Combine(dir, ".vf-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (var stream = File.Create(probe, 1, FileOptions.DeleteOnClose))
                {
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
