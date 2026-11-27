using System.Collections.Generic;

namespace HermesVaultManager
{
    internal sealed class VaultItem
    {
        public string Handle { get; set; }
        public string Kind { get; set; }
        public string Label { get; set; }
        public string Identifier { get; set; }
        public string Origin { get; set; }
    }

    internal sealed class VaultResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public string Raw { get; set; }
        public string Handle { get; set; }
        public List<VaultItem> Items { get; set; }

        public VaultResult()
        {
            Items = new List<VaultItem>();
        }
    }

    internal sealed class UpdateInfo
    {
        public bool Available { get; set; }
        public string Version { get; set; }
        public string ReleaseUrl { get; set; }
        public string MsiUrl { get; set; }
        public string Notes { get; set; }
        public string Error { get; set; }
    }
}
